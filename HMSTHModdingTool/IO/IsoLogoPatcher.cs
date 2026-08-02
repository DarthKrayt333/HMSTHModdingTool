using System;
using System.IO;
using System.Collections.Generic;

namespace HMSTHModdingTool
{
    public static class IsoLogoPatcher
    {
        public class PatchOptions
        {
        }

        const int SECTOR_2352 = 2352;
        const int SECTOR_2048 = 2048;
        const int DATA_OFF = 24;
        const int DATA_LEN = 2048;
        const int ECC_START = 2076;
        const int ECC_LEN = 276;
        const byte PADDING = 0xDD;

        static readonly byte[]
        SYNC_PATTERN = new byte[]
        {
            0x00, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0xFF,
            0xFF, 0xFF, 0xFF, 0x00
        };

        static readonly
        (int offset, byte[] data)[]
        MASTER_PATCHES = new[]
        {
            (98, new byte[] {
                0x30, 0x32 }),
            (101, new byte[] {
                0x39 }),
            (103, new byte[] {
                0x35 }),
            (130, new byte[] {
                0x00 }),
            (265, new byte[] {
                0x93 }),
            (269, new byte[] {
                0xDD }),
            (272, new byte[] {
                0x01, 0x4B, 0x00,
                0x00, 0x00, 0x4A,
                0x10, 0x00, 0x00,
                0x93, 0x69, 0x95,
                0x39, 0xDD }),
            (288, new byte[] {
                0x03 }),
            (297, new byte[] {
                0x00, 0x00, 0x00,
                0x00, 0xC4 }),
            (304, new byte[] {
                0x00, 0x00 }),
            (309, new byte[] {
                0x00 }),
            (317, new byte[] {
                0x00 }),
            (319, new byte[] {
                0x00 }),
            (816, new byte[] {
                0x43, 0x44, 0x56,
                0x44, 0x47, 0x45,
                0x4E, 0x20, 0x31,
                0x2E, 0x32 }),
        };

        // ═══════════════════════════════
        // MAIN COMMAND - bool overload
        // kept for backward compat
        // ═══════════════════════════════
        public static void PatchIso(
            string isoPath,
            PatchOptions opts = null,
            bool isJap = false)
        {
            // Try auto-detect version
            // from ISO filesystem first
            GameVersion version =
                GameVersion.USA;
            if (isJap)
                version =
                    GameVersion.JAP;

            try
            {
                string detElf;
                version =
                    HMSTHModdingTool
                    .IO.HarvestIso
                    .AutoDetectGameVersion(
                        isoPath,
                        out detElf);
            }
            catch
            {
                // Auto-detect failed,
                // fall back to isJap flag
                if (isJap)
                    version =
                        GameVersion.JAP;
                else
                    version =
                        GameVersion.USA;
            }

            PatchIso(
                isoPath, opts, version);
        }

        // ═══════════════════════════════
        // MAIN COMMAND - GameVersion
        // ═══════════════════════════════
        public static void PatchIso(
            string isoPath,
            PatchOptions opts,
            GameVersion version)
        {
            if (!File.Exists(isoPath))
                throw new
                    FileNotFoundException(
                    "File not found",
                    isoPath);

            string vLabel;
            switch (version)
            {
                case GameVersion.JAP:
                    vLabel =
                        " fixps2logo [JAP]";
                    break;
                case GameVersion.DEMO:
                    vLabel =
                        " fixps2logo" +
                        " [JAP DEMO]";
                    break;
                default:
                    vLabel =
                        " fixps2logo [USA]";
                    break;
            }

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═════════════════════" +
                "════════════════════");
            Console.WriteLine(vLabel);
            Console.WriteLine(
                "═════════════════════" +
                "════════════════════");
            Console.ResetColor();

            int sectorSize =
                DetectSectorSize(isoPath);

            Console.WriteLine(
                "  Format: " +
                sectorSize +
                "-byte sectors");

            if (sectorSize == SECTOR_2352)
            {
                PatchBinFile(
                    isoPath, version);
            }
            else if (sectorSize ==
                     SECTOR_2048)
            {
                PatchIsoFile(
                    isoPath, version);
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  ERROR:" +
                    " Unknown format!");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("  Done!");
            Console.ResetColor();
        }

