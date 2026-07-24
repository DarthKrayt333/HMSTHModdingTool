using System;
using System.Collections.Generic;
using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles automated LBA table
    ///     patching in SLUS_202.51 /
    ///     SLPS_201.04 inside a HMSTH ISO.
    ///     No hardcoded file lists needed.
    ///     Reads all files from ISO and
    ///     sorts by LBA automatically.
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
            GetElfFilename(bool isJap)
        {
            return isJap
                ? SLPS_FILENAME
                : SLUS_FILENAME;
        }

        public static uint
            GetLbaTableStart(bool isJap)
        {
            return isJap
                ? SLPS_LBA_TABLE_START
                : SLUS_LBA_TABLE_START;
        }

        public static uint
            GetLbaTableEnd(bool isJap)
        {
            return isJap
                ? SLPS_LBA_TABLE_END
                : SLUS_LBA_TABLE_END;
        }

        // ═════════════════════════════
        // AUTO DETECT VERSION
        // ═════════════════════════════

        /// <summary>
        ///     Scans the ISO filesystem
        ///     to detect JAP or USA.
        ///     Returns true if JAP
        ///     (SLPS_201.04 found).
        ///     Returns false if USA
        ///     (SLUS_202.51 found).
        ///     Throws warning if neither.
        /// </summary>
        public static bool
            AutoDetectVersion(
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

            bool hasJap =
                files.ContainsKey(
                    @"\SLPS_201.04");
            bool hasUsa =
                files.ContainsKey(
                    @"\SLUS_202.51");

            if (hasJap)
            {
                detectedElf =
                    @"\SLPS_201.04";
                return true;
            }
            else if (hasUsa)
            {
                detectedElf =
                    @"\SLUS_202.51";
                return false;
            }
            else
            {
                detectedElf = null;
                throw new
                    InvalidDataException(
                    "Neither" +
                    " SLUS_202.51 nor" +
                    " SLPS_201.04 was" +
                    " found inside" +
                    " the ISO.\n" +
                    "  This does not" +
                    " appear to be a" +
                    " valid HMSTH" +
                    " disc image.");
            }
        }

        // ═════════════════════════════
        // FIX LBA (USA - no flag)
        // ═════════════════════════════

        /// <summary>
        ///     Auto-fixes the LBA table
        ///     (USA version).
        /// </summary>
        public static int FixLba(
            string isoPath)
        {
            return FixLba(
                isoPath, false);
        }

        // ═════════════════════════════
        // FIX LBA - MAIN METHOD
        // No hardcoded file list.
        // Reads ISO filesystem and
        // sorts files by LBA.
        // Only needs ELF filename.
        // ═════════════════════════════

        /// <summary>
        ///     Auto-fixes the LBA table
        ///     inside the ELF which is
        ///     inside the ISO.
        ///     No hardcoded file list.
        ///     Reads all files from ISO
        ///     and sorts by LBA.
        /// </summary>
        /// <param name="isoPath">
        ///     Path to the HMSTH ISO
        /// </param>
        /// <param name="isJap">
        ///     True for JAP version
        /// </param>
        /// <returns>
        ///     Number of LBA entries
        ///     changed
        /// </returns>
        public static int FixLba(
            string isoPath,
            bool isJap)
        {
            if (!File.Exists(isoPath))
                throw new
                    FileNotFoundException(
                    "ISO file not found",
                    isoPath);

            uint lbaTableStart =
                GetLbaTableStart(isJap);
            uint lbaTableEnd =
                GetLbaTableEnd(isJap);
            int lbaTableSize =
                (int)(lbaTableEnd -
                      lbaTableStart);
            string elfFilename =
                GetElfFilename(isJap);
            string versionName = isJap
                ? "JAP (SLPS_201.04)"
                : "USA (SLUS_202.51)";

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
                    " not found in ISO");

            var elf = files[elfKey];
            TextOut.Print(
                $"{elfFilename}" +
                $" at LBA {elf.Lba}," +
                $" size {elf.Size}" +
                " bytes");

            // ── 4. Sort all files
            //       by LBA ascending
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

            // ── 5. Filter out
            //       zero-size entries
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

            // ── 6. Check table
            //       capacity
            int maxEntries =
                lbaTableSize / 8;

            // System area entry
            // takes slot 0
            int availableSlots =
                maxEntries - 1;

            // ── CRITICAL CHECK ────────
            // If game files exceed
            // the table capacity
            // something is very wrong
            // and we must abort
            if (gameFiles.Count >
                availableSlots)
            {
                throw new
                    InvalidDataException(
                    "CRITICAL: Found " +
                    gameFiles.Count +
                    " files in ISO but" +
                    " LBA table only" +
                    " has space for " +
                    availableSlots +
                    " entries!\n" +
                    "  The ISO may be" +
                    " corrupted or" +
                    " this is not a" +
                    " valid HMSTH ISO." +
                    " Aborting to" +
                    " prevent damage.");
            }

            TextOut.Print(
                "LBA table capacity:" +
                $" {maxEntries}" +
                " entries (" +
                availableSlots +
                " for files + 1" +
                " system area)");

            // ── 7. Get first file LBA
            //       for system area
            uint firstLba = 0;
            if (gameFiles.Count > 0)
                firstLba =
                    gameFiles[0]
                    .Value.Lba;

            // ── 8. Build new LBA table
            byte[] newTable =
                new byte[lbaTableSize];

            // Entry 0 = system area
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
                    // This should never
                    // happen because we
                    // checked capacity
                    // above but just
                    // in case log it
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

            // ── CRITICAL: If anything
            //    was skipped for any
            //    reason abort now
            if (skippedFiles.Count > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "\n  CRITICAL" +
                    " WARNING!");
                Console.WriteLine(
                    "  The following" +
                    " files were" +
                    " SKIPPED during" +
                    " LBA table" +
                    " building:");
                foreach (var s
                         in skippedFiles)
                    Console.WriteLine(
                        "    - " + s);
                Console.WriteLine(
                    "\n  This should" +
                    " NEVER happen." +
                    " The game will" +
                    " NOT work" +
                    " correctly!");
                Console.WriteLine(
                    "  ABORTING to" +
                    " prevent ISO" +
                    " damage.");
                Console.ResetColor();
                Console.WriteLine();
                throw new
                    InvalidDataException(
                    "LBA table build" +
                    " was incomplete." +
                    " " +
                    skippedFiles.Count +
                    " file(s) were" +
                    " skipped." +
                    " Aborting.");
            }

            TextOut.Print(
                $"Built LBA table:" +
                $" {written} entries" +
                $" (1 system area +" +
                $" {written - 1}" +
                $" files)");

            // ── 9. Read existing table
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
                    " correct - no" +
                    " changes needed");
                return 0;
            }

            TextOut.Print(
                $"Writing" +
                $" {diffCount}" +
                $" changed LBA" +
                $" entries to" +
                $" {elfFilename}" +
                $" at offset 0x" +
                $"{lbaTableStart:X}");

            // ── 10. Write new table
            WriteBytesAtLba(
                isoPath,
                rawSectorSize,
                userDataOffset,
                elf.Lba,
                lbaTableStart,
                newTable);

            // ── 11. Verify write
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
                        " failed - write" +
                        " did not" +
                        " persist");
            }

            TextOut.PrintSuccess(
                "LBA table patched" +
                " successfully - " +
                $"{diffCount}" +
                " entries updated");
            return diffCount;
        }

        // ═════════════════════════════
        // ISO 9660 HELPERS
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
