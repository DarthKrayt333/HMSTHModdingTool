using System;
using System.IO;
using System.Text;

namespace HMSTHModdingTool
{
    /// <summary>
    /// Converts between BIN/CUE and ISO
    /// formats. Handles 2352-byte raw
    /// sectors to 2048-byte ISO sectors
    /// and vice versa.
    /// </summary>
    public static class IsoConverter
    {
        const int SECTOR_2048 = 2048;
        const int SECTOR_2352 = 2352;
        const int SECTOR_2336 = 2336;
        const int RAW_DATA_OFFSET = 24;
        const int SUB_DATA_OFFSET = 8;

        // CD-ROM sync pattern
        static readonly byte[] SYNC =
        {
            0x00, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0x00
        };

        // ═══════════════════════════════
        // MAIN CONVERT
        // ═══════════════════════════════
        public static void Convert(
            string inputPath,
            string outputPath)
        {
            if (!File.Exists(inputPath))
            {
                // Check for .cue file
                string ext =
                    Path.GetExtension(
                        inputPath)
                        .ToLower();

                if (ext == ".cue")
                {
                    string binPath =
                        FindBinFromCue(
                            inputPath);

                    if (binPath != null &&
                        File.Exists(binPath))
                    {
                        inputPath = binPath;
                    }
                    else
                    {
                        throw
                            new FileNotFoundException(
                            "BIN file not" +
                            " found from CUE",
                            inputPath);
                    }
                }
                else
                {
                    throw
                        new FileNotFoundException(
                        "Input not found",
                        inputPath);
                }
            }

            long fileSize =
                new FileInfo(inputPath)
                    .Length;

            string outExt =
                Path.GetExtension(
                    outputPath)
                    .ToLower();

            int srcSector =
                DetectSectorSize(
                    inputPath, fileSize);

            int dstSector;
            if (outExt == ".bin")
                dstSector = SECTOR_2352;
            else
                dstSector = SECTOR_2048;

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "Converting: " +
                srcSector + " → " +
                dstSector);
            Console.ResetColor();

            if (srcSector == dstSector)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Same format!" +
                    " Copying...");
                Console.ResetColor();

