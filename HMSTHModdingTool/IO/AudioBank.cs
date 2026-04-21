using System;
using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles PS2 sound bank files (BD + HD) used for music and
    ///     sound effects (including SE.HDA which is just BD/HD).
    ///
    ///     Offset logic mirrors ps2-bankmod.py exactly:
    ///
    ///     get_vag_param_offset(index)
    ///       = HD[ vagi + 0x10 + (index * 4) ]  (U32 LE)
    ///
    ///     get_vag_offset(index)
    ///       = HD[ vagi + get_vag_param_offset(index) + 0x00 ]  (U32 LE)
    ///
    ///     get_vag_sample_rate(index)
    ///       = HD[ vagi + get_vag_param_offset(index) + 0x04 ]  (U16 LE)
    ///
    ///     max_vag_index
    ///       = HD[ vagi + 0x0C ]  (U32 LE)
    ///
    ///     bd_size
    ///       = HD[ 0x20 ]  (U32 LE)
    ///
    ///     vagi_chunk_offset
    ///       = HD[ 0x30 ]  (U32 LE)
    /// </summary>
    class AudioBank
    {
        // ── HD structure offsets ─────────────────────────────────────────
        private const int HD_BD_SIZE_OFFSET = 0x20; // U32 LE
        private const int HD_VAGI_OFFSET_OFFSET = 0x30; // U32 LE

        // ── VAGI chunk offsets (relative to vagi_chunk_offset) ───────────
        private const int VAGI_MAX_INDEX_OFFSET = 0x0C; // U32 LE
        private const int VAGI_PARAM_TABLE_OFFSET = 0x10; // base of param offset table

        // ── VAGp header ──────────────────────────────────────────────────
        private const int VAG_HEADER_SIZE = 0x30;
        private const int VAG_DATA_SIZE_OFFSET = 0x0C; // U32 BE
        private const int VAG_SAMPLE_RATE_OFFSET = 0x10; // U32 BE

        // ─────────────────────────────────────────────────────────────────
        // PARAM HELPERS — exact mirror of ps2-bankmod.py
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        ///     get_vag_param_offset(hdbuf, vagi_chunk_offset, index)
        ///     = hdbuf[ vagi + 0x10 + (index * 4) ]  (U32 LE)
        /// </summary>
        private static int GetVagParamOffset(byte[] hd, int vagi, int index)
        {
            return (int)ReadU32LE(hd, vagi + VAGI_PARAM_TABLE_OFFSET + (index * 4));
        }

        /// <summary>
        ///     get_vag_offset(hdbuf, vagi_chunk_offset, index)
        ///     = hdbuf[ vagi + get_vag_param_offset(index) + 0x00 ]  (U32 LE)
        /// </summary>
        private static int GetVagOffset(byte[] hd, int vagi, int index)
        {
            return (int)ReadU32LE(hd, vagi + GetVagParamOffset(hd, vagi, index) + 0x00);
        }

        /// <summary>
        ///     get_vag_sample_rate(hdbuf, vagi_chunk_offset, index)
        ///     = hdbuf[ vagi + get_vag_param_offset(index) + 0x04 ]  (U16 LE)
        /// </summary>
        private static ushort GetVagSampleRate(byte[] hd, int vagi, int index)
        {
            return ReadU16LE(hd, vagi + GetVagParamOffset(hd, vagi, index) + 0x04);
        }

        /// <summary>
        ///     put_vag_offset — writes new BD offset for a VAG index
        /// </summary>
        private static void PutVagOffset(byte[] hd, int vagi, int index, uint value)
        {
            WriteU32LE(hd, vagi + GetVagParamOffset(hd, vagi, index) + 0x00, value);
        }

        /// <summary>
        ///     put_vag_sample_rate — writes new sample rate for a VAG index
        /// </summary>
        private static void PutVagSampleRate(byte[] hd, int vagi, int index, ushort value)
        {
            WriteU16LE(hd, vagi + GetVagParamOffset(hd, vagi, index) + 0x04, value);
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        ///     Computes the size of a VAG at the given index.
        ///     Last VAG: bd_size - vag_offset
        ///     Others:   next_vag_offset - this_vag_offset
        /// </summary>
        private static int GetVagSize(byte[] hd, int vagi, int index, int maxIndex)
        {
            int vagOffset = GetVagOffset(hd, vagi, index);
            if (index < maxIndex)
            {
                int nextOffset = GetVagOffset(hd, vagi, index + 1);
                return nextOffset - vagOffset;
            }
            else
            {
                uint bdSize = ReadU32LE(hd, HD_BD_SIZE_OFFSET);
                return (int)bdSize - vagOffset;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        ///     Extracts a single VAG by index from BD/HD.
        /// </summary>
        public static void ExtractVag(
            string HdPath,
            string BdPath,
            int Index,
            string OutputVagPath)
        {
            byte[] HdData = File.ReadAllBytes(HdPath);
            byte[] BdData = File.ReadAllBytes(BdPath);

            ValidateHd(HdData);

            int Vagi = (int)ReadU32LE(HdData, HD_VAGI_OFFSET_OFFSET);
            int MaxIndex = (int)ReadU32LE(HdData, Vagi + VAGI_MAX_INDEX_OFFSET);

            if (Index < 0 || Index > MaxIndex)
                throw new ArgumentException(string.Format(
                    "Invalid VAG index {0}. Valid range: 0-{1}.",
                    Index, MaxIndex));

            int VagOffset = GetVagOffset(HdData, Vagi, Index);
            ushort VagRate = GetVagSampleRate(HdData, Vagi, Index);
            int VagSize = GetVagSize(HdData, Vagi, Index, MaxIndex);

            if (VagOffset < 0 || VagOffset + VagSize > BdData.Length)
                throw new InvalidDataException(string.Format(
                    "VAG index {0} points outside BD " +
                    "(offset 0x{1:X}, size 0x{2:X}).",
                    Index, VagOffset, VagSize));

            // ── Build VAGp header ────────────────────────────────────────
            byte[] Header = new byte[VAG_HEADER_SIZE];
            Header[0] = (byte)'V';
            Header[1] = (byte)'A';
            Header[2] = (byte)'G';
            Header[3] = (byte)'p';
            WriteU32BE(Header, 0x04, 0x20);
            WriteU32BE(Header, VAG_DATA_SIZE_OFFSET, (uint)VagSize);
            WriteU32BE(Header, VAG_SAMPLE_RATE_OFFSET, (uint)VagRate);

            // ── Write .VAG ───────────────────────────────────────────────
            byte[] Adpcm = new byte[VagSize];
            Array.Copy(BdData, VagOffset, Adpcm, 0, VagSize);

            using (FileStream fs = new FileStream(OutputVagPath,
                FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(Header, 0, Header.Length);
                fs.Write(Adpcm, 0, Adpcm.Length);
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(string.Format(
                "  Extracted index {0} -> {1} " +
                "(rate {2} Hz, size 0x{3:X})",
                Index,
                Path.GetFileName(OutputVagPath),
                VagRate, VagSize));
            Console.ResetColor();
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        ///     Extracts all VAGs from BD/HD into a folder.
        ///     Output files named 000.VAG, 001.VAG, ...
        /// </summary>
        public static void ExtractAllVags(
            string HdPath,
            string BdPath,
            string OutFolder)
        {
            byte[] HdData = File.ReadAllBytes(HdPath);
            byte[] BdData = File.ReadAllBytes(BdPath);

            ValidateHd(HdData);

            int Vagi = (int)ReadU32LE(HdData, HD_VAGI_OFFSET_OFFSET);
            int MaxIndex = (int)ReadU32LE(HdData, Vagi + VAGI_MAX_INDEX_OFFSET);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format(
                "  Found {0} VAG(s) in bank. Extracting all...",
                MaxIndex + 1));
            Console.ResetColor();

            Directory.CreateDirectory(OutFolder);

            for (int Index = 0; Index <= MaxIndex; Index++)
            {
                int VagOffset = GetVagOffset(HdData, Vagi, Index);
                ushort VagRate = GetVagSampleRate(HdData, Vagi, Index);
                int VagSize = GetVagSize(HdData, Vagi, Index, MaxIndex);

                // ── Bounds check ─────────────────────────────────────────
                if (VagOffset < 0 ||
                    VagSize <= 0 ||
                    VagOffset + VagSize > BdData.Length)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(string.Format(
                        "  Skipped index {0:000} " +
                        "(out of bounds: offset 0x{1:X}, size 0x{2:X}).",
                        Index, VagOffset, VagSize));
                    Console.ResetColor();
                    continue;
                }

                // ── Build VAGp header ─────────────────────────────────────
                byte[] Header = new byte[VAG_HEADER_SIZE];
                Header[0] = (byte)'V';
                Header[1] = (byte)'A';
                Header[2] = (byte)'G';
                Header[3] = (byte)'p';
                WriteU32BE(Header, 0x04, 0x20);
                WriteU32BE(Header, VAG_DATA_SIZE_OFFSET, (uint)VagSize);
                WriteU32BE(Header, VAG_SAMPLE_RATE_OFFSET, (uint)VagRate);

                // ── Write .VAG ────────────────────────────────────────────
                string OutPath = Path.Combine(OutFolder,
                    string.Format("{0:000}.VAG", Index));

                byte[] Adpcm = new byte[VagSize];
                Array.Copy(BdData, VagOffset, Adpcm, 0, VagSize);

                using (FileStream fs = new FileStream(OutPath,
                    FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(Header, 0, Header.Length);
                    fs.Write(Adpcm, 0, Adpcm.Length);
                }

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(string.Format(
                    "  Extracted index {0:00} -> {1} " +
                    "(rate {2} Hz, size 0x{3:X})",
                    Index,
                    Path.GetFileName(OutPath),
                    VagRate, VagSize));
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format(
                "\n  Output folder : {0}",
                Path.GetFileName(OutFolder)));
            Console.ResetColor();
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        ///     Replaces a single VAG by index in BD/HD.
        ///     Mirrors ps2-bankmod.py import mode exactly.
        /// </summary>
        public static void ImportVag(
            string HdPath,
            string BdPath,
            int Index,
            string InputVagPath)
        {
            byte[] HdData = File.ReadAllBytes(HdPath);
            byte[] BdData = File.ReadAllBytes(BdPath);

            ValidateHd(HdData);

            int Vagi = (int)ReadU32LE(HdData, HD_VAGI_OFFSET_OFFSET);
            int MaxIndex = (int)ReadU32LE(HdData, Vagi + VAGI_MAX_INDEX_OFFSET);

            if (Index < 0 || Index > MaxIndex)
                throw new ArgumentException(string.Format(
                    "Invalid VAG index {0}. Valid range: 0-{1}.",
                    Index, MaxIndex));

            // ── Read input VAG ───────────────────────────────────────────
            byte[] VagData = File.ReadAllBytes(InputVagPath);
            if (VagData.Length < VAG_HEADER_SIZE ||
                VagData[0] != (byte)'V' ||
                VagData[1] != (byte)'A' ||
                VagData[2] != (byte)'G' ||
                VagData[3] != (byte)'p')
                throw new InvalidDataException(
                    "Invalid VAG file! Expected 'VAGp' magic at 0x00.");

            uint NewRate = ReadU32BE(VagData, VAG_SAMPLE_RATE_OFFSET);
            byte[] NewAdpcm = new byte[VagData.Length - VAG_HEADER_SIZE];
            Array.Copy(VagData, VAG_HEADER_SIZE, NewAdpcm, 0, NewAdpcm.Length);
            int NewAdpcmSz = NewAdpcm.Length;

            // ── Get old VAG info ─────────────────────────────────────────
            int OldOffset = GetVagOffset(HdData, Vagi, Index);
            int OldSize = GetVagSize(HdData, Vagi, Index, MaxIndex);

            // ── Update sample rate ───────────────────────────────────────
            PutVagSampleRate(HdData, Vagi, Index, (ushort)(NewRate & 0xFFFF));

            // ── Update offsets for subsequent VAGs ───────────────────────
            int Delta = NewAdpcmSz - OldSize;
            for (int sub = Index + 1; sub <= MaxIndex; sub++)
            {
                int SubOldOffset = GetVagOffset(HdData, Vagi, sub);
                PutVagOffset(HdData, Vagi, sub,
                    (uint)(SubOldOffset + Delta));
            }

            // ── Update BD size ───────────────────────────────────────────
            uint OldBdSize = ReadU32LE(HdData, HD_BD_SIZE_OFFSET);
            WriteU32LE(HdData, HD_BD_SIZE_OFFSET,
                (uint)((int)OldBdSize + Delta));

            // ── Rebuild BD ───────────────────────────────────────────────
            byte[] NewBd = new byte[BdData.Length + Delta];
            Array.Copy(BdData, 0,
                NewBd, 0, OldOffset);
            Array.Copy(NewAdpcm, 0,
                NewBd, OldOffset, NewAdpcmSz);
            Array.Copy(BdData, OldOffset + OldSize,
                NewBd, OldOffset + NewAdpcmSz,
                BdData.Length - (OldOffset + OldSize));

            // ── Write files ──────────────────────────────────────────────
            File.WriteAllBytes(HdPath, HdData);
            File.WriteAllBytes(BdPath, NewBd);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format(
                "  Replaced index {0} " +
                "(rate {1} Hz, old 0x{2:X} -> new 0x{3:X})",
                Index, NewRate, OldSize, NewAdpcmSz));
            Console.ResetColor();
        }

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        ///     Replaces all VAGs from a folder in BD/HD.
        ///     - More files than max index: stops at max index
        ///     - Fewer files than max index: replaces only up to file count
        /// </summary>
        public static void ReplaceAllVags(
    string HdPath,
    string BdPath,
    string FolderWithVags)
        {
            byte[] HdData = File.ReadAllBytes(HdPath);
            byte[] BdData = File.ReadAllBytes(BdPath);

            ValidateHd(HdData);

            int Vagi = (int)ReadU32LE(HdData, HD_VAGI_OFFSET_OFFSET);
            int MaxIndex = (int)ReadU32LE(HdData, Vagi + VAGI_MAX_INDEX_OFFSET);

            // ── Scan folder ──────────────────────────────────────────────
            string[] VagFiles = Directory.GetFiles(FolderWithVags, "*.VAG");
            if (VagFiles.Length == 0)
                throw new ArgumentException(
                    "No .VAG files found in folder: " + FolderWithVags);

            Array.Sort(VagFiles, StringComparer.OrdinalIgnoreCase);

            int ReplaceCount = Math.Min(VagFiles.Length, MaxIndex + 1);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format(
                "  Found {0} .VAG file(s). " +
                "Will replace {1} VAG(s) (max index {2}).",
                VagFiles.Length, ReplaceCount, MaxIndex));
            Console.ResetColor();

            byte[] NewBd = new byte[BdData.Length];
            Array.Copy(BdData, 0, NewBd, 0, BdData.Length);

            for (int i = 0; i < ReplaceCount; i++)
            {
                string InPath = VagFiles[i];
                byte[] VagData = File.ReadAllBytes(InPath);

                // ── Validate ─────────────────────────────────────────────
                if (VagData.Length < VAG_HEADER_SIZE ||
                    VagData[0] != (byte)'V' ||
                    VagData[1] != (byte)'A' ||
                    VagData[2] != (byte)'G' ||
                    VagData[3] != (byte)'p')
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(string.Format(
                        "  Skipped {0} (missing VAGp magic).",
                        Path.GetFileName(InPath)));
                    Console.ResetColor();
                    continue;
                }

                uint NewRate = ReadU32BE(VagData, VAG_SAMPLE_RATE_OFFSET);
                byte[] NewAdpcm = new byte[VagData.Length - VAG_HEADER_SIZE];
                Array.Copy(VagData, VAG_HEADER_SIZE,
                    NewAdpcm, 0, NewAdpcm.Length);
                int NewAdpcmSz = NewAdpcm.Length;

                // ── Get old VAG info from CURRENT HD state ───────────────
                // GetVagOffset reads the CURRENT offset from HD which is
                // already updated from previous iterations — use it directly
                // as the position in NewBd (no TotalDelta needed)
                int OldOffset = GetVagOffset(HdData, Vagi, i);
                int OldSize = GetVagSize(HdData, Vagi, i, MaxIndex);
                int Delta = NewAdpcmSz - OldSize;

                // ── Update sample rate in HD ─────────────────────────────
                PutVagSampleRate(HdData, Vagi, i,
                    (ushort)(NewRate & 0xFFFF));

                // ── Update subsequent VAG offsets in HD ──────────────────
                for (int sub = i + 1; sub <= MaxIndex; sub++)
                {
                    int SubOld = GetVagOffset(HdData, Vagi, sub);
                    PutVagOffset(HdData, Vagi, sub,
                        (uint)(SubOld + Delta));
                }

                // ── Update BD size in HD ─────────────────────────────────
                uint OldBdSize = ReadU32LE(HdData, HD_BD_SIZE_OFFSET);
                WriteU32LE(HdData, HD_BD_SIZE_OFFSET,
                    (uint)((int)OldBdSize + Delta));

                // ── Splice into NewBd ─────────────────────────────────────
                // OldOffset is already the correct position in NewBd
                // because HD offsets are updated each iteration
                byte[] Spliced = new byte[NewBd.Length + Delta];

                Array.Copy(NewBd, 0,
                    Spliced, 0, OldOffset);
                Array.Copy(NewAdpcm, 0,
                    Spliced, OldOffset, NewAdpcmSz);
                Array.Copy(NewBd, OldOffset + OldSize,
                    Spliced, OldOffset + NewAdpcmSz,
                    NewBd.Length - (OldOffset + OldSize));

                NewBd = Spliced;

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(string.Format(
                    "  Replaced index {0:00} ({1}) " +
                    "rate {2} Hz (old 0x{3:X} -> new 0x{4:X})",
                    i,
                    Path.GetFileName(InPath),
                    NewRate, OldSize, NewAdpcmSz));
                Console.ResetColor();
            }

            // ── Write files ──────────────────────────────────────────────
            File.WriteAllBytes(HdPath, HdData);
            File.WriteAllBytes(BdPath, NewBd);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(string.Format(
                "\n  Saved : {0} / {1}",
                Path.GetFileName(BdPath),
                Path.GetFileName(HdPath)));
            Console.ResetColor();
        }

        // ─────────────────────────────────────────────────────────────────
        // VALIDATION
        // ─────────────────────────────────────────────────────────────────

        private static void ValidateHd(byte[] HdData)
        {
            if (HdData.Length < 0x40)
                throw new InvalidDataException("HD file too small.");

            if (ReadAscii(HdData, 0x00, 8) != "IECSsreV" ||
                ReadAscii(HdData, 0x10, 8) != "IECSdaeH")
                throw new InvalidDataException(
                    "HD file missing expected magic " +
                    "(IECSsreV / IECSdaeH).");
        }

        // ─────────────────────────────────────────────────────────────────
        // BINARY HELPERS
        // ─────────────────────────────────────────────────────────────────

        private static string ReadAscii(byte[] buf, int offset, int len)
        {
            return System.Text.Encoding.ASCII
                .GetString(buf, offset, len).TrimEnd('\0');
        }

        private static uint ReadU32LE(byte[] buf, int offset)
        {
            return (uint)(
                 buf[offset] |
                (buf[offset + 1] << 8) |
                (buf[offset + 2] << 16) |
                (buf[offset + 3] << 24));
        }

        private static ushort ReadU16LE(byte[] buf, int offset)
        {
            return (ushort)(buf[offset] | (buf[offset + 1] << 8));
        }

        private static uint ReadU32BE(byte[] buf, int offset)
        {
            return (uint)(
                (buf[offset] << 24) |
                (buf[offset + 1] << 16) |
                (buf[offset + 2] << 8) |
                 buf[offset + 3]);
        }

        private static void WriteU32LE(byte[] buf, int offset, uint value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteU16LE(byte[] buf, int offset, ushort value)
        {
            buf[offset + 0] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteU32BE(byte[] buf, int offset, uint value)
        {
            buf[offset + 0] = (byte)((value >> 24) & 0xFF);
            buf[offset + 1] = (byte)((value >> 16) & 0xFF);
            buf[offset + 2] = (byte)((value >> 8) & 0xFF);
            buf[offset + 3] = (byte)(value & 0xFF);
        }
    }
}