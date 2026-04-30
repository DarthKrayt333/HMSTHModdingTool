using System;
using System.Collections.Generic;
using System.IO;
using HMSTHModdingTool.IO.Compression;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles the HDA format from Harvest Moon: Save the Homeland.
    ///
    ///     Supports:
    ///       -xhda        : extract (decompress if needed)
    ///       -chda uncomp : pack uncompressed
    ///       -chda        : smart pack
    ///
    ///     Smart pack rules:
    ///       - files <= 64 bytes are stored RAW (flag=0)
    ///       - all other files are compressed (flag=1)
    ///       - if normal compression expands, use single-literal
    ///         compressed stream (still flag=1)
    /// </summary>
    class HarvestDataArchive
    {
        // ═══════════════════════════════════════════════════════
        // MAGIC BYTES
        // ═══════════════════════════════════════════════════════

        private static readonly byte[] MAGIC_RDTB =
            { 0x52, 0x44, 0x54, 0x42 };

        private static readonly byte[] MAGIC_GDTB =
            { 0x47, 0x44, 0x54, 0x42 };

        private static readonly byte[] MAGIC_SRDB =
            { 0x53, 0x52, 0x44, 0x42 };

        private static readonly byte[] MAGIC_HDA =
        {
            0x10, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        private static readonly byte[] MAGIC_HD_START =
            { 0x49, 0x45, 0x43, 0x53, 0x73, 0x72, 0x65, 0x56 };

        private static readonly byte[] MAGIC_SQ_LINE2 =
            { 0x49, 0x45, 0x43, 0x53, 0x75, 0x71, 0x65, 0x53 };

        // ═══════════════════════════════════════════════════════
        // UNPACK
        // ═══════════════════════════════════════════════════════

        public static void Unpack(string Data, string OutputFolder)
        {
            using (FileStream Input =
                new FileStream(Data, FileMode.Open))
            {
                string archiveName =
                    Path.GetFileNameWithoutExtension(Data).ToUpper();
                Unpack(Input, OutputFolder, archiveName);
            }
        }

        public static void Unpack(
            Stream Data,
            string OutputFolder,
            string archiveName = "FILE")
        {
            if (!Directory.Exists(OutputFolder))
                Directory.CreateDirectory(OutputFolder);

            BinaryReader Reader = new BinaryReader(Data);

            uint BaseOffset = Reader.ReadUInt32();
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
                uint Padding = Reader.ReadUInt32();

                byte[] Buffer = new byte[CompressedLength];
                Data.Read(Buffer, 0, Buffer.Length);

                if (IsCompressed)
                    Buffer = HarvestCompression.Decompress(Buffer);

                buffers.Add(Buffer);

                Data.Seek(Position, SeekOrigin.Begin);
                Offset = Reader.ReadUInt32();

                if (Offset == 0 &&
                    Data.Position - 4 > BaseOffset)
                    break;
            }

            bool isAudioHDA = DetectAudioHDA(buffers);

            if (isAudioHDA)
                WriteAudioFiles(buffers, OutputFolder, archiveName);
            else
                WriteDataFiles(buffers, OutputFolder, archiveName);
        }

        // ═══════════════════════════════════════════════════════
        // PACK — UNCOMPRESSED
        // ═══════════════════════════════════════════════════════

        public static void Pack(string Data, string InputFolder)
        {
            using (FileStream Output =
                new FileStream(Data, FileMode.Create))
            {
                Pack(Output, InputFolder);
            }
        }

        public static void Pack(Stream Data, string InputFolder)
        {
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

                Writer.Write(0u);               // uncompressed flag
                Writer.Write(Buffer.Length);    // decompressed size
                Writer.Write(Buffer.Length);    // stored size
                Writer.Write(0u);               // padding

                Data.Write(Buffer, 0, Buffer.Length);
            }

            while ((Data.Position & 0xf) != 0)
                Data.WriteByte(0);
        }

        // ═══════════════════════════════════════════════════════
        // PACK — SMART COMPRESSED
        // ═══════════════════════════════════════════════════════

        public static void PackCompressed(
            string outputHda,
            string inputFolder)
        {
            string[] files = GetSortedFiles(inputFolder);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Packing " + files.Length +
                " file(s) with Smart Compression → " +
                Path.GetFileName(outputHda));
            Console.WriteLine(
                "  Rule: files <= 64 bytes = RAW (flag=0). All others = Compressed (flag=1).");
            Console.WriteLine(
                "  If compression expands data, use single-literal compressed stream (minimal overhead).");
            Console.ResetColor();
            Console.WriteLine();

            var rawDatas = new byte[files.Length][];
            var storedDatas = new byte[files.Length][];
            var compressedFlags = new bool[files.Length];

            long totalRaw = 0;
            long totalStored = 0;
            int compCount = 0;
            int rawCount = 0;

            // ── Pretty counter width: [01/50], [002/150], etc. ──
            int indexWidth = Math.Max(2, files.Length.ToString().Length);
            string totalText = files.Length.ToString("D" + indexWidth);

            for (int i = 0; i < files.Length; i++)
            {
                string fname = Path.GetFileName(files[i]);
                rawDatas[i] = File.ReadAllBytes(files[i]);
                int rawLen = rawDatas[i].Length;
                totalRaw += rawLen;

                string currentText = (i + 1).ToString("D" + indexWidth);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(
                    "  [{0}/{1}] {2,-30}  ",
                    currentText,
                    totalText,
                    fname.Length > 30
                        ? fname.Substring(0, 27) + "..."
                        : fname);
                Console.ResetColor();

                // ── Only <= 64 bytes are RAW ───────────────────
                if (rawLen <= 64)
                {
                    storedDatas[i] = rawDatas[i];
                    compressedFlags[i] = false;
                    totalStored += rawLen;
                    rawCount++;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK RAW (<= 64 bytes)");
                    Console.ResetColor();
                    continue;
                }

                // ── Try normal compression first ────────────────
                byte[] comp = HarvestCompression.Compress(rawDatas[i]);
                bool verified = HarvestCompression.VerifyRoundTrip(
                    rawDatas[i], comp);

                // ── If compression fails or expands, use single-literal compressed stream
                if (!verified || comp.Length > rawLen)
                {
                    comp = HarvestCompression.CompressAsLiterals(rawDatas[i]);
                    verified = HarvestCompression.VerifyRoundTrip(
                        rawDatas[i], comp);
                }

                storedDatas[i] = comp;
                compressedFlags[i] = true; // everything > 64 bytes stays compressed flag=1
                totalStored += comp.Length;
                compCount++;

                double ratio = rawLen == 0
                    ? 0
                    : (double)comp.Length / rawLen * 100.0;

                if (ratio <= 100.1)
                    Console.ForegroundColor = ConsoleColor.Green;
                else
                    Console.ForegroundColor = ConsoleColor.Yellow;

                Console.WriteLine(
                    "OK {0:N0} → {1:N0} bytes ({2:F1}%)",
                    rawLen,
                    comp.Length,
                    ratio);
                Console.ResetColor();
            }

            // ── Write HDA ──────────────────────────────────────
            using (FileStream fs =
                new FileStream(outputHda, FileMode.Create))
            using (BinaryWriter wr = new BinaryWriter(fs))
            {
                wr.Write(0x10u);
                wr.Write(0u);
                wr.Write(0u);
                wr.Write(0u);

                int dataStart = Align(files.Length * 4);
                var entryOffsets = new int[files.Length];
                int cursor = dataStart;

                for (int i = 0; i < files.Length; i++)
                {
                    entryOffsets[i] = cursor;
                    cursor += Align(0x10 + storedDatas[i].Length);
                }

                // offset table
                for (int i = 0; i < files.Length; i++)
                {
                    fs.Seek(0x10 + i * 4, SeekOrigin.Begin);
                    wr.Write((uint)entryOffsets[i]);
                }

                // entries
                for (int i = 0; i < files.Length; i++)
                {
                    fs.Seek(entryOffsets[i] + 0x10, SeekOrigin.Begin);

                    wr.Write(compressedFlags[i] ? 1u : 0u);
                    wr.Write((uint)rawDatas[i].Length);
                    wr.Write((uint)storedDatas[i].Length);
                    wr.Write(0u);

                    fs.Write(storedDatas[i], 0, storedDatas[i].Length);

                    while ((fs.Position & 0xF) != 0)
                        fs.WriteByte(0);
                }
            }

            // ── Summary ────────────────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  ── Summary ─────────────────────────────");
            Console.WriteLine(
                "  Files packed   : " + files.Length);
            Console.WriteLine(
                "  Compressed     : " + compCount);
            Console.WriteLine(
                "  Stored RAW     : " + rawCount);

            double overallRatio = totalRaw == 0
                ? 0
                : (double)totalStored / totalRaw * 100.0;

            Console.WriteLine(
                string.Format(
                    "  Overall ratio  : {0:F1}%",
                    overallRatio));
            Console.WriteLine(
                "  Output         : " + outputHda);
            Console.WriteLine(
                "  ─────────────────────────────────────────");
            Console.ResetColor();
        }

        // ═══════════════════════════════════════════════════════
        // AUDIO HDA DETECTION
        // ═══════════════════════════════════════════════════════

        private static bool DetectAudioHDA(List<byte[]> buffers)
        {
            if (buffers.Count < 2 || buffers.Count > 3)
                return false;
            if (!IsHDFile(buffers[1])) return false;
            if (buffers.Count == 3 && !IsSQFile(buffers[2]))
                return false;
            return true;
        }

        // ═══════════════════════════════════════════════════════
        // WRITE HELPERS
        // ═══════════════════════════════════════════════════════

        private static void WriteAudioFiles(
            List<byte[]> buffers,
            string outputFolder,
            string archiveName)
        {
            string[] audioExt = { ".BD", ".HD", ".SQ" };

            for (int i = 0; i < buffers.Count; i++)
            {
                string ext = (i < audioExt.Length)
                    ? audioExt[i]
                    : string.Format("_{0:D5}.bin", i);

                string fileName = archiveName + ext;
                string filePath = Path.Combine(outputFolder, fileName);

                File.WriteAllBytes(filePath, buffers[i]);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("  [AUDIO] " + fileName);
                Console.ResetColor();
            }
        }

        private static void WriteDataFiles(
            List<byte[]> buffers,
            string outputFolder,
            string archiveName)
        {
            for (int i = 0; i < buffers.Count; i++)
            {
                string detectedExt = DetectExtension(buffers[i]);

                string fileName = (detectedExt == ".HDA")
                    ? string.Format("{0}_{1:D2}{2}",
                        archiveName, i, detectedExt)
                    : string.Format("{0}_{1:D5}{2}",
                        archiveName, i, detectedExt);

                string filePath = Path.Combine(outputFolder, fileName);

                File.WriteAllBytes(filePath, buffers[i]);

                if (detectedExt == ".bin")
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  [???]   " + fileName);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(
                        "  [" +
                        detectedExt.TrimStart('.').PadRight(4) +
                        "] " + fileName);
                }

                Console.ResetColor();
            }
        }

        // ═══════════════════════════════════════════════════════
        // EXTENSION DETECTION
        // ═══════════════════════════════════════════════════════

        private static string DetectExtension(byte[] data)
        {
            if (data == null || data.Length < 4)
                return ".bin";

            if (StartsWith(data, MAGIC_GDTB)) return ".gdtb";
            if (StartsWith(data, MAGIC_RDTB)) return ".rdtb";
            if (StartsWith(data, MAGIC_SRDB)) return ".srdb";

            if (data.Length >= 16 && StartsWith(data, MAGIC_HDA))
                return ".HDA";

            if (IsSQFile(data)) return ".SQ";
            if (IsHDFile(data)) return ".HD";

            return ".bin";
        }

        private static bool IsHDFile(byte[] data)
        {
            if (data == null || data.Length < 8) return false;
            if (!StartsWith(data, MAGIC_HD_START)) return false;
            if (IsSQFile(data)) return false;
            return true;
        }

        private static bool IsSQFile(byte[] data)
        {
            if (data == null || data.Length < 0x18) return false;
            if (!StartsWith(data, MAGIC_HD_START)) return false;

            for (int i = 0; i < MAGIC_SQ_LINE2.Length; i++)
                if (data[0x10 + i] != MAGIC_SQ_LINE2[i])
                    return false;

            return true;
        }

        private static bool StartsWith(byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length) return false;

            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;

            return true;
        }

        // ═══════════════════════════════════════════════════════
        // SORTED FILE LISTING
        // ═══════════════════════════════════════════════════════

        private static string[] GetSortedFiles(string inputFolder)
        {
            string[] files = Directory.GetFiles(inputFolder);

            Array.Sort(files, (a, b) =>
            {
                int ia = ExtractFileIndex(Path.GetFileName(a));
                int ib = ExtractFileIndex(Path.GetFileName(b));
                return ia.CompareTo(ib);
            });

            return files;
        }

        private static int ExtractFileIndex(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            int u = name.LastIndexOf('_');
            if (u < 0) return 0;

            int result;
            return int.TryParse(name.Substring(u + 1), out result)
                ? result
                : 0;
        }

        // ═══════════════════════════════════════════════════════
        // ALIGNMENT
        // ═══════════════════════════════════════════════════════

        private static int Align(int Value)
        {
            if ((Value & 0xf) != 0)
                Value = ((Value & ~0xf) + 0x10);
            return Value;
        }
    }
}
