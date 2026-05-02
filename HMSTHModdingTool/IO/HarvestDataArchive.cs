using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HMSTHModdingTool.IO.Compression;

namespace HMSTHModdingTool.IO
{
    class HarvestDataArchive
    {
        // ═══════════════════════════════════════════════════════════
        // MAGIC BYTES
        // ═══════════════════════════════════════════════════════════

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

        // ═══════════════════════════════════════════════════════════
        // UNPACK — PUBLIC ENTRY POINTS
        // ═══════════════════════════════════════════════════════════

        public static void Unpack(string Data, string OutputFolder)
        {
            using (FileStream Input =
                new FileStream(Data, FileMode.Open))
            {
                string archiveName =
                    Path.GetFileNameWithoutExtension(Data)
                        .ToUpper();
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

            // ── Step 1: Read file header ────────────────────────
            uint BaseOffset = Reader.ReadUInt32();

            if (BaseOffset == 0 || BaseOffset > 0x1000)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [WARN] Unexpected BaseOffset = 0x" +
                    BaseOffset.ToString("X") +
                    ". Expected 0x10. Proceeding anyway.");
                Console.ResetColor();
            }

            Data.Seek(BaseOffset, SeekOrigin.Begin);

            // ── Step 2: Read first offset to find table size ───
            uint firstRelOffset = Reader.ReadUInt32();

            if (firstRelOffset == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [WARN] HDA first entry offset is zero." +
                    " Archive may be empty.");
                Console.ResetColor();
                return;
            }

            int maxPossibleSlots = (int)(firstRelOffset / 4);
            uint[] tempTable = new uint[maxPossibleSlots];
            tempTable[0] = firstRelOffset;

            for (int i = 1; i < maxPossibleSlots; i++)
                tempTable[i] = Reader.ReadUInt32();

            // ── Step 3: Trim trailing padding zeros ────────────
            int lastRealSlot = 0;
            for (int i = 0; i < maxPossibleSlots; i++)
                if (tempTable[i] != 0) lastRealSlot = i;

            int tableSlots = lastRealSlot + 1;

