using System;
using System.IO;
using System.Collections.Generic;

namespace HMSTHModdingTool.BadEndingRemover
{
    // ═════════════════════════════════════════════
    // BAD ENDING REMOVER
    // ═════════════════════════════════════════════
    // Ported from Python HMSTH-Bad-Ending-Remover
    // Original: https://github.com/DarthKrayt333/
    //           HMSTH-Bad-Ending-Remover
    //
    // Removes:
    //   - 1-year cap
    //   - Year 2 Winter 30 bad ending
    //   - Winter -> Spring season transition crash
    //
    // Supports: ELF, ISO (2048), BIN (2352)
    // ═════════════════════════════════════════════
    public static class BadEndingRemover
    {
        // ─────────────────────────────────────
        // PATCH DEFINITIONS
        // ─────────────────────────────────────
        private struct PatchEntry
        {
            public uint Ps2Addr;
            public uint PatchValue;
            public string Description;
        }

        private static readonly PatchEntry[]
            PATCHES = new PatchEntry[]
        {
            new PatchEntry
            {
                Ps2Addr = 0x0017800C,
                PatchValue = 0x1000002B,
                Description =
                    "Remove 1-year cap /" +
                    " Skip Y2W30 bad-end gate"
            },
            new PatchEntry
            {
                Ps2Addr = 0x00178040,
                PatchValue = 0x00000000,
                Description =
                    "NOP bad-end cutscene" +
                    " call (safety)"
            },
            new PatchEntry
            {
                Ps2Addr = 0x001A1CE4,
                PatchValue = 0x30420003,
                Description =
                    "Season wrap #1: sleep" +
                    " first rollover"
            },
            new PatchEntry
            {
                Ps2Addr = 0x001A1E24,
                PatchValue = 0x30420003,
                Description =
                    "Season wrap #2: sleep" +
                    " Winter->Spring path"
            },
        };

        // ─────────────────────────────────────
        // CONSTANTS
        // ─────────────────────────────────────
        private const int ISO_SECTOR_SIZE = 2048;

        // ═════════════════════════════════════
        // SECTOR FORMAT
        // ═════════════════════════════════════
        private class SectorFormat
        {
            public int SectorSize;
            public int DataSize;
            public int DataOffset;

            public SectorFormat(
                int sectorSize,
                int dataSize,
                int dataOffset)
            {
                SectorSize = sectorSize;
                DataSize = dataSize;
                DataOffset = dataOffset;
            }

            public long LogicalToPhysical(
                long sectorStartLba,
                long logicalOffset)
            {
                long sectorInFile =
                    logicalOffset / DataSize;
                long offsetInSectorData =
                    logicalOffset % DataSize;
                long absoluteLba =
                    sectorStartLba + sectorInFile;
                return (absoluteLba * SectorSize)
                    + DataOffset
                    + offsetInSectorData;
            }

            public byte[] ReadLogical(
                byte[] discData,
                long sectorStartLba,
                long logicalOffset,
                int length)
            {
                var result =
                    new List<byte>(length);
                int remaining = length;
                long currentLogical =
                    logicalOffset;

                while (remaining > 0)
                {
                    long offsetInSector =
                        currentLogical % DataSize;
                    int canRead = Math.Min(
                        remaining,
                        DataSize -
                            (int)offsetInSector);
                    long physicalPos =
                        LogicalToPhysical(
                            sectorStartLba,
                            currentLogical);

                    if (physicalPos + canRead >
                        discData.Length)
                        break;

                    for (int i = 0;
                         i < canRead; i++)
                        result.Add(
                            discData[
                                physicalPos + i]);

                    currentLogical += canRead;
                    remaining -= canRead;
                }
                return result.ToArray();
            }

            public void WriteLogical(
                string filePath,
                long sectorStartLba,
                long logicalOffset,
                byte[] data)
            {
                int remaining = data.Length;
                long currentLogical =
                    logicalOffset;
                int dataPos = 0;

                using (var fs = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None))
                {
                    while (remaining > 0)
                    {
                        long offsetInSector =
                            currentLogical %
                            DataSize;
                        int canWrite = Math.Min(
                            remaining,
                            DataSize -
                                (int)offsetInSector);
                        long physicalPos =
                            LogicalToPhysical(
                                sectorStartLba,
                                currentLogical);

                        fs.Seek(physicalPos,
                            SeekOrigin.Begin);
                        fs.Write(data,
                            dataPos, canWrite);

                        currentLogical += canWrite;
                        dataPos += canWrite;
                        remaining -= canWrite;
                    }
                }
            }
        }

