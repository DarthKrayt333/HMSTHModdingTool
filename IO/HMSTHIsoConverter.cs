using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace HMSTHModdingTool
{
    // ═══════════════════════════════════════════
    // HMSTH CD/DVD ISO CONVERTER
    // CD:  root=22, first data=51
    // DVD: root=261 (+239), first data=681 (+630)
    // DVD adds Z1.;1 (1GB) and Z2.;1 (500MB)
    // dummies at end of root dir
    // Commands:
    //   todvd <in> <out.iso>
    //   tocd  <in> <out.iso>
    // ═══════════════════════════════════════════
    public static class HMSTHIsoConverter
    {
        const int SEC = 2048;

        // CD constants
        const int CD_ROOT_LBA = 22;
        const int CD_FIRST_DATA = 51;

        // DVD constants
        const int DVD_ROOT_LBA = 261;
        const int DVD_FIRST_DATA = 681;

        // Shifts
        const int DIR_SHIFT = 239;
        // 261 - 22
        const int DATA_SHIFT = 630;
        // 681 - 51

        // DVD dummy sizes
        const long Z1_SIZE = 1_073_741_824L;
        const long Z2_SIZE = 524_288_000L;
        const int Z1_SECTORS =
            (int)(Z1_SIZE / SEC);
        const int Z2_SECTORS =
            (int)(Z2_SIZE / SEC);

        // Path table LBAs
        const int CD_PATH_L_LBA = 18;
        const int CD_PATH_M_LBA = 20;
        const int DVD_PATH_L_LBA = 257;
        const int DVD_PATH_M_LBA = 259;

        // ═══════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ═══════════════════════════════════════
        public static void ToDVD(
            string inputPath,
            string outputPath)
        {
            Convert(inputPath, outputPath,
                toDvd: true);
        }

        public static void ToCD(
            string inputPath,
            string outputPath)
        {
            Convert(inputPath, outputPath,
                toDvd: false);
        }

        // ═══════════════════════════════════════
        // IOP FILE PATCH
        // IOPRP22.IMG has 2 bytes that
        // determine CD vs DVD mode
        // ═══════════════════════════════════════
        const int IOP_PATCH_OFF_1 = 0x26534;
        const int IOP_PATCH_OFF_2 = 0x26537;

        const byte IOP_CD_BYTE_1 = 0x00;
        const byte IOP_CD_BYTE_2 = 0x8E;

        const byte IOP_DVD_BYTE_1 = 0x02;
        const byte IOP_DVD_BYTE_2 = 0x24;

        // IOP file location
        // In CD: /IOP/IOPRP22.IMG at LBA 51
        // In DVD: /IOP/IOPRP22.IMG at LBA 681
        // (first file after directories)


        // ═══════════════════════════════════════
        // SAFE MEMORY ALLOCATION
        // Handles both 32-bit and 64-bit
        // ═══════════════════════════════════════
        static byte[] TryAllocate(
            long size,
            string what)
        {
            try
            {
                if (size > int.MaxValue)
                {
                    throw new
                        OutOfMemoryException(
                        $"{what}: size " +
                        $"{size:N0} exceeds" +
                        " 2GB limit");
                }
                return new byte[size];
            }
            catch (OutOfMemoryException)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.WriteLine(
                    " OUT OF MEMORY!");
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.ResetColor();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  Cannot allocate " +
                    $"{size:N0} bytes " +
                    $"for {what}");
                Console.WriteLine();
                Console.WriteLine(
                    "  You are running in" +
                    " 32-bit mode which is" +
                    " limited to ~1.5GB.");
                Console.WriteLine(
                    "  DVD conversion needs" +
                    " ~1.7GB of memory.");
                Console.WriteLine();
                Console.WriteLine(
                    "  SOLUTIONS:");
                Console.WriteLine(
                    "  1. Run on 64-bit" +
                    " Windows (recommended)");
                Console.WriteLine(
                    "  2. Set project to" +
                    " x64 target");
                Console.WriteLine(
                    "  3. Close other apps" +
                    " to free memory");
                Console.ResetColor();
                Console.WriteLine();
                return null;
            }
        }

        // ═══════════════════════════════════════
        // MAIN CONVERT
        // ═══════════════════════════════════════
        static void Convert(
            string inputPath,
            string outputPath,
            bool toDvd)
        {
            if (!File.Exists(inputPath))
            {
                TextOut.PrintError(
                    "File not found: " +
                    inputPath);
                return;
            }

            // ─── Force output extension
            //     to lowercase ─────────────────
            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        outputPath));
            if (string.IsNullOrEmpty(outDir))
                outDir = Directory
                    .GetCurrentDirectory();
            string outName =
                Path.GetFileNameWithoutExtension(
                    outputPath);
            string outExtLower =
                Path.GetExtension(outputPath)
                    .ToLower();
            outputPath = Path.Combine(
                outDir,
                outName + outExtLower);

            // ─── Validate DVD output ────────
            // DVD format only works with .iso
            // (BIN is a CD-specific 2352-byte
            //  sector format that PS2 DVD
            //  drives don't understand)
            bool checkOutIsBin =
                outExtLower == ".bin" ||
                outExtLower == ".raw";

            if (toDvd && checkOutIsBin)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Blue;
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.WriteLine(
                    " WARNING: DVD CAN'T" +
                    " BE .BIN FORMAT!");
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.ResetColor();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  You tried to save" +
                    " a DVD as: " +
                    outExtLower);
                Console.WriteLine();
                Console.WriteLine(
                    "  PS2 DVDs use 2048-" +
                    "byte sectors (.iso)");
                Console.WriteLine(
                    "  BIN format (2352-" +
                    "byte) is only for CDs!");
                Console.WriteLine();
                Console.WriteLine(
                    "  Please check your" +
                    " typed filename and" +
                    " use .iso extension:");
                Console.WriteLine(
                    "    Wrong: todvd" +
                    " input.bin" +
                    " output.bin");
                Console.WriteLine(
                    "    Right: todvd" +
                    " input.bin" +
                    " output.iso");
                Console.ForegroundColor =
                    ConsoleColor.Blue;
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            // ─── Load input ─────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "Loading input file...");
            Console.ResetColor();

            byte[] input =
                File.ReadAllBytes(inputPath);

            // ─── Detect BIN or ISO ──────────
            int inSectorSize;
            int inDataOff;
            byte[] inData;

            DetectFormat(input,
                out inSectorSize,
                out inDataOff);

            if (inSectorSize == SEC)
            {
                inData = input;
                Console.WriteLine(
                    "  Format: ISO (2048)");
            }
            else
            {
                Console.WriteLine(
                    $"  Format: BIN/RAW " +
                    $"({inSectorSize}," +
                    $" data+{inDataOff})");
                Console.WriteLine(
                    "  Extracting 2048-byte" +
                    " sectors from BIN...");
                inData = ExtractIsoFromBin(
                    input,
                    inSectorSize,
                    inDataOff);
            }

            // ─── Verify input format ────────
            bool isInputCd = IsCdIso(inData);
            bool isInputDvd = IsDvdIso(inData);

            if (toDvd && isInputDvd)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Input is already" +
                    " a DVD ISO!");
                Console.ResetColor();
                return;
            }
            if (!toDvd && isInputCd)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Input is already" +
                    " a CD ISO!");
                Console.ResetColor();
                return;
            }
            if (toDvd && !isInputCd)
            {
                TextOut.PrintError(
                    "Input is not a valid" +
                    " HMSTH CD ISO!");
                return;
            }
            if (!toDvd && !isInputDvd)
            {
                TextOut.PrintError(
                    "Input is not a valid" +
                    " HMSTH DVD ISO!");
                return;
            }

            // ─── Find main ELF & version ────
            int inElfSec =
                FindMainElfSector(inData);
            if (inElfSec < 0)
            {
                TextOut.PrintError(
                    "Main ELF not found!");
                return;
            }
            int inElfOff = inElfSec * SEC;
            GameVersion gv = DetectVersion(
                inData, inElfOff);
            int lbaTableOff =
                GetLbaTableOffset(gv);
            string vName =
                GetVersionName(gv);

            PrintBanner(toDvd
                ? "CD → DVD" : "DVD → CD");

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Input:   " + inputPath);
            Console.WriteLine(
                "  Output:  " + outputPath);
            Console.WriteLine(
                "  Version: " + vName);
            Console.WriteLine(
                $"  Main ELF at sector" +
                $" {inElfSec}");
            Console.ResetColor();
            Console.WriteLine();

            byte[] output;

            if (toDvd)
                output = BuildDvd(
                    inData, gv, lbaTableOff);
            else
                output = BuildCd(
                    inData, gv, lbaTableOff);

            // Check if allocation failed
            if (output == null)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  Conversion aborted" +
                    " due to memory error.");
                Console.ResetColor();
                return;
            }

            // ─── Determine output format ────
            // Uses the already-lowercased
            // outputPath extension
            string outExt = Path
                .GetExtension(outputPath)
                .ToLower();

            bool outIsBin =
                outExt == ".bin" ||
                outExt == ".raw";

            // ─── Save ────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "Writing output...");
            Console.ResetColor();

            if (outIsBin)
            {
                Console.WriteLine(
                    "  Format: BIN/RAW (2352)");
                int numSec =
                    output.Length / SEC;
                long binSize =
                    (long)numSec * 2352;
                Console.WriteLine(
                    $"  Size: " +
                    $"{binSize:N0}" +
                    " bytes" +
                    $" ({numSec:N0}" +
                    " sectors)");

                // Stream directly to file
                // to avoid OOM on large DVDs
                ConvertIsoToBinStream(
                    output, outputPath);

                // Write .cue file
                string cuePath =
                    Path.ChangeExtension(
                        outputPath, ".cue");
                string cueContent =
                    "FILE \"" +
                    Path.GetFileName(outputPath) +
                    "\" BINARY\r\n" +
                    "  TRACK 01 MODE1/2352\r\n" +
                    "    INDEX 01 00:00:00\r\n";
                File.WriteAllText(
                    cuePath, cueContent);

                Console.WriteLine(
                    "  Also created: " +
                    Path.GetFileName(cuePath));
            }
            else
            {
                Console.WriteLine(
                    "  Format: ISO (2048)");
                Console.WriteLine(
                    $"  Size: " +
                    $"{output.Length:N0}" +
                    $" bytes" +
                    $" ({output.Length / SEC:N0}" +
                    " sectors)");

                File.WriteAllBytes(
                    outputPath, output);
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                toDvd
                ? " DVD image created!"
                : " CD image created!");
            Console.WriteLine(
                $" {outputPath}");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();

            // ─── Auto-fix ────────────────────
            // Runs on both ISO and BIN outputs
            if (!outIsBin)
            {
                RunAutoFix(outputPath,
                    gv, toDvd,
                    skipLba: false);
            }
            else
            {
                // For BIN: create temp ISO,
                // fix it, convert back to BIN
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "Running auto-fix on" +
                    " BIN output...");
                Console.WriteLine(
                    "  (Creating temp ISO," +
                    " fixing, reconverting" +
                    " to BIN)");
                Console.ResetColor();

                string tempIso =
                    outputPath + ".tmp.iso";

                try
                {
                    // Write ISO output to temp
                    File.WriteAllBytes(
                        tempIso, output);

                    // Free the output buffer
                    // to make room for fixed
                    output = null;
                    GC.Collect();

                    // Run fixiso on temp ISO
                    RunAutoFix(tempIso, gv,
                        toDvd,
                        skipLba: false);

                    // Read fixed ISO back
                    byte[] fixedIso =
                        File.ReadAllBytes(
                            tempIso);

                    // Convert to BIN
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "  Reconverting fixed" +
                        " ISO back to BIN...");
                    Console.ResetColor();

                    // Stream directly
                    ConvertIsoToBinStream(
                        fixedIso, outputPath);

                    // Delete temp
                    if (File.Exists(tempIso))
                        File.Delete(tempIso);

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "  Fixed BIN saved!");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  BIN auto-fix" +
                        " warning: " +
                        ex.Message);
                    Console.ResetColor();

                    try
                    {
                        if (File.Exists(tempIso))
                            File.Delete(tempIso);
                    }
                    catch { }
                }

                Console.WriteLine();
            }
        }

        // ═══════════════════════════════════════
        // BUILD DVD FROM CD
        // ═══════════════════════════════════════
        static byte[] BuildDvd(
            byte[] cd,
            GameVersion gv,
            int lbaTableOff)
        {
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[1/6] Reading CD" +
                " structure...");
            Console.ResetColor();

            int cdSectors = cd.Length / SEC;
            int cdGameSectors =
                cdSectors - CD_FIRST_DATA;

            // DVD layout:
            // 0..15: system area (blank)
            // 16-17: PVD + terminator
            // 18-256: path tables + padding
            // 257: L path table
            // 259: M path table
            // 261: root dir
            // 262..289: subdirs (29 dirs)
            // 290..680: padding (blank)
            // 681..: game data
            // then Z1 (524288 sectors)
            // then Z2 (256000 sectors)

            int dvdGameStart = DVD_FIRST_DATA;
            int dvdGameEnd =
                dvdGameStart + cdGameSectors;
            int z1Start = dvdGameEnd;
            int z2Start = z1Start + Z1_SECTORS;
            int dvdTotalSec =
                z2Start + Z2_SECTORS;
            long dvdSize =
                (long)dvdTotalSec * SEC;

            Console.WriteLine(
                $"  DVD sectors:" +
                $" {dvdTotalSec:N0}");
            Console.WriteLine(
                $"  DVD size:" +
                $" {dvdSize:N0} bytes");

            byte[] dvd =
                TryAllocate(dvdSize, "DVD");
            if (dvd == null) return null;

            // ─── Copy system area 0-15 ──────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[2/6] Copying system" +
                " area (sectors 0-15)...");
            Console.ResetColor();

            int sysBytes = 16 * SEC;
            Array.Copy(cd, 0, dvd, 0,
                sysBytes);

            // ─── Rebuild PVD (sector 16) ────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[3/6] Building PVD...");
            Console.ResetColor();

            // Copy CD PVD to DVD PVD
            Array.Copy(cd, 16 * SEC,
                dvd, 16 * SEC, SEC);

            // Copy VD terminator (17)
            Array.Copy(cd, 17 * SEC,
                dvd, 17 * SEC, SEC);

            // ─── Add UDF descriptors ────────
            // This is what makes PS2 recognize
            // the disc as DVD instead of CD
            // Sectors 18, 19, 20 get UDF
            // Bridge Format markers
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Adding UDF Bridge Format" +
                " descriptors (DVD markers)...");
            Console.ResetColor();

            // Sector 18: BEA01
            // (Beginning Extended Area)
            int udfOff = 18 * SEC;
            Array.Clear(dvd, udfOff, SEC);
            dvd[udfOff + 0] = 0x00;
            dvd[udfOff + 1] = (byte)'B';
            dvd[udfOff + 2] = (byte)'E';
            dvd[udfOff + 3] = (byte)'A';
            dvd[udfOff + 4] = (byte)'0';
            dvd[udfOff + 5] = (byte)'1';
            dvd[udfOff + 6] = 0x01;
            dvd[udfOff + 7] = 0x00;

            // Sector 19: NSR02
            // (Non-Sequential Recording v2)
            udfOff = 19 * SEC;
            Array.Clear(dvd, udfOff, SEC);
            dvd[udfOff + 0] = 0x00;
            dvd[udfOff + 1] = (byte)'N';
            dvd[udfOff + 2] = (byte)'S';
            dvd[udfOff + 3] = (byte)'R';
            dvd[udfOff + 4] = (byte)'0';
            dvd[udfOff + 5] = (byte)'2';
            dvd[udfOff + 6] = 0x01;
            dvd[udfOff + 7] = 0x00;

            // Sector 20: TEA01
            // (Terminating Extended Area)
            udfOff = 20 * SEC;
            Array.Clear(dvd, udfOff, SEC);
            dvd[udfOff + 0] = 0x00;
            dvd[udfOff + 1] = (byte)'T';
            dvd[udfOff + 2] = (byte)'E';
            dvd[udfOff + 3] = (byte)'A';
            dvd[udfOff + 4] = (byte)'0';
            dvd[udfOff + 5] = (byte)'1';
            dvd[udfOff + 6] = 0x01;
            dvd[udfOff + 7] = 0x00;

            // ─── Clear Master Disc markers ──
            // CD has SLUS-20251 markers at
            // sectors 14-15. DVD does not
            // have these. Clear them for DVD.
            Array.Clear(dvd, 14 * SEC, SEC);
            Array.Clear(dvd, 15 * SEC, SEC);

            int pvdOff = 16 * SEC;

            // Update volume size
            WriteU32LeBe(dvd, pvdOff + 80,
                (uint)dvdTotalSec);

            // Update path table LBAs
            WriteU32Le(dvd, pvdOff + 140,
                (uint)DVD_PATH_L_LBA);
            WriteU32Le(dvd, pvdOff + 144,
                0); // optional L
            WriteU32Be(dvd, pvdOff + 148,
                (uint)DVD_PATH_M_LBA);
            WriteU32Be(dvd, pvdOff + 152,
                0); // optional M

            // Update root dir record
            // (LBA and size at +156)
            // The root dir record is 34 bytes
            // Copy structure from CD
            int rootRecOff = pvdOff + 156;
            // The root dir size stays 2048
            // (single sector)
            // Update root LBA
            WriteU32LeBe(dvd,
                rootRecOff + 2,
                (uint)DVD_ROOT_LBA);
            // Size stays 2048
            WriteU32LeBe(dvd,
                rootRecOff + 10, 2048);

            // ─── Rebuild path tables ────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[4/6] Rebuilding path" +
                " tables...");
            Console.ResetColor();

            RebuildPathTables(
                cd, dvd,
                CD_PATH_L_LBA,
                DVD_PATH_L_LBA,
                DVD_PATH_M_LBA,
                DIR_SHIFT);

            // ─── Rebuild directory tree ─────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[5/6] Rebuilding" +
                " directory tree...");
            Console.ResetColor();

            // For each directory in CD,
            // copy sector to new DVD location
            // and fix all LBAs inside
            int rewrittenDirs = RewriteAllDirs(
                cd, dvd,
                CD_ROOT_LBA,
                DVD_ROOT_LBA,
                DIR_SHIFT,
                DATA_SHIFT,
                isTowardDvd: true);

            Console.WriteLine(
                $"  Rewrote {rewrittenDirs}" +
                " directory sectors.");

            // Add Z1 and Z2 dummy entries
            // to root directory
            AddDummyEntryToRoot(
                dvd, DVD_ROOT_LBA,
                "Z1.Z", (uint)z1Start,
                (uint)Z1_SIZE);
            AddDummyEntryToRoot(
                dvd, DVD_ROOT_LBA,
                "Z2.Z", (uint)z2Start,
                (uint)Z2_SIZE);

            Console.WriteLine(
                $"  Added Z1. at" +
                $" LBA {z1Start}" +
                $" (1 GB dummy)");
            Console.WriteLine(
                $"  Added Z2. at" +
                $" LBA {z2Start}" +
                $" (500 MB dummy)");

            // ─── Copy game data ─────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[6/6] Copying game" +
                " data + fixing ELF...");
            Console.ResetColor();

            // Copy sectors CD:51..end to
            // DVD:681..
            for (int s = CD_FIRST_DATA;
                 s < cdSectors; s++)
            {
                int cdOff = s * SEC;
                int dvdOff =
                    (s + DATA_SHIFT) * SEC;
                if (dvdOff + SEC >
                    dvd.Length) break;
                Array.Copy(cd, cdOff,
                    dvd, dvdOff, SEC);
            }

            // ─── Fix ELF LBA table ──────────
            // ELF is at CD sector 37894
            // → DVD sector 37894 + 630 = 38524
            int cdElfSec =
                FindMainElfSector(cd);
            int dvdElfSec =
                cdElfSec + DATA_SHIFT;
            int dvdElfOff = dvdElfSec * SEC;

            int lbaTableAbs =
                dvdElfOff + lbaTableOff;

            int fixedEntries = 0;

            // Scan a bigger region for LBAs
            // The LBA table extends beyond
            // what we thought. Scan 4KB total
            // (1024 entries)
            const int SCAN_ENTRIES = 1024;

            // Get CD total sectors to know
            // upper bound of valid LBAs
            int cdTotalSec = cd.Length / SEC;

            for (int i = 0;
                 i < SCAN_ENTRIES; i++)
            {
                int off = lbaTableAbs + i * 4;
                if (off + 4 > dvd.Length)
                    break;
                uint val = ReadU32Le(dvd, off);

                // Skip zeros
                if (val == 0) continue;

                // Only patch values that
                // look like valid CD LBAs
                // (between CD_FIRST_DATA and
                //  CD total sectors)
                if (val < (uint)CD_FIRST_DATA)
                    continue;
                if (val >= (uint)cdTotalSec)
                    continue;

                WriteU32Le(dvd, off,
                    val + (uint)DATA_SHIFT);
                fixedEntries++;
            }

            Console.WriteLine(
                $"  Fixed {fixedEntries}" +
                " LBA entries in ELF" +
                " (scanned " +
                $"{SCAN_ENTRIES} slots).");

            Console.WriteLine(
                $"  Fixed {fixedEntries}" +
                " LBA entries in ELF.");

            // ─── Patch IOP file for DVD mode ────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "  Patching IOP file" +
                " (CD → DVD mode)...");
            Console.ResetColor();

            PatchIopFile(
                dvd,
                DVD_FIRST_DATA,
                toDvd: true);

            return dvd;
        }

        // ═══════════════════════════════════════
        // BUILD CD FROM DVD
        // ═══════════════════════════════════════
        static byte[] BuildCd(
            byte[] dvd,
            GameVersion gv,
            int lbaTableOff)
        {
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[1/6] Reading DVD" +
                " structure...");
            Console.ResetColor();

            // Find where game data ends
            // (before Z1)
            int z1Lba = FindRootEntryLba(
                dvd, DVD_ROOT_LBA, "Z1.");
            if (z1Lba < 0)
            {
                TextOut.PrintError(
                    "Z1 dummy not found" +
                    " in DVD!");
                return null;
            }

            int dvdGameSectors =
                z1Lba - DVD_FIRST_DATA;
            int cdTotalSec =
                CD_FIRST_DATA + dvdGameSectors;
            long cdSize =
                (long)cdTotalSec * SEC;

            Console.WriteLine(
                $"  CD sectors:" +
                $" {cdTotalSec:N0}");
            Console.WriteLine(
                $"  CD size:" +
                $" {cdSize:N0} bytes");

            byte[] cd =
                TryAllocate(cdSize, "CD");
            if (cd == null) return null;

            // ─── Copy system area 0-15 ──────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[2/6] Copying system" +
                " area...");
            Console.ResetColor();

            Array.Copy(dvd, 0, cd, 0,
                16 * SEC);

            // ─── Rebuild PVD ────────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[3/6] Building PVD...");
            Console.ResetColor();

            // Copy PVD
            Array.Copy(dvd, 16 * SEC,
                cd, 16 * SEC, SEC);
            Array.Copy(dvd, 17 * SEC,
                cd, 17 * SEC, SEC);

            // ─── Clear UDF descriptors ──────
            // CD doesn't use UDF, just ISO9660
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Removing UDF descriptors" +
                " (CD mode)...");
            Console.ResetColor();
            Array.Clear(cd, 18 * SEC, SEC);
            Array.Clear(cd, 19 * SEC, SEC);
            Array.Clear(cd, 20 * SEC, SEC);

            // ─── Add Master Disc markers ────
            // CD needs SLUS-20251 markers
            // at sectors 14 and 15
            // Copy from CD template
            byte[] masterMarker =
                new byte[SEC];
            byte[] slusStr = System.Text
                .Encoding.ASCII.GetBytes(
                    "SLUS-20251");
            // Detect version
            GameVersion mgv = DetectVersion(
                dvd, FindMainElfSector(dvd) * SEC);
            switch (mgv)
            {
                case GameVersion.JAP:
                    slusStr = System.Text
                        .Encoding.ASCII.GetBytes(
                            "SLPS-20104");
                    break;
                case GameVersion.DEMO:
                    slusStr = System.Text
                        .Encoding.ASCII.GetBytes(
                            "SLPM-60147");
                    break;
            }

            // Fill with spaces (0x20)
            for (int i = 0; i < SEC; i++)
                masterMarker[i] = 0x20;

            // Copy SLUS string
            Array.Copy(slusStr, 0,
                masterMarker, 0,
                slusStr.Length);

            // PlayStation Master Disc string
            byte[] mdStr = System.Text
                .Encoding.ASCII.GetBytes(
                    "20020905PlayStation" +
                    " Master Disc 2");
            Array.Copy(mdStr, 0,
                masterMarker, 0x60,
                mdStr.Length);

            // Copy to sectors 14 and 15
            Array.Copy(masterMarker, 0,
                cd, 14 * SEC, SEC);
            Array.Copy(masterMarker, 0,
                cd, 15 * SEC, SEC);

            int pvdOff = 16 * SEC;

            WriteU32LeBe(cd, pvdOff + 80,
                (uint)cdTotalSec);
            WriteU32Le(cd, pvdOff + 140,
                (uint)CD_PATH_L_LBA);
            WriteU32Le(cd, pvdOff + 144, 0);
            WriteU32Be(cd, pvdOff + 148,
                (uint)CD_PATH_M_LBA);
            WriteU32Be(cd, pvdOff + 152, 0);

            int rootRecOff = pvdOff + 156;
            WriteU32LeBe(cd,
                rootRecOff + 2,
                (uint)CD_ROOT_LBA);
            WriteU32LeBe(cd,
                rootRecOff + 10, 2048);

            // ─── Path tables ────────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[4/6] Rebuilding path" +
                " tables...");
            Console.ResetColor();

            RebuildPathTables(
                dvd, cd,
                DVD_PATH_L_LBA,
                CD_PATH_L_LBA,
                CD_PATH_M_LBA,
                -DIR_SHIFT);

            // ─── Rebuild dirs ───────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[5/6] Rebuilding" +
                " directory tree...");
            Console.ResetColor();

            int rewritten = RewriteAllDirs(
                dvd, cd,
                DVD_ROOT_LBA,
                CD_ROOT_LBA,
                -DIR_SHIFT,
                -DATA_SHIFT,
                isTowardDvd: false);

            Console.WriteLine(
                $"  Rewrote {rewritten}" +
                " directory sectors.");

            // Root will have Z1/Z2 entries
            // that got shifted - remove them
            RemoveDummyEntries(
                cd, CD_ROOT_LBA);

            // ─── Copy game data ─────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[6/6] Copying game" +
                " data + fixing ELF...");
            Console.ResetColor();

            for (int s = 0;
                 s < dvdGameSectors; s++)
            {
                int dvdOff =
                    (DVD_FIRST_DATA + s)
                    * SEC;
                int cdOff =
                    (CD_FIRST_DATA + s)
                    * SEC;
                if (dvdOff + SEC >
                    dvd.Length) break;
                if (cdOff + SEC >
                    cd.Length) break;
                Array.Copy(dvd, dvdOff,
                    cd, cdOff, SEC);
            }

            // Fix ELF LBA table
            int dvdElfSec =
                FindMainElfSector(dvd);
            int cdElfSec =
                dvdElfSec - DATA_SHIFT;
            int cdElfOff = cdElfSec * SEC;

            int lbaTableAbs =
                cdElfOff + lbaTableOff;

            int fixedEntries = 0;

            const int SCAN_ENTRIES = 1024;

            int dvdTotalSec = dvd.Length / SEC;

            for (int i = 0;
                 i < SCAN_ENTRIES; i++)
            {
                int off = lbaTableAbs + i * 4;
                if (off + 4 > cd.Length)
                    break;
                uint val = ReadU32Le(cd, off);

                if (val == 0) continue;

                // Only patch values that
                // look like valid DVD LBAs
                if (val < (uint)DVD_FIRST_DATA)
                    continue;
                if (val >= (uint)dvdTotalSec)
                    continue;

                WriteU32Le(cd, off,
                    val - (uint)DATA_SHIFT);
                fixedEntries++;
            }

            Console.WriteLine(
                $"  Fixed {fixedEntries}" +
                " LBA entries in ELF" +
                " (scanned " +
                $"{SCAN_ENTRIES} slots).");

            Console.WriteLine(
                $"  Fixed {fixedEntries}" +
                " LBA entries in ELF.");

            // ─── Patch IOP file for CD mode ─
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "  Patching IOP file" +
                " (DVD → CD mode)...");
            Console.ResetColor();
            PatchIopFile(
                cd,
                CD_FIRST_DATA,
                toDvd: false);

            return cd;
        }

        // ═══════════════════════════════════════
        // REBUILD PATH TABLES
        // Read from src at srcLba
        // Write to dst at dstLba (L) and dstM (M)
        // Shift each entry LBA by shift
        // ═══════════════════════════════════════
        static void RebuildPathTables(
            byte[] src, byte[] dst,
            int srcLba, int dstLba,
            int dstM, int shift)
        {
            int srcOff = srcLba * SEC;

            // Path tables are variable
            // Copy raw path table sector
            // Fix each entry's LBA

            byte[] newTable = new byte[SEC];
            int p = 0;
            int sp = srcOff;

            while (sp < srcOff + SEC)
            {
                byte nl = src[sp];
                if (nl == 0) break;
                byte ea = src[sp + 1];
                uint lba = ReadU32Le(
                    src, sp + 2);
                ushort parent =
                    ReadU16Le(src, sp + 6);
                int nameEnd = sp + 8 + nl;

                // Write LE version
                newTable[p] = nl;
                newTable[p + 1] = ea;
                uint newLba =
                    (uint)((int)lba + shift);
                WriteU32Le(newTable,
                    p + 2, newLba);
                WriteU16Le(newTable,
                    p + 6, parent);
                Array.Copy(src,
                    sp + 8,
                    newTable,
                    p + 8, nl);

                p += 8 + nl;
                if (nl % 2 != 0)
                    p++;
                sp += 8 + nl;
                if (nl % 2 != 0)
                    sp++;
            }

            // Write L table (LE)
            Array.Copy(newTable, 0,
                dst, dstLba * SEC, SEC);

            // Build M table (BE)
            byte[] mTable = new byte[SEC];
            int mp = 0;
            int rp = 0;

            while (rp < SEC)
            {
                byte nl = newTable[rp];
                if (nl == 0) break;
                byte ea = newTable[rp + 1];
                uint lba = ReadU32Le(
                    newTable, rp + 2);
                ushort parent =
                    ReadU16Le(newTable,
                        rp + 6);

                mTable[mp] = nl;
                mTable[mp + 1] = ea;
                WriteU32Be(mTable,
                    mp + 2, lba);
                WriteU16Be(mTable,
                    mp + 6, parent);
                Array.Copy(newTable,
                    rp + 8,
                    mTable,
                    mp + 8, nl);

                mp += 8 + nl;
                if (nl % 2 != 0) mp++;
                rp += 8 + nl;
                if (nl % 2 != 0) rp++;
            }

            Array.Copy(mTable, 0,
                dst, dstM * SEC, SEC);
        }

        // ═══════════════════════════════════════
        // REWRITE ALL DIRECTORIES
        // Recursively walk source dirs,
        // copy each sector to dst at new LBA,
        // fix all inner LBAs
        // Returns count of dirs rewritten
        // ═══════════════════════════════════════
        static int RewriteAllDirs(
            byte[] src, byte[] dst,
            int srcRootLba,
            int dstRootLba,
            int dirShift,
            int dataShift,
            bool isTowardDvd)
        {
            int count = 0;
            var visited =
                new HashSet<int>();
            RewriteDir(
                src, dst,
                srcRootLba, dstRootLba,
                dstRootLba,
                dirShift, dataShift,
                visited, ref count);
            return count;
        }

        static void RewriteDir(
            byte[] src, byte[] dst,
            int srcLba, int dstLba,
            int parentDstLba,
            int dirShift, int dataShift,
            HashSet<int> visited,
            ref int count)
        {
            if (visited.Contains(srcLba))
                return;
            visited.Add(srcLba);

            int srcOff = srcLba * SEC;
            int dstOff = dstLba * SEC;

            // Copy sector first
            Array.Copy(src, srcOff,
                dst, dstOff, SEC);
            count++;

            // Fix entries in copied sector
            int pos = dstOff;
            int end = dstOff + SEC;
            int entryIdx = 0;

            while (pos < end)
            {
                byte rl = dst[pos];
                if (rl == 0) break;

                byte nl = dst[pos + 32];
                byte flags = dst[pos + 25];
                bool isDir =
                    (flags & 0x02) != 0;

                uint origLba = ReadU32Le(
                    dst, pos + 2);

                if (entryIdx == 0)
                {
                    // "." entry
                    WriteU32LeBe(dst,
                        pos + 2,
                        (uint)dstLba);
                }
                else if (entryIdx == 1)
                {
                    // ".." entry
                    WriteU32LeBe(dst,
                        pos + 2,
                        (uint)parentDstLba);
                }
                else
                {
                    // Regular entry
                    uint newLba;
                    if (isDir)
                    {
                        newLba = (uint)(
                            (int)origLba
                            + dirShift);
                    }
                    else
                    {
                        newLba = (uint)(
                            (int)origLba
                            + dataShift);
                    }
                    WriteU32LeBe(dst,
                        pos + 2, newLba);

                    // Recurse into subdirs
                    if (isDir)
                    {
                        RewriteDir(
                            src, dst,
                            (int)origLba,
                            (int)newLba,
                            dstLba,
                            dirShift,
                            dataShift,
                            visited,
                            ref count);
                    }
                }

                entryIdx++;
                pos += rl;
            }
        }

        // ═══════════════════════════════════════
        // ADD DUMMY ENTRY TO ROOT DIRECTORY
        // ═══════════════════════════════════════
        static void AddDummyEntryToRoot(
            byte[] data, int rootLba,
            string name,
            uint lba, uint size)
        {
            int rootOff = rootLba * SEC;
            int end = rootOff + SEC;

            // Find last used byte
            int pos = rootOff;
            int lastEnd = rootOff;

            while (pos < end)
            {
                byte rl = data[pos];
                if (rl == 0) break;
                lastEnd = pos + rl;
                pos += rl;
            }

            // Build entry
            string fullName = name + ";1";
            byte[] nameBytes = System.Text
                .Encoding.ASCII.GetBytes(
                    fullName);
            int nameLen = nameBytes.Length;

            int recLen = 33 + nameLen;
            if (recLen % 2 != 0) recLen++;

            if (lastEnd + recLen > end)
                return;

            int wp = lastEnd;

            data[wp] = (byte)recLen;
            data[wp + 1] = 0;
            WriteU32LeBe(data, wp + 2, lba);
            WriteU32LeBe(data, wp + 10, size);

            // Date bytes (dummy)
            data[wp + 18] = 100; // year
            data[wp + 19] = 1;
            data[wp + 20] = 1;
            data[wp + 21] = 0;
            data[wp + 22] = 0;
            data[wp + 23] = 0;
            data[wp + 24] = 0;

            data[wp + 25] = 0; // file
            data[wp + 26] = 0;
            data[wp + 27] = 0;
            data[wp + 28] = 1;
            data[wp + 29] = 0;
            data[wp + 30] = 1;
            data[wp + 31] = 0;
            data[wp + 32] = (byte)nameLen;
            Array.Copy(nameBytes, 0,
                data, wp + 33, nameLen);
        }

        // ═══════════════════════════════════════
        // REMOVE Z1/Z2 DUMMY ENTRIES
        // ═══════════════════════════════════════
        static void RemoveDummyEntries(
            byte[] data, int rootLba)
        {
            int rootOff = rootLba * SEC;
            int end = rootOff + SEC;
            int pos = rootOff;

            while (pos < end)
            {
                byte rl = data[pos];
                if (rl == 0) break;
                byte nl = data[pos + 32];
                if (nl > 0)
                {
                    string name = System.Text
                        .Encoding.ASCII
                        .GetString(data,
                            pos + 33, nl);
                    if (name.StartsWith("Z1.")
                        || name.StartsWith(
                            "Z2."))
                    {
                        // Zero out
                        for (int i = 0;
                             i < rl; i++)
                            data[pos + i] = 0;
                    }
                }
                pos += rl;
            }
        }

        // ═══════════════════════════════════════
        // FIND ENTRY LBA IN ROOT DIRECTORY
        // ═══════════════════════════════════════
        static int FindRootEntryLba(
            byte[] data, int rootLba,
            string namePrefix)
        {
            int off = rootLba * SEC;
            int end = off + SEC;
            int pos = off;

            while (pos < end)
            {
                byte rl = data[pos];
                if (rl == 0) break;
                byte nl = data[pos + 32];
                if (nl > 0)
                {
                    string name = System.Text
                        .Encoding.ASCII
                        .GetString(data,
                            pos + 33, nl);
                    if (name.StartsWith(
                        namePrefix,
                        StringComparison
                        .OrdinalIgnoreCase))
                    {
                        return (int)ReadU32Le(
                            data, pos + 2);
                    }
                }
                pos += rl;
            }
            return -1;
        }

        // ═══════════════════════════════════════
        // DETECT CD vs DVD
        // ═══════════════════════════════════════
        static bool IsCdIso(byte[] data)
        {
            if (data.Length < 17 * SEC)
                return false;
            int pvd = 16 * SEC;
            if (data[pvd] != 1) return false;
            uint rootLba = ReadU32Le(
                data, pvd + 156 + 2);
            return rootLba == CD_ROOT_LBA;
        }

        static bool IsDvdIso(byte[] data)
        {
            if (data.Length < 17 * SEC)
                return false;
            int pvd = 16 * SEC;
            if (data[pvd] != 1) return false;
            uint rootLba = ReadU32Le(
                data, pvd + 156 + 2);
            return rootLba == DVD_ROOT_LBA;
        }

        // ═══════════════════════════════════════
        // FIND MAIN ELF SECTOR
        // ═══════════════════════════════════════
        static int FindMainElfSector(
            byte[] data)
        {
            int n = data.Length / SEC;
            for (int s = 0; s < n; s++)
            {
                int off = s * SEC;
                if (off + 4 > data.Length)
                    break;
                if (data[off] != 0x7F ||
                    data[off + 1] != (byte)'E' ||
                    data[off + 2] != (byte)'L' ||
                    data[off + 3] != (byte)'F')
                    continue;
                uint entry = ReadU32Le(
                    data, off + 0x18);
                if (entry == 0x00100008u)
                    return s;
            }
            return -1;
        }

        // ═══════════════════════════════════════
        // BIN/RAW FORMAT DETECTION
        // ═══════════════════════════════════════
        static void DetectFormat(
            byte[] data,
            out int sectorSize,
            out int dataOff)
        {
            if (data.Length >= 16 &&
                data[0] == 0x00 &&
                data[1] == 0xFF &&
                data[2] == 0xFF &&
                data[3] == 0xFF &&
                data[11] == 0x00)
            {
                sectorSize = 2352;
                if (data[15] == 0x02)
                    dataOff = 24;
                else
                    dataOff = 16;
                return;
            }
            sectorSize = 2048;
            dataOff = 0;
        }

        static byte[] ExtractIsoFromBin(
            byte[] bin,
            int sectorSize,
            int dataOff)
        {
            int numSec =
                bin.Length / sectorSize;
            long isoSize =
                (long)numSec * SEC;

            byte[] iso = TryAllocate(
                isoSize,
                "ISO buffer");
            if (iso == null)
                throw new
                    OutOfMemoryException(
                    "Cannot allocate ISO" +
                    " buffer");

            for (int s = 0; s < numSec; s++)
            {
                int srcOff =
                    s * sectorSize + dataOff;
                int dstOff = s * SEC;
                if (srcOff + SEC > bin.Length)
                    break;
                Array.Copy(bin, srcOff,
                    iso, dstOff, SEC);
            }
            return iso;
        }

        // ═══════════════════════════════════════
        // DETECT GAME VERSION
        // ═══════════════════════════════════════
        static GameVersion DetectVersion(
            byte[] data, int elfOff)
        {
            byte[] demo = System.Text
                .Encoding.ASCII.GetBytes(
                    "SLPM_601.47");
            byte[] jap = System.Text
                .Encoding.ASCII.GetBytes(
                    "SLPS_201.04");
            byte[] usa = System.Text
                .Encoding.ASCII.GetBytes(
                    "SLUS_202.51");

            // Scan the entire file for the
            // ELF filename. This is slow
            // for DVD but reliable.
            // Use a stride to speed it up.
            // The filename appears in
            // multiple places (root dir,
            // path table, SYSTEM.CNF).

            // First try root directories
            // (fast path)
            int[] rootLbas =
                new int[] { 22, 261 };
            foreach (int lba in rootLbas)
            {
                int rootOff = lba * SEC;
                if (rootOff + SEC > data.Length)
                    continue;
                for (int i = rootOff;
                     i < rootOff + SEC - 11;
                     i++)
                {
                    if (Match(data, i, demo))
                        return GameVersion.DEMO;
                    if (Match(data, i, jap))
                        return GameVersion.JAP;
                    if (Match(data, i, usa))
                        return GameVersion.USA;
                }
            }

            // Also check SYSTEM.CNF which
            // is typically right before ELF
            // Scan from ELF - 4KB
            // to ELF + 4KB
            int checkStart =
                Math.Max(0, elfOff - 4096);
            int checkEnd = Math.Min(
                data.Length, elfOff + 4096);
            for (int i = checkStart;
                 i + 11 < checkEnd; i++)
            {
                if (Match(data, i, demo))
                    return GameVersion.DEMO;
                if (Match(data, i, jap))
                    return GameVersion.JAP;
                if (Match(data, i, usa))
                    return GameVersion.USA;
            }

            // Path tables (CD sec 18/DVD 257)
            int[] pathLbas =
                new int[] { 18, 257 };
            foreach (int lba in pathLbas)
            {
                int pOff = lba * SEC;
                if (pOff + SEC > data.Length)
                    continue;
                for (int i = pOff;
                     i < pOff + SEC - 11;
                     i++)
                {
                    if (Match(data, i, demo))
                        return GameVersion.DEMO;
                    if (Match(data, i, jap))
                        return GameVersion.JAP;
                    if (Match(data, i, usa))
                        return GameVersion.USA;
                }
            }

            return GameVersion.USA;
        }

        static int GetLbaTableOffset(
            GameVersion gv)
        {
            switch (gv)
            {
                case GameVersion.DEMO:
                    return 0x1633E0;
                case GameVersion.JAP:
                    return 0x162360;
                default:
                    return 0x162460;
            }
        }

        static string GetVersionName(
            GameVersion gv)
        {
            switch (gv)
            {
                case GameVersion.DEMO:
                    return "JAP DEMO";
                case GameVersion.JAP:
                    return "Japanese";
                default:
                    return "USA";
            }
        }

        // ═══════════════════════════════════════
        // AUTO-FIX AFTER CONVERSION
        // ═══════════════════════════════════════
        static void RunAutoFix(
            string path,
            GameVersion gv,
            bool isDvd,
            bool skipLba = false)
        {
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "Running auto-fix on" +
                " output ISO...");
            Console.ResetColor();

            try
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [1/3] Repairing" +
                    " ISO structure...");
                Console.ResetColor();
                IsoRepair.FixIso(path);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [1/3] warning: " +
                    ex.Message);
                Console.ResetColor();
            }

            try
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [2/3] Fixing PS2" +
                    " logo..." +
                    (isDvd
                        ? " (DVD mode)"
                        : " (CD mode)"));
                Console.ResetColor();
                IsoLogoPatcher.PatchIso(
                    path, null, gv);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [2/3] warning: " +
                    ex.Message);
                Console.ResetColor();
            }

            if (skipLba)
            {
                Console.ForegroundColor =
                    ConsoleColor.DarkGray;
                Console.WriteLine(
                    "  [3/3] Skipping LBA" +
                    " table fix" +
                    " (already patched" +
                    " during conversion)");
                Console.ResetColor();
            }
            else
            {
                try
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [3/3] Fixing LBA" +
                        " table...");
                    Console.ResetColor();
                    int changes =
                        HarvestIso.FixLba(
                            path, gv);
                    Console.WriteLine(
                        changes == 0
                        ? "        LBA OK."
                        : $"        Patched" +
                          $" {changes} LBAs.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [3/3] warning: " +
                        ex.Message);
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                " ALL DONE! ISO is" +
                " ready to play.");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═══════════════════════════════════════
        // BANNER
        // ═══════════════════════════════════════
        static void PrintBanner(
            string direction)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                $" HMSTH ISO Converter" +
                $" [{direction}]");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═══════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════
        static bool Match(
            byte[] data, int off,
            byte[] pat)
        {
            if (off + pat.Length > data.Length)
                return false;
            for (int i = 0; i < pat.Length; i++)
                if (data[off + i] != pat[i])
                    return false;
            return true;
        }

        static uint ReadU32Le(
            byte[] data, int off)
        {
            if (off < 0 ||
                off + 4 > data.Length)
                return 0;
            return (uint)(
                data[off] |
                (data[off + 1] << 8) |
                (data[off + 2] << 16) |
                (data[off + 3] << 24));
        }

        static ushort ReadU16Le(
            byte[] data, int off)
        {
            if (off < 0 ||
                off + 2 > data.Length)
                return 0;
            return (ushort)(
                data[off] |
                (data[off + 1] << 8));
        }

        static void WriteU32Le(
            byte[] data, int off,
            uint val)
        {
            if (off < 0 ||
                off + 4 > data.Length)
                return;
            data[off] =
                (byte)(val & 0xFF);
            data[off + 1] =
                (byte)((val >> 8) & 0xFF);
            data[off + 2] =
                (byte)((val >> 16) & 0xFF);
            data[off + 3] =
                (byte)((val >> 24) & 0xFF);
        }

        static void WriteU32Be(
            byte[] data, int off,
            uint val)
        {
            if (off < 0 ||
                off + 4 > data.Length)
                return;
            data[off] =
                (byte)((val >> 24) & 0xFF);
            data[off + 1] =
                (byte)((val >> 16) & 0xFF);
            data[off + 2] =
                (byte)((val >> 8) & 0xFF);
            data[off + 3] =
                (byte)(val & 0xFF);
        }

        static void WriteU16Le(
            byte[] data, int off,
            ushort val)
        {
            if (off < 0 ||
                off + 2 > data.Length)
                return;
            data[off] =
                (byte)(val & 0xFF);
            data[off + 1] =
                (byte)((val >> 8) & 0xFF);
        }

        static void WriteU16Be(
            byte[] data, int off,
            ushort val)
        {
            if (off < 0 ||
                off + 2 > data.Length)
                return;
            data[off] =
                (byte)((val >> 8) & 0xFF);
            data[off + 1] =
                (byte)(val & 0xFF);
        }

        static void WriteU32LeBe(
            byte[] data, int off,
            uint val)
        {
            WriteU32Le(data, off, val);
            WriteU32Be(data, off + 4, val);
        }

        // ═══════════════════════════════════════
        // CONVERT ISO 2048 TO BIN 2352
        // Streams directly to file to avoid
        // out-of-memory errors on large DVDs
        // ═══════════════════════════════════════
        static void ConvertIsoToBinStream(
            byte[] iso,
            string outputPath)
        {
            int numSec = iso.Length / SEC;

            byte[] sync = new byte[]
            {
                0x00, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0x00
            };

            // Reusable per-sector buffer
            byte[] secBuf = new byte[2352];

            using (var fs = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                65536))
            {
                for (int s = 0;
                     s < numSec; s++)
                {
                    Array.Clear(secBuf, 0,
                        2352);

                    // Sync (12 bytes)
                    Array.Copy(sync, 0,
                        secBuf, 0, 12);

                    // Header (MSF + mode)
                    int lba = s + 150;
                    int minute =
                        lba / (60 * 75);
                    int second =
                        (lba / 75) % 60;
                    int frame = lba % 75;

                    secBuf[12] =
                        ToBcd(minute);
                    secBuf[13] =
                        ToBcd(second);
                    secBuf[14] =
                        ToBcd(frame);
                    secBuf[15] = 0x02;

                    // Subheader Mode2 Form1
                    secBuf[16] = 0x00;
                    secBuf[17] = 0x00;
                    secBuf[18] = 0x08;
                    secBuf[19] = 0x00;
                    secBuf[20] = 0x00;
                    secBuf[21] = 0x00;
                    secBuf[22] = 0x08;
                    secBuf[23] = 0x00;

                    // User data
                    Array.Copy(iso,
                        s * SEC,
                        secBuf, 24,
                        SEC);

                    // EDC/ECC stays zero

                    fs.Write(secBuf, 0,
                        2352);

                    // Progress report
                    if (s % 50000 == 0
                        && s > 0)
                    {
                        double pct =
                            (double)s /
                            numSec *
                            100.0;
                        Console.Write(
                            $"\r  " +
                            $"Writing BIN:" +
                            $" {pct:F1}%");
                    }
                }
            }

            Console.WriteLine();
        }

        static byte ToBcd(int val)
        {
            return (byte)(
                ((val / 10) << 4) |
                (val % 10));
        }

        // ═══════════════════════════════════════
        // PATCH IOP FILE IN OUTPUT
        // Sets 2 bytes to mark as CD or DVD
        // ═══════════════════════════════════════
        static void PatchIopFile(
            byte[] data,
            int firstDataLba,
            bool toDvd)
        {
            // IOP file is the first file in
            // the game data area
            // (starts at CD_FIRST_DATA=51 or
            //  DVD_FIRST_DATA=681)
            int iopFileOff =
                firstDataLba * SEC;

            int patchOff1 =
                iopFileOff + IOP_PATCH_OFF_1;
            int patchOff2 =
                iopFileOff + IOP_PATCH_OFF_2;

            if (patchOff1 >= data.Length ||
                patchOff2 >= data.Length)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  WARNING: IOP patch" +
                    " offsets out of range!");
                Console.ResetColor();
                return;
            }

            byte before1 = data[patchOff1];
            byte before2 = data[patchOff2];

            if (toDvd)
            {
                data[patchOff1] =
                    IOP_DVD_BYTE_1;
                data[patchOff2] =
                    IOP_DVD_BYTE_2;

                Console.WriteLine(
                    $"  IOP patch: DVD mode");
                Console.WriteLine(
                    $"    +0x{IOP_PATCH_OFF_1:X}:" +
                    $" 0x{before1:X2}" +
                    $" → 0x{IOP_DVD_BYTE_1:X2}");
                Console.WriteLine(
                    $"    +0x{IOP_PATCH_OFF_2:X}:" +
                    $" 0x{before2:X2}" +
                    $" → 0x{IOP_DVD_BYTE_2:X2}");
            }
            else
            {
                data[patchOff1] =
                    IOP_CD_BYTE_1;
                data[patchOff2] =
                    IOP_CD_BYTE_2;

                Console.WriteLine(
                    $"  IOP patch: CD mode");
                Console.WriteLine(
                    $"    +0x{IOP_PATCH_OFF_1:X}:" +
                    $" 0x{before1:X2}" +
                    $" → 0x{IOP_CD_BYTE_1:X2}");
                Console.WriteLine(
                    $"    +0x{IOP_PATCH_OFF_2:X}:" +
                    $" 0x{before2:X2}" +
                    $" → 0x{IOP_CD_BYTE_2:X2}");
            }
        }

        // ═══════════════════════════════════════
        // CONVERT ISO ↔ BIN/CUE
        // Auto-detects direction based on
        // output extension:
        //   .iso → creates ISO (2048/sector)
        //   .bin → creates BIN + CUE (2352)
        // Runs fixiso after conversion
        // (only for .iso output)
        // ═══════════════════════════════════════
        public static void ConvertIsoBin(
            string inputPath,
            string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                TextOut.PrintError(
                    "File not found: " +
                    inputPath);
                return;
            }

            // ─── Force output extension
            //     to lowercase ─────────────
            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        outputPath));
            if (string.IsNullOrEmpty(outDir))
                outDir = Directory
                    .GetCurrentDirectory();
            string outName =
                Path.GetFileNameWithoutExtension(
                    outputPath);
            string outExtLower =
                Path.GetExtension(outputPath)
                    .ToLower();
            outputPath = Path.Combine(
                outDir,
                outName + outExtLower);

            bool outIsBin =
                outExtLower == ".bin" ||
                outExtLower == ".raw";
            bool outIsIso =
                outExtLower == ".iso" ||
                outExtLower == ".img";

            if (!outIsBin && !outIsIso)
            {
                TextOut.PrintError(
                    "Output extension must" +
                    " be .iso or .bin!");
                return;
            }

            // ─── Print banner ────────────
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                outIsBin
                ? " Convert to BIN+CUE"
                : " Convert to ISO");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Input:  " + inputPath);
            Console.WriteLine(
                "  Output: " + outputPath);
            Console.ResetColor();
            Console.WriteLine();

            // ─── Load and detect input ──
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "Loading input file...");
            Console.ResetColor();

            byte[] input =
                File.ReadAllBytes(inputPath);

            int inSectorSize;
            int inDataOff;
            DetectFormat(input,
                out inSectorSize,
                out inDataOff);

            string inFmt =
                inSectorSize == SEC
                ? "ISO (2048)"
                : $"BIN/RAW " +
                  $"({inSectorSize}, " +
                  $"data+{inDataOff})";
            Console.WriteLine(
                "  Input format: " + inFmt);

            // ─── Extract 2048 sectors ───
            byte[] iso2048;
            if (inSectorSize == SEC)
            {
                iso2048 = input;
            }
            else
            {
                Console.WriteLine(
                    "  Extracting 2048-byte" +
                    " sectors from BIN...");
                iso2048 = ExtractIsoFromBin(
                    input, inSectorSize,
                    inDataOff);
            }

            Console.WriteLine();

            // ─── Check CD vs DVD ────────
            // convertiso only supports
            // CD format
            bool inputIsDvd =
                IsDvdIso(iso2048);
            bool inputIsCd =
                IsCdIso(iso2048);

            if (inputIsDvd)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Blue;
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.WriteLine(
                    " WARNING: DVD ISO" +
                    " DETECTED!");
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.WriteLine(
                    "  The input file is a" +
                    " HMSTH DVD ISO.");
                Console.WriteLine(
                    "  convertiso only" +
                    " supports CD format!");
                Console.WriteLine();
                Console.WriteLine(
                    "  To convert a DVD to" +
                    " CD first, use:");
                Console.WriteLine(
                    "    tocd <dvd.iso>" +
                    " <cd.iso>");
                Console.WriteLine();
                Console.WriteLine(
                    "  Then run convertiso" +
                    " on the CD output.");
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            if (!inputIsCd)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Blue;
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.WriteLine(
                    " WARNING: NOT A VALID" +
                    " HMSTH CD ISO!");
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.WriteLine(
                    "  The input file is" +
                    " not a HMSTH CD ISO.");
                Console.WriteLine(
                    "  convertiso only" +
                    " supports valid HMSTH" +
                    " CD images.");
                Console.WriteLine(
                    "═══════════════════" +
                    "═══════════════════");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Verified: valid" +
                " HMSTH CD ISO");
            Console.ResetColor();

            // ─── Detect version for
            //     fixiso later ──────────
            GameVersion gv =
                GameVersion.USA;
            int elfSec =
                FindMainElfSector(iso2048);
            if (elfSec >= 0)
            {
                gv = DetectVersion(
                    iso2048, elfSec * SEC);
                Console.ForegroundColor =
                    ConsoleColor.DarkGray;
                Console.WriteLine(
                    "  Detected version: " +
                    GetVersionName(gv));
                Console.ResetColor();
            }

            // ─── Write output ───────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "Writing output...");
            Console.ResetColor();

            if (outIsBin)
            {
                Console.WriteLine(
                    "  Format: BIN/RAW (2352)");
                int numSec =
                    iso2048.Length / SEC;
                long binSize =
                    (long)numSec * 2352;
                Console.WriteLine(
                    $"  Size: " +
                    $"{binSize:N0}" +
                    $" bytes" +
                    $" ({numSec:N0}" +
                    " sectors)");

                // Stream directly to file
                ConvertIsoToBinStream(
                    iso2048, outputPath);

                // Write .cue file
                string cuePath =
                            Path.ChangeExtension(
                        outputPath, ".cue");
                string cueContent =
                    "FILE \"" +
                    Path.GetFileName(outputPath) +
                    "\" BINARY\r\n" +
                    "  TRACK 01 MODE1/2352\r\n" +
                    "    INDEX 01 00:00:00\r\n";
                File.WriteAllText(
                    cuePath, cueContent);

                Console.WriteLine(
                    "  Also created: " +
                    Path.GetFileName(cuePath));
            }
            else
            {
                Console.WriteLine(
                    "  Format: ISO (2048)");
                Console.WriteLine(
                    $"  Size: " +
                    $"{iso2048.Length:N0}" +
                    $" bytes" +
                    $" ({iso2048.Length / SEC:N0}" +
                    " sectors)");

                File.WriteAllBytes(
                    outputPath, iso2048);
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                outIsBin
                ? " BIN/CUE created!"
                : " ISO created!");
            Console.WriteLine(
                $" {outputPath}");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();

            // ─── Auto-fix ───────────────
            // Runs on both ISO and BIN
            // For BIN: fix the ISO first,
            // then reconvert to BIN
            bool isDvdOutput =
                IsDvdIso(iso2048);

            if (outIsIso)
            {
                // Direct fixiso on ISO
                RunAutoFix(outputPath, gv,
                    isDvdOutput,
                    skipLba: false);
            }
            else
            {
                // For BIN: create temp ISO,
                // fix it, convert back to BIN
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "Running auto-fix on" +
                    " BIN output...");
                Console.WriteLine(
                    "  (Creating temp ISO," +
                    " fixing, reconverting" +
                    " to BIN)");
                Console.ResetColor();

                string tempIso =
                    outputPath + ".tmp.iso";

                try
                {
                    // Write ISO 2048 data
                    // to temp file
                    File.WriteAllBytes(
                        tempIso, iso2048);

                    // Free the iso2048 buffer
                    iso2048 = null;
                    GC.Collect();

                    // Run fixiso on temp
                    RunAutoFix(tempIso, gv,
                        isDvdOutput,
                        skipLba: false);

                    // Read fixed ISO back
                    byte[] fixedIso =
                        File.ReadAllBytes(
                            tempIso);

                    // Convert to BIN
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "  Reconverting fixed" +
                        " ISO back to BIN...");
                    Console.ResetColor();

                    // Stream directly
                    ConvertIsoToBinStream(
                        fixedIso, outputPath);

                    // Delete temp
                    if (File.Exists(tempIso))
                        File.Delete(tempIso);

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "  Fixed BIN saved!");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  BIN auto-fix" +
                        " warning: " +
                        ex.Message);
                    Console.ResetColor();

                    // Cleanup temp on error
                    try
                    {
                        if (File.Exists(tempIso))
                            File.Delete(tempIso);
                    }
                    catch { }
                }

                Console.WriteLine();
            }
        }
    }
}
