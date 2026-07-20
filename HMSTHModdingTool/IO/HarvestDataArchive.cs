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

        private static readonly byte[] MAGIC_ELF =
            { 0x7F, 0x45, 0x4C, 0x46 };

        // HDA container magic: first 4 bytes
        // = 0x10 00 00 00, next 12 bytes = 00
        private static readonly byte[] MAGIC_HDA =
        {
            0x10, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        // HD soundbank header magic
        private static readonly byte[] MAGIC_HD_START =
            { 0x49, 0x45, 0x43, 0x53,
              0x73, 0x72, 0x65, 0x56 };

        // SQ sequence second-line magic
        private static readonly byte[] MAGIC_SQ_LINE2 =
            { 0x49, 0x45, 0x43, 0x53,
              0x75, 0x71, 0x65, 0x53 };

        // ═══════════════════════════════════════════════════════════
        // SLOT ROLE CONSTANTS
        //
        //   0 = regular file (not audio)
        //   1 = .BD  — slot immediately before .HD
        //   2 = .HD  — confirmed by magic header
        //   3 = .SQ  — confirmed by magic header
        // ═══════════════════════════════════════════════════════════

        private const int ROLE_REGULAR = 0;
        private const int ROLE_BD = 1;
        private const int ROLE_HD = 2;
        private const int ROLE_SQ = 3;

        // ═══════════════════════════════════════════════════════════
        // UNPACK — PUBLIC ENTRY POINTS
        // ═══════════════════════════════════════════════════════════

        public static void Unpack(
            string Data,
            string OutputFolder)
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

            BinaryReader Reader =
                new BinaryReader(Data);

            // ── Step 1: Read file header ─────────────────────────
            uint BaseOffset = Reader.ReadUInt32();

            if (BaseOffset == 0 || BaseOffset > 0x1000)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [WARN] Unexpected BaseOffset = 0x" +
                    BaseOffset.ToString("X") +
                    ". Expected 0x10. Proceeding anyway.");
                Console.ResetColor();
            }

            Data.Seek(BaseOffset, SeekOrigin.Begin);

            // ── Step 2: Read first offset ────────────────────────
            uint firstRelOffset = Reader.ReadUInt32();

            if (firstRelOffset == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [WARN] HDA first entry offset" +
                    " is zero. Archive may be empty.");
                Console.ResetColor();
                return;
            }

            int maxPossibleSlots =
                (int)(firstRelOffset / 4);
            uint[] tempTable =
                new uint[maxPossibleSlots];
            tempTable[0] = firstRelOffset;

            for (int i = 1; i < maxPossibleSlots; i++)
                tempTable[i] = Reader.ReadUInt32();

            // ── Step 3: Trim trailing padding zeros ──────────────
            int lastRealSlot = 0;
            for (int i = 0; i < maxPossibleSlots; i++)
                if (tempTable[i] != 0)
                    lastRealSlot = i;

            int tableSlots = lastRealSlot + 1;

            int realFileCount = 0;
            for (int i = 0; i < tableSlots; i++)
                if (tempTable[i] != 0)
                    realFileCount++;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  HDA: " + tableSlots +
                " table slot(s), " +
                realFileCount + " real file(s).");
            Console.ResetColor();
            Console.WriteLine();

            // ── Step 4: Pre-scan — build slotRole[] ─────────────
            //
            // We scan every slot ONCE before extracting.
            // This tells us the role of every slot purely
            // from magic headers and position:
            //
            //   ROLE_HD  → slot has magic "IECSsreV"
            //               and is NOT an SQ file
            //
            //   ROLE_SQ  → slot has magic "IECSsreV"
            //               AND second-line "IECSuqeS"
            //
            //   ROLE_BD  → the nearest non-empty slot
            //               that comes BEFORE an HD slot.
            //               No content check needed at all.
            //               Position relative to HD is the
            //               only rule that matters.
            //
            //   ROLE_REGULAR → everything else.
            //               Never assigned .BD regardless
            //               of what its bytes look like.
            // ────────────────────────────────────────────────────

            int[] slotRole = new int[tableSlots];

            {
                long savedPos = Data.Position;

                for (int i = 0; i < tableSlots; i++)
                {
                    uint peekOff = tempTable[i];
                    if (peekOff == 0) continue;

                    long peekAbs =
                        (long)BaseOffset +
                        (long)peekOff;

                    if (peekAbs + 0x10 > Data.Length)
                        continue;

                    Data.Seek(
                        peekAbs, SeekOrigin.Begin);

                    uint pkComp = Reader.ReadUInt32();
                    uint pkDecomp = Reader.ReadUInt32();
                    uint pkStored = Reader.ReadUInt32();
                    Reader.ReadUInt32(); // padding

                    if (pkStored == 0) continue;
                    if (peekAbs + 0x10 + pkStored
                        > Data.Length) continue;

                    // Read up to 64 bytes to sniff
                    // magic. SQ needs 0x18 (24) bytes,
                    // HD needs 8, so 64 is enough for
                    // both even with slack.
                    int sniffLen =
                        (int)Math.Min(64u, pkStored);
                    byte[] sniff = new byte[sniffLen];
                    int sread = 0;
                    while (sread < sniffLen)
                    {
                        int rd = Data.Read(
                            sniff, sread,
                            sniffLen - sread);
                        if (rd <= 0) break;
                        sread += rd;
                    }

                    byte[] checkBuf = sniff;

                    // If this entry is compressed we
                    // must decompress it before we can
                    // read the real magic bytes.
                    if (pkComp == 1)
                    {
                        try
                        {
                            Data.Seek(
                                peekAbs + 0x10,
                                SeekOrigin.Begin);
                            byte[] full =
                                new byte[pkStored];
                            int fr = 0;
                            while (fr < (int)pkStored)
                            {
                                int rr = Data.Read(
                                    full, fr,
                                    (int)pkStored - fr);
                                if (rr <= 0) break;
                                fr += rr;
                            }
                            checkBuf =
                                HarvestCompression
                                    .Decompress(full);
                        }
                        catch
                        {
                            // Decompress failed —
                            // keep raw sniff bytes.
                            // It won't match HD/SQ
                            // magic so it will stay
                            // ROLE_REGULAR which is safe.
                            checkBuf = sniff;
                        }
                    }

                    // ── Check magic ──────────────────────────────
                    if (IsSQFile(checkBuf))
                    {
                        // SQ check must come FIRST because
                        // SQ files also start with the HD
                        // magic — IsHDFile() excludes SQ,
                        // but we check SQ first anyway for
                        // clarity and safety.
                        slotRole[i] = ROLE_SQ;
                    }
                    else if (IsHDFile(checkBuf))
                    {
                        slotRole[i] = ROLE_HD;

                        // ── KEY RULE ─────────────────────────────
                        // The slot immediately before this HD
                        // (walking backwards over any empty
                        // gap slots) is the .BD body file.
                        // No content inspection needed at all.
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (tempTable[j] != 0)
                            {
                                // Only mark it BD if it
                                // hasn't already been
                                // identified as something
                                // with a real magic header.
                                // (Protects against weird
                                // edge-case archives where
                                // an HD follows another HD.)
                                if (slotRole[j] ==
                                    ROLE_REGULAR)
                                {
                                    slotRole[j] = ROLE_BD;
                                }
                                break;
                            }
                        }
                    }
                    // ROLE_REGULAR (0) is already the
                    // default so nothing to do for that.
                }

                Data.Seek(savedPos, SeekOrigin.Begin);
            }

            // ── Step 5: Extract each entry ───────────────────────
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
                    continue;
                }

                long entryAbsPos =
                    (long)BaseOffset +
                    (long)relOffset;

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

                Data.Seek(
                    entryAbsPos, SeekOrigin.Begin);

                // ── Read 16-byte entry header ──────────────────
                uint compressedFlag =
                    Reader.ReadUInt32();
                uint decompressedSize =
                    Reader.ReadUInt32();
                uint storedSize =
                    Reader.ReadUInt32();
                uint entryPadding =
                    Reader.ReadUInt32();

                bool isCompressed = (compressedFlag == 1);

                if (storedSize == 0)
                {
                    slotMap[i] = null;
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [WARN] Slot " + i +
                        " has storedSize=0. Skipping.");
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
                        " data past end of file." +
                        " Skipping.");
                    Console.ResetColor();
                    continue;
                }

                // ── Read raw stored bytes ──────────────────────
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

                bool isV2Compression = false;

                // ── Decompress if needed ───────────────────────
                if (isCompressed)
                {
                    try
                    {
                        byte[] decompressed =
                            HarvestCompression.Decompress(
                                buffer,
                                (int)decompressedSize,
                                out isV2Compression);

                        if (decompressed.Length !=
                            (int)decompressedSize)
                        {
                            Console.ForegroundColor =
                                ConsoleColor.Yellow;
                            Console.WriteLine(
                                string.Format(
                                    "  [WARN] Entry {0}:" +
                                    " expected {1:N0}" +
                                    " decomp, got {2:N0}.",
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

                // ── Determine extension from slotRole ─────────
                //
                // This is the ENTIRE detection logic for audio:
                //
                //   ROLE_BD → ".BD"  (position-based, no sniff)
                //   ROLE_HD → ".HD"  (magic confirmed in pre-scan)
                //   ROLE_SQ → ".SQ"  (magic confirmed in pre-scan)
                //   ROLE_REGULAR → DetectExtension(buffer)
                //                  which checks all known magic
                //                  headers but NEVER returns .BD
                //
                // Result: .BD is 100% position-based.
                //         Zero heuristics. Zero false positives.
                // ─────────────────────────────────────────────

                string detectedExt;

                switch (slotRole[i])
                {
                    case ROLE_BD:
                        detectedExt = ".BD";
                        break;

                    case ROLE_HD:
                        detectedExt = ".HD";
                        break;

                    case ROLE_SQ:
                        detectedExt = ".SQ";
                        break;

                    default:
                        // ROLE_REGULAR — use magic headers only.
                        // DetectExtension() never returns .BD.
                        detectedExt =
                            DetectExtension(buffer);
                        break;
                }

                // ── Build output filename ──────────────────────
                string fileName;

                switch (detectedExt)
                {
                    case ".BD":
                    case ".HD":
                    case ".SQ":
                        // Audio files: clean name
                        // matching game convention,
                        // e.g. "BGM.BD", "BGM.HD"
                        fileName = archiveName + detectedExt;
                        break;

                    case ".HDA":
                        // Nested HDA archives get a
                        // two-digit index suffix
                        fileName = string.Format(
                            "{0}_{1:D2}{2}",
                            archiveName,
                            fileIndex,
                            detectedExt);
                        break;

                    default:
                        // Everything else gets a
                        // five-digit index suffix
                        fileName = string.Format(
                            "{0}_{1:D5}{2}",
                            archiveName,
                            fileIndex,
                            detectedExt);
                        break;
                }

                slotMap[i] = fileName;
                fileIndex++;

                // ── Write output file ──────────────────────────
                string filePath =
                    Path.Combine(OutputFolder, fileName);
                File.WriteAllBytes(filePath, buffer);

                // ── Console output ─────────────────────────────
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.Write(
                    "  [" +
                    detectedExt.TrimStart('.')
                               .PadRight(4) +
                    "] ");

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.Write(
                    "Slot " +
                    i.ToString("D2").PadRight(4) +
                    " ");

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.Write("→ ");

                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.Write(
                    fileName.PadRight(26) + "  ");

                Console.ForegroundColor =
                    ConsoleColor.Blue;

                string compLabel =
                    !isCompressed ? "comp=NO" :
                    isV2Compression ? "comp_v2=YES" :
                                      "comp=YES";

                Console.WriteLine(
                    string.Format(
                        "(stored={0:N0}" +
                        "  decomp={1:N0}" +
                        "  {2})",
                        storedSize,
                        isCompressed
                            ? buffer.Length
                            : (int)storedSize,
                        compLabel));

                Console.ResetColor();
            }

            Console.WriteLine();

            // ── Step 6: Write manifest if there are gap slots ────
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

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  Manifest saved: " +
                    manifestName);
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

            using (StreamWriter sw =
                new StreamWriter(path))
            {
                sw.WriteLine("SLOTS=" + totalSlots);
                for (int i = 0; i < totalSlots; i++)
                {
                    if (string.IsNullOrEmpty(slotMap[i]))
                        sw.WriteLine(i + "=EMPTY");
                    else
                        sw.WriteLine(
                            i + "=" + slotMap[i]);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // MANIFEST — DETECT BY CONTENT
        // ═══════════════════════════════════════════════════════════

        private static bool IsManifestFile(
            string filePath)
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
            catch { return false; }
        }

        // ═══════════════════════════════════════════════════════════
        // MANIFEST — READ
        // ═══════════════════════════════════════════════════════════

        private static bool ReadManifest(
            string folder,
            out int totalSlots,
            out string[] slotFiles,
            out string manifestPath)
        {
            totalSlots = 0;
            slotFiles = null;
            manifestPath = null;

            foreach (string f in
                Directory.GetFiles(folder))
            {
                if (IsManifestFile(f))
                {
                    manifestPath = f;
                    break;
                }
            }

            if (manifestPath == null) return false;

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Manifest found: " +
                Path.GetFileName(manifestPath));
            Console.ResetColor();

            string[] lines =
                File.ReadAllLines(manifestPath);
            if (lines.Length == 0) return false;

            string firstLine = lines[0].Trim();
            if (!firstLine.StartsWith("SLOTS="))
                return false;

            if (!int.TryParse(
                    firstLine.Substring(6),
                    out totalSlots))
                return false;

            if (totalSlots <= 0) return false;

            slotFiles = new string[totalSlots];

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                    continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                int slotIdx;
                if (!int.TryParse(
                        line.Substring(0, eq),
                        out slotIdx))
                    continue;

                if (slotIdx < 0 ||
                    slotIdx >= totalSlots)
                    continue;

                string val =
                    line.Substring(eq + 1).Trim();

                slotFiles[slotIdx] =
                    (val == "EMPTY" ||
                     string.IsNullOrEmpty(val))
                        ? null
                        : val;
            }

            return true;
        }

        // Backward-compat overload (no manifest path out)
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
        // RESOLVE SLOT FILES BY ORDER
        // ═══════════════════════════════════════════════════════════

        private static string[] ResolveSlotFilesByOrder(
            string[] slotFiles,
            string inputFolder,
            string manifestPath)
        {
            int totalSlots = slotFiles.Length;

            string[] folderFiles =
                GetSortedFilesExcluding(
                    inputFolder, manifestPath);

            int expectedFiles = 0;
            for (int i = 0; i < totalSlots; i++)
                if (slotFiles[i] != null)
                    expectedFiles++;

            if (folderFiles.Length != expectedFiles)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [WARN] Manifest expects " +
                    expectedFiles +
                    " file(s) but folder contains " +
                    folderFiles.Length + " file(s).");
                Console.WriteLine(
                    "  Will pack whichever is fewer.");
                Console.ResetColor();
            }

            string[] resolved =
                new string[totalSlots];
            int fileIdx = 0;

            for (int slot = 0;
                 slot < totalSlots; slot++)
            {
                if (slotFiles[slot] == null)
                {
                    resolved[slot] = null;
                    continue;
                }

                if (fileIdx < folderFiles.Length)
                {
                    resolved[slot] =
                        folderFiles[fileIdx];
                    fileIdx++;
                }
                else
                {
                    resolved[slot] = null;
                }
            }

            return resolved;
        }

        // ═══════════════════════════════════════════════════════════
        // PACK — UNCOMPRESSED
        // ═══════════════════════════════════════════════════════════

        public static void Pack(
            string Data,
            string InputFolder)
        {
            using (FileStream Output =
                new FileStream(Data, FileMode.Create))
            {
                Pack(Output, InputFolder);
            }
        }

        public static void Pack(
            Stream Data,
            string InputFolder)
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
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Using original table layout (" +
                    totalSlots + " slots).");
                Console.ResetColor();

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
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Using original table layout (" +
                    totalSlots + " slots).");
                Console.ResetColor();

                string[] resolvedPaths =
                    ResolveSlotFilesByOrder(
                        slotFiles,
                        inputFolder,
                        manifestPath);

                using (FileStream fs =
                    new FileStream(
                        outputHda,
                        FileMode.Create))
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
        // ═══════════════════════════════════════════════════════════

        private static void PackWithManifest(
            Stream Data,
            string inputFolder,
            int totalSlots,
            string[] slotFiles,
            bool compress)
        {
            BinaryWriter wr = new BinaryWriter(Data);

            int realCount = 0;
            foreach (string s in slotFiles)
                if (!string.IsNullOrEmpty(s))
                    realCount++;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
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
                Math.Max(2,
                    realCount.ToString().Length);

            byte[][] rawDatas =
                new byte[totalSlots][];
            byte[][] storedDatas =
                new byte[totalSlots][];
            bool[] compFlags =
                new bool[totalSlots];

            long totalRaw = 0;
            long totalStored = 0;
            int compCount = 0;
            int rawCount = 0;
            int fileNum = 0;

            for (int slot = 0;
                 slot < totalSlots; slot++)
            {
                string fullPath = slotFiles[slot];

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

                string fname =
                    Path.GetFileName(fullPath);
                string currentText =
                    fileNum.ToString(
                        "D" + indexWidth);
                string totalText =
                    realCount.ToString(
                        "D" + indexWidth);

                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.Write(
                    "  [{0}/{1}] Slot {2}:" +
                    " {3,-28}  ",
                    currentText, totalText,
                    slot,
                    fname.Length > 28
                        ? fname.Substring(0, 25)
                          + "..."
                        : fname);
                Console.ResetColor();

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
                            : "OK RAW" +
                              " (uncompressed mode)");
                    Console.ResetColor();
                }
                else
                {
                    byte[] comp =
                        HarvestCompression.Compress(
                            rawDatas[slot]);
                    bool verified =
                        HarvestCompression
                            .VerifyRoundTrip(
                                rawDatas[slot],
                                comp);

                    if (!verified ||
                        comp.Length >= rawLen)
                    {
                        storedDatas[slot] =
                            rawDatas[slot];
                        compFlags[slot] = false;
                        totalStored += rawLen;
                        rawCount++;

                        Console.ForegroundColor =
                            ConsoleColor.Green;
                        Console.WriteLine(
                            "OK RAW (comp >= raw," +
                            " stored uncompressed" +
                            " like game)");
                        Console.ResetColor();
                    }
                    else
                    {
                        storedDatas[slot] = comp;
                        compFlags[slot] = true;
                        totalStored += comp.Length;
                        compCount++;

                        double ratio = rawLen == 0
                            ? 0
                            : (double)comp.Length
                              / rawLen * 100.0;

                        Console.ForegroundColor =
                            ConsoleColor.Green;
                        Console.WriteLine(
                            "OK {0:N0} → {1:N0}" +
                            " bytes ({2:F1}%)",
                            rawLen,
                            comp.Length,
                            ratio);
                        Console.ResetColor();
                    }
                }
            }

            // ── Phase 2: Calculate offsets ───────────────────────
            int tableSize = totalSlots * 4;
            int dataAreaStart = Align(tableSize);

            uint[] entryRelOffsets =
                new uint[totalSlots];
            int cursor = dataAreaStart;

            for (int slot = 0;
                 slot < totalSlots; slot++)
            {
                if (storedDatas[slot] == null)
                {
                    entryRelOffsets[slot] = 0;
                }
                else
                {
                    entryRelOffsets[slot] =
                        (uint)cursor;
                    cursor += Align(
                        0x10 +
                        storedDatas[slot].Length);
                }
            }

            // ── Phase 3: Write HDA ───────────────────────────────
            wr.Write(0x10u);
            wr.Write(0u);
            wr.Write(0u);
            wr.Write(0u);

            Data.Seek(0x10, SeekOrigin.Begin);
            for (int slot = 0;
                 slot < totalSlots; slot++)
                wr.Write(entryRelOffsets[slot]);

            for (int slot = 0;
                 slot < totalSlots; slot++)
            {
                if (storedDatas[slot] == null)
                    continue;

                long absPos =
                    0x10L +
                    entryRelOffsets[slot];
                Data.Seek(
                    absPos, SeekOrigin.Begin);

                wr.Write(
                    compFlags[slot] ? 1u : 0u);
                wr.Write(
                    (uint)rawDatas[slot].Length);
                wr.Write(
                    (uint)storedDatas[slot].Length);
                wr.Write(0u);

                Data.Write(
                    storedDatas[slot], 0,
                    storedDatas[slot].Length);

                while ((Data.Position & 0xF) != 0)
                    Data.WriteByte(0);
            }

            // ── Summary ──────────────────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
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
                "  ────────────────────────────────────────");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Offset table layout:");
            Console.ResetColor();

            for (int slot = 0;
                 slot < totalSlots; slot++)
            {
                if (entryRelOffsets[slot] == 0)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "    Slot " + slot +
                        ": 00 00 00 00" +
                        "  (EMPTY GAP)");
                }
                else
                {
                    byte[] offBytes =
                        BitConverter.GetBytes(
                            entryRelOffsets[slot]);

                    string displayName =
                        slotFiles[slot] != null
                            ? Path.GetFileName(
                                slotFiles[slot])
                            : "?";

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        string.Format(
                            "    Slot {0}:" +
                            " {1:X2} {2:X2}" +
                            " {3:X2} {4:X2}" +
                            "  → abs 0x{5:X8}" +
                            "  ({6})",
                            slot,
                            offBytes[0],
                            offBytes[1],
                            offBytes[2],
                            offBytes[3],
                            0x10 +
                            entryRelOffsets[slot],
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
            string[] Files =
                GetSortedFiles(InputFolder);
            BinaryWriter Writer =
                new BinaryWriter(Data);

            Writer.Write(0x10u);
            Writer.Write(0u);
            Writer.Write(0u);
            Writer.Write(0u);

            int tableSize = Files.Length * 4;
            int dataAreaStart = Align(tableSize);

            var entryRelOffsets =
                new int[Files.Length];
            int cursor = dataAreaStart;

            for (int i = 0; i < Files.Length; i++)
            {
                entryRelOffsets[i] = cursor;
                byte[] raw =
                    File.ReadAllBytes(Files[i]);
                cursor +=
                    Align(0x10 + raw.Length);
            }

            Data.Seek(0x10, SeekOrigin.Begin);
            for (int i = 0; i < Files.Length; i++)
                Writer.Write(
                    (uint)entryRelOffsets[i]);

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

                Data.Write(
                    Buffer, 0, Buffer.Length);

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
            string[] files =
                GetSortedFiles(inputFolder);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Packing " + files.Length +
                " file(s) with Smart Compression → " +
                Path.GetFileName(outputHda));
            Console.ResetColor();
            Console.WriteLine();

            var rawDatas =
                new byte[files.Length][];
            var storedDatas =
                new byte[files.Length][];
            var compressedFlags =
                new bool[files.Length];

            long totalRaw = 0;
            long totalStored = 0;
            int compCount = 0;
            int rawCount = 0;

            int indexWidth =
                Math.Max(2,
                    files.Length.ToString().Length);
            string totalText =
                files.Length.ToString(
                    "D" + indexWidth);

            for (int i = 0; i < files.Length; i++)
            {
                string fname =
                    Path.GetFileName(files[i]);
                rawDatas[i] =
                    File.ReadAllBytes(files[i]);
                int rawLen = rawDatas[i].Length;
                totalRaw += rawLen;

                string currentText =
                    (i + 1).ToString(
                        "D" + indexWidth);

                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.Write(
                    "  [{0}/{1}] {2,-30}  ",
                    currentText, totalText,
                    fname.Length > 30
                        ? fname.Substring(0, 27)
                          + "..."
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

                if (!verified ||
                    comp.Length >= rawLen)
                {
                    storedDatas[i] = rawDatas[i];
                    compressedFlags[i] = false;
                    totalStored += rawLen;
                    rawCount++;

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "OK RAW (comp >= raw," +
                        " stored uncompressed" +
                        " like game)");
                    Console.ResetColor();
                }
                else
                {
                    storedDatas[i] = comp;
                    compressedFlags[i] = true;
                    totalStored += comp.Length;
                    compCount++;

                    double ratio = rawLen == 0
                        ? 0
                        : (double)comp.Length /
                          rawLen * 100.0;

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "OK {0:N0} → {1:N0}" +
                        " bytes ({2:F1}%)",
                        rawLen,
                        comp.Length,
                        ratio);
                    Console.ResetColor();
                }
            }

            using (FileStream fs =
                new FileStream(
                    outputHda,
                    FileMode.Create))
            using (BinaryWriter wr =
                new BinaryWriter(fs))
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

                for (int i = 0;
                     i < files.Length; i++)
                {
                    entryOffsets[i] = cursor;
                    cursor += Align(
                        0x10 +
                        storedDatas[i].Length);
                }

                fs.Seek(0x10, SeekOrigin.Begin);
                for (int i = 0;
                     i < files.Length; i++)
                    wr.Write(
                        (uint)entryOffsets[i]);

                for (int i = 0;
                     i < files.Length; i++)
                {
                    long absPos =
                        0x10L + entryOffsets[i];
                    fs.Seek(
                        absPos,
                        SeekOrigin.Begin);

                    wr.Write(
                        compressedFlags[i]
                            ? 1u : 0u);
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
            Console.ForegroundColor =
                ConsoleColor.Cyan;
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
                "  ────────────────────────────────────────");
            Console.ResetColor();
        }

        // ═══════════════════════════════════════════════════════════
        // EXTENSION DETECTION — magic headers only
        //
        // This method NEVER returns ".BD".
        // .BD is assigned exclusively by slotRole[]
        // which uses position relative to a confirmed
        // .HD slot. No heuristics. No guessing.
        // ═══════════════════════════════════════════════════════════

        private static string DetectExtension(
            byte[] data)
        {
            if (data == null || data.Length < 4)
                return ".bin";

            if (StartsWith(data, MAGIC_GDTB))
                return ".gdtb";
            if (StartsWith(data, MAGIC_RDTB))
                return ".rdtb";
            if (StartsWith(data, MAGIC_SRDB))
                return ".srdb";
            if (StartsWith(data, MAGIC_ELF))
                return ".elf";

            if (data.Length >= 16 &&
                StartsWith(data, MAGIC_HDA))
                return ".HDA";

            // SQ check must come before HD check
            // because SQ files also begin with the
            // HD magic header bytes.
            if (IsSQFile(data)) return ".SQ";
            if (IsHDFile(data)) return ".HD";

            // .BD is NEVER returned here.
            // It is assigned only by slotRole[]
            // in the Unpack() pre-scan step.
            return ".bin";
        }

        // ═══════════════════════════════════════════════════════════
        // AUDIO FILE DETECTORS
        // ═══════════════════════════════════════════════════════════

        private static bool IsHDFile(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;
            if (!StartsWith(data, MAGIC_HD_START))
                return false;
            // SQ files also start with HD magic,
            // so exclude them here.
            if (IsSQFile(data)) return false;
            return true;
        }

        private static bool IsSQFile(byte[] data)
        {
            if (data == null || data.Length < 0x18)
                return false;
            if (!StartsWith(data, MAGIC_HD_START))
                return false;
            for (int i = 0;
                 i < MAGIC_SQ_LINE2.Length; i++)
                if (data[0x10 + i] !=
                    MAGIC_SQ_LINE2[i])
                    return false;
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        // UTILITY
        // ═══════════════════════════════════════════════════════════

        private static bool StartsWith(
            byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length)
                return false;
            for (int i = 0; i < magic.Length; i++)
                if (data[i] != magic[i])
                    return false;
            return true;
        }

        private static string[] GetSortedFiles(
            string inputFolder)
        {
            return GetSortedFilesExcluding(
                inputFolder, null);
        }

        private static string[] GetSortedFilesExcluding(
            string inputFolder,
            string excludeFullPath)
        {
            string[] allFiles =
                Directory.GetFiles(inputFolder);

            var filtered = new List<string>();
            foreach (string f in allFiles)
            {
                if (IsManifestFile(f)) continue;

                if (excludeFullPath != null &&
                    string.Equals(
                        Path.GetFullPath(f),
                        Path.GetFullPath(
                            excludeFullPath),
                        StringComparison
                            .OrdinalIgnoreCase))
                    continue;

                filtered.Add(f);
            }

            string[] files = filtered.ToArray();

            Array.Sort(files, (a, b) =>
            {
                int ia = ExtractFileIndex(
                    Path.GetFileName(a));
                int ib = ExtractFileIndex(
                    Path.GetFileName(b));
                return ia.CompareTo(ib);
            });

            return files;
        }

        private static int ExtractFileIndex(
            string fileName)
        {
            string name =
                Path.GetFileNameWithoutExtension(
                    fileName);
            int u = name.LastIndexOf('_');
            if (u < 0) return 0;

            int result;
            return int.TryParse(
                    name.Substring(u + 1),
                    out result)
                ? result
                : 0;
        }

        private static int Align(int Value)
        {
            if ((Value & 0xF) != 0)
                Value = ((Value & ~0xF) + 0x10);
            return Value;
        }
    }
}
