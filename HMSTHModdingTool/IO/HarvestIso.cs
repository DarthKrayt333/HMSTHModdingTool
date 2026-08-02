using System;
using System.Collections.Generic;
using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles automated LBA table
    ///     patching in SLUS_202.51 /
    ///     SLPS_201.04 / SLPM_601.47
    ///     inside a HMSTH ISO.
    /// </summary>
    public class HarvestIso
    {
        // ─── USA: SLUS_202.51 ─────────
        private const uint
            SLUS_LBA_TABLE_START =
                0x162460;
        private const uint
            SLUS_LBA_TABLE_END =
                0x162D30;

        // ─── JAP: SLPS_201.04 ─────────
        private const uint
            SLPS_LBA_TABLE_START =
                0x162360;
        private const uint
            SLPS_LBA_TABLE_END =
                0x162C30;

        // ─── DEMO: SLPM_601.47 ────────
        private const uint
            SLPM_LBA_TABLE_START =
                0x1633E0;
        private const uint
            SLPM_LBA_TABLE_END =
                0x163CB0;

        private const int
            BYTES_PER_SECTOR = 2048;

        // ─── ISO 9660 ─────────────────
        private const int
            LOGICAL_SECTOR_SIZE = 2048;
        private const int
            ISO_PVD_LBA = 16;
        private const int
            ISO_ROOT_DIR_OFFSET = 156;

        // ─── ELF filenames ────────────
        private const string
            SLUS_FILENAME =
                @"\SLUS_202.51";
        private const string
            SLPS_FILENAME =
                @"\SLPS_201.04";
        private const string
            SLPM_FILENAME =
                @"\SLPM_601.47";

        // ═════════════════════════════
        // PUBLIC HELPERS
        // ═════════════════════════════

        public static string
            GetRealPath(string path)
        {
            if (string.IsNullOrEmpty(
                    path))
                return path;
            if (!File.Exists(path))
                return path;
            try
            {
                string dir =
                    Path.GetDirectoryName(
                        Path.GetFullPath(
                            path));
                string typed =
                    Path.GetFileName(
                        path);
                if (string
                    .IsNullOrEmpty(dir))
                    dir = Directory
                        .GetCurrentDirectory();
                string[] matches =
                    Directory.GetFiles(
                        dir, typed);
                if (matches.Length > 0)
                {
                    string realName =
                        Path.GetFileName(
                            matches[0]);
                    return Path.Combine(
                        dir, realName);
                }
            }
            catch { }
            return path;
        }

        public static string
            GetElfFilename(
                GameVersion version)
        {
            switch (version)
            {
                case GameVersion.JAP:
                    return SLPS_FILENAME;
                case GameVersion.DEMO:
                    return SLPM_FILENAME;
                default:
                    return SLUS_FILENAME;
            }
        }

        /// <summary>
        ///     Legacy overload.
        /// </summary>
        public static string
            GetElfFilename(bool isJap)
        {
            return GetElfFilename(
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        public static uint
            GetLbaTableStart(
                GameVersion version)
        {
            switch (version)
            {
                case GameVersion.JAP:
                    return
                        SLPS_LBA_TABLE_START;
                case GameVersion.DEMO:
                    return
                        SLPM_LBA_TABLE_START;
                default:
                    return
                        SLUS_LBA_TABLE_START;
            }
        }

        /// <summary>
        ///     Legacy overload.
        /// </summary>
        public static uint
            GetLbaTableStart(bool isJap)
        {
            return GetLbaTableStart(
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        public static uint
            GetLbaTableEnd(
                GameVersion version)
        {
            switch (version)
            {
                case GameVersion.JAP:
                    return
                        SLPS_LBA_TABLE_END;
                case GameVersion.DEMO:
                    return
                        SLPM_LBA_TABLE_END;
                default:
                    return
                        SLUS_LBA_TABLE_END;
            }
        }

        /// <summary>
        ///     Legacy overload.
        /// </summary>
        public static uint
            GetLbaTableEnd(bool isJap)
        {
            return GetLbaTableEnd(
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        // ═════════════════════════════
        // AUTO DETECT VERSION
        // Now returns GameVersion enum
        // ═════════════════════════════

        /// <summary>
        ///     Scans the ISO filesystem
        ///     to detect USA, JAP, or
        ///     JAP DEMO version.
        /// </summary>
        public static GameVersion
            AutoDetectGameVersion(
                string isoPath,
                out string detectedElf)
        {
            detectedElf = null;

            int raw;
            int uoff;
            DetectIsoFormat(
                isoPath,
                out raw,
                out uoff);

            var files = ScanIso(
                isoPath, raw, uoff);

            bool hasDemo =
                files.ContainsKey(
                    @"\SLPM_601.47");
            bool hasJap =
                files.ContainsKey(
                    @"\SLPS_201.04");
            bool hasUsa =
                files.ContainsKey(
                    @"\SLUS_202.51");

            // Demo takes priority
            // over JAP since DEMO
            // is also Japanese
            if (hasDemo)
            {
                detectedElf =
                    @"\SLPM_601.47";
                return GameVersion.DEMO;
            }
            else if (hasJap)
            {
                detectedElf =
                    @"\SLPS_201.04";
                return GameVersion.JAP;
            }
            else if (hasUsa)
            {
                detectedElf =
                    @"\SLUS_202.51";
                return GameVersion.USA;
            }
            else
            {
                throw new
                    InvalidDataException(
                    "Neither" +
                    " SLUS_202.51," +
                    " SLPS_201.04, nor" +
                    " SLPM_601.47 was" +
                    " found inside" +
                    " the ISO.\n" +
                    "  This does not" +
                    " appear to be a" +
                    " valid HMSTH" +
                    " disc image.");
            }
        }

        /// <summary>
        ///     Legacy overload that
        ///     returns bool for JAP.
        ///     Kept for backward compat.
        /// </summary>
        public static bool
            AutoDetectVersion(
                string isoPath,
                out string detectedElf)
        {
            GameVersion v =
                AutoDetectGameVersion(
                    isoPath,
                    out detectedElf);
            return v == GameVersion.JAP
                || v == GameVersion.DEMO;
        }

        // ═════════════════════════════
        // FIX LBA (USA - no flag)
        // ═════════════════════════════

        public static int FixLba(
            string isoPath)
        {
            return FixLba(
                isoPath,
                GameVersion.USA);
        }

        /// <summary>
        ///     Legacy bool overload.
        /// </summary>
        public static int FixLba(
            string isoPath,
            bool isJap)
        {
            return FixLba(
                isoPath,
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        // ═════════════════════════════
        // FIX LBA - MAIN METHOD
        // Now uses GameVersion enum
        // ═════════════════════════════

        public static int FixLba(
            string isoPath,
            GameVersion version)
        {
            if (!File.Exists(isoPath))
                throw new
                    FileNotFoundException(
                    "ISO file not found",
                    isoPath);

            uint lbaTableStart =
                GetLbaTableStart(
                    version);
            uint lbaTableEnd =
                GetLbaTableEnd(
                    version);
            int lbaTableSize =
                (int)(lbaTableEnd -
                      lbaTableStart);
            string elfFilename =
                GetElfFilename(
                    version);
            string versionName;
            switch (version)
            {
                case GameVersion.JAP:
                    versionName =
                        "JAP" +
                        " (SLPS_201.04)";
                    break;
                case GameVersion.DEMO:
                    versionName =
                        "JAP DEMO" +
                        " (SLPM_601.47)";
                    break;
                default:
                    versionName =
                        "USA" +
                        " (SLUS_202.51)";
                    break;
            }

            TextOut.Print(
                $"Version:" +
                $" {versionName}");
            TextOut.Print(
                $"ELF: {elfFilename}");
            TextOut.Print(
                "LBA table: 0x" +
                $"{lbaTableStart:X6}" +
                " - 0x" +
                $"{lbaTableEnd:X6}" +
                $" ({lbaTableSize}" +
                " bytes)");
            TextOut.Print(
                "Opening ISO: " +
                isoPath);

            // ── 1. Detect ISO format
            int rawSectorSize;
            int userDataOffset;
            DetectIsoFormat(
                isoPath,
                out rawSectorSize,
                out userDataOffset);
            TextOut.Print(
                "ISO format:" +
                $" raw_sector=" +
                $"{rawSectorSize}," +
                " user_data_offset=" +
                $"{userDataOffset}");

            // ── 2. Scan ISO filesystem
            var files = ScanIso(
                isoPath,
                rawSectorSize,
                userDataOffset);
            TextOut.Print(
                $"Found {files.Count}" +
                " files in ISO");

            // ── 3. Find ELF
            string elfKey =
                elfFilename.ToUpper();
            if (!files.ContainsKey(
                    elfKey))
                throw new Exception(
                    $"{elfFilename}" +
                    " not found in" +
                    " ISO");

            var elf = files[elfKey];
            TextOut.Print(
                $"{elfFilename}" +
                $" at LBA {elf.Lba}," +
                $" size {elf.Size}" +
                " bytes");

            // ── 4. Sort all files
            var sortedFiles =
                new List<
                    KeyValuePair<
                        string,
                        IsoEntry>>(
                    files);
            sortedFiles.Sort(
                (a, b) =>
                    a.Value.Lba
                    .CompareTo(
                        b.Value.Lba));

            // ── 5. Filter zero-size
            var gameFiles =
                new List<
                    KeyValuePair<
                        string,
                        IsoEntry>>();
            foreach (var kvp
                     in sortedFiles)
            {
                if (kvp.Value.Size == 0)
                    continue;
                gameFiles.Add(kvp);
            }

            TextOut.Print(
                "Game files sorted" +
                " by LBA: " +
                gameFiles.Count);

            // ── 6. Check capacity
            int maxEntries =
                lbaTableSize / 8;
            int availableSlots =
                maxEntries - 1;

            if (gameFiles.Count >
                availableSlots)
            {
                throw new
                    InvalidDataException(
                    "CRITICAL: Found " +
                    gameFiles.Count +
                    " files but LBA" +
                    " table only has" +
                    " space for " +
                    availableSlots +
                    " entries!");
            }

            TextOut.Print(
                "LBA table capacity:" +
                $" {maxEntries}" +
                " entries");

            // ── 7. First file LBA
            uint firstLba = 0;
            if (gameFiles.Count > 0)
                firstLba =
                    gameFiles[0]
                    .Value.Lba;

            // ── 8. Build new table
            byte[] newTable =
                new byte[lbaTableSize];

            WriteUInt32Le(
                newTable, 0, 0);
            WriteUInt32Le(
                newTable, 4,
                firstLba > 0
                    ? firstLba - 1
                    : 0);

            int pos = 8;
            int written = 1;
            var skippedFiles =
                new List<string>();

            foreach (var kvp
                     in gameFiles)
            {
                if (pos + 8 >
                    lbaTableSize)
                {
                    skippedFiles.Add(
                        kvp.Key +
                        " (table full)");
                    continue;
                }

                var entry = kvp.Value;
                uint sectors =
                    (entry.Size +
                     BYTES_PER_SECTOR
                     - 1) /
                    BYTES_PER_SECTOR;
                if (sectors < 1)
                    sectors = 1;
                uint lbaEnd =
                    entry.Lba +
                    sectors - 1;

                WriteUInt32Le(
                    newTable, pos,
                    entry.Lba);
                WriteUInt32Le(
                    newTable, pos + 4,
                    lbaEnd);
                pos += 8;
                written++;
            }

            if (skippedFiles.Count > 0)
            {
                throw new
                    InvalidDataException(
                    "LBA table build" +
                    " incomplete." +
                    " Aborting.");
            }

            TextOut.Print(
                $"Built LBA table:" +
                $" {written} entries");

            // ── 9. Read existing
            byte[] oldTable =
                ReadBytesAtLba(
                    isoPath,
                    rawSectorSize,
                    userDataOffset,
                    elf.Lba,
                    lbaTableStart,
                    lbaTableSize);

            int diffCount = 0;
            for (int i = 0;
                 i < lbaTableSize;
                 i += 8)
            {
                if (i + 4 >
                    lbaTableSize)
                    break;
                uint oldS =
                    ReadUInt32Le(
                        oldTable, i);
                uint oldE =
                    ReadUInt32Le(
                        oldTable,
                        i + 4);
                uint newS =
                    ReadUInt32Le(
                        newTable, i);
                uint newE =
                    ReadUInt32Le(
                        newTable,
                        i + 4);
                if (oldS != newS ||
                    oldE != newE)
                    diffCount++;
            }

            if (diffCount == 0)
            {
                TextOut.PrintSuccess(
                    "LBA table already" +
                    " correct");
                return 0;
            }

            TextOut.Print(
                $"Writing" +
                $" {diffCount}" +
                $" changed entries");

            // ── 10. Write new table
            WriteBytesAtLba(
                isoPath,
                rawSectorSize,
                userDataOffset,
                elf.Lba,
                lbaTableStart,
                newTable);

            // ── 11. Verify
            byte[] verify =
                ReadBytesAtLba(
                    isoPath,
                    rawSectorSize,
                    userDataOffset,
                    elf.Lba,
                    lbaTableStart,
                    lbaTableSize);
            for (int i = 0;
                 i < lbaTableSize;
                 i++)
            {
                if (verify[i] !=
                    newTable[i])
                    throw new Exception(
                        "Verification" +
                        " failed");
            }

            TextOut.PrintSuccess(
                "LBA table patched" +
                " - " +
                $"{diffCount}" +
                " entries updated");
            return diffCount;
        }

        // ═════════════════════════════
        // ISO 9660 HELPERS
        // (unchanged - kept as-is)
        // ═════════════════════════════

        private static void
            DetectIsoFormat(
                string path,
                out int rawSectorSize,
                out int userDataOffset)
        {
            long fsize =
                new FileInfo(path)
                    .Length;
            var candidates =
                new (int raw, int off)[]
                {
                    (2048, 0),
                    (2352, 16),
                    (2352, 24),
                    (2336, 8),
                    (2448, 16)
                };

            using (var fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
            {
                foreach (var c
                         in candidates)
                {
                    if (fsize <
                        17L * c.raw)
                        continue;
                    fs.Seek(
                        16L * c.raw +
                        c.off,
                        SeekOrigin.Begin);
                    byte[] m =
                        new byte[6];
                    fs.Read(m, 0, 6);
                    if (m[0] == 0x01 &&
                        m[1] == 'C' &&
                        m[2] == 'D' &&
                        m[3] == '0' &&
                        m[4] == '0' &&
                        m[5] == '1')
                    {
                        rawSectorSize =
                            c.raw;
                        userDataOffset =
                            c.off;
                        return;
                    }
                }
            }
            throw new Exception(
                "Not a valid" +
                " ISO 9660 image");
        }

        private static long LbaToRaw(
            uint lba,
            int rawSectorSize,
            int userDataOffset,
            uint byteOff = 0)
        {
            return
                (long)lba *
                rawSectorSize +
                userDataOffset +
                byteOff;
        }

        private static byte[]
            ReadBytesAtLba(
                string path,
                int raw,
                int uoff,
                uint lba,
                uint byteOff,
                int size)
        {
            byte[] result =
                new byte[size];
            int remaining = size;
            int resultPos = 0;
            uint curLba =
                lba +
                byteOff /
                LOGICAL_SECTOR_SIZE;
            uint curOff =
                byteOff %
                LOGICAL_SECTOR_SIZE;

            using (var fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
            {
                while (remaining > 0)
                {
                    int avail =
                        LOGICAL_SECTOR_SIZE
                        - (int)curOff;
                    int toRead =
                        Math.Min(
                            remaining,
                            avail);
                    fs.Seek(
                        LbaToRaw(
                            curLba,
                            raw, uoff,
                            curOff),
                        SeekOrigin.Begin);
                    fs.Read(
                        result,
                        resultPos,
                        toRead);
                    remaining -= toRead;
                    resultPos += toRead;
                    curLba++;
                    curOff = 0;
                }
            }
            return result;
        }

        private static void
            WriteBytesAtLba(
                string path,
                int raw,
                int uoff,
                uint lba,
                uint byteOff,
                byte[] data)
        {
            int remaining =
                data.Length;
            int dataPos = 0;
            uint curLba =
                lba +
                byteOff /
                LOGICAL_SECTOR_SIZE;
            uint curOff =
                byteOff %
                LOGICAL_SECTOR_SIZE;

            using (var fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.Read))
            {
                while (remaining > 0)
                {
                    int avail =
                        LOGICAL_SECTOR_SIZE
                        - (int)curOff;
                    int toWrite =
                        Math.Min(
                            remaining,
                            avail);
                    fs.Seek(
                        LbaToRaw(
                            curLba,
                            raw, uoff,
                            curOff),
                        SeekOrigin.Begin);
                    fs.Write(
                        data,
                        dataPos,
                        toWrite);
                    remaining -= toWrite;
                    dataPos += toWrite;
                    curLba++;
                    curOff = 0;
                }
            }
        }

        private class IsoEntry
        {
            public uint Lba;
            public uint Size;
        }

        private static
            Dictionary<string, IsoEntry>
            ScanIso(
                string path,
                int raw,
                int uoff)
        {
            var files =
                new Dictionary<
                    string,
                    IsoEntry>();

            byte[] pvd =
                ReadBytesAtLba(
                    path, raw, uoff,
                    ISO_PVD_LBA,
                    0,
                    LOGICAL_SECTOR_SIZE);

            if (pvd[0] != 0x01 ||
                pvd[1] != 'C' ||
                pvd[2] != 'D' ||
                pvd[3] != '0' ||
                pvd[4] != '0' ||
                pvd[5] != '1')
                throw new Exception(
                    "PVD not found");

            uint rootLba =
                ReadUInt32Le(
                    pvd,
                    ISO_ROOT_DIR_OFFSET
                    + 2);
            uint rootSize =
                ReadUInt32Le(
                    pvd,
                    ISO_ROOT_DIR_OFFSET
                    + 10);

            ParseDirectory(
                path, raw, uoff,
                rootLba, rootSize,
                "", files, 0);
            return files;
        }

        private static void
            ParseDirectory(
                string path,
                int raw,
                int uoff,
                uint dirLba,
                uint dirSize,
                string curPath,
                Dictionary<
                    string,
                    IsoEntry> outFiles,
                int depth)
        {
            if (depth > 20) return;

            byte[] dirData =
                ReadBytesAtLba(
                    path, raw, uoff,
                    dirLba, 0,
                    (int)dirSize);
            int pos = 0;

            while (pos <
                   dirData.Length)
            {
                int rlen =
                    dirData[pos];
                if (rlen == 0)
                {
                    int nextSector =
                        ((pos /
                          LOGICAL_SECTOR_SIZE)
                         + 1) *
                        LOGICAL_SECTOR_SIZE;
                    if (nextSector >=
                        dirData.Length)
                        break;
                    pos = nextSector;
                    continue;
                }
                if (pos + rlen >
                    dirData.Length)
                    break;

                uint eLba =
                    ReadUInt32Le(
                        dirData,
                        pos + 2);
                uint eSize =
                    ReadUInt32Le(
                        dirData,
                        pos + 10);
                byte flags =
                    dirData[pos + 25];
                int nlen =
                    dirData[pos + 32];

                bool isDir =
                    (flags & 0x02) != 0;
                bool isDot =
                    (nlen == 1 &&
                     (dirData[pos + 33]
                      == 0x00 ||
                      dirData[pos + 33]
                      == 0x01));

                if (!isDot && nlen > 0)
                {
                    string nstr =
                        System.Text
                        .Encoding.ASCII
                        .GetString(
                            dirData,
                            pos + 33,
                            nlen);
                    int semi =
                        nstr.IndexOf(';');
                    if (semi >= 0)
                        nstr =
                            nstr.Substring(
                                0, semi);
                    string full =
                        curPath +
                        @"\" + nstr;

                    if (isDir)
                    {
                        ParseDirectory(
                            path,
                            raw, uoff,
                            eLba, eSize,
                            full,
                            outFiles,
                            depth + 1);
                    }
                    else
                    {
                        outFiles[
                            full.ToUpper()]
                            = new IsoEntry
                            {
                                Lba = eLba,
                                Size = eSize
                            };
                    }
                }
                pos += rlen;
            }
        }

        private static uint
            ReadUInt32Le(
                byte[] data,
                int offset)
        {
            return (uint)(
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        private static void
            WriteUInt32Le(
                byte[] data,
                int offset,
                uint value)
        {
            data[offset] =
                (byte)(value & 0xFF);
            data[offset + 1] =
                (byte)((value >> 8)
                       & 0xFF);
            data[offset + 2] =
                (byte)((value >> 16)
                       & 0xFF);
            data[offset + 3] =
                (byte)((value >> 24)
                       & 0xFF);
        }
    }
}