        private static readonly SectorFormat
            FORMAT_2048 = new SectorFormat(
                2048, 2048, 0);
        private static readonly SectorFormat
            FORMAT_2352_MODE1 = new SectorFormat(
                2352, 2048, 16);
        private static readonly SectorFormat
            FORMAT_2352_MODE2 = new SectorFormat(
                2352, 2048, 24);

        // ═════════════════════════════════════
        // DETECT DISC FORMAT
        // ═════════════════════════════════════
        private static SectorFormat
            DetectDiscFormat(byte[] data)
        {
            var formatsToTest = new[]
            {
                new { Fmt = FORMAT_2048,
                    Start = 16 * 2048 },
                new { Fmt = FORMAT_2352_MODE2,
                    Start = 16 * 2352 },
                new { Fmt = FORMAT_2352_MODE1,
                    Start = 16 * 2352 },
            };

            foreach (var test in formatsToTest)
            {
                int checkPos = test.Start +
                    test.Fmt.DataOffset + 1;
                if (checkPos + 5 <= data.Length)
                {
                    if (data[checkPos] == 'C' &&
                        data[checkPos + 1] == 'D' &&
                        data[checkPos + 2] == '0' &&
                        data[checkPos + 3] == '0' &&
                        data[checkPos + 4] == '1')
                        return test.Fmt;
                }
            }

            // Check sync pattern for BIN
            byte[] syncPattern = new byte[]
            {
                0x00, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0x00
            };

            if (data.Length >= 12)
            {
                bool matchSync = true;
                for (int i = 0; i < 12; i++)
                {
                    if (data[i] != syncPattern[i])
                    {
                        matchSync = false;
                        break;
                    }
                }
                if (matchSync)
                    return FORMAT_2352_MODE2;
            }

            return FORMAT_2048;
        }

        // ═════════════════════════════════════
        // ELF PARSER
        // ═════════════════════════════════════
        private class ElfParser
        {
            public byte[] Data;
            public List<ElfSegment> Segments =
                new List<ElfSegment>();
            public bool IsElf = false;

            public class ElfSegment
            {
                public uint FileOffset;
                public uint Vaddr;
                public uint FileSize;
                public uint MemSize;
            }

            public ElfParser(byte[] elfData)
            {
                Data = elfData;
                Parse();
            }

            private void Parse()
            {
                if (Data.Length < 4)
                    return;
                if (Data[0] != 0x7F ||
                    Data[1] != 0x45 ||
                    Data[2] != 0x4C ||
                    Data[3] != 0x46)
                    return;

                IsElf = true;

                uint ePhoff = BitConverter
                    .ToUInt32(Data, 0x1C);
                ushort ePhentsize =
                    BitConverter.ToUInt16(
                        Data, 0x2A);
                ushort ePhnum =
                    BitConverter.ToUInt16(
                        Data, 0x2C);

                for (int i = 0; i < ePhnum; i++)
                {
                    int phStart = (int)(ePhoff +
                        i * ePhentsize);
                    if (phStart + 24 >
                        Data.Length)
                        break;

                    uint pType = BitConverter
                        .ToUInt32(Data, phStart);
                    uint pOffset = BitConverter
                        .ToUInt32(Data,
                            phStart + 4);
                    uint pVaddr = BitConverter
                        .ToUInt32(Data,
                            phStart + 8);
                    uint pFilesz = BitConverter
                        .ToUInt32(Data,
                            phStart + 16);
                    uint pMemsz = BitConverter
                        .ToUInt32(Data,
                            phStart + 20);

                    if (pType == 1)
                    {
                        Segments.Add(
                            new ElfSegment
                            {
                                FileOffset = pOffset,
                                Vaddr = pVaddr,
                                FileSize = pFilesz,
                                MemSize = pMemsz,
                            });
                    }
                }
            }