            int realFileCount = 0;
            for (int i = 0; i < tableSlots; i++)
                if (tempTable[i] != 0) realFileCount++;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  HDA: " + tableSlots +
                " table slot(s), " +
                realFileCount + " real file(s).");
            Console.ResetColor();
            Console.WriteLine();

            // ── Step 4: Read each entry ─────────────────────────
            var buffers = new List<byte[]>();
            string[] slotMap = new string[tableSlots];
            int fileIndex = 0;

            for (int i = 0; i < tableSlots; i++)
            {
                uint relOffset = tempTable[i];

                // ── Empty gap slot ─────────────────────────────
                if (relOffset == 0)
                {
                    slotMap[i] = null;

                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "  [SKIP] Table slot " + i +
                        " is an empty gap.");
                    Console.ResetColor();
                    continue;
                }

                long entryAbsPos =
                    (long)BaseOffset + (long)relOffset;

                if (entryAbsPos >= Data.Length)
                {
                    slotMap[i] = null;

                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  [ERROR] Entry " + i +
                        " offset 0x" +
                        entryAbsPos.ToString("X") +
                        " beyond file end. Skipping.");
                    Console.ResetColor();
                    continue;
                }

                Data.Seek(entryAbsPos, SeekOrigin.Begin);

                // ── Read 16-byte entry header ──────────────────
                uint compressedFlag = Reader.ReadUInt32();
                uint decompressedSize = Reader.ReadUInt32();
                uint storedSize = Reader.ReadUInt32();
                uint entryPadding = Reader.ReadUInt32();

                bool isCompressed = (compressedFlag == 1);

                if (storedSize == 0)
                {
                    slotMap[i] = null;

                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [WARN] Entry " + i +
                        " has storedSize=0, skipping.");
                    Console.ResetColor();
                    continue;
                }

                long dataEnd =
                    entryAbsPos + 0x10 + storedSize;

                if (dataEnd > Data.Length)
                {
                    slotMap[i] = null;

                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  [ERROR] Entry " + i +
                        " data past end of file. Skipping.");
                    Console.ResetColor();
                    continue;
                }

                // ── Read file data ─────────────────────────────
                byte[] buffer = new byte[storedSize];
                int totalRead = 0;
                while (totalRead < (int)storedSize)
                {
                    int n = Data.Read(
                        buffer, totalRead,
                        (int)storedSize - totalRead);
                    if (n <= 0) break;
                    totalRead += n;
                }

                // ── Decompress if needed ───────────────────────
                if (isCompressed)
                {
                    try
                    {
                        byte[] decompressed =
                            HarvestCompression.Decompress(
                                buffer);

                        if (decompressed.Length !=
                            (int)decompressedSize)
                        {
                            Console.ForegroundColor =
                                ConsoleColor.Yellow;
                            Console.WriteLine(
                                string.Format(
                                    "  [WARN] Entry {0}:" +
                                    " expected {1:N0} decomp," +
                                    " got {2:N0}.",
                                    i,
                                    decompressedSize,
                                    decompressed.Length));
                            Console.ResetColor();
                        }

                        buffer = decompressed;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Red;
                        Console.WriteLine(
                            "  [ERROR] Decompress failed" +
                            " entry " + i +
                            ": " + ex.Message);
                        Console.ResetColor();
                    }
                }

                buffers.Add(buffer);

                // ── Build filename ─────────────────────────────
                string detectedExt = DetectExtension(buffer);
                string fileName;

                if (detectedExt == ".HDA")
                    fileName = string.Format(
                        "{0}_{1:D2}{2}",
                        archiveName,
                        fileIndex,
                        detectedExt);
                else
                    fileName = string.Format(
                        "{0}_{1:D5}{2}",
                        archiveName,
                        fileIndex,
                        detectedExt);

                slotMap[i] = fileName;
                fileIndex++;

                // ── Write file ─────────────────────────────────
                string filePath =
                    Path.Combine(OutputFolder, fileName);
                File.WriteAllBytes(filePath, buffer);

                // [ext ] in Magenta
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(
                    "  [" +
                    detectedExt.TrimStart('.')
                               .PadRight(4) +
                    "] ");

                // Slot N in Cyan
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Slot " + i + " ");

                // → in Green
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("→ ");

                // Filename in White
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(fileName + "  ");

                // (stored=... decomp=... comp=...) in Yellow
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(
                    string.Format(
                        "(stored={0:N0}" +
                        "  decomp={1:N0}" +
                        "  comp={2})",
                        storedSize,
                        isCompressed
                            ? buffer.Length
                            : (int)storedSize,
                        isCompressed ? "YES" : "NO "));

                Console.ResetColor();
            }

            // ── Blank line after file list ──────────────────────
            Console.WriteLine();

            // ── Step 5: Write manifest ONLY if there are gaps ───
            bool hasEmptyGaps = false;
            for (int i = 0; i < tableSlots; i++)
            {
                if (slotMap[i] == null)
                {
                    hasEmptyGaps = true;
                    break;
                }
            }

            if (hasEmptyGaps)
            {
                string manifestName = string.Format(
                    "{0}_{1:D5}.bin",
                    archiveName,
                    fileIndex);

                WriteManifest(
                    OutputFolder,
                    manifestName,
                    tableSlots,
                    slotMap);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(
                    "  Manifest saved: " + manifestName);
                Console.WriteLine(
                    "  Keep this file in the folder" +
                    " for repacking!");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        // ═══════════════════════════════════════════════════════════
        // MANIFEST — WRITE
        // ═══════════════════════════════════════════════════════════

        private static void WriteManifest(
            string folder,
            string manifestName,
            int totalSlots,
            string[] slotMap)
        {
            string path =
                Path.Combine(folder, manifestName);

            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.WriteLine("SLOTS=" + totalSlots);

                for (int i = 0; i < totalSlots; i++)
                {
                    if (string.IsNullOrEmpty(slotMap[i]))
                        sw.WriteLine(i + "=EMPTY");
                    else
                        sw.WriteLine(i + "=" + slotMap[i]);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // MANIFEST — DETECT BY CONTENT
        // ═══════════════════════════════════════════════════════════

        private static bool IsManifestFile(string filePath)
        {
            try
            {
                string firstLine =
                    File.ReadLines(filePath)
                        .FirstOrDefault();

                return firstLine != null &&
                       firstLine.Trim()
                                .StartsWith("SLOTS=");
            }
            catch
            {
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // MANIFEST — READ
        // Returns the manifest file path via out parameter so
        // callers can exclude it from directory scans.
        // ═══════════════════════════════════════════════════════════

        private static bool ReadManifest(
            string folder,
            out int totalSlots,
            out string[] slotFiles,
            out string manifestPath)   // ← NEW out param
        {
            totalSlots = 0;
            slotFiles = null;
            manifestPath = null;

            // ── Find the manifest file ─────────────────────────
            foreach (string f in Directory.GetFiles(folder))
            {
                if (IsManifestFile(f))
                {
                    manifestPath = f;
                    break;
                }
            }

            if (manifestPath == null)
                return false;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                "  Manifest found: " +
                Path.GetFileName(manifestPath));
            Console.ResetColor();

            string[] lines =
                File.ReadAllLines(manifestPath);

            if (lines.Length == 0)
                return false;

            string firstLine = lines[0].Trim();
            if (!firstLine.StartsWith("SLOTS="))
                return false;

            if (!int.TryParse(
                    firstLine.Substring(6),
                    out totalSlots))
                return false;

            if (totalSlots <= 0)
                return false;

            slotFiles = new string[totalSlots];

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                int slotIdx;
                if (!int.TryParse(
                        line.Substring(0, eq),
                        out slotIdx))
                    continue;

                if (slotIdx < 0 || slotIdx >= totalSlots)
                    continue;

                string val =
                    line.Substring(eq + 1).Trim();

                // null = empty gap, otherwise stores the
                // original filename (informational only now)
                slotFiles[slotIdx] =
                    (val == "EMPTY" ||
                     string.IsNullOrEmpty(val))
                        ? null
                        : val;
            }

            return true;
        }

        // ── Backward-compat overload (no manifest path) ────────
        private static bool ReadManifest(
            string folder,
            out int totalSlots,
            out string[] slotFiles)
        {
            string manifestPath;
            return ReadManifest(
                folder,
                out totalSlots,
                out slotFiles,
                out manifestPath);
        }

        // ═══════════════════════════════════════════════════════════
        // RESOLVE SLOT FILES BY ORDER  ← KEY NEW METHOD
        //
        // Given:
        //   • slotFiles[]  — the manifest layout (null = EMPTY)
        //   • inputFolder  — the folder to scan
        //   • manifestPath — excluded from file list
        //
        // Returns a new slotFiles[] where every non-null entry
        // is replaced with the FULL PATH of the matching file
        // from the folder, matched by ORDER (1st real file →
        // 1st non-empty slot, 2nd real file → 2nd non-empty
        // slot, etc.).
        //
        // This means it works even when the user has renamed
        // files, or is repacking files from a different archive
        // into the same HDA slot layout.
        // ═══════════════════════════════════════════════════════════

        private static string[] ResolveSlotFilesByOrder(
            string[] slotFiles,
            string inputFolder,
            string manifestPath)
        {
            int totalSlots = slotFiles.Length;

            // ── Get sorted non-manifest files in the folder ────
            string[] folderFiles =
                GetSortedFilesExcluding(
                    inputFolder, manifestPath);

            // ── Count how many non-empty slots the manifest has
            int expectedFiles = 0;
            for (int i = 0; i < totalSlots; i++)
                if (slotFiles[i] != null) expectedFiles++;

            // ── Warn if counts don't match ─────────────────────
            if (folderFiles.Length != expectedFiles)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [WARN] Manifest expects " +
                    expectedFiles +
                    " file(s) but folder contains " +
                    folderFiles.Length + " file(s).");
                Console.WriteLine(
                    "  Will pack whichever is fewer.");
                Console.ResetColor();
            }

            // ── Build resolved slot map ────────────────────────
            // resolved[slot] = full path, or null for gaps
            string[] resolved = new string[totalSlots];
            int fileIdx = 0;

            for (int slot = 0; slot < totalSlots; slot++)
            {
                if (slotFiles[slot] == null)
                {
                    // Empty gap — keep null
                    resolved[slot] = null;
                    continue;
                }

                if (fileIdx < folderFiles.Length)
                {
                    resolved[slot] = folderFiles[fileIdx];
                    fileIdx++;
                }
                else
                {
                    // More slots than files — treat as missing
                    resolved[slot] = null;
                }
            }

            return resolved;
        }

        // ═══════════════════════════════════════════════════════════
        // PACK — UNCOMPRESSED
        // ═══════════════════════════════════════════════════════════

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
            int totalSlots;
            string[] slotFiles;
            string manifestPath;

            bool hasManifest = ReadManifest(
                InputFolder,
                out totalSlots,
                out slotFiles,
                out manifestPath);

            if (hasManifest)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    "  Using original table layout (" +
                    totalSlots + " slots).");
                Console.ResetColor();

                // ── Resolve files by ORDER, not filename ───────
                string[] resolvedPaths =
                    ResolveSlotFilesByOrder(
                        slotFiles,
                        InputFolder,
                        manifestPath);

                PackWithManifest(
                    Data, InputFolder,
                    totalSlots, resolvedPaths,
                    false);
            }
            else
            {
                PackLegacy(Data, InputFolder);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PACK — SMART COMPRESSED
        // ═══════════════════════════════════════════════════════════

        public static void PackCompressed(
            string outputHda,
            string inputFolder)
        {
            int totalSlots;
            string[] slotFiles;
            string manifestPath;

            bool hasManifest = ReadManifest(
                inputFolder,
                out totalSlots,
                out slotFiles,
                out manifestPath);

            if (hasManifest)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    "  Using original table layout (" +
                    totalSlots + " slots).");
                Console.ResetColor();

                // ── Resolve files by ORDER, not filename ───────
                string[] resolvedPaths =
                    ResolveSlotFilesByOrder(
                        slotFiles,
                        inputFolder,
                        manifestPath);

                using (FileStream fs =
                    new FileStream(
                        outputHda, FileMode.Create))
                {
                    PackWithManifest(
                        fs, inputFolder,
                        totalSlots, resolvedPaths,
                        true);
                }
            }
            else
            {
                PackCompressedLegacy(
                    outputHda, inputFolder);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // PACK WITH MANIFEST
        //
        // slotFiles[] now contains FULL PATHS (or null for gaps),
        // already resolved by ResolveSlotFilesByOrder.
        // The inputFolder param is kept for the summary display.
        // ═══════════════════════════════════════════════════════════

        private static void PackWithManifest(
            Stream Data,
            string inputFolder,
            int totalSlots,
            string[] slotFiles,   // full paths now
            bool compress)
        {
            BinaryWriter wr = new BinaryWriter(Data);

            int realCount = 0;
            foreach (string s in slotFiles)
                if (!string.IsNullOrEmpty(s)) realCount++;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Packing " + realCount +
                " file(s) into " + totalSlots +
                " slot(s)" +
                (compress
                    ? " (Smart Compressed)"
                    : " (Uncompressed)"));
            Console.ResetColor();
            Console.WriteLine();

            int indexWidth =
                Math.Max(2, realCount.ToString().Length);

            // ── Phase 1: Load and compress ──────────────────────
            byte[][] rawDatas = new byte[totalSlots][];
            byte[][] storedDatas = new byte[totalSlots][];
            bool[] compFlags = new bool[totalSlots];

            long totalRaw = 0;
            long totalStored = 0;
            int compCount = 0;
            int rawCount = 0;
            int fileNum = 0;

            for (int slot = 0; slot < totalSlots; slot++)
            {
                string fullPath = slotFiles[slot];

                // ── Empty gap ──────────────────────────────────
                if (string.IsNullOrEmpty(fullPath))
                {
                    rawDatas[slot] = null;
                    storedDatas[slot] = null;
                    compFlags[slot] = false;

                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "  [SKIP] Slot " + slot +
                        " is an empty gap.");
                    Console.ResetColor();
                    continue;
                }

                // ── File missing from folder ───────────────────
                if (!File.Exists(fullPath))
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  [ERROR] File not found: " +
                        Path.GetFileName(fullPath));
                    Console.ResetColor();

                    rawDatas[slot] = null;
                    storedDatas[slot] = null;
                    compFlags[slot] = false;
                    continue;
                }

                rawDatas[slot] =
                    File.ReadAllBytes(fullPath);
                int rawLen = rawDatas[slot].Length;
                totalRaw += rawLen;
                fileNum++;

                string fname = Path.GetFileName(fullPath);
                string currentText =
                    fileNum.ToString("D" + indexWidth);
                string totalText =
                    realCount.ToString("D" + indexWidth);

                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.Write(
                    "  [{0}/{1}] Slot {2}: {3,-28}  ",
                    currentText,
                    totalText,
                    slot,
                    fname.Length > 28
                        ? fname.Substring(0, 25) + "..."
                        : fname);
                Console.ResetColor();

                // ── RAW or compress ────────────────────────────
                if (!compress || rawLen <= 64)
                {
                    storedDatas[slot] = rawDatas[slot];
                    compFlags[slot] = false;
                    totalStored += rawLen;
                    rawCount++;

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        rawLen <= 64
                            ? "OK RAW (<= 64 bytes)"
                            : "OK RAW (uncompressed mode)");
                    Console.ResetColor();
                }
                else
                {
                    byte[] comp =
                        HarvestCompression.Compress(
                            rawDatas[slot]);
                    bool verified =
                        HarvestCompression.VerifyRoundTrip(
                            rawDatas[slot], comp);

                    if (!verified || comp.Length > rawLen)
                    {
                        comp =
                            HarvestCompression
                                .CompressAsLiterals(
                                    rawDatas[slot]);
                        verified =
                            HarvestCompression
                                .VerifyRoundTrip(
                                    rawDatas[slot], comp);
                    }

                    storedDatas[slot] = comp;
                    compFlags[slot] = true;
                    totalStored += comp.Length;
                    compCount++;

                    double ratio = rawLen == 0
                        ? 0
                        : (double)comp.Length /
                          rawLen * 100.0;

                    Console.ForegroundColor =
                        ratio <= 100.1
                            ? ConsoleColor.Green
                            : ConsoleColor.Yellow;

                    Console.WriteLine(
                        "OK {0:N0} → {1:N0} bytes ({2:F1}%)",
                        rawLen, comp.Length, ratio);
                    Console.ResetColor();
                }
            }

            // ── Phase 2: Calculate offsets ───────────────────────
            int tableSize = totalSlots * 4;
            int dataAreaStart = Align(tableSize);

            uint[] entryRelOffsets = new uint[totalSlots];
            int cursor = dataAreaStart;

            for (int slot = 0; slot < totalSlots; slot++)
            {
                if (storedDatas[slot] == null)
                {
                    entryRelOffsets[slot] = 0;
                }
                else
                {
                    entryRelOffsets[slot] = (uint)cursor;
                    cursor += Align(
                        0x10 + storedDatas[slot].Length);
                }
            }

            // ── Phase 3: Write HDA ──────────────────────────────
            wr.Write(0x10u);
            wr.Write(0u);
            wr.Write(0u);
            wr.Write(0u);

            Data.Seek(0x10, SeekOrigin.Begin);
            for (int slot = 0; slot < totalSlots; slot++)
                wr.Write(entryRelOffsets[slot]);

            for (int slot = 0; slot < totalSlots; slot++)
            {
                if (storedDatas[slot] == null)
                    continue;

                long absPos =
                    0x10L + entryRelOffsets[slot];
                Data.Seek(absPos, SeekOrigin.Begin);

                wr.Write(compFlags[slot] ? 1u : 0u);
                wr.Write((uint)rawDatas[slot].Length);
                wr.Write((uint)storedDatas[slot].Length);
                wr.Write(0u);

                Data.Write(
                    storedDatas[slot], 0,
                    storedDatas[slot].Length);

                while ((Data.Position & 0xF) != 0)
                    Data.WriteByte(0);
            }

            // ── Summary ────────────────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  ── Summary ──────────────────────────────");
            Console.WriteLine(
                "  Total slots    : " + totalSlots);
            Console.WriteLine(
                "  Files packed   : " + realCount);
            Console.WriteLine(
                "  Empty slots    : " +
                (totalSlots - realCount));

            if (compress)
            {
                Console.WriteLine(
                    "  Compressed     : " + compCount);
                Console.WriteLine(
                    "  Stored RAW     : " + rawCount);

                double overallRatio = totalRaw == 0
                    ? 0
                    : (double)totalStored /
                      totalRaw * 100.0;

                Console.WriteLine(
                    string.Format(
                        "  Overall ratio  : {0:F1}%",
                        overallRatio));
            }

            Console.WriteLine(
                "  ─────────────────────────────────────────");
            Console.ResetColor();

            // ── Show offset table layout ───────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Offset table layout:");
            Console.ResetColor();

            for (int slot = 0; slot < totalSlots; slot++)
            {
                if (entryRelOffsets[slot] == 0)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "    Slot " + slot +
                        ": 00 00 00 00  (EMPTY GAP)");
                }
                else
                {
                    byte[] offBytes =
                        BitConverter.GetBytes(
                            entryRelOffsets[slot]);

                    string displayName =
                        slotFiles[slot] != null
                            ? Path.GetFileName(slotFiles[slot])
                            : "?";

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        string.Format(
                            "    Slot {0}:" +
                            " {1:X2} {2:X2} {3:X2} {4:X2}" +
                            "  → abs 0x{5:X8}  ({6})",
                            slot,
                            offBytes[0], offBytes[1],
                            offBytes[2], offBytes[3],
                            0x10 + entryRelOffsets[slot],
                            displayName));
                }

                Console.ResetColor();
            }
        }

        // ═══════════════════════════════════════════════════════════
        // LEGACY PACK — UNCOMPRESSED (no manifest)
        // ═══════════════════════════════════════════════════════════

        private static void PackLegacy(
            Stream Data,
            string InputFolder)
        {
            string[] Files = GetSortedFiles(InputFolder);
            BinaryWriter Writer = new BinaryWriter(Data);

            Writer.Write(0x10u);
            Writer.Write(0u);
            Writer.Write(0u);
            Writer.Write(0u);

            int tableSize = Files.Length * 4;
            int dataAreaStart = Align(tableSize);

            var entryRelOffsets = new int[Files.Length];
            int cursor = dataAreaStart;

            for (int i = 0; i < Files.Length; i++)
            {
                entryRelOffsets[i] = cursor;
                byte[] raw = File.ReadAllBytes(Files[i]);
                cursor += Align(0x10 + raw.Length);
            }

            Data.Seek(0x10, SeekOrigin.Begin);
            for (int i = 0; i < Files.Length; i++)
                Writer.Write((uint)entryRelOffsets[i]);

            for (int i = 0; i < Files.Length; i++)
            {
                byte[] Buffer =
                    File.ReadAllBytes(Files[i]);

                Data.Seek(
                    0x10 + entryRelOffsets[i],
                    SeekOrigin.Begin);

                Writer.Write(0u);
                Writer.Write((uint)Buffer.Length);
                Writer.Write((uint)Buffer.Length);
                Writer.Write(0u);

                Data.Write(Buffer, 0, Buffer.Length);

                while ((Data.Position & 0xF) != 0)
                    Data.WriteByte(0);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // LEGACY PACK — COMPRESSED (no manifest)
        // ═══════════════════════════════════════════════════════════

        private static void PackCompressedLegacy(
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
            Console.ResetColor();
            Console.WriteLine();

            var rawDatas = new byte[files.Length][];
            var storedDatas = new byte[files.Length][];
            var compressedFlags = new bool[files.Length];

            long totalRaw = 0;
            long totalStored = 0;
            int compCount = 0;
            int rawCount = 0;

            int indexWidth =
                Math.Max(2, files.Length.ToString().Length);
            string totalText =
                files.Length.ToString("D" + indexWidth);

            for (int i = 0; i < files.Length; i++)
            {
                string fname =
                    Path.GetFileName(files[i]);
                rawDatas[i] =
                    File.ReadAllBytes(files[i]);
                int rawLen = rawDatas[i].Length;
                totalRaw += rawLen;

                string currentText =
                    (i + 1).ToString("D" + indexWidth);

                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.Write(
                    "  [{0}/{1}] {2,-30}  ",
                    currentText, totalText,
                    fname.Length > 30
                        ? fname.Substring(0, 27) + "..."
                        : fname);
                Console.ResetColor();

                if (rawLen <= 64)
                {
                    storedDatas[i] = rawDatas[i];
                    compressedFlags[i] = false;
                    totalStored += rawLen;
                    rawCount++;

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "OK RAW (<= 64 bytes)");
                    Console.ResetColor();
                    continue;
                }

                byte[] comp =
                    HarvestCompression.Compress(
                        rawDatas[i]);
                bool verified =
                    HarvestCompression.VerifyRoundTrip(
                        rawDatas[i], comp);

                if (!verified || comp.Length > rawLen)
                {
                    comp =
                        HarvestCompression
                            .CompressAsLiterals(
                                rawDatas[i]);
                    verified =
                        HarvestCompression.VerifyRoundTrip(
                            rawDatas[i], comp);
                }

                storedDatas[i] = comp;
                compressedFlags[i] = true;
                totalStored += comp.Length;
                compCount++;

                double ratio = rawLen == 0
                    ? 0
                    : (double)comp.Length /
                      rawLen * 100.0;

                Console.ForegroundColor =
                    ratio <= 100.1
                        ? ConsoleColor.Green
                        : ConsoleColor.Yellow;

                Console.WriteLine(
                    "OK {0:N0} → {1:N0} bytes ({2:F1}%)",
                    rawLen, comp.Length, ratio);
                Console.ResetColor();
            }

            using (FileStream fs =
                new FileStream(outputHda, FileMode.Create))
            using (BinaryWriter wr = new BinaryWriter(fs))
            {
                wr.Write(0x10u);
                wr.Write(0u);
                wr.Write(0u);
                wr.Write(0u);

                int dataStart =
                    Align(files.Length * 4);
                var entryOffsets =
                    new int[files.Length];
                int cursor = dataStart;

                for (int i = 0; i < files.Length; i++)
                {
                    entryOffsets[i] = cursor;
                    cursor += Align(
                        0x10 + storedDatas[i].Length);
                }

                fs.Seek(0x10, SeekOrigin.Begin);
                for (int i = 0; i < files.Length; i++)
                    wr.Write((uint)entryOffsets[i]);

                for (int i = 0; i < files.Length; i++)
                {
                    long absPos =
                        0x10L + entryOffsets[i];
                    fs.Seek(absPos, SeekOrigin.Begin);

                    wr.Write(
                        compressedFlags[i] ? 1u : 0u);
                    wr.Write(
                        (uint)rawDatas[i].Length);
                    wr.Write(
                        (uint)storedDatas[i].Length);
                    wr.Write(0u);

                    fs.Write(
                        storedDatas[i], 0,
                        storedDatas[i].Length);

                    while ((fs.Position & 0xF) != 0)
                        fs.WriteByte(0);
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  ── Summary ──────────────────────────────");
            Console.WriteLine(
                "  Files packed   : " + files.Length);
            Console.WriteLine(
                "  Compressed     : " + compCount);
            Console.WriteLine(
                "  Stored RAW     : " + rawCount);

            double overallRatio = totalRaw == 0
                ? 0
                : (double)totalStored /
                  totalRaw * 100.0;

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

        // ═══════════════════════════════════════════════════════════
        // AUDIO HDA DETECTION
        // ═══════════════════════════════════════════════════════════

        private static bool DetectAudioHDA(
            List<byte[]> buffers)
        {
            if (buffers.Count < 2 || buffers.Count > 3)
                return false;
            if (!IsHDFile(buffers[1])) return false;
            if (buffers.Count == 3 &&
                !IsSQFile(buffers[2]))
                return false;
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        // WRITE HELPERS
        // ═══════════════════════════════════════════════════════════

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

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    "  [AUDIO] " + fileName);
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
                string detectedExt =
                    DetectExtension(buffers[i]);

                string fileName = (detectedExt == ".HDA")
                    ? string.Format("{0}_{1:D2}{2}",
                        archiveName, i, detectedExt)
                    : string.Format("{0}_{1:D5}{2}",
                        archiveName, i, detectedExt);

                string filePath =
                    Path.Combine(outputFolder, fileName);

                File.WriteAllBytes(filePath, buffers[i]);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    "  [" +
                    detectedExt.TrimStart('.')
                               .PadRight(4) +
                    "] " + fileName);
                Console.ResetColor();
            }
        }

        // ═══════════════════════════════════════════════════════════
        // EXTENSION DETECTION
        // ═══════════════════════════════════════════════════════════

        private static string DetectExtension(byte[] data)
        {
            if (data == null || data.Length < 4)
                return ".bin";

            if (StartsWith(data, MAGIC_GDTB)) return ".gdtb";
            if (StartsWith(data, MAGIC_RDTB)) return ".rdtb";
            if (StartsWith(data, MAGIC_SRDB)) return ".srdb";

            if (data.Length >= 16 &&
                StartsWith(data, MAGIC_HDA))
                return ".HDA";

            if (IsSQFile(data)) return ".SQ";
            if (IsHDFile(data)) return ".HD";

            return ".bin";
        }

        private static bool IsHDFile(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;
            if (!StartsWith(data, MAGIC_HD_START))
                return false;
            if (IsSQFile(data)) return false;
            return true;
        }

        private static bool IsSQFile(byte[] data)
        {
            if (data == null || data.Length < 0x18)
                return false;
            if (!StartsWith(data, MAGIC_HD_START))
                return false;

            for (int i = 0; i < MAGIC_SQ_LINE2.Length; i++)
                if (data[0x10 + i] != MAGIC_SQ_LINE2[i])
                    return false;

            return true;
        }

        private static bool StartsWith(
            byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i]) return false;
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        // SORTED FILE LISTING — excludes ALL manifests
        // ═══════════════════════════════════════════════════════════

        private static string[] GetSortedFiles(
            string inputFolder)
        {
            return GetSortedFilesExcluding(
                inputFolder, null);
        }

        /// <summary>
        /// Returns all non-manifest files in the folder,
        /// sorted by the numeric index in their filename.
        /// Optionally excludes a specific file by full path
        /// (the manifest, already located by ReadManifest).
        /// </summary>
        private static string[] GetSortedFilesExcluding(
            string inputFolder,
            string excludeFullPath)
        {
            string[] allFiles =
                Directory.GetFiles(inputFolder);

            var filtered = new List<string>();
            foreach (string f in allFiles)
            {
                // Skip by content (manifest detection)
                if (IsManifestFile(f))
                    continue;

                // Skip by explicit path if provided
                if (excludeFullPath != null &&
                    string.Equals(
                        Path.GetFullPath(f),
                        Path.GetFullPath(excludeFullPath),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                filtered.Add(f);
            }

            string[] files = filtered.ToArray();

            Array.Sort(files, (a, b) =>
            {
                int ia =
                    ExtractFileIndex(Path.GetFileName(a));
                int ib =
                    ExtractFileIndex(Path.GetFileName(b));
                return ia.CompareTo(ib);
            });

            return files;
        }

        private static int ExtractFileIndex(
            string fileName)
        {
            string name =
                Path.GetFileNameWithoutExtension(fileName);
            int u = name.LastIndexOf('_');
            if (u < 0) return 0;

            int result;
            return int.TryParse(
                    name.Substring(u + 1), out result)
                ? result
                : 0;
        }

        // ═══════════════════════════════════════════════════════════
        // ALIGNMENT
        // ═══════════════════════════════════════════════════════════

        private static int Align(int Value)
        {
            if ((Value & 0xF) != 0)
                Value = ((Value & ~0xF) + 0x10);
            return Value;
        }
    }
}
