using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    /// Fakes the year on all
    /// files (and PVD) in an
    /// ISO/BIN, but only if
    /// original year > 2001.
    /// Keeps month/day/time
    /// unchanged.
    /// </summary>
    public static class IsoFakeYear
    {
        const int SECTOR_2048 =
            2048;
        const int SECTOR_2352 =
            2352;
        const int DATA_OFF_2352 =
            24;
        const int PVD_LBA = 16;

        static int _sectorSize;
        static int _dataOff;

        public static void Run(
            string isoPath,
            int fakeYear)
        {
            if (!File.Exists(
                    isoPath))
                throw new
                    FileNotFoundException(
                    "File not found",
                    isoPath);

            if (fakeYear < 1900 ||
                fakeYear > 2155)
            {
                Console.ForegroundColor
                    = ConsoleColor
                        .Yellow;
                Console.WriteLine(
                    "  ERROR: Year" +
                    " must be" +
                    " between 1900" +
                    " and 2155");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor
                = ConsoleColor.Cyan;
            Console.WriteLine(
                "═══════════════" +
                "═══════════════" +
                "════════");
            Console.WriteLine(
                " fakeyear " +
                fakeYear);
            Console.WriteLine(
                "═══════════════" +
                "═══════════════" +
                "════════");
            Console.ResetColor();

            DetectFormat(isoPath);

            Console.WriteLine(
                "  Format: " +
                _sectorSize +
                "-byte sectors");
            Console.WriteLine(
                "  Data offset:" +
                " " + _dataOff);
            Console.WriteLine(
                "  Target year:" +
                " " + fakeYear);
            Console.WriteLine(
                "  Rule: Only" +
                " changes if" +
                " year > 2001");
            Console.WriteLine();

            int totalChanged = 0;
            int totalScanned = 0;

            // Save original OS
            // file timestamps
            DateTime origCreated =
                File.GetCreationTime(
                    isoPath);
            DateTime origModified =
                File.GetLastWriteTime(
                    isoPath);
            DateTime origAccessed =
                File.GetLastAccessTime(
                    isoPath);

            using (var fs =
                new FileStream(
                    isoPath,
                    FileMode.Open,
                    FileAccess
                        .ReadWrite))
            {
                Console.WriteLine(
                    "[1/2] Patching" +
                    " PVD dates" +
                    " (sector 16)" +
                    "...");

                int pvdChanged =
                    PatchPvdDates(
                        fs,
                        fakeYear);

                Console.WriteLine(
                    "  PVD dates" +
                    " changed: " +
                    pvdChanged +
                    "/4");

                totalChanged +=
                    pvdChanged;

                Console.WriteLine();
                Console.WriteLine(
                    "[2/2] Patching" +
                    " file entries" +
                    "...");

                int fileChanged =
                    PatchAllFiles(
                        fs,
                        fakeYear,
                        out totalScanned);

                Console.WriteLine(
                    "  Files" +
                    " scanned: " +
                    totalScanned);
                Console.WriteLine(
                    "  Files" +
                    " changed: " +
                    fileChanged);

                totalChanged +=
                    fileChanged;
            }

            // Fix Windows file
            // timestamps if they
            // have year > 2001
            Console.WriteLine();
            Console.WriteLine(
                "[3/3] Fixing OS" +
                " file timestamps" +
                "...");

            int osChanged =
                FixOsTimestamps(
                    isoPath,
                    origCreated,
                    origModified,
                    origAccessed,
                    fakeYear);

            Console.WriteLine(
                "  OS timestamps" +
                " changed: " +
                osChanged + "/3");

            totalChanged +=
                osChanged;

            Console.WriteLine();
            Console.ForegroundColor
                = ConsoleColor.Green;
            Console.WriteLine(
                "  Done! Total" +
                " entries" +
                " changed: " +
                totalChanged);
            Console.ResetColor();
        }

        // ═══════════════════════
        // FORMAT DETECTION
        // ═══════════════════════
        static void DetectFormat(
            string path)
        {
            byte[] sync =
                new byte[12];
            using (var fs =
                File.OpenRead(path))
                fs.Read(sync, 0, 12);

            if (sync[0] == 0x00 &&
                sync[1] == 0xFF &&
                sync[11] == 0x00)
            {
                _sectorSize =
                    SECTOR_2352;
                _dataOff =
                    DATA_OFF_2352;
            }
            else
            {
                _sectorSize =
                    SECTOR_2048;
                _dataOff = 0;
            }
        }

        static long
        GetSectorDataPos(int lba)
        {
            return (long)lba *
                _sectorSize +
                _dataOff;
        }

        // ═══════════════════════
        // PVD DATE PATCHING
        //
        // PVD in ISO 9660 has
        // 4 dates in ASCII
        // format at offsets:
        // 813 - Creation
        // 830 - Modification
        // 847 - Expiration
        // 864 - Effective
        //
        // Each is 17 bytes:
        // YYYYMMDDHHMMSSCC + GMT
        // ═══════════════════════
        static int PatchPvdDates(
            FileStream fs,
            int fakeYear)
        {
            long pvdPos =
                GetSectorDataPos(
                    PVD_LBA);

            byte[] pvd =
                new byte[2048];
            fs.Position = pvdPos;
            fs.Read(pvd, 0,
                    2048);

            // First verify this
            // is actually a PVD
            if (pvd[0] != 0x01 ||
                pvd[1] != 0x43 ||
                pvd[2] != 0x44 ||
                pvd[3] != 0x30 ||
                pvd[4] != 0x30 ||
                pvd[5] != 0x31)
            {
                Console.ForegroundColor
                    = ConsoleColor
                        .Yellow;
                Console.WriteLine(
                    "  WARNING: PVD" +
                    " signature not" +
                    " found at" +
                    " sector 16!");
                Console.ResetColor();
                return 0;
            }

            int changed = 0;

            int[] dateOffsets =
                new int[] {
                    813, 830,
                    847, 864
                };
            string[] labels =
                new string[] {
                    "Creation",
                    "Modification",
                    "Expiration",
                    "Effective"
                };

            for (int i = 0;
                 i < 4; i++)
            {
                if (PatchPvdDate(
                        pvd,
                        dateOffsets[
                            i],
                        fakeYear,
                        labels[i]))
                    changed++;
            }

            // Always write back
            // so any changes
            // are persisted
            if (changed > 0)
            {
                fs.Position =
                    pvdPos;
                fs.Write(pvd, 0,
                         2048);
                fs.Flush();
            }

            return changed;
        }

        static bool
        PatchPvdDate(
            byte[] pvd,
            int offset,
            int fakeYear,
            string label)
        {
            // Show raw bytes
            // for debugging
            string rawDate =
                Encoding.ASCII
                    .GetString(
                        pvd,
                        offset,
                        16);

            // Check if all
            // zeros (0x00) or
            // all '0' (0x30)
            // which means
            // "no date set"
            bool allZeros = true;
            for (int i = 0;
                 i < 16; i++)
            {
                byte b =
                    pvd[offset
                        + i];
                if (b != 0x30 &&
                    b != 0x00)
                {
                    allZeros =
                        false;
                    break;
                }
            }

            if (allZeros)
            {
                Console.WriteLine(
                    "  [" + label +
                    "] date is" +
                    " empty," +
                    " skipping");
                return false;
            }

            // Parse year (first
            // 4 ASCII digits)
            char c0 = (char)
                pvd[offset];
            char c1 = (char)
                pvd[offset + 1];
            char c2 = (char)
                pvd[offset + 2];
            char c3 = (char)
                pvd[offset + 3];

            if (!char.IsDigit(c0)
                || !char.IsDigit(
                    c1)
                || !char.IsDigit(
                    c2)
                || !char.IsDigit(
                    c3))
            {
                Console.WriteLine(
                    "  [" + label +
                    "] non-digit" +
                    " year bytes" +
                    " (raw: " +
                    rawDate.Substring(
                        0, 4) +
                    "), skipping");
                return false;
            }

            int origYear =
                (c0 - '0') * 1000
                + (c1 - '0') * 100
                + (c2 - '0') * 10
                + (c3 - '0');

            if (origYear <= 2001)
            {
                Console.WriteLine(
                    "  [" + label +
                    "] year " +
                    origYear +
                    " <= 2001," +
                    " skipping");
                return false;
            }

            // Write new year
            string yearStr =
                fakeYear.ToString(
                    "0000");
            pvd[offset] = (byte)
                yearStr[0];
            pvd[offset + 1] =
                (byte)yearStr[1];
            pvd[offset + 2] =
                (byte)yearStr[2];
            pvd[offset + 3] =
                (byte)yearStr[3];

            Console.WriteLine(
                "  [" + label +
                "] " + origYear +
                " -> " + fakeYear +
                " (kept " +
                rawDate.Substring(
                    4, 12) + ")");

            return true;
        }

        // ═══════════════════════
        // FILE ENTRY PATCHING
        // ═══════════════════════
        static int PatchAllFiles(
            FileStream fs,
            int fakeYear,
            out int totalScanned)
        {
            totalScanned = 0;
            int totalChanged = 0;

            long pvdPos =
                GetSectorDataPos(
                    PVD_LBA);
            byte[] pvd =
                new byte[2048];
            fs.Position = pvdPos;
            fs.Read(pvd, 0,
                    2048);

            uint rootLba =
                BitConverter
                    .ToUInt32(
                        pvd, 158);
            uint rootSize =
                BitConverter
                    .ToUInt32(
                        pvd, 166);

            var visited =
                new HashSet<uint>();

            PatchDirectory(
                fs,
                rootLba,
                rootSize,
                fakeYear,
                visited,
                ref totalScanned,
                ref totalChanged,
                0);

            return totalChanged;
        }

        static void
        PatchDirectory(
            FileStream fs,
            uint dirLba,
            uint dirSize,
            int fakeYear,
            HashSet<uint>
                visited,
            ref int scanned,
            ref int changed,
            int depth)
        {
            if (depth > 20)
                return;
            if (visited.Contains(
                    dirLba))
                return;
            visited.Add(dirLba);

            int numSectors =
                (int)((dirSize +
                       2047) /
                      2048);

            byte[] dirData =
                new byte[
                    numSectors *
                    2048];

            for (int s = 0;
                 s < numSectors;
                 s++)
            {
                long secPos =
                    GetSectorDataPos(
                        (int)dirLba
                        + s);
                fs.Position =
                    secPos;
                fs.Read(dirData,
                        s * 2048,
                        2048);
            }

            var subDirs = new
                List<(uint lba,
                      uint size)>
                ();

            int pos = 0;
            bool dirChanged =
                false;

            while (pos <
                   dirSize)
            {
                int recLen =
                    dirData[pos];

                if (recLen == 0)
                {
                    pos =
                        ((pos /
                          2048) + 1)
                        * 2048;
                    if (pos >=
                        dirSize)
                        break;
                    continue;
                }

                if (recLen < 33)
                    break;

                int datePos =
                    pos + 18;

                byte yearSince1900
                    = dirData[
                        datePos];

                if (yearSince1900
                    != 0)
                {
                    int actualYear
                        = 1900 +
                          yearSince1900;

                    scanned++;

                    if (actualYear
                        > 2001)
                    {
                        int newYearVal
                            = fakeYear
                              - 1900;

                        if (newYearVal
                            >= 0 &&
                            newYearVal
                            <= 255)
                        {
                            dirData[
                                datePos]
                            = (byte)
                              newYearVal;
                            changed++;
                            dirChanged
                                = true;
                        }
                    }
                }

                uint eLba =
                    BitConverter
                        .ToUInt32(
                            dirData,
                            pos + 2);
                uint eSize =
                    BitConverter
                        .ToUInt32(
                            dirData,
                            pos + 10);
                byte flags =
                    dirData[
                        pos + 25];
                int nlen =
                    dirData[
                        pos + 32];

                bool isDir =
                    (flags & 0x02)
                    != 0;
                bool isDot =
                    (nlen == 1 &&
                     (dirData[
                         pos + 33]
                      == 0x00 ||
                      dirData[
                          pos + 33]
                      == 0x01));

                if (isDir &&
                    !isDot &&
                    eLba > 0)
                {
                    subDirs.Add(
                        (eLba,
                         eSize));
                }

                pos += recLen;
            }

            // Only write back
            // if we changed
            // something
            if (dirChanged)
            {
                for (int s = 0;
                     s < numSectors;
                     s++)
                {
                    long secPos =
                        GetSectorDataPos(
                            (int)dirLba
                            + s);
                    fs.Position =
                        secPos;
                    fs.Write(
                        dirData,
                        s * 2048,
                        2048);
                }
                fs.Flush();
            }

            foreach (var sd
                     in subDirs)
            {
                PatchDirectory(
                    fs,
                    sd.lba,
                    sd.size,
                    fakeYear,
                    visited,
                    ref scanned,
                    ref changed,
                    depth + 1);
            }
        }

        // ═══════════════════════
        // FIX OS TIMESTAMPS
        // Changes Windows file
        // dates ONLY if year
        // > 2001 (same rule as
        // inside the ISO)
        // ═══════════════════════
        static int FixOsTimestamps(
            string path,
            DateTime origCreated,
            DateTime origModified,
            DateTime origAccessed,
            int fakeYear)
        {
            int changed = 0;

            try
            {
                // Creation time
                if (origCreated.Year
                    > 2001)
                {
                    DateTime newTime =
                        new DateTime(
                            fakeYear,
                            origCreated
                                .Month,
                            origCreated
                                .Day,
                            origCreated
                                .Hour,
                            origCreated
                                .Minute,
                            origCreated
                                .Second);
                    File.SetCreationTime(
                        path,
                        newTime);
                    Console.WriteLine(
                        "  [Created]" +
                        " " +
                        origCreated.Year +
                        " -> " +
                        fakeYear);
                    changed++;
                }
                else
                {
                    Console.WriteLine(
                        "  [Created]" +
                        " year " +
                        origCreated.Year +
                        " <= 2001," +
                        " skipping");
                }

                // Modified time
                if (origModified.Year
                    > 2001)
                {
                    DateTime newTime =
                        new DateTime(
                            fakeYear,
                            origModified
                                .Month,
                            origModified
                                .Day,
                            origModified
                                .Hour,
                            origModified
                                .Minute,
                            origModified
                                .Second);
                    File.SetLastWriteTime(
                        path,
                        newTime);
                    Console.WriteLine(
                        "  [Modified]" +
                        " " +
                        origModified
                            .Year +
                        " -> " +
                        fakeYear);
                    changed++;
                }
                else
                {
                    Console.WriteLine(
                        "  [Modified]" +
                        " year " +
                        origModified
                            .Year +
                        " <= 2001," +
                        " skipping");
                }

                // Accessed time
                if (origAccessed.Year
                    > 2001)
                {
                    DateTime newTime =
                        new DateTime(
                            fakeYear,
                            origAccessed
                                .Month,
                            origAccessed
                                .Day,
                            origAccessed
                                .Hour,
                            origAccessed
                                .Minute,
                            origAccessed
                                .Second);
                    File
                        .SetLastAccessTime(
                            path,
                            newTime);
                    Console.WriteLine(
                        "  [Accessed]" +
                        " " +
                        origAccessed
                            .Year +
                        " -> " +
                        fakeYear);
                    changed++;
                }
                else
                {
                    Console.WriteLine(
                        "  [Accessed]" +
                        " year " +
                        origAccessed
                            .Year +
                        " <= 2001," +
                        " skipping");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor
                    = ConsoleColor
                        .Yellow;
                Console.WriteLine(
                    "  Warning: Could" +
                    " not set OS" +
                    " timestamps: " +
                    ex.Message);
                Console.ResetColor();
            }

            return changed;
        }
    }
}
