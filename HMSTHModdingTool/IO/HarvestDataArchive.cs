using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HMSTHModdingTool.IO.Compression;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles the HDA format from Harvest Moon: Save the Homeland.
    ///     Supports smart file recognition for known formats:
    ///     .GDTB, .RDTB, .SRDB (3D/Texture archives)
    ///     .HDA  (nested HDA archives)
    ///     .BD, .HD, .SQ (PS2 Sound Bank audio)
    /// </summary>
    class HarvestDataArchive
    {
        // ═══════════════════════════════════════════════════════
        // MAGIC BYTES - FILE FORMAT SIGNATURES
        // ═══════════════════════════════════════════════════════

        // "RDTB" - 3D Model/Render Data Table Binary
        private static readonly byte[] MAGIC_RDTB =
            { 0x52, 0x44, 0x54, 0x42 };

        // "GDTB" - Graphics/Texture Data Table Binary
        private static readonly byte[] MAGIC_GDTB =
            { 0x47, 0x44, 0x54, 0x42 };

        // "SRDB" - Stage/Scene Render Data Binary (map models)
        private static readonly byte[] MAGIC_SRDB =
            { 0x53, 0x52, 0x44, 0x42 };

        // HDA - Nested HDA archive
        // "10 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"
        private static readonly byte[] MAGIC_HDA =
        {
            0x10, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        // "IECSsreV" - .HD Sound Bank Header
        // Offset 0x00: 49 45 43 53 73 72 65 56
        private static readonly byte[] MAGIC_HD_START =
            { 0x49, 0x45, 0x43, 0x53, 0x73, 0x72, 0x65, 0x56 };

        // "IECSquoS" - .SQ MIDI Sequence
        // Offset 0x00: 49 45 43 53 73 72 65 56 (same first 8)
        // Offset 0x10: 49 45 43 53 75 71 65 53
        private static readonly byte[] MAGIC_SQ_LINE2 =
            { 0x49, 0x45, 0x43, 0x53, 0x75, 0x71, 0x65, 0x53 };

        // ═══════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Unpacks the data inside a HDA file.
        /// </summary>
        /// <param name="Data">The full path to the HDA file</param>
        /// <param name="OutputFolder">
        ///     The output folder where the files should be placed
        /// </param>
        public static void Unpack(string Data, string OutputFolder)
        {
            using (FileStream Input =
                new FileStream(Data, FileMode.Open))
            {
                // Derive archive base name from file name
                // e.g. "COMMON.HDA" -> "COMMON"
                // e.g. "BGM_FRM.HDA" -> "BGM_FRM"
                string archiveName =
                    Path.GetFileNameWithoutExtension(Data)
                        .ToUpper();

                Unpack(Input, OutputFolder, archiveName);
            }
        }

        /// <summary>
        ///     Unpacks the data inside a HDA file stream.
        /// </summary>
        /// <param name="Data">The HDA stream</param>
        /// <param name="OutputFolder">
        ///     The output folder where the files should be placed
        /// </param>
        /// <param name="archiveName">
        ///     The base name used for output files (e.g. "COMMON")
        /// </param>
        public static void Unpack(
            Stream Data,
            string OutputFolder,
            string archiveName = "FILE")
        {
            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            BinaryReader Reader = new BinaryReader(Data);

            uint BaseOffset = Reader.ReadUInt32();

            // ── Collect all raw file buffers first ──────────────
            // We need all buffers before naming audio files,
            // because .BD/.HD/.SQ identity depends on their
            // position relative to each other.
            var buffers = new List<byte[]>();

            Data.Seek(BaseOffset, SeekOrigin.Begin);
            uint Offset = Reader.ReadUInt32();
            uint FirstOffset = BaseOffset + Offset;

            while (Data.Position < FirstOffset + 4)
            {
                long Position = Data.Position;
                Data.Seek(BaseOffset + Offset, SeekOrigin.Begin);

                bool IsCompressed = Reader.ReadUInt32() == 1;
                uint DecompressedLength = Reader.ReadUInt32();
                uint CompressedLength = Reader.ReadUInt32();
                uint Padding = Reader.ReadUInt32(); // 0x0

                byte[] Buffer = new byte[CompressedLength];
                Data.Read(Buffer, 0, Buffer.Length);

                if (IsCompressed)
                    Buffer = HarvestCompression.Decompress(Buffer);

                buffers.Add(Buffer);

                Data.Seek(Position, SeekOrigin.Begin);
                Offset = Reader.ReadUInt32();
                if (Offset == 0 &&
                    Data.Position - 4 > BaseOffset) break;
            }

            // ── Determine if this is an audio HDA ───────────────
            // Audio HDA = BAR.HDA / SE.HDA style
            // Detected by checking if files match
            // .BD/.HD/.SQ or .BD/.HD patterns
            bool isAudioHDA = DetectAudioHDA(buffers);

            // ── Write all files with smart names ────────────────
            if (isAudioHDA)
            {
                WriteAudioFiles(
                    buffers, OutputFolder, archiveName);
            }
            else
            {
                WriteDataFiles(
                    buffers, OutputFolder, archiveName);
            }
        }

        // ═══════════════════════════════════════════════════════
        // PACK
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Packs the data inside a folder into a HDA file.
        /// </summary>
        public static void Pack(string Data, string InputFolder)
        {
            using (FileStream Output =
                new FileStream(Data, FileMode.Create))
            {
                Pack(Output, InputFolder);
            }
        }

        /// <summary>
        ///     Packs the data inside a folder into a HDA file.
        ///     Files are sorted to maintain correct order.
        ///     Named files (e.g. COMMON_00000.GDTB) are
        ///     sorted by their numeric index so order is
        ///     preserved correctly on repack.
        /// </summary>
        public static void Pack(Stream Data, string InputFolder)
        {
            // Sort files by numeric index embedded in filename
            // e.g. COMMON_00000.GDTB < COMMON_00003.bin
            string[] Files = GetSortedFiles(InputFolder);

            BinaryWriter Writer = new BinaryWriter(Data);

            Writer.Write(0x10u);
            Data.Seek(0xc, SeekOrigin.Current);

            int DataOffset = Align(Files.Length * 4);
            for (int i = 0; i < Files.Length; i++)
            {
                Data.Seek(0x10 + i * 4, SeekOrigin.Begin);
                Writer.Write(DataOffset);

                byte[] Buffer = File.ReadAllBytes(Files[i]);
                Data.Seek(DataOffset + 0x10, SeekOrigin.Begin);
                DataOffset += Buffer.Length + 0x10;
                DataOffset = Align(DataOffset);

                Writer.Write(0u); // Uncompressed
                Writer.Write(Buffer.Length);
                Writer.Write(Buffer.Length);
                Writer.Write(0u);

                Data.Write(Buffer, 0, Buffer.Length);
            }

            while ((Data.Position & 0xf) != 0)
                Data.WriteByte(0);
        }

        // ═══════════════════════════════════════════════════════
        // AUDIO HDA DETECTION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Detects whether this HDA is an audio archive.
        ///
        ///     Audio HDA patterns:
        ///     BGM style:  [BD] [HD] [SQ]  (3 files)
        ///     SE  style:  [BD] [HD]        (2 files)
        ///
        ///     .HD is identified by magic "IECSsreV"
        ///     .SQ is identified by:
        ///         offset 0x00: "IECSsreV"
        ///         offset 0x10: "IECSquoS"
        ///     .BD has no strict magic but is the
        ///         first file when HD is second.
        /// </summary>
        private static bool DetectAudioHDA(List<byte[]> buffers)
        {
            if (buffers.Count < 2 || buffers.Count > 3)
                return false;

            // Check if file[1] is .HD
            if (!IsHDFile(buffers[1]))
                return false;

            // For 3-file archive, check file[2] is .SQ
            if (buffers.Count == 3 &&
                !IsSQFile(buffers[2]))
                return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════
        // WRITE AUDIO FILES  (no _XXXXX numbering)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Writes audio files with names:
        ///     BGM_FRM.BD, BGM_FRM.HD, BGM_FRM.SQ
        ///     or
        ///     SE.BD, SE.HD
        ///
        ///     The archiveName IS the track/bank name.
        ///     Order: BD first, HD second, SQ third.
        /// </summary>
        private static void WriteAudioFiles(
            List<byte[]> buffers,
            string outputFolder,
            string archiveName)
        {
            // Extensions in order: BD, HD, SQ
            string[] audioExt =
                { ".BD", ".HD", ".SQ" };

            for (int i = 0; i < buffers.Count; i++)
            {
                string ext = (i < audioExt.Length)
                    ? audioExt[i]
                    : string.Format("_{0:D5}.bin", i);

                string fileName = archiveName + ext;
                string filePath =
                    Path.Combine(outputFolder, fileName);

                File.WriteAllBytes(filePath, buffers[i]);

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  [AUDIO] " + fileName);
                Console.ResetColor();
            }
        }

        // ═══════════════════════════════════════════════════════
        // WRITE DATA FILES  (with _XXXXX numbering + extension)
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Writes data files using smart naming:
        ///
        ///     Recognized:
        ///       COMMON_00000.GDTB
        ///       COMMON_00003.RDTB
        ///       COMMON_00008.SRDB
        ///       COMMON_00001.HDA  (nested)
        ///
        ///     Unrecognized:
        ///       COMMON_00001.bin
        ///       COMMON_00002.bin
        /// </summary>
        private static void WriteDataFiles(
            List<byte[]> buffers,
            string outputFolder,
            string archiveName)
        {
            for (int i = 0; i < buffers.Count; i++)
            {
                // 0-based index, 5 digits: _00000
                int fileNumber = i;

                string detectedExt =
                    DetectExtension(buffers[i]);

                string fileName = (detectedExt == ".HDA")
                    ? string.Format(
                        "{0}_{1:D2}{2}",
                        archiveName,
                        fileNumber,
                        detectedExt)
                    : string.Format(
                        "{0}_{1:D5}{2}",
                        archiveName,
                        fileNumber,
                        detectedExt);

                string filePath =
                    Path.Combine(outputFolder, fileName);

                File.WriteAllBytes(filePath, buffers[i]);

                // Color-code output by type
                if (detectedExt == ".bin")
                {
                    Console.ForegroundColor =
                        ConsoleColor.DarkGray;
                    Console.WriteLine(
                        "  [???]   " + fileName);
                }
                else
                {
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "  [" +
                        detectedExt.TrimStart('.')
                            .PadRight(4) +
                        "] " + fileName);
                }

                Console.ResetColor();
            }
        }

        // ═══════════════════════════════════════════════════════
        // FILE FORMAT DETECTION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Detects the file extension based on magic bytes.
        ///     Returns ".GDTB", ".RDTB", ".SRDB", ".HDA",
        ///             ".HD",   ".SQ",  or ".bin"
        /// </summary>
        private static string DetectExtension(byte[] data)
        {
            if (data == null || data.Length < 4)
                return ".bin";

            // ── 4-byte magic checks ────────────────────────────
            if (StartsWith(data, MAGIC_GDTB))
                return ".gdtb";

            if (StartsWith(data, MAGIC_RDTB))
                return ".rdtb";

            if (StartsWith(data, MAGIC_SRDB))
                return ".srdb";

            // ── 16-byte HDA magic ──────────────────────────────
            if (data.Length >= 16 &&
                StartsWith(data, MAGIC_HDA))
                return ".HDA";

            // ── Audio file checks ──────────────────────────────
            // .SQ must be checked BEFORE .HD
            // because .SQ also starts with MAGIC_HD_START
            if (IsSQFile(data))
                return ".SQ";

            if (IsHDFile(data))
                return ".HD";

            // .BD has no reliable magic in all cases.
            // It is identified by position in audio HDA,
            // not here. So it falls through to .bin
            // unless detected in context.
            return ".bin";
        }

        // ─────────────────────────────────────────
        // .HD Detection
        // Magic at offset 0x00: 49 45 43 53 73 72 65 56
        // Second line (0x10) must NOT be SQ marker
        // ─────────────────────────────────────────
        private static bool IsHDFile(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;

            // Must start with IECSsreV
            if (!StartsWith(data, MAGIC_HD_START))
                return false;

            // If it is a .SQ it will also have
            // MAGIC_SQ_LINE2 at offset 0x10
            // So if that matches -> it is .SQ not .HD
            if (IsSQFile(data))
                return false;

            return true;
        }

        // ─────────────────────────────────────────
        // .SQ Detection
        // Offset 0x00: 49 45 43 53 73 72 65 56
        // Offset 0x10: 49 45 43 53 75 71 65 53
        // ─────────────────────────────────────────
        private static bool IsSQFile(byte[] data)
        {
            if (data == null || data.Length < 0x18)
                return false;

            // First 8 bytes = IECSsreV
            if (!StartsWith(data, MAGIC_HD_START))
                return false;

            // Bytes at offset 0x10 = IECSquoS
            for (int i = 0; i < MAGIC_SQ_LINE2.Length; i++)
            {
                if (data[0x10 + i] != MAGIC_SQ_LINE2[i])
                    return false;
            }

            return true;
        }

        // ─────────────────────────────────────────
        // Byte Array StartsWith helper
        // ─────────────────────────────────────────
        private static bool StartsWith(
            byte[] data,
            byte[] magic)
        {
            if (data.Length < magic.Length)
                return false;

            for (int i = 0; i < magic.Length; i++)
            {
                if (data[i] != magic[i])
                    return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════
        // SORTED FILE LISTING FOR PACK
        // Sorts by embedded number in filename
        // COMMON_00000.GDTB -> index 0
        // COMMON_00003.bin  -> index 3
        // BGM_FRM.BD        -> index 0 (audio, no number)
        // ═══════════════════════════════════════════════════════
        private static string[] GetSortedFiles(
            string inputFolder)
        {
            string[] files =
                Directory.GetFiles(inputFolder);

            Array.Sort(files, (a, b) =>
            {
                int idxA = ExtractFileIndex(
                    Path.GetFileName(a));
                int idxB = ExtractFileIndex(
                    Path.GetFileName(b));
                return idxA.CompareTo(idxB);
            });

            return files;
        }

        /// <summary>
        ///     Extracts the numeric index from a filename.
        ///     "COMMON_00003.bin" -> 3
        ///     "BGM_FRM.BD"       -> 0  (audio, no index)
        ///     Falls back to 0 if no number found.
        /// </summary>
        private static int ExtractFileIndex(
            string fileName)
        {
            // Strip extension
            string name =
                Path.GetFileNameWithoutExtension(
                    fileName);

            // Find last underscore
            int underscorePos = name.LastIndexOf('_');
            if (underscorePos < 0)
                return 0;

            string numPart =
                name.Substring(underscorePos + 1);

            int result;
            if (int.TryParse(numPart, out result))
                return result;

            return 0;
        }

        // ═══════════════════════════════════════════════════════
        // ALIGNMENT HELPER
        // ═══════════════════════════════════════════════════════
        private static int Align(int Value)
        {
            if ((Value & 0xf) != 0)
                Value = ((Value & ~0xf) + 0x10);
            return Value;
        }
    }
}