            public long Ps2ToElfOffset(
                uint ps2Addr)
            {
                foreach (var seg in Segments)
                {
                    if (seg.Vaddr <= ps2Addr &&
                        ps2Addr <
                        seg.Vaddr + seg.FileSize)
                    {
                        return seg.FileOffset +
                            (ps2Addr - seg.Vaddr);
                    }
                }
                return -1;
            }
        }

        // ═════════════════════════════════════
        // DISC LOCATOR (finds SLUS in ISO/BIN)
        // ═════════════════════════════════════
        private class DiscLocator
        {
            public byte[] Data;
            public SectorFormat Fmt;
            public long SlusLba = -1;
            public long SlusSize = -1;

            public DiscLocator(
                byte[] data,
                SectorFormat fmt)
            {
                Data = data;
                Fmt = fmt;
            }

            public bool FindSlus(
                out string method)
            {
                method = null;

                long result = TryIso9660();
                if (result >= 0)
                {
                    method = "iso9660";
                    return true;
                }

                result = TrySectorScan();
                if (result >= 0)
                {
                    SlusLba = result;
                    method = "sector_scan";
                    return true;
                }

                return false;
            }

            private byte[] ReadSectorData(
                long lba,
                int offsetInSector = 0,
                int length = -1)
            {
                if (length < 0)
                    length = Fmt.DataSize -
                        offsetInSector;

                long physicalPos =
                    (lba * Fmt.SectorSize) +
                    Fmt.DataOffset +
                    offsetInSector;

                if (physicalPos + length >
                    Data.Length)
                    return null;

                byte[] result =
                    new byte[length];
                Array.Copy(Data, physicalPos,
                    result, 0, length);
                return result;
            }

            private long TryIso9660()
            {
                try
                {
                    byte[] pvd = ReadSectorData(
                        16);
                    if (pvd == null ||
                        pvd[0] != 1 ||
                        pvd[1] != 'C' ||
                        pvd[2] != 'D' ||
                        pvd[3] != '0' ||
                        pvd[4] != '0' ||
                        pvd[5] != '1')
                        return -1;

                    uint rootLba =
                        BitConverter.ToUInt32(
                            pvd, 156 + 2);
                    uint rootSize =
                        BitConverter.ToUInt32(
                            pvd, 156 + 10);

                    var rootData =
                        new List<byte>();
                    int sectorsToRead =
                        (int)((rootSize + 2047)
                            / 2048);
                    for (int i = 0;
                         i < sectorsToRead;
                         i++)
                    {
                        byte[] sec =
                            ReadSectorData(
                                rootLba + i);
                        if (sec == null) break;
                        rootData.AddRange(sec);
                    }

                    byte[] rootBytes =
                        rootData.ToArray();
                    int limit = Math.Min(
                        rootBytes.Length,
                        (int)rootSize);

                    int offset = 0;
                    while (offset < limit)
                    {
                        int entryLen =
                            rootBytes[offset];
                        if (entryLen == 0)
                        {
                            offset = ((offset /
                                2048) + 1) * 2048;
                            if (offset >= limit)
                                break;
                            continue;
                        }

                        if (offset + entryLen >
                            limit) break;

                        if (entryLen < 33)
                        {
                            offset += entryLen;
                            continue;
                        }

                        int nameLen =
                            rootBytes[offset
                                + 32];
                        if (nameLen > 0 &&
                            33 + nameLen <=
                            entryLen)
                        {
                            string name =
                                System.Text
                                    .Encoding
                                    .ASCII
                                    .GetString(
                                        rootBytes,
                                        offset + 33,
                                        nameLen);
                            int semi = name
                                .IndexOf(';');
                            if (semi >= 0)
                                name = name
                                    .Substring(
                                        0, semi);

                            string upper =
                                name.ToUpper();
                            if (upper.Contains(
                                    "SLUS_202.51") ||
                                upper.Contains(
                                    "SLUS_20251") ||
                                upper.Contains(
                                    "SLUS-20251"))
                            {
                                uint fileLba =
                                    BitConverter
                                    .ToUInt32(
                                        rootBytes,
                                        offset + 2);
                                uint fileSize =
                                    BitConverter
                                    .ToUInt32(
                                        rootBytes,
                                        offset + 10);
                                SlusLba = fileLba;
                                SlusSize =
                                    fileSize;
                                return fileLba;
                            }
                        }

                        offset += entryLen;
                    }
                    return -1;
                }
                catch
                {
                    return -1;
                }
            }