        // ═══════════════════════════════
        // PATCH BIN FILE (2352 bytes)
        // GameVersion overload
        // ═══════════════════════════════
        static void PatchBinFile(
            string path,
            GameVersion version)
        {
            List<Blob> blobs;
            byte padByte;

            switch (version)
            {
                case GameVersion.JAP:
                    blobs =
                        GetEmbeddedBlobsJap();
                    padByte = 0x45;
                    break;

                case GameVersion.DEMO:
                    blobs =
                        GetEmbeddedBlobsDemo();
                    padByte = 0x9D;
                    break;

                default:
                    blobs =
                        GetEmbeddedBlobs();
                    padByte = PADDING;
                    break;
            }

            var eccData =
                GetEmbeddedEcc();

            Console.WriteLine(
                "  " + blobs.Count +
                " logo blobs (embedded)");

            using (var fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite))
            {
                DoPatchWork(
                    fs,
                    blobs,
                    eccData,
                    padByte);
            }
        }

        // ═══════════════════════════════
        // PATCH BIN FILE (2352 bytes)
        // Legacy bool overload kept
        // ═══════════════════════════════
        static void PatchBinFile(
            string path,
            bool isJap = false)
        {
            PatchBinFile(
                path,
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        // ═══════════════════════════════
        // PATCH ISO FILE (2048 bytes)
        // GameVersion overload
        // ═══════════════════════════════
        static void PatchIsoFile(
            string isoPath,
            GameVersion version)
        {
            string tempBin =
                isoPath + ".tmp.bin";

            Console.WriteLine(
                "  Converting ISO" +
                " to temp BIN...");

            Convert2048To2352(
                isoPath, tempBin);

            Console.WriteLine(
                "  Patching temp BIN...");

            PatchBinFile(
                tempBin, version);

            Console.WriteLine(
                "  Converting patched" +
                " BIN back to ISO...");

            Convert2352To2048(
                tempBin, isoPath);

            File.Delete(tempBin);

            Console.WriteLine(
                "  ISO format preserved" +
                " (2048 bytes/sector)");
        }

        // ═══════════════════════════════
        // PATCH ISO FILE (2048 bytes)
        // Legacy bool overload kept
        // ═══════════════════════════════
        static void PatchIsoFile(
            string isoPath,
            bool isJap = false)
        {
            PatchIsoFile(
                isoPath,
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        // ═══════════════════════════════
        // CORE PATCH WORK
        // Unchanged from your original
        // ═══════════════════════════════
        static void DoPatchWork(
            FileStream fs,
            List<Blob> blobs,
            Dictionary<int, byte[]>
                eccData,
            byte padding = PADDING)
        {
            Console.WriteLine();
            Console.WriteLine(
                "  [1/4] Logo sectors" +
                " 0-11...");

            for (int lba = 0;
                 lba <= 11; lba++)
            {
                byte[] sec =
                    BuildLogoSector(
                        lba,
                        blobs,
                        padding);
                WriteSectorData(
                    fs, lba, sec);
            }

            Console.WriteLine(
                "  [2/4] Master markers" +
                " 14-15...");

            for (int lba = 14;
                 lba <= 15; lba++)
            {
                PatchMasterSector(
                    fs, lba);
            }

            Console.WriteLine(
                "  [3/4] EDC recalc...");

            for (int lba = 0;
                 lba <= 15; lba++)
            {
                if (lba == 12 ||
                    lba == 13)
                    continue;
                ComputeEdc(fs, lba);
            }

            Console.WriteLine(
                "  [4/4] ECC write...");

            foreach (var kvp in eccData)
            {
                WriteEcc(
                    fs,
                    kvp.Key,
                    kvp.Value);
            }
        }

        // ═══════════════════════════════
        // CONVERT 2048 → 2352
        // Unchanged from your original
        // ═══════════════════════════════
        static void Convert2048To2352(
            string inPath,
            string outPath)
        {
            long origSize =
                new FileInfo(
                    inPath).Length;
            long numSectors =
                origSize / SECTOR_2048;

            using (var input =
                File.OpenRead(inPath))
            using (var output =
                File.Create(outPath))
            {
                byte[] dataBuf =
                    new byte[SECTOR_2048];
                byte[] rawBuf =
                    new byte[SECTOR_2352];

                for (long lba = 0;
                     lba < numSectors;
                     lba++)
                {
                    input.Read(
                        dataBuf, 0,
                        SECTOR_2048);

                    BuildRawSector(
                        rawBuf,
                        dataBuf,
                        (int)lba);

                    output.Write(
                        rawBuf, 0,
                        SECTOR_2352);
                }
            }
        }

        // ═══════════════════════════════
        // CONVERT 2352 → 2048
        // Unchanged from your original
        // ═══════════════════════════════
        static void Convert2352To2048(
            string inPath,
            string outPath)
        {
            long origSize =
                new FileInfo(
                    inPath).Length;
            long numSectors =
                origSize / SECTOR_2352;

            using (var input =
                File.OpenRead(inPath))
            using (var output =
                File.Create(outPath))
            {
                byte[] rawBuf =
                    new byte[SECTOR_2352];
                byte[] dataBuf =
                    new byte[SECTOR_2048];

                for (long lba = 0;
                     lba < numSectors;
                     lba++)
                {
                    input.Read(
                        rawBuf, 0,
                        SECTOR_2352);

                    Array.Copy(
                        rawBuf,
                        DATA_OFF,
                        dataBuf, 0,
                        SECTOR_2048);

                    output.Write(
                        dataBuf, 0,
                        SECTOR_2048);
                }
            }
        }

        // ═══════════════════════════════
        // BUILD RAW SECTOR
        // Unchanged from your original
        // ═══════════════════════════════
        static void BuildRawSector(
            byte[] raw,
            byte[] data,
            int lba)
        {
            Array.Clear(
                raw, 0, SECTOR_2352);

            Array.Copy(
                SYNC_PATTERN, 0,
                raw, 0, 12);

            int minutes =
                (lba + 150) / (60 * 75);
            int seconds =
                ((lba + 150) / 75) % 60;
            int frames =
                (lba + 150) % 75;

            raw[12] = ToBcd(minutes);
            raw[13] = ToBcd(seconds);
            raw[14] = ToBcd(frames);
            raw[15] = 0x02;

            Array.Copy(
                data, 0,
                raw, DATA_OFF,
                SECTOR_2048);
        }

        static byte ToBcd(int val)
        {
            return (byte)(
                ((val / 10) << 4) |
                (val % 10));
        }

        // ═══════════════════════════════
        // BLOB CLASS
        // Unchanged from your original
        // ═══════════════════════════════
        class Blob
        {
            public int Sector;
            public int Offset;
            public byte[] Data;
        }

        // ═══════════════════════════════
        // BLOB PARSERS
        // GetEmbeddedBlobs and
        // GetEmbeddedBlobsJap unchanged
        // GetEmbeddedBlobsDemo is NEW
        // ═══════════════════════════════
        static List<Blob>
            GetEmbeddedBlobs()
        {
            byte[] raw =
                Convert.FromBase64String(
                    BLOBS_B64);

            var result =
                new List<Blob>();

            int pos = 0;
            while (pos < raw.Length)
            {
                int sec =
                    raw[pos]
                    | (raw[pos + 1]
                       << 8);
                int off =
                    raw[pos + 2]
                    | (raw[pos + 3]
                       << 8);
                int len =
                    raw[pos + 4]
                    | (raw[pos + 5]
                       << 8);
                pos += 6;

                byte[] data =
                    new byte[len];
                Array.Copy(
                    raw, pos,
                    data, 0, len);
                pos += len;

                result.Add(
                    new Blob
                    {
                        Sector = sec,
                        Offset = off,
                        Data = data
                    });
            }
            return result;
        }

        static List<Blob>
            GetEmbeddedBlobsJap()
        {
            byte[] raw =
                Convert.FromBase64String(
                    BLOBS_B64_JAP);

            var result =
                new List<Blob>();

            int pos = 0;
            while (pos < raw.Length)
            {
                int sec =
                    raw[pos] |
                    (raw[pos + 1] << 8);
                int off =
                    raw[pos + 2] |
                    (raw[pos + 3] << 8);
                int len =
                    raw[pos + 4] |
                    (raw[pos + 5] << 8);
                pos += 6;

                byte[] data =
                    new byte[len];
                Array.Copy(
                    raw, pos,
                    data, 0, len);
                pos += len;

                result.Add(new Blob
                {
                    Sector = sec,
                    Offset = off,
                    Data = data
                });
            }
            return result;
        }

        // ═══════════════════════════════
        // NEW - DEMO BLOB PARSER
        // ═══════════════════════════════
        static List<Blob>
            GetEmbeddedBlobsDemo()
        {
            byte[] raw =
                Convert.FromBase64String(
                    BLOBS_B64_DEMO);

            var result =
                new List<Blob>();

            int pos = 0;
            while (pos < raw.Length)
            {
                int sec =
                    raw[pos] |
                    (raw[pos + 1] << 8);
                int off =
                    raw[pos + 2] |
                    (raw[pos + 3] << 8);
                int len =
                    raw[pos + 4] |
                    (raw[pos + 5] << 8);
                pos += 6;

                byte[] data =
                    new byte[len];
                Array.Copy(
                    raw, pos,
                    data, 0, len);
                pos += len;

                result.Add(new Blob
                {
                    Sector = sec,
                    Offset = off,
                    Data = data
                });
            }
            return result;
        }

        // ═══════════════════════════════
        // ECC PARSER
        // Unchanged from your original
        // ═══════════════════════════════
        static Dictionary<int, byte[]>
            GetEmbeddedEcc()
        {
            byte[] raw =
                Convert.FromBase64String(
                    ECC_B64);

            var result =
                new Dictionary<int,
                    byte[]>();

            int pos = 0;
            while (pos < raw.Length)
            {
                int sec =
                    raw[pos]
                    | (raw[pos + 1]
                       << 8);
                pos += 2;

                byte[] ecc =
                    new byte[ECC_LEN];
                Array.Copy(
                    raw, pos,
                    ecc, 0, ECC_LEN);
                pos += ECC_LEN;

                result[sec] = ecc;
            }
            return result;
        }

        // ═══════════════════════════════
        // BUILD LOGO SECTOR
        // Unchanged from your original
        // ═══════════════════════════════
        static byte[] BuildLogoSector(
            int lba,
            List<Blob> blobs,
            byte padding = PADDING)
        {
            byte[] data =
                new byte[DATA_LEN];

            for (int i = 0;
                 i < DATA_LEN; i++)
                data[i] = padding;

            foreach (var b in blobs)
            {
                if (b.Sector != lba)
                    continue;
                int copyLen = Math.Min(
                    b.Data.Length,
                    DATA_LEN - b.Offset);
                if (copyLen > 0 &&
                    b.Offset >= 0 &&
                    b.Offset < DATA_LEN)
                {
                    Array.Copy(
                        b.Data, 0,
                        data, b.Offset,
                        copyLen);
                }
            }
            return data;
        }

        // ═══════════════════════════════
        // WRITE SECTOR DATA
        // Unchanged from your original
        // ═══════════════════════════════
        static void WriteSectorData(
            FileStream fs,
            int lba,
            byte[] data)
        {
            long pos =
                (long)lba *
                SECTOR_2352 +
                DATA_OFF;
            fs.Position = pos;
            fs.Write(data, 0, DATA_LEN);
        }

        // ═══════════════════════════════
        // PATCH MASTER SECTOR
        // Unchanged from your original
        // ═══════════════════════════════
        static void PatchMasterSector(
            FileStream fs, int lba)
        {
            long dataPos =
                (long)lba *
                SECTOR_2352 +
                DATA_OFF;

            byte[] sec =
                new byte[DATA_LEN];

            fs.Position = dataPos;
            fs.Read(sec, 0, DATA_LEN);

            foreach (var patch
                     in MASTER_PATCHES)
            {
                Array.Copy(
                    patch.data, 0,
                    sec,
                    patch.offset,
                    patch.data.Length);
            }

            fs.Position = dataPos;
            fs.Write(sec, 0, DATA_LEN);
        }

        // ═══════════════════════════════
        // WRITE ECC
        // Unchanged from your original
        // ═══════════════════════════════
        static void WriteEcc(
            FileStream fs,
            int lba,
            byte[] ecc)
        {
            long pos =
                (long)lba *
                SECTOR_2352 +
                ECC_START;
            fs.Position = pos;
            fs.Write(ecc, 0, ECC_LEN);
        }

        // ═══════════════════════════════
        // EDC TABLE + COMPUTE EDC
        // Unchanged from your original
        // ═══════════════════════════════
        static uint[] edcTable;

        static void InitEdcTable()
        {
            if (edcTable != null)
                return;
            edcTable = new uint[256];
            for (uint i = 0;
                 i < 256; i++)
            {
                uint e = i;
                for (int j = 0;
                     j < 8; j++)
                {
                    if ((e & 1) != 0)
                        e = (e >> 1)
                            ^ 0xD8018001u;
                    else
                        e = e >> 1;
                }
                edcTable[i] = e;
            }
        }

        static void ComputeEdc(
            FileStream fs, int lba)
        {
            InitEdcTable();

            long secStart =
                (long)lba *
                SECTOR_2352;

            byte[] sector =
                new byte[SECTOR_2352];

            fs.Position = secStart;
            fs.Read(sector, 0,
                    SECTOR_2352);

            uint edc = 0;
            for (int i = 16;
                 i < 2072; i++)
            {
                edc = (edc >> 8)
                    ^ edcTable[
                        (edc ^
                         sector[i])
                        & 0xFF];
            }

            sector[2072] =
                (byte)(edc & 0xFF);
            sector[2073] =
                (byte)((edc >> 8)
                       & 0xFF);
            sector[2074] =
                (byte)((edc >> 16)
                       & 0xFF);
            sector[2075] =
                (byte)((edc >> 24)
                       & 0xFF);

            fs.Position = secStart;
            fs.Write(sector, 0,
                     SECTOR_2352);
        }

        // ═══════════════════════════════
        // DETECT SECTOR SIZE
        // Unchanged from your original
        // ═══════════════════════════════
        static int DetectSectorSize(
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
                return SECTOR_2352;

            long size =
                new FileInfo(path)
                    .Length;

            if (size % SECTOR_2048
                == 0)
                return SECTOR_2048;

            return 0;
        }

        // ═══════════════════════════════
        // EMBEDDED DATA
        // BLOBS_B64 and ECC_B64 are
        // YOUR EXISTING values -
        // paste them back in here
        // exactly as they were!
        // BLOBS_B64_JAP is also YOUR
        // EXISTING value - paste it
        // back exactly as it was!
        // BLOBS_B64_DEMO is NEW -
        // paste the output from
        // compress_demo_logo.py here
        // ═══════════════════════════════

        const string BLOBS_B64 =
       "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";

        const string ECC_B64 =
            "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";

        const string BLOBS_B64_JAP =
            "AAAvBBsABqEg4AMjQmKiTY2sjE7uLgkoCAgo6U5tgEbFAACvBR0Ai5EQ0HMzUnKSsv2cnF7ePhkYeHgY+b9d8JeoTUcAAC8HHwBSurq6urq6urq6urq6urq6urq6urq6urq6mjiylUJlAQCvAB8AUrq6urq6urq6urq6urq6urq6urq6urq6urq6uvuz4wEALwIgAFK6upqampr62tra2tra2tra2tr6urq6urq6urq6uvHnAQCvAyAAi/HR9jY21nYXFxcXFxcXFxcXF5eRffi6urq6urq6e8sBAAUEEgAnp4eHh4eHh4eHh4eHh4dHBAUBACQECADkRoeHh6fHZQEAdQQLAGWFBKRH5ERExcUFAQDOBAgAZSenh4eHRoQBAC8FIQAGgcGG5uaGJoeHh4eHh4eHh4eHZoENK5+6urq6urq63iYBAIUFFgAKlxcXFxcXFxcXFxcXFxcUNUjMwwYlAQCkBQgAD7cXFxeXdaUBAPIFEQDkQO1pStV0FLVVqkuIie7OZgEATgYIAMUKlxcXF7duAQDFBgsAhSk4urq6urq6+k8BAAUHGAAfutra2tra2tra2tra2toaepnc01aIg+UBACQHCADUutra2rq+xAEAcAcTAAdv1VD9vht6Ghp6WrvYGNne3uIBAM4HCAClf7ra2tq6FgIARgAKAMS3urq6urq6uooCAIUAGQDfurq6urq6urq6urq6urq6urq6upo48+7FAgCkAAgAlLq6urq6GcQCAO8AFABBNV+7urq6urq6urq6urq6urq6TAIATgEIAKXfurq6urrWAgDHAQkAYlq6urq6urpRAgAFAhkAP7q6urq6md29vb29nTxe+Jq6urq6urpe7AIAJAIIAJS6urq6unkkAgBuAhUA5Hd7urq6urqamby9vZ0cnH//fv6CAgCLAggAIC1NTU0tA2UCALsCCAAlYg1NTU3NwQIAzgIIAKXfurq6urrWAgBHAwkAR1m6urq6urrzAgCFAxoAP7q6urq60M2tra2trcxOSzBburq6urq6fGECAKQDCACUurq6urp5JAIA7gMVAA1burq6urpa0ImsTK2tDIxv727uBgIACwQIAHU9XV1dHfcFAgA7BAgAZDB9XV1d3SgCAE4ECACl37q6urq69gIAxwQJACWdurq6urq6fQIABQUHAD+6urq6uvQCABQFCwAFwGr+urq6urq6qAIAJAUIAJS6urq6unkkAgBtBQoARBG6urq6urg3gAIAiwUIADC6urq6ur0lAgC7BQgAxDm6urq6urQCAM4FCADllNAwMDDwaAIASAYIABK6urq6urqdAgCFBgcAP7q6urq6lAIAlgYKAGSKurq6urq6smQCAKQGCACUurq6urp5JAIA7QYJAMD4urq6urpwBgIACwcIABC6urq6up0lAgA7BwgAJHm6urq6upQCAE4HCABlweAgICCAJgIAyAcIAHK6urq6urqdAwAFAAcAP7q6urq6lAMAFwAJAAEZurq6urp7QgMAJAAIAJS6urq6unkkAwBtAAgALrq6urq6+g8DAIsACAAQurq6urr9JQMAuwAIACR5urq6urr0AwBHAQkAJf26urq6urqdAwCFAQcAP7q6urq6lAMAlwEJAAUxurq6urq6qwMApAEIAJS6urq6unkkAwC0ARAABaUEpKTHp4cmhuaGxmeFZQMAzAEJAIWG5ubm5uaGJAMA5AEHAMem5ubmhqUDAO0BCAAqurq6urrbYQMACwIPABC6urq6utymxubm5oYmBQMAHwIQAGWlZKSkZ4eHBobm5ubnRAUDADsCDwAkebq6urq6Fkbm5ubmpucDAE4CCABlRobm5uamBwMAYwINAGUEJmADYmJiwyCmhGUDAH0CFABlpaUkR6SkJ6eHh2bm5ubm5ocEBQMAxwIJAGe+urq6urq6XQMABQMHAD+6urq6upQDABgDCACourq6urq6FgMAJAMIAJS6urq6unkkAwA0AxIA5orVdHS0NxdW1jbWdvQKLiJHAwBMAwkABPX2NjY2NpZPAwBjAwgApaqWNjb2lGcDAG0DCABXurq6uroYpAMAiwMPABC6urq6unjWFjY2NvYXBAMAnwMSAERK1XR0NBcXt9Y2NjZ3qumNZgMAuwMPACR5urq6uro/lzY2NjaWSwMAzgMIAOV1ljY2NpauAwDhAxIAp6KuVXZwE1JSUtMwlrWJTGEFAwD9AxcAhWsV1RR0dJQ3FxeXNjY2NjY3FUgspgUDAEcECQAtu7q6urq6unMDAIUEBwA/urq6urqUAwCYBAgAr7q6urq6unADAKQECACUurq6urp5JAMAtAQTAEL7GhoaOtra+pqamvo6Ox4yFIIDAMwECgDlkbqampqaulPFAwDjBAgAzTi6mpq68wQDAO0ECAA2urq6urrYRwMACwUPABC6urq6urqampqamrq4pAMAHwUUAKR4GhoaGtra+pqampramzn9Vm/EAwA7BQ8AJHm6urq6urqampqamrqzAwBOBQgApT+6mpqaujYDAF8FFQBELPeynpuaurq6urq6upp6+b1RzocDAH0FGADEPjp6GhoaOtra2vqampqamtq7mRy2zmcDAMYFCgCGkLq6urq6urrXAwAFBgcAP7q6urq6lAMAGAYIAMy6urq6urowAwAkBggAlLq6urq6eSQDADQGFAAi+rq6urq6urq6urq6urq6uhryYwMATQYJAG76urq6urpYhgMAYgYIAAVxurq6uprpAwBtBggA9rq6urq628YDAIsGDwAQurq6urq6urq6urq6W6QDAJ8GFQCkW7q6urq6urq6urq6urq6urp/SCUDALsGDwAkebq6urq6urq6urq6ulIDAM4GCACl37q6urq61gMA3gYXACXJHPq6urq6urq6urq6urq6urq6HjWkAwD9BhkAxBm6urq6urq6urq6urq6urq6urq6uh5KRAMARQcLAOR1erq6urq6uroIAwCFBwcAP7q6urq6lAMAmAcIACy6urq6uroQAwCkBwgAlLq6urq6eSQDALQHFQCD2dl53t6//3+cXpi6urq6urq6kAEDAM0HCQDGWbq6urq6+u4DAOIHCADmGbq6urofpgMA7QcIABa6urq6ujpCBAALAA8AELq6urq6mDNzExMTM7EkBAAfABUAhB75Of7eXv8/nJ9Y2rq6urq6ut4CBAA7AA8AJHm6urq6uv6QExMTE9NVBABOAAgApd+6urq6utYEAF4AGADMPrq6urq6uvoZvbJdX9i6urq6urr4S2QEAH0AGQAkebq6urq6WxlZ3r/fvN/5Orq6urq6urksBAC1ABsABYRh4COjYmKDAwPAIEMibxU5urq6urq6urgDBAAFAQcAP7q6urq6lAQAGAEIAC+6urq6urpxBAAkAQgAlLq6urq6eSQEADQBFQAGSIkpzs6v72+sbiuRWLq6urq6+moEAE0BCgAFV7q6urq6ujJnBABiAQcAjvq6urr6qAQAbQEIANS6urq6urrrBACLAQ8AELq6urq6X8NjAwMDI6AlBACfARYApcmp6Y7OTu/PjI8IF366urq6uroxpQQAuwEPACR5urq6uroxgAMDAwPDwQQAzgEIAKXfurq6urrWBADdARkAAHy6urq6urrZtymtTW1PS1M7urq6urp5DAQA/QEaACR5urq6urr2KQnOTu9Pz6k0X7q6urq6uhLHBAAzAh0Ah01ItVHwE7NScpMTczAQsDJ/+7q6urq6urraFmQEAIUCBwA/urq6urqUBACYAggASbq6urq6uvQEAKQCCACUurq6urp5JAQAtAIEAGUlBWUEAL8CCwDloSq4urq6urq8RwQAzgIJAKB5urq6urq7TAQA4QIIAIWwurq6uh7GBADtAgsAS7q6urq6uryvxoUEAAsDCAAQurq6urr9JQQAHwMFAGUlBQVlBAAqAwsABeeuv7q6urq6OCIEADsDCAAkebq6urq69AQATgMIAKXfurq6urrWBABdAwsANbq6urq6uhnIBmUEAGwDCwDFYza6urq6urqwhQQAfQMIACR5urq6urqvBACGAwEAZQQAjAMLAGVHbxy6urq6upsuBACxAx4A5y8XXZl6urq6urq6urq6urq6urq6urq6urq6mvAgBAAFBAcAP7q6urq6lAQAGAQIAAu6urq6urqJBAAkBAgAlLq6urq6eSQEAEEECQAnMbq6urq6u20EAE4ECQBlUbq6urq6uvQEAGEEBwCj2Lq6uroUBABtBBMAjdq6urq6urqfFqvuDMLDoKHHZQQAiwQIABC6urq6up0lBACsBAkA5euaurq6uro1BAC7BAgAJHm6urq6upQEAM4ECACl37q6urq61gQA3AQKAGT8urq6urq6t0QEAO4ECQAgWLq6urq62AEEAP0ECAAkebq6urq6jwQADwUIAO4aurq6uroRBAAwBR4ApHUf2rq6urq6urq6urq6urq6urq6urq6urq6G5BDBACFBQcAP7q6urq6lAQAlwUJACURurq6urq4wAQApAUIAJS6urq6unkkBADCBQgArTq6urq6uosEAM8FCQDvOrq6urq6/CcEAOAFCABld7q6urpZgAQA7QUVAKdcurq6urq6upp7/nwy05CRV+kjxQQACwYIABC6urq6up0lBAAtBggAATi6urq6unAEADsGCAAkebq6urq6lAQATgYIAKXfurq6urrWBABcBgkAYDu6urq6ujpMBABuBgkAJZC6urq6upoPBAB9BggAJHm6urq6uo8EAI8GCQAn3rq6urq6fSUEAK8GHgAnSti6urq6urq6urraGjra+pqaurq6uvoa2N+wykMEAAUHBwA/urq6urqUBAAXBwkAgDm6urq6uvKFBAAkBwgAlLq6urq6eSQEAEIHCAAGmLq6urq6NAQATwcJAKd9urq6urqb7gQAYAcIAAb+urq6uhdlBABuBxUArzi6urq6urq6urq6urq6uto5EwgHBACLBwgAELq6urq6nSUEAK0HCQDlHLq6urq6MmUEALsHCAAkebq6urq6lAQAzgcIAKXfurq6urrWBADcBwkAr7q6urq6uvmnBADvBwgAjvq6urq6urQEAP0HAwAkeboFAAAABQC6urq6jwUADwAJAGUzurq6urp/hQUALwAdAIh4urq6urq6+n5SERd0Vxe31tYx0dExVlRrz6AEBQCFAAcAP7q6urq6lAUAlgAJACARurq6urq6awUApAAIAJS6urq6unkkBQDCAAgAxNm6urq6upQFANAACQCO2rq6urq6EuUFAOAABwCO+rq6ujuCBQDuABYAxOp4urq6urq6urq6urq6urq6uvkLBQUACwEIABC6urq6up0lBQAuAQgAcrq6urq63QUFADsBCAAkebq6urq6lAUATgEIAKXfurq6urrWBQBcAQkAdbq6urq6uh3FBQBvAQkApvi6urq6urxEBQB9AQgAJHm6urq6uo8FAJABCAC2urq6urr/pQUArgEbACM+urq6urq6mNYOYiGHpOeHBoaGIcHBwSaExQUABQIHAD+6urq6upQFAA8CEACBwmJiQuKvsZu6urq6upwhBQAkAggAlLq6urq6eSQFADcCCQDlR8YB4cHB4cQFAEICCAAkebq6urq6lAUAUAIJAAZ5urq6urqYIQUAXwIIAKRyurq6uv3EBQBvAhUAB4pe2rq6urq6urq6urq6urq6ulniBQCLAggAELq6urq6nSUFAKICCQDF5GZh4cHBoQcFAK4CCABSurq6urq9JQUAuwIIACR5urq6urqUBQDOAggApd+6urq6utYFANsCCQBlMLq6urq6upEFAO8CCQBE/7q6urq6ewYFAP0CCAAkebq6urq6jwUAEAMIANa6urq6ut+lBQAuAwoAFLq6urq6uprXpgUAhQMHAD+6urq6upQFAI8DDwDKMlJSUvKf2rq6urq6We8FAKQDCACUurq6urp5JAUAtAMMAGSDbus0FhHR0dHxowUAwgMIACR5urq6urqUBQDQAwkABba6urq6upopBQDfAwcA7du6urr7yQUA8AMVAOdOd/M8/355GDsamrq6urq6uroQhQUACwQIABC6urq6up0lBQAfBAwAxWCsS7WXUdHR0ZEPBQAuBAgAUrq6urq6nSUFADsECAAkebq6urq6lAUATgQIAKXfurq6urrWBQBbBAkApT+6urq6uro3BQBvBAkABR26urq6ulrABQB9BAgAJHm6urq6uo8FAJAECADWurq6urrfpQUArQQKAOW9urq6urq6/YEFAAUFBwA/urq6urqUBQAPBQ8A0Lq6urq6urq6urqbvciFBQAkBQgAlLq6urq6eSQFADMFDQDgCpNeWzqaurq6urqPBQBCBQgAJHm6urq6upQFAFEFCQAs+7q6urq6U+UFAF4FCABll7q6urqyJwUAcgUTAOeDzO8OCYgKldFeurq6urq6+OEFAIsFCAAQurq6urqdJQUAngUNAOZocJzYGvq6urq6ulUFAK4FCABSurq6urqdJQUAuwUIACR5urq6urqUBQDOBQgApd+6urq6utYFANsFCQCkOLq6urq6uvUFAPAFCAAzurq6urr6IgUA/QUIACR5urq6urqPBQAQBggA1rq6urq636UFAC0GCQDneLq6urq6uksFAIUGBwA/urq6urqUBQCPBg4AkZpaWlpaWlue/VAVrcQFAKQGCACUurq6urp5JAUAsgYOAKPQe7q6urq6urqaurqPBQDCBggAJHm6urq6upQFANEGCQBn07q6urq6OKMFAN4GBwCnPrq6uppoBQD3Bg4AZcWFxOEOPrq6urq6mi8FAAsHCAAQurq6urqdJQUAHQcOAGdX+bq6urq6urqamrqqBQAuBwgAUrq6urq6nSUFADsHCAAkebq6urq6lAUATgcIAKXfurq6urrWBQBbBwkApxu6urq6urpVBQBwBwgAMLq6urq6uu0FAH0HCAAkebq6urq6jwUAkAcIANa6urq6ut+lBQCtBwkAAFq6urq6urpsBgAFAAcAP7q6urq6lAYADwAMAO+VVVVVVVXrSY1AJAYAJAAIAJS6urq6unkkBgAxAA8AZzC6urq6urp4spHW1vbDBgBCAAgAJHm6urq6upQGAFIACABO+rq6urq69AYAXgAHAKzaurq6fkEGAHwACQBl6rq6urq6uosGAIsACAAQurq6urqdJQYAnAAPAGWJG7q6urq6WlwQ9ja2jAYArgAIAFK6urq6up0lBgC7AAgAJHm6urq6upQGAM4ACACl37q6urq61gYA2wAJAIbburq6urq6VQYA8AAIAHC6urq6urpMBgD9AAgAJHm6urq6uo8GABABCADWurq6urrfpQYALQEJAML6urq6urr6IgYAhQEHAD+6urq6upQGAI8BCQDlRERERERE5WUGAKQBCACUurq6urp5JAYAsQEPAG/burq6urreqKKhhuaGBAYAwgEIACR5urq6urqUBgDSAQkAZj66urq6ut8HBgDdAQgAhDC6urq6CmUGAP0BCACBOLq6urq61wYACwIIABC6urq6up0lBgAcAg8AIN+6urq6uph3TCCm5qakBgAuAggAUrq6urq6nSUGADsCCAAkebq6urq6lAYATgIIAKXfurq6urrWBgBbAgkAZju6urq6urpVBgBwAggAELq6urq6uu0GAH0CCAAkebq6urq6jwYAkAIIANa6urq6ut+lBgCtAgkATLq6urq6uhoDBgAFAwcAP7q6urq6lAYAJAMIAJS6urq6unkkBgAwAwoAZRG6urq6utrr5QYAQgMIACR5urq6urqUBgBTAwgAlbq6urq6emwGAF0DBwAtG7q6unlhBgB9AwgAxb26urq6urAGAIsDCAAQurq6urqdJQYAnAMJAEq6urq6urrWZgYArgMIAFK6urq6up0lBgC7AwgAJHm6urq6upQGAM4DCACl37q6urq61gYA2wMJAEf4urq6urq61QYA8AMIAHO6urq6uvriBgD9AwgAJHm6urq6uo8GABAECADWurq6urrfpQYALQQJAA+6urq6urpawAYAhQQHAD+6urq6upQGAKQECACUurq6urp5JAYAsAQJAGS/urq6urpfZgYAwgQIACR5urq6urqUBgDTBAkAAPm6urq6ulHlBgDdBAcA1Lq6urr2ZQYA/gQHAFO6urq6unIGAAsFCAAQurq6urqdJQYAGwUJAGXwurq6urqbYgYALgUIAFK6urq6up0lBgA7BQgAJHm6urq6upQGAE4FCACl37q6urq61gYAWwUJAGQ+urq6urq6lAYAbwUJAGXyurq6uroaYwYAfQUIACR5urq6urqPBgCQBQgA1rq6urq636UGAJ8FBwCEYs9J7OClBgCtBQkAr7q6urq6ulrABgAFBgcAP7q6urq6lAYAJAYIAJS6urq6unkkBgAwBggAATu6urq6uvQGAEIGCAAkebq6urq6lAYAUwYQAAURurq6urp4gQdfurq6++wGAH4GBwDxurq6urqwBgCLBggAELq6urq6nSUGAJsGCQDlPLq6urq6/8QGAK4GCABSurq6urqdJQYAuwYIACR5urq6urqUBgDOBggApd+6urq6utYGANsGCQAls7q6urq6ulEGAO8GCQCFnLq6urq6+2EGAP0GCAAkebq6urq6jwYAEAcIANa6urq6ut+lBgAdBwoABU0qdDSUFNSLgAYALQcJAO66urq6urpawAYAhQcHAD+6urq6upQGAKQHCACUurq6urp5JAYAsAcIAE36urq6urqoBgDCBwgAJHm6urq6upQGANQHDwBPerq6urq6aEl6urq6UwcGAP4HAgDQugcAAAAFALq6urrXBwALAAgAELq6urq6nSUHABsACQAE/rq6urq6EmUHAC4ACABSurq6urqdJQcAOwAIACR5urq6urpXBwBOAAgApd+6urq6utYHAFwACQAUurq6urq60iUHAG8ACQCnubq6urq6fsQHAH0ACAAkebq6urq6jwcAkAAIANa6urq6ut+lBwCcAAwAZYww1oqJSenINnEgBwCtAAkAybq6urq6ulrABwAFAQcAP7q6urq6lAcAJAEIAJS6urq6unkkBwAwAQgAbLq6urq6uksHAEIBCAAkebq6urq6lAcAVAEOACffurq6urqSnbq6urtsBwB9AQgA5Ry6urq6uosHAIsBCAAQurq6urrc5QcAmwEJACR5urq6urrSZQcArgEIAFK6urq6up0lBwC7AQgAJHm6urq6ulYHAM4BCACl37q6urq61gcA3AEJAEm6urq6urqepAcA7wEJAIwaurq6urr2ZQcA/QEIACR5urq6urqPBwAQAggA1rq6urq636UHABwCDQBD/auv8NdXNqrj1nEnBwAtAgkAybq6urq6ulrABwCFAgcAP7q6urq6lAcApAIIAJS6urq6unkkBwCwAggA4xq6urq6uvQHAMICCAAkebq6urq6lAcA1QINAJW6urq6uptaurq6vKQHAP0CCACBmLq6urq6TgcACwMIADC6urq6uv9EBwAbAwkARJ+6urq6ur/kBwAuAwgAUrq6urq6nSUHADsDCADEGbq6urq6lgcATgMIAKXfurq6urrWBwBcAwkAQhq6urq6ulrDBwBvAwgA97q6urq6uskHAH0DCAAkebq6urq6jwcAkAMIANa6urq6ut+lBwCcAw0AlZwkz9rLrndb4GC5DAcArQMJAMm6urq6urpawAcABQQHAD+6urq6upQHACQECACUurq6urp5JAcAMAQJAOeZurq6urq8xgcAQgQIACR5urq6urqUBwBVBAwA4Fm6urq6urq6unpLBwB9BAgAi7q6urq6u2MHAIsECABQurq6urpZJAcAmwQJACUyurq6urqb4gcArgQIAFK6urq6up0lBwC7BAgAJL66urq6unAHAM4ECACl37q6urq61gcA3AQJAAceurq6urq6VQcA7gQJAOafurq6urobYwcA/QQIACR5urq6urqPBwAQBQgA1rq6urq636UHABsFAwAlsjUHAB8FBwBu2s4tCnsDBwAnBQIAfLUHAC0FCQDJurq6urq6WsAHAIUFBwA/urq6urqUBwCkBQgAlLq6urq6eSQHALAFCgDFcrq6urq6+hXnBwDCBQgAJHm6urq6upQHANUFDABlt7q6urq6urq6XGYHAPsFCgCFTd+6urq6un/kBwALBggA9rq6urq6W+YHABwGCgDUurq6urq68eBlBwAuBggAUrq6urq6nSUHADsGCQClP7q6urq63eUHAE4GCACl37q6urq61gcAXAYLAGU3urq6urq6fi8EBwBtBgoA5pXaurq6urrSJAcAfQYIACR5urq6urqPBwCQBggA1rq6urq636UHAJsGAwCFvA4HAJ8GBwBOurdVEngmBwCnBgIAc7YHAK0GCQDJurq6urq6WsAHAAUHBwA/urq6urqUBwAkBwgAlLq6urq6eSQHADEHGQALurq6urq6mbTu7YAhZiFDAswYurq6urqUBwBWBwoAjbu6urq6urq6KwcAbgcWACWAoSGn5wREpYVkYc0rspq6urq6usoHAIsHDQDUurq6urraaiblpaTEBwCcBxoAjfu6urq6ujvQiWzj4SamAEIN3Lq6urq6nSUHALsHDgAlPbq6urq6GG+khaVnpQcAzgcIAKXfurq6urrWBwDdBxkAo566urq6urof6oxAh0ZCzhZ4urq6urpajgcA/QcDACR5uggAAAAFALq6urqPCAAQAAgA1rq6urq636UIABsAAwAlfVUIAB8ABwBu2k6CKjsACAAnAAIAvRQIAC0AIgDJurq6urq6WkMFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYXlCACFAAcAP7q6urq6lAgApAAIAJS6urq6unkkCACxABkAgd66urq6uroa3t3wEZcRUxKderq6urq6lAgA1gAKAKTSurq6urq6+CAIAO4AFgAkkZEx13cVVSpKdbY9+Lq6urq6uj8BCAALAQ0AiLq6urq6uhm36oq0DAgAHAEaAKSxurq6urq6utlc0/FW9hBSHbi6urq6up0lCAA8AQ0AsLq6urq6utwU6lXUAAgATgEIAKXfurq6urrWCABdARkAxevburq6urq6O/xQN/ezPtq6urq6uvrx5wgAfQEIACR5urq6urqPCACQAQgA1rq6urq636UIAJwBBgDUfATP+4YIAKMBBgADmw0h+U8IAK0BIwDpurq6urq6OpQLi4uLi4uLi4uLi4uLi4uLi4uLi4uLiwoJJQgABQIHAD+6urq6upQIACQCCACUurq6urp5JAgAMQIZAAUJvLq6urq6urq6urr6urq6urq6urq6upQIAFcCCQAOGrq6urq6saUIAG4CFQCkW7q62tpaWtsbWpq6urq6urq6fkkIAIsCDQCAPrq6urq6utq7u7qKCACdAhkAA9I6urq6urq6urq6+pq6urq6urq6urqdJQgAvAINAAgburq6urq6Grt6Oq0IAM4CCACl37q6urq61ggA3gIXAKcU/rq6urq6urq62vq6urq6urq6ONHgCAD9AggAJHm6urq6uo8IABADCADWurq6urrfpQgAHAMGACPdq+y1RwgAIwMGAOeUSfcRhwgALQMjAAm6urq6urq6O1tbW1tbW1tbW1tbW1tbW1tbW1tbW1tb21ylCACFAwcAn7q6urq6lAgApAMIAJS6urq6ujnECACyAxgAhU/WvVkb+rq6urq6urq6urq6urq6urqUCADXAwgA5366urq6mYIIAO4DFQCkW7q6urq6urq6urq6urqauJwRDsQIAAsEDQBlaF8aurq6urq6urpVCAAeBBgAwvTyfpg6urq6urq6urq6urq6urq6up0lCAA8BA0ARtQZmrq6urq6urq6TAgATgQIAET/urq6urr2CABfBBUAh2lwPxu6urq6urq6urq6ulo+0yuBCAB9BAgAxDm6urq6uq8IAJAECAD2urq6urr/RAgAnAQMAGVP8LeJwsMNixBxIAgArQQjAK3aurq6urq6urq6urq6urq6urq6urq6urq6urq6urq6ut+lCAAFBQcA8F+cnJw/KwgAJAUIACs/nJycX7MECAAzBRcAZcataUr3kNLdvb293V1dshNzExAxkS4IAFcFCADFXLq6urpQxQgAbgUUACSRk12yPb293JycnPy90pbrrCFlCACMBQwA5G93HZycnBydvTwJCACfBRcAB+IOC/RQcj29vb39XV1d83MT0DHxqgUIAL0FDAAGKTF8nJycfJ29nYMIAM4FCACFMF+cnJwfiggA4AUTAMVgLwqQPH55WBgYGR68k9XOw0QIAP0FCAAEs1+cnJy8jQgAEAYIAIofnJycXzCFCAAdBgoABU2V0PExUFBKgAgALQYjAEZfurq6urq6urq6urq6urq6urq6urq6urq6urq6urq6uv+lCACFBgcAbW+MjIwvwAgApAYIAMAvjIyMT+3FCAC2BhQAZYVGoMLNTK1M7U1NoiMDIwAhoccIANcGBwAAeLq6unpOCADuBhEAJYCjTU0tTK3srIyMjK3ipuUIAA4HCgCnDayMrCytrcwBCAAhBxUAZcUHYALNra1MjU1NTeNjI+DBgcZlCAA+BwsAZcFsrIyMDK1MrQcIAE4HCAAFbW+MjIwvQwgAYwcNAKWgzG4JyIiIyS5Po8QIAH0HCADF7U+MjKxPJggAkAcIAEMvjIyMb20FCACfBwcA54/IK6mtZAgArgciAK8TXjk5OTk5OTk5OTk5OTk5OTk5OTk5OTk5OTk5OTmZ8qUJAFYACADldLq6uroypwkA5wAFAGUlxcUFCQAhAQMABcVlCQAuASIAJQNu6enp6enp6enp6enp6enp6enp6enp6enp6enp6UgvJQkA1gEHAKA5urq6Gi8JALECHgAFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBWUJAFYDBwAqurq6up8HCQDVBAcABny6urq6dAkAVQYHAGt6urq6OQMJANQHCAAHf7q6urrU5QoAVAEHAGx6urq6WyMKANMCCAAFNrq6urpzBAoAUwQHAGLZurq6OEwKANIFCAAFFrq6urpzpQoAUgcHAEEZurq6+mkLANEACAAFCpq6uro+xgsAUQIHACZ92Tk52eoLANEDBwBmickpyelB";

        const string BLOBS_B64_DEMO = "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";
    }
}
