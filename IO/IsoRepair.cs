using System;
using System.IO;
using System.Text;

namespace HMSTHModdingTool
{
    /// <summary>
    /// ISO Repair — CORRECTED
    ///
    /// Detects if "ISO" file is actually
    /// raw BIN (2352-byte sectors) and
    /// auto-converts to true ISO (2048).
    ///
    /// This is the ACTUAL fix needed for
    /// most "broken" PS2 ISOs — they are
    /// really BIN files in disguise.
    /// </summary>
    public static class IsoRepair
    {
        const int SECTOR_2048 = 2048;
        const int SECTOR_2352 = 2352;
        const int SECTOR_2336 = 2336;
        const int RAW_DATA_OFF = 24;

        // ═══════════════════════════════
        // MAIN FIX
        // ═══════════════════════════════
        public static void FixIso(
            string isoPath)
        {
            if (!File.Exists(isoPath))
                throw new FileNotFoundException(
                    "ISO not found",
                    isoPath);

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═════════════════════" +
                "════════════════════");
            Console.WriteLine(
                " ISO Auto-Repair" +
                " (BIN→ISO detection)");
            Console.WriteLine(
                "═════════════════════" +
                "════════════════════");
            Console.ResetColor();

            long fileSize =
                new FileInfo(isoPath)
                    .Length;

            int detectedSize =
                DetectSectorSize(
                    isoPath, fileSize);

            Console.WriteLine(
                "  File size:   " +
                fileSize.ToString("N0") +
                " bytes");
            Console.WriteLine(
                "  Detected as: " +
                detectedSize +
                "-byte sectors");

            // ─── Verify PVD signature
            //     at correct location
            bool pvdOk = VerifyPvd(
                isoPath, detectedSize);

            Console.WriteLine(
                "  PVD signature: " +
                (pvdOk
                    ? "OK"
                    : "MISSING"));

            if (detectedSize ==
                SECTOR_2048 && pvdOk)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine(
                    "  ISO is already" +
                    " in correct" +
                    " 2048-byte format.");
                Console.WriteLine(
                    "  No fix needed!");
                Console.ResetColor();
                return;
            }

            if (detectedSize ==
                SECTOR_2352)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine();
                Console.WriteLine(
                    "  This file is" +
                    " actually a BIN" +
                    " (2352-byte)!");
                Console.WriteLine(
                    "  Converting" +
                    " in-place to true" +
                    " 2048-byte ISO...");
                Console.ResetColor();