            private long TrySectorScan()
            {
                long totalSectors =
                    Data.Length / Fmt.SectorSize;
                for (long lba = 0;
                     lba < totalSectors;
                     lba++)
                {
                    byte[] sec = ReadSectorData(
                        lba, 0, 32);
                    if (sec == null) break;

                    if (sec[0] == 0x7F &&
                        sec[1] == 0x45 &&
                        sec[2] == 0x4C &&
                        sec[3] == 0x46)
                    {
                        if (sec.Length >= 0x1C)
                        {
                            ushort eMachine =
                                BitConverter
                                    .ToUInt16(
                                        sec,
                                        0x12);
                            uint eEntry =
                                BitConverter
                                    .ToUInt32(
                                        sec,
                                        0x18);
                            if (eMachine ==
                                0x08 &&
                                eEntry >=
                                    0x00100000 &&
                                eEntry <=
                                    0x00200000)
                                return lba;
                        }
                    }
                }
                return -1;
            }

            public byte[] ExtractSlusBytes(
                int maxSize = 2_000_000)
            {
                if (SlusLba < 0) return null;
                int size = SlusSize > 0
                    ? (int)SlusSize
                    : maxSize;
                return Fmt.ReadLogical(
                    Data, SlusLba, 0, size);
            }
        }

        // ═════════════════════════════════════
        // UNIFIED PATCHER
        // ═════════════════════════════════════
        private class Patcher
        {
            public string FilePath;
            public byte[] Data;
            public string FileType = "unknown";
            public SectorFormat Fmt;
            public long SlusLba = 0;
            public ElfParser Elf;
            public bool IsDisc = false;

            public Patcher(string filePath)
            {
                FilePath = filePath;
            }

            public bool Load(out string msg)
            {
                msg = null;
                Data = File.ReadAllBytes(
                    FilePath);

                // Check for ELF
                if (Data.Length >= 4 &&
                    Data[0] == 0x7F &&
                    Data[1] == 0x45 &&
                    Data[2] == 0x4C &&
                    Data[3] == 0x46)
                {
                    FileType = "ELF";
                    IsDisc = false;
                    Elf = new ElfParser(Data);
                    msg = "ELF file detected";
                    return true;
                }

                // Disc image
                IsDisc = true;
                Fmt = DetectDiscFormat(Data);

                if (Fmt.SectorSize == 2048)
                    FileType =
                        "ISO (2048 byte sectors)";
                else if (Fmt.DataOffset == 24)
                    FileType =
                        "BIN (2352 byte sectors," +
                        " Mode 2 Form 1)";
                else
                    FileType =
                        "BIN (2352 byte sectors," +
                        " Mode 1)";

                var locator = new DiscLocator(
                    Data, Fmt);
                string method;
                if (!locator.FindSlus(
                    out method))
                {
                    msg = "SLUS_202.51 not found" +
                        " in " + FileType;
                    return false;
                }

                SlusLba = locator.SlusLba;

                byte[] slusBytes = locator
                    .ExtractSlusBytes();
                if (slusBytes == null ||
                    slusBytes.Length < 4 ||
                    slusBytes[0] != 0x7F ||
                    slusBytes[1] != 0x45 ||
                    slusBytes[2] != 0x4C ||
                    slusBytes[3] != 0x46)
                {
                    msg = "SLUS at LBA " +
                        SlusLba + " but content" +
                        " is not ELF";
                    return false;
                }

                Elf = new ElfParser(slusBytes);
                long physOff =
                    SlusLba * Fmt.SectorSize;
                msg = FileType + " detected." +
                    " SLUS at LBA " + SlusLba +
                    " (physical offset 0x" +
                    physOff.ToString("X") +
                    ", method: " + method + ")";
                return true;
            }