                File.Copy(
                    inputPath,
                    outputPath,
                    true);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Done!");
                Console.ResetColor();
                return;
            }

            long totalSectors =
                fileSize / srcSector;

            using (var src =
                File.OpenRead(inputPath))
            using (var dst =
                File.Create(outputPath))
            {
                byte[] srcBuf =
                    new byte[srcSector];
                byte[] dstBuf =
                    new byte[dstSector];

                for (long s = 0;
                     s < totalSectors;
                     s++)
                {
                    src.Read(
                        srcBuf, 0,
                        srcSector);

                    if (srcSector ==
                        SECTOR_2352 &&
                        dstSector ==
                        SECTOR_2048)
                    {
                        // 2352 → 2048
                        // Extract data
                        // portion
                        Array.Copy(
                            srcBuf,
                            RAW_DATA_OFFSET,
                            dstBuf,
                            0,
                            SECTOR_2048);
                    }
                    else if (
                        srcSector ==
                        SECTOR_2336 &&
                        dstSector ==
                        SECTOR_2048)
                    {
                        // 2336 → 2048
                        Array.Copy(
                            srcBuf,
                            SUB_DATA_OFFSET,
                            dstBuf,
                            0,
                            SECTOR_2048);
                    }
                    else if (
                        srcSector ==
                        SECTOR_2048 &&
                        dstSector ==
                        SECTOR_2352)
                    {
                        // 2048 → 2352
                        BuildRawSector(
                            srcBuf,
                            dstBuf,
                            (int)s);
                    }
                    else
                    {
                        // Direct copy of
                        // smaller amount
                        int copyLen =
                            Math.Min(
                                srcSector,
                                dstSector);
                        Array.Clear(
                            dstBuf, 0,
                            dstSector);
                        Array.Copy(
                            srcBuf, 0,
                            dstBuf, 0,
                            copyLen);
                    }

                    dst.Write(
                        dstBuf, 0,
                        dstSector);

                    if (s % 10000 == 0)
                    {
                        double pct =
                            (double)s /
                            totalSectors *
                            100.0;
                        Console.Write(
                            "\r  {0:F1}%",
                            pct);
                    }
                }
            }

            Console.Write(
                "\r  100.0%   \n");

            // If output is .bin, also
            // create .cue file
            if (outExt == ".bin")
            {
                string cuePath =
                    Path.ChangeExtension(
                        outputPath, ".cue");
                string binName =
                    Path.GetFileName(
                        outputPath);

                File.WriteAllText(
                    cuePath,
                    "FILE \"" + binName +
                    "\" BINARY\r\n" +
                    "  TRACK 01" +
                    " MODE2/2352\r\n" +
                    "    INDEX 01" +
                    " 00:00:00\r\n");

                Console.WriteLine(
                    "  Created: " +
                    cuePath);
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Conversion complete!" +
                " " +
                new FileInfo(outputPath)
                    .Length
                    .ToString("N0") +
                " bytes");
            Console.ResetColor();
        }

        // ═══════════════════════════════
        // DETECT SECTOR SIZE
        // ═══════════════════════════════
        static int DetectSectorSize(
            string path, long fileSize)
        {
            if (fileSize % SECTOR_2352 == 0)
            {
                byte[] hdr = new byte[16];
                using (var fs =
                    File.OpenRead(path))
                {
                    fs.Read(hdr, 0, 16);
                }

                if (hdr[0] == 0x00 &&
                    hdr[1] == 0xFF &&
                    hdr[2] == 0xFF &&
                    hdr[11] == 0x00)
                    return SECTOR_2352;
            }

            if (fileSize % SECTOR_2336 == 0
                && fileSize % SECTOR_2048
                   != 0)
                return SECTOR_2336;

            return SECTOR_2048;
        }

        // ═══════════════════════════════
        // BUILD RAW SECTOR (2352)
        // ═══════════════════════════════
        static void BuildRawSector(
            byte[] data,
            byte[] raw,
            int lba)
        {
            Array.Clear(raw, 0, 2352);

            // Sync
            Array.Copy(
                SYNC, 0, raw, 0, 12);

            // Header (MSF + mode)
            int minutes =
                (lba + 150) / (60 * 75);
            int seconds =
                ((lba + 150) / 75) % 60;
            int frames =
                (lba + 150) % 75;

            raw[12] = ToBcd(minutes);
            raw[13] = ToBcd(seconds);
            raw[14] = ToBcd(frames);
            raw[15] = 0x01; // Mode 1

            // Data at offset 24
            Array.Copy(
                data, 0,
                raw, RAW_DATA_OFFSET,
                SECTOR_2048);

            // EDC/ECC would go here
            // but most tools don't
            // need them for ISO
        }

        static byte ToBcd(int val)
        {
            return (byte)(
                ((val / 10) << 4) |
                (val % 10));
        }

        // ═══════════════════════════════
        // FIND BIN FROM CUE
        // ═══════════════════════════════
        static string FindBinFromCue(
            string cuePath)
        {
            try
            {
                string[] lines =
                    File.ReadAllLines(
                        cuePath);

                foreach (string line
                         in lines)
                {
                    string trim =
                        line.Trim()
                            .ToUpper();

                    if (trim.StartsWith(
                            "FILE"))
                    {
                        int q1 =
                            line.IndexOf(
                                '"');
                        int q2 =
                            line.IndexOf(
                                '"',
                                q1 + 1);

                        if (q1 >= 0 &&
                            q2 > q1)
                        {
                            string binName =
                                line
                                    .Substring(
                                        q1 + 1,
                                        q2 - q1
                                        - 1);

                            string dir =
                                Path
                                    .GetDirectoryName(
                                        cuePath);

                            if (string
                                .IsNullOrEmpty(
                                    dir))
                                return
                                    binName;

                            return
                                Path.Combine(
                                    dir,
                                    binName);
                        }
                    }
                }
            }
            catch
            {
            }

            // Fallback: same name .bin
            return
                Path.ChangeExtension(
                    cuePath, ".bin");
        }
    }
}
