using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using HMSTHModdingTool.IO.Compression;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles the HDA format from Harvest Moon: Save the Homeland.
    ///
    ///     Supports:
    ///       -xhda  : extract (decompress if needed)
    ///       -chda  : pack uncompressed
    ///       -chda comp : pack with HMSTH LZO compression + progress bar
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
                    Data.Position - 4 > BaseOffset) break;
            }

            bool isAudioHDA = DetectAudioHDA(buffers);

            if (isAudioHDA)
                WriteAudioFiles(buffers, OutputFolder, archiveName);
            else
                WriteDataFiles(buffers, OutputFolder, archiveName);
        }

        // ═══════════════════════════════════════════════════════
        // PACK  — uncompressed  (original behaviour)
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
        // PACK COMPRESSED
        // ═══════════════════════════════════════════════════════

        /// <summary>
        ///     Packs all files from InputFolder into a HDA archive,
        ///     compressing each file with the HMSTH LZO compressor.
        ///
        ///     HDA entry header layout (16 bytes per file):
        ///       [0x00] uint32  compressed flag  (1 = compressed)
        ///       [0x04] uint32  decompressed size
        ///       [0x08] uint32  compressed (stored) size
        ///       [0x0C] uint32  padding (0)
        ///
        ///     The offset table at the start of the HDA points to each
        ///     entry's header (not the data).  The first uint32 of the
        ///     HDA is the base offset of the offset table itself (0x10).
        /// </summary>
        public static void PackCompressed(
            string outputHda,
            string inputFolder)
        {
            string[] files = GetSortedFiles(inputFolder);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Packing " + files.Length +
                " file(s) with compression → " +
                Path.GetFileName(outputHda));
            Console.ResetColor();
            Console.WriteLine();

            // ── Compress all files first ──────────────────────
            // We need sizes before we can write the offset table.
            var rawDatas = new byte[files.Length][];
            var compDatas = new byte[files.Length][];

            long totalRaw = 0;
            long totalComp = 0;

            for (int i = 0; i < files.Length; i++)
            {
                string fname = Path.GetFileName(files[i]);
                rawDatas[i] = File.ReadAllBytes(files[i]);
                int rawLen = rawDatas[i].Length;
                totalRaw += rawLen;

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(
                    "  [{0}/{1}] {2,-30}  ",
                    i + 1, files.Length,
                    fname.Length > 30
                        ? fname.Substring(0, 27) + "..."
                        : fname);
                Console.ResetColor();

                // ── Compression progress bar ──────────────────
                var sw = Stopwatch.StartNew();

                // We pass a callback that draws the progress bar.
                // The callback is invoked for each percentage point.
                int barWidth = 38;
                int lastDrawn = -1;

                void OnProgress(int cur, int total)
                {
                    double frac = total == 0
                        ? 1.0
                        : (double)cur / total;
                    int pct = (int)(frac * 100.0);
                    if (pct == lastDrawn) return;
                    lastDrawn = pct;

                    double elSec = sw.Elapsed.TotalSeconds;
                    double mbDone = cur / 1048576.0;
                    double mbTotal = total / 1048576.0;
                    double mbps = elSec > 0
                        ? (cur / 1048576.0) / elSec
                        : 0;
                    double eta = (mbps > 0 && frac < 1.0)
                        ? (total - cur) / 1048576.0 / mbps
                        : 0;

                    int filled = (int)(frac * barWidth);
                    var bar = new StringBuilder("[");
                    for (int b = 0; b < barWidth; b++)
                        bar.Append(b < filled ? '█' : '░');
                    bar.Append(']');

                    // Format sizes with KB/MB auto-switch
                    string sizeStr = total < 1024 * 1024
                        ? string.Format(
                            "{0:F1}/{1:F1} KB",
                            cur / 1024.0,
                            total / 1024.0)
                        : string.Format(
                            "{0:F1}/{1:F1} MB",
                            mbDone, mbTotal);

                    string line = string.Format(
                        "\r    Compressing: {0} {1,5:F1}%  {2}  {3:F1} MB/s  ETA {4:F0}s   ",
                        bar, frac * 100.0, sizeStr, mbps, eta);

                    // Write to stderr so it doesn't pollute stdout
                    Console.Error.Write(line);
                }

                compDatas[i] = HarvestCompression.Compress(
                    rawDatas[i],
                    OnProgress);

                sw.Stop();
                double elTotal = sw.Elapsed.TotalSeconds;
                double ratioVal = rawLen == 0
                    ? 0
                    : (double)compDatas[i].Length / rawLen * 100.0;
                totalComp += compDatas[i].Length;

                // Clear the progress line and print final status
                Console.Error.Write(
                    "\r" + new string(' ', 100) + "\r");

                // Final bar (100%)
                string doneBar =
                    "[" + new string('█', barWidth) + "]";

                // Format sizes
                string doneSizes = rawLen < 1024 * 1024
                    ? string.Format(
                        "{0:F1}/{1:F1} KB",
                        rawLen / 1024.0,
                        rawLen / 1024.0)
                    : string.Format(
                        "{0:F1}/{1:F1} MB",
                        rawLen / 1048576.0,
                        rawLen / 1048576.0);

                double mbpsAvg = elTotal > 0
                    ? rawLen / 1048576.0 / elTotal
                    : 0;

                // Print the "Done" line on stderr
                Console.Error.WriteLine(
                    string.Format(
                        "    Compressing: {0} {1,5:F1}%  {2}  " +
                        "{3:F1} MB/s  ETA 0s   " +
                        "✓ Done in {4:F2}s  " +
                        "({5:N0} bytes compressed, {6:F1}% of original)",
                        doneBar,
                        100.0,
                        doneSizes,
                        mbpsAvg,
                        elTotal,
                        compDatas[i].Length,
                        ratioVal));

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓");
                Console.ResetColor();
            }

            // ── Write the HDA file ────────────────────────────
            using (FileStream fs =
                new FileStream(outputHda, FileMode.Create))
            using (BinaryWriter wr = new BinaryWriter(fs))
            {
                // Base offset of offset table = 0x10
                wr.Write(0x10u);
                // 12 bytes padding to reach 0x10
                wr.Write(0u); wr.Write(0u); wr.Write(0u);

                // Calculate where each file's header starts.
                // Offset table: files.Length * 4 bytes, aligned to 16.
                // Each entry: 16-byte header + data + alignment.
                int dataStart = Align(files.Length * 4);
                var entryOffsets = new int[files.Length];
                int cursor = dataStart;

                for (int i = 0; i < files.Length; i++)
                {
                    entryOffsets[i] = cursor;
                    // 16-byte header + compressed data + alignment
                    int entrySize = 0x10 + compDatas[i].Length;
                    entrySize = Align(entrySize);
                    cursor += entrySize;
                }

                // Write offset table
                for (int i = 0; i < files.Length; i++)
                {
                    fs.Seek(0x10 + i * 4, SeekOrigin.Begin);
                    wr.Write((uint)entryOffsets[i]);
                }

                // Write each entry
                for (int i = 0; i < files.Length; i++)
                {
                    fs.Seek(entryOffsets[i] + 0x10,
                            SeekOrigin.Begin);

                    int rawLen = rawDatas[i].Length;
                    int compLen = compDatas[i].Length;

                    wr.Write(1u);               // compressed flag = 1
                    wr.Write((uint)rawLen);     // decompressed size
                    wr.Write((uint)compLen);    // compressed size
                    wr.Write(0u);               // padding

                    fs.Write(compDatas[i], 0, compLen);

                    // Align to 16-byte boundary
                    while ((fs.Position & 0xF) != 0)
                        fs.WriteByte(0);
                }
            }

            // ── Summary ───────────────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ── Summary ──────────────────────────────");
            Console.WriteLine(
                string.Format(
                    "  Files packed  : {0}",
                    files.Length));
            Console.WriteLine(
                string.Format(
                    "  Raw total     : {0:N0} bytes  ({1:F2} MB)",
                    totalRaw,
                    totalRaw / 1048576.0));
            Console.WriteLine(
                string.Format(
                    "  Compressed    : {0:N0} bytes  ({1:F2} MB)",
                    totalComp,
                    totalComp / 1048576.0));

            // HDA file size
            long hdaSize = new FileInfo(outputHda).Length;
            Console.WriteLine(
                string.Format(
                    "  HDA file size : {0:N0} bytes  ({1:F2} MB)",
                    hdaSize,
                    hdaSize / 1048576.0));

            double overallRatio = totalRaw == 0
                ? 0
                : (double)totalComp / totalRaw * 100.0;
            Console.WriteLine(
                string.Format(
                    "  Overall ratio : {0:F1}%",
                    overallRatio));
            Console.WriteLine(
                "  Output        : " + outputHda);
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
                string filePath =
                    Path.Combine(outputFolder, fileName);

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
                    ? string.Format(
                        "{0}_{1:D2}{2}",
                        archiveName, i, detectedExt)
                    : string.Format(
                        "{0}_{1:D5}{2}",
                        archiveName, i, detectedExt);

                string filePath =
                    Path.Combine(outputFolder, fileName);

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
                        detectedExt.TrimStart('.')
                                   .PadRight(4) +
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
                if (data[0x10 + i] != MAGIC_SQ_LINE2[i]) return false;
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
            string name =
                Path.GetFileNameWithoutExtension(fileName);
            int u = name.LastIndexOf('_');
            if (u < 0) return 0;
            int result;
            return int.TryParse(name.Substring(u + 1), out result)
                ? result : 0;
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