            public long GetPhysicalOffset(
                uint ps2Addr)
            {
                if (Elf == null || !Elf.IsElf)
                    return -1;
                long elfOffset = Elf
                    .Ps2ToElfOffset(ps2Addr);
                if (elfOffset < 0) return -1;
                if (!IsDisc)
                    return elfOffset;
                return Fmt.LogicalToPhysical(
                    SlusLba, elfOffset);
            }

            public byte[] ReadPs2(
                uint ps2Addr, int size)
            {
                if (Elf == null || !Elf.IsElf)
                    return null;
                long elfOffset = Elf
                    .Ps2ToElfOffset(ps2Addr);
                if (elfOffset < 0) return null;

                if (!IsDisc)
                {
                    if (elfOffset + size >
                        Data.Length)
                        return null;
                    byte[] result =
                        new byte[size];
                    Array.Copy(Data, elfOffset,
                        result, 0, size);
                    return result;
                }
                else
                {
                    return Fmt.ReadLogical(
                        Data, SlusLba,
                        elfOffset, size);
                }
            }

            public long WritePs2(
                uint ps2Addr, byte[] data)
            {
                if (Elf == null || !Elf.IsElf)
                    throw new Exception(
                        "ELF parser not" +
                        " initialized");
                long elfOffset = Elf
                    .Ps2ToElfOffset(ps2Addr);
                if (elfOffset < 0)
                    throw new Exception(
                        "PS2 address 0x" +
                        ps2Addr.ToString("X8") +
                        " not mapped to file");

                if (!IsDisc)
                {
                    using (var fs =
                        new FileStream(
                            FilePath,
                            FileMode.Open,
                            FileAccess.Write,
                            FileShare.None))
                    {
                        fs.Seek(elfOffset,
                            SeekOrigin.Begin);
                        fs.Write(data, 0,
                            data.Length);
                    }
                    // Update cached data
                    Array.Copy(data, 0, Data,
                        elfOffset, data.Length);
                    return elfOffset;
                }
                else
                {
                    Fmt.WriteLogical(
                        FilePath, SlusLba,
                        elfOffset, data);
                    // Reload data
                    Data = File.ReadAllBytes(
                        FilePath);
                    return Fmt
                        .LogicalToPhysical(
                            SlusLba, elfOffset);
                }
            }
        }

        // ═════════════════════════════════════
        // PUBLIC API
        // ═════════════════════════════════════