                ConvertInPlace2352to2048(
                    isoPath);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine();
                Console.WriteLine(
                    "  ISO fixed" +
                    " successfully!");
                Console.ResetColor();
                return;
            }

            if (detectedSize ==
                SECTOR_2336)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine();
                Console.WriteLine(
                    "  Mode 2/2336" +
                    " detected." +
                    " Converting...");
                Console.ResetColor();

                ConvertInPlace2336to2048(
                    isoPath);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  ISO fixed!");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine(
                "  Unknown format." +
                " Cannot auto-repair.");
            Console.ResetColor();
        }

        // ═══════════════════════════════
        // DETECT SECTOR SIZE
        // (checks CD sync pattern +
        //  looks for CD001 at LBA 16)
        // ═══════════════════════════════
        static int DetectSectorSize(
            string path, long fileSize)
        {
            // CD sync pattern:
            // 00 FF FF FF FF FF FF FF
            // FF FF FF 00
            byte[] sync = new byte[12];
            using (var fs =
                File.OpenRead(path))
                fs.Read(sync, 0, 12);

            bool syncOk =
                sync[0] == 0x00 &&
                sync[1] == 0xFF &&
                sync[2] == 0xFF &&
                sync[3] == 0xFF &&
                sync[4] == 0xFF &&
                sync[5] == 0xFF &&
                sync[6] == 0xFF &&
                sync[7] == 0xFF &&
                sync[8] == 0xFF &&
                sync[9] == 0xFF &&
                sync[10] == 0xFF &&
                sync[11] == 0x00;

            if (syncOk)
                return SECTOR_2352;

            // Test: is "CD001" at
            // offset 16*2048 + 1?
            if (fileSize >=
                16 * SECTOR_2048 + 6)
            {
                byte[] test = new byte[5];
                using (var fs =
                    File.OpenRead(path))
                {
                    fs.Position =
                        16 * SECTOR_2048
                        + 1;
                    fs.Read(test, 0, 5);
                }

                if (test[0] == 0x43 &&
                    test[1] == 0x44 &&
                    test[2] == 0x30 &&
                    test[3] == 0x30 &&
                    test[4] == 0x31)
                    return SECTOR_2048;
            }

            // Test: CD001 at
            // offset 16*2352 + 24 + 1?
            if (fileSize >=
                16 * SECTOR_2352 +
                RAW_DATA_OFF + 6)
            {
                byte[] test = new byte[5];
                using (var fs =
                    File.OpenRead(path))
                {
                    fs.Position =
                        16 * SECTOR_2352
                        + RAW_DATA_OFF
                        + 1;
                    fs.Read(test, 0, 5);
                }

                if (test[0] == 0x43 &&
                    test[1] == 0x44 &&
                    test[2] == 0x30 &&
                    test[3] == 0x30 &&
                    test[4] == 0x31)
                    return SECTOR_2352;
            }

            // Fallback: divisibility
            if (fileSize %
                SECTOR_2352 == 0)
                return SECTOR_2352;
            if (fileSize %
                SECTOR_2336 == 0 &&
                fileSize %
                SECTOR_2048 != 0)
                return SECTOR_2336;

            return SECTOR_2048;
        }

        // ═══════════════════════════════
        // VERIFY PVD (CD001 at LBA 16)
        // ═══════════════════════════════
        static bool VerifyPvd(
            string path, int sectorSize)
        {
            int dataOff = 0;
            if (sectorSize == SECTOR_2352)
                dataOff = RAW_DATA_OFF;
            else if (sectorSize ==
                     SECTOR_2336)
                dataOff = 8;

            long pvdPos =
                (long)16 * sectorSize +
                dataOff;

            long fileSize =
                new FileInfo(path).Length;

            if (pvdPos + 6 > fileSize)
                return false;

            byte[] check = new byte[6];
            using (var fs =
                File.OpenRead(path))
            {
                fs.Position = pvdPos;
                fs.Read(check, 0, 6);
            }

            // Type 01 + "CD001"
            return
                check[0] == 0x01 &&
                check[1] == 0x43 &&
                check[2] == 0x44 &&
                check[3] == 0x30 &&
                check[4] == 0x30 &&
                check[5] == 0x31;
        }

        // ═══════════════════════════════
        // CONVERT 2352 → 2048 IN-PLACE
        // ═══════════════════════════════
        static void ConvertInPlace2352to2048(
            string path)
        {
            string tempPath =
                path + ".tmp";

            long fileSize =
                new FileInfo(path).Length;
            long totalSectors =
                fileSize / SECTOR_2352;

            Console.WriteLine(
                "  Sectors: " +
                totalSectors);

            byte[] raw =
                new byte[SECTOR_2352];
            byte[] data =
                new byte[SECTOR_2048];

            using (var src =
                File.OpenRead(path))
            using (var dst =
                File.Create(tempPath))
            {
                long report = 0;
                for (long s = 0;
                     s < totalSectors;
                     s++)
                {
                    src.Read(
                        raw, 0,
                        SECTOR_2352);

                    // Extract user data
                    // from offset 24
                    Array.Copy(
                        raw,
                        RAW_DATA_OFF,
                        data,
                        0,
                        SECTOR_2048);

                    dst.Write(
                        data, 0,
                        SECTOR_2048);

                    if (s - report >=
                        10000)
                    {
                        report = s;
                        double pct =
                            (double)s /
                            totalSectors *
                            100.0;
                        Console.Write(
                            "\r  " +
                            "Progress: " +
                            "{0:F1}%",
                            pct);
                    }
                }
            }

            Console.Write(
                "\r  Progress: " +
                "100.0%   \n");

            // Replace original file
            File.Delete(path);
            File.Move(tempPath, path);
        }

        // ═══════════════════════════════
        // CONVERT 2336 → 2048 IN-PLACE
        // ═══════════════════════════════
        static void ConvertInPlace2336to2048(
            string path)
        {
            string tempPath =
                path + ".tmp";

            long fileSize =
                new FileInfo(path).Length;
            long totalSectors =
                fileSize / SECTOR_2336;

            byte[] raw =
                new byte[SECTOR_2336];
            byte[] data =
                new byte[SECTOR_2048];

            using (var src =
                File.OpenRead(path))
            using (var dst =
                File.Create(tempPath))
            {
                for (long s = 0;
                     s < totalSectors;
                     s++)
                {
                    src.Read(
                        raw, 0,
                        SECTOR_2336);

                    // Data offset 8
                    Array.Copy(
                        raw, 8,
                        data, 0,
                        SECTOR_2048);

                    dst.Write(
                        data, 0,
                        SECTOR_2048);
                }
            }

            File.Delete(path);
            File.Move(tempPath, path);
        }
    }
}