        // ─────────────────────────────────
        // ANALYZE FILE
        // ─────────────────────────────────
        public static void Analyze(
            string filePath)
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.WriteLine(
                "  Bad Ending Remover -" +
                " Analyze File");
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.ResetColor();
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                TextOut.PrintError(
                    "File not found: " +
                    filePath);
                return;
            }

            var patcher = new Patcher(filePath);
            string msg;
            if (!patcher.Load(out msg))
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine("  " + msg);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine("  " + msg);
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  ELF Program Headers:");
            Console.ResetColor();
            for (int i = 0;
                 i < patcher.Elf.Segments.Count;
                 i++)
            {
                var seg = patcher.Elf
                    .Segments[i];
                Console.WriteLine(
                    "    Segment " + i +
                    ": file_offset=0x" +
                    seg.FileOffset
                        .ToString("X") +
                    ", vaddr=0x" +
                    seg.Vaddr
                        .ToString("X8") +
                    ", filesz=" +
                    seg.FileSize);
            }
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Patch Verification" +
                " (current values):");
            Console.ResetColor();
            foreach (var patch in PATCHES)
            {
                byte[] current =
                    patcher.ReadPs2(
                        patch.Ps2Addr, 4);
                long physOff =
                    patcher.GetPhysicalOffset(
                        patch.Ps2Addr);

                if (current == null)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "    0x" +
                        patch.Ps2Addr
                            .ToString("X8") +
                        ": READ ERROR");
                    Console.ResetColor();
                }
                else
                {
                    uint curVal = BitConverter
                        .ToUInt32(current, 0);
                    byte[] newBytes =
                        BitConverter.GetBytes(
                            patch.PatchValue);
                    bool alreadyPatched = true;
                    for (int i = 0; i < 4; i++)
                        if (current[i] !=
                            newBytes[i])
                        {
                            alreadyPatched =
                                false;
                            break;
                        }
                    string status =
                        alreadyPatched
                        ? " (ALREADY PATCHED)"
                        : "";

                    Console.WriteLine(
                        "    0x" +
                        patch.Ps2Addr
                            .ToString("X8") +
                        " -> physical 0x" +
                        physOff.ToString("X"));
                    Console.ForegroundColor =
                        alreadyPatched
                        ? ConsoleColor.Green
                        : ConsoleColor.Yellow;
                    Console.WriteLine(
                        "      Current: 0x" +
                        curVal.ToString("X8") +
                        ", Will patch to: 0x" +
                        patch.PatchValue
                            .ToString("X8") +
                        status);
                    Console.ResetColor();
                    Console.ForegroundColor =
                        ConsoleColor.DarkGray;
                    Console.WriteLine(
                        "      " +
                        patch.Description);
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  [OK] Analysis complete.");
            Console.ResetColor();
        }

        // ─────────────────────────────────
        // APPLY PATCHES
        // ─────────────────────────────────
        public static void Apply(
            string filePath,
            bool createBackup = true)
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.WriteLine(
                "  Bad Ending Remover -" +
                " Apply Patches");
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.ResetColor();
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                TextOut.PrintError(
                    "File not found: " +
                    filePath);
                return;
            }

            var patcher = new Patcher(filePath);
            string msg;
            if (!patcher.Load(out msg))
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine("  " + msg);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine("  " + msg);
            Console.ResetColor();
            Console.WriteLine();

            // Backup
            if (createBackup)
            {
                string backupPath =
                    filePath + ".bak";
                if (!File.Exists(backupPath))
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [*] Creating" +
                        " backup...");
                    Console.ResetColor();
                    try
                    {
                        File.Copy(filePath,
                            backupPath);
                        Console.ForegroundColor
                            = ConsoleColor
                                .Green;
                        Console.WriteLine(
                            "  [OK] Backup: " +
                            Path.GetFileName(
                                backupPath));
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor
                            = ConsoleColor
                                .Yellow;
                        Console.WriteLine(
                            "  [FAIL] Backup" +
                            " failed: " +
                            ex.Message);
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor =
                        ConsoleColor.DarkGray;
                    Console.WriteLine(
                        "  [*] Backup exists: " +
                        Path.GetFileName(
                            backupPath));
                    Console.ResetColor();
                }
                Console.WriteLine();
            }

            int applied = 0;
            int failed = 0;

            foreach (var patch in PATCHES)
            {
                try
                {
                    byte[] current =
                        patcher.ReadPs2(
                            patch.Ps2Addr, 4);
                    if (current == null)
                    {
                        Console.ForegroundColor
                            = ConsoleColor
                                .Red;
                        Console.WriteLine(
                            "  [FAIL] 0x" +
                            patch.Ps2Addr
                                .ToString("X8")
                            + ": address not" +
                            " in file");
                        Console.ResetColor();
                        failed++;
                        continue;
                    }

                    byte[] patchBytes =
                        BitConverter.GetBytes(
                            patch.PatchValue);
                    long physOff = patcher
                        .WritePs2(
                            patch.Ps2Addr,
                            patchBytes);

                    byte[] verify =
                        patcher.ReadPs2(
                            patch.Ps2Addr, 4);
                    bool ok = true;
                    for (int i = 0; i < 4; i++)
                        if (verify[i] !=
                            patchBytes[i])
                        {
                            ok = false;
                            break;
                        }

                    if (ok)
                    {
                        Console.ForegroundColor
                            = ConsoleColor
                                .Green;
                        Console.WriteLine(
                            "  [OK] 0x" +
                            patch.Ps2Addr
                                .ToString("X8") +
                            " (physical 0x" +
                            physOff.ToString(
                                "X") + "): " +
                            patch.Description);
                        Console.ResetColor();
                        Console.ForegroundColor
                            = ConsoleColor
                                .DarkGray;
                        Console.WriteLine(
                            "       " +
                            BitConverter
                                .ToString(
                                    current)
                                .Replace(
                                    "-", "") +
                            " -> " +
                            BitConverter
                                .ToString(
                                    verify)
                                .Replace(
                                    "-", ""));
                        Console.ResetColor();
                        applied++;
                    }
                    else
                    {
                        Console.ForegroundColor
                            = ConsoleColor
                                .Red;
                        Console.WriteLine(
                            "  [FAIL] 0x" +
                            patch.Ps2Addr
                                .ToString("X8") +
                            ": verify failed");
                        Console.ResetColor();
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  [ERR] 0x" +
                        patch.Ps2Addr
                            .ToString("X8") +
                        ": " + ex.Message);
                    Console.ResetColor();
                    failed++;
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.ResetColor();

            if (applied > 0 && failed == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  DONE! Applied " +
                    applied + " patches!");
                Console.WriteLine();
                Console.WriteLine(
                    "  The file is fully" +
                    " patched standalone.");
                Console.WriteLine(
                    "  No PNACH needed -" +
                    " load directly in PCSX2.");
                if (patcher.IsDisc &&
                    patcher.Fmt.SectorSize ==
                        2352)
                {
                    Console.WriteLine();
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  Note: BIN file" +
                        " was patched with" +
                        " sector-aware" +
                        " writes.");
                    Console.WriteLine(
                        "  If PCSX2 does not" +
                        " accept the BIN, try" +
                        " converting to ISO.");
                }
                Console.ResetColor();
            }
            else if (failed > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Partial: Applied " +
                    applied + ", Failed " +
                    failed);
                Console.ResetColor();
            }
        }

        // ─────────────────────────────────
        // VERIFY PATCHES
        // ─────────────────────────────────
        public static void Verify(
            string filePath)
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.WriteLine(
                "  Bad Ending Remover -" +
                " Verify Patches");
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.ResetColor();
            Console.WriteLine();

            if (!File.Exists(filePath))
            {
                TextOut.PrintError(
                    "File not found: " +
                    filePath);
                return;
            }

            var patcher = new Patcher(filePath);
            string msg;
            if (!patcher.Load(out msg))
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine("  " + msg);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine("  " + msg);
            Console.ResetColor();
            Console.WriteLine();

            bool allPatched = true;
            foreach (var patch in PATCHES)
            {
                byte[] current = patcher.ReadPs2(
                    patch.Ps2Addr, 4);
                byte[] expected = BitConverter
                    .GetBytes(patch.PatchValue);

                bool matches = true;
                if (current == null)
                    matches = false;
                else
                    for (int i = 0; i < 4; i++)
                        if (current[i] !=
                            expected[i])
                        {
                            matches = false;
                            break;
                        }

                if (matches)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "  [OK]  0x" +
                        patch.Ps2Addr
                            .ToString("X8") +
                        ": PATCHED (" +
                        BitConverter.ToString(
                            current)
                            .Replace("-", "") +
                        ")");
                    Console.ResetColor();
                }
                else
                {
                    uint curVal = current !=
                        null
                        ? BitConverter
                            .ToUInt32(current, 0)
                        : 0;
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  [FAIL] 0x" +
                        patch.Ps2Addr
                            .ToString("X8") +
                        ": NOT PATCHED");
                    Console.WriteLine(
                        "         Current: 0x" +
                        curVal.ToString("X8") +
                        ", Expected: 0x" +
                        patch.PatchValue
                            .ToString("X8"));
                    Console.ResetColor();
                    allPatched = false;
                }
            }

            Console.WriteLine();
            if (allPatched)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  [OK] All patches" +
                    " present in file!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [FAIL] Some patches" +
                    " are missing.");
                Console.ResetColor();
            }
        }

        // ─────────────────────────────────
        // RESTORE FROM BACKUP
        // ─────────────────────────────────
        public static void Restore(
            string filePath)
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.WriteLine(
                "  Bad Ending Remover -" +
                " Restore Backup");
            Console.WriteLine(
                "═════════════════════════════" +
                "═════════════");
            Console.ResetColor();
            Console.WriteLine();

            string backupPath = filePath +
                ".bak";
            if (!File.Exists(backupPath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  No backup file found: " +
                    backupPath);
                Console.ResetColor();
                return;
            }

            try
            {
                File.Copy(backupPath, filePath,
                    true);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  [OK] Restored from" +
                    " backup: " +
                    Path.GetFileName(
                        backupPath));
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  [ERR] Restore failed: " +
                    ex.Message);
                Console.ResetColor();
            }
        }
    }
}
