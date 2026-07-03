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
        // MAIN COMMAND
        // ═══════════════════════════════
        public static void PatchIso(
            string isoPath,
            PatchOptions opts = null)
        {
            if (!File.Exists(
                    isoPath))
                throw new
                    FileNotFoundException(
                    "File not found",
                    isoPath);

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═════════════════════" +
                "════════════════════");
            Console.WriteLine(
                " fixps2logo");
            Console.WriteLine(
                "═════════════════════" +
                "════════════════════");
            Console.ResetColor();

            int sectorSize =
                DetectSectorSize(
                    isoPath);

            Console.WriteLine(
                "  Format: " +
                sectorSize +
                "-byte sectors");

            if (sectorSize ==
                SECTOR_2352)
            {
                // BIN format -
                // patch directly
                PatchBinFile(
                    isoPath);
            }
            else if (sectorSize ==
                     SECTOR_2048)
            {
                // ISO format -
                // patch via temp
                // BIN then convert
                // back to preserve
                // original format
                PatchIsoFile(
                    isoPath);
            }
            else
            {
                Console.ForegroundColor
                    = ConsoleColor.Yellow;
                Console.WriteLine(
                    "  ERROR: Unknown" +
                    " format!");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine(
                "  Done!");
            Console.ResetColor();
        }

        // ═══════════════════════════════
        // PATCH BIN FILE (2352 bytes)
        // Direct in-place patching
        // ═══════════════════════════════
        static void PatchBinFile(
            string path)
        {
            var blobs =
                GetEmbeddedBlobs();
            var eccData =
                GetEmbeddedEcc();

            Console.WriteLine(
                "  " +
                blobs.Count +
                " logo blobs" +
                " (embedded)");
            Console.WriteLine(
                "  " +
                eccData.Count +
                " ECC entries" +
                " (embedded)");

            using (var fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite))
            {
                DoPatchWork(
                    fs, blobs,
                    eccData);
            }
        }

        // ═══════════════════════════════
        // PATCH ISO FILE (2048 bytes)
        // 1) Convert to temp BIN
        // 2) Patch the BIN
        // 3) Convert back to ISO
        // 4) Preserves original size
        // ═══════════════════════════════
        static void PatchIsoFile(
            string isoPath)
        {
            string tempBin =
                isoPath + ".tmp.bin";

            Console.WriteLine(
                "  Converting ISO" +
                " to temp BIN...");

            // Step 1: Convert ISO
            // to BIN (temporary)
            Convert2048To2352(
                isoPath, tempBin);

            // Step 2: Patch the
            // temp BIN
            Console.WriteLine(
                "  Patching temp" +
                " BIN...");

            PatchBinFile(tempBin);

            // Step 3: Convert
            // patched BIN back to
            // ISO (overwrite)
            Console.WriteLine(
                "  Converting" +
                " patched BIN back" +
                " to ISO...");

            Convert2352To2048(
                tempBin, isoPath);

            // Step 4: Cleanup
            File.Delete(tempBin);

            Console.WriteLine(
                "  ISO format" +
                " preserved (2048" +
                " bytes/sector)");
        }

        // ═══════════════════════════════
        // CORE PATCH WORK
        // ═══════════════════════════════
        static void DoPatchWork(
            FileStream fs,
            List<Blob> blobs,
            Dictionary<int, byte[]>
                eccData)
        {
            Console.WriteLine();
            Console.WriteLine(
                "  [1/4] Logo" +
                " sectors 0-11" +
                "...");

            for (int lba = 0;
                 lba <= 11; lba++)
            {
                byte[] sec =
                    BuildLogoSector(
                        lba, blobs);
                WriteSectorData(
                    fs, lba, sec);
            }

            Console.WriteLine(
                "  [2/4] Master" +
                " markers 14-15" +
                "...");

            for (int lba = 14;
                 lba <= 15; lba++)
            {
                PatchMasterSector(
                    fs, lba);
            }

            Console.WriteLine(
                "  [3/4] EDC" +
                " recalc...");

            for (int lba = 0;
                 lba <= 15; lba++)
            {
                if (lba == 12 ||
                    lba == 13)
                    continue;
                ComputeEdc(
                    fs, lba);
            }

            Console.WriteLine(
                "  [4/4] ECC" +
                " write...");

            foreach (var kvp
                     in eccData)
            {
                WriteEcc(
                    fs,
                    kvp.Key,
                    kvp.Value);
            }
        }

        // ═══════════════════════════════
        // CONVERT 2048 → 2352
        // (adds sync + headers)
        // ═══════════════════════════════
        static void
        Convert2048To2352(
            string inPath,
            string outPath)
        {
            long origSize =
                new FileInfo(
                    inPath).Length;
            long numSectors =
                origSize /
                SECTOR_2048;

            using (var input =
                File.OpenRead(
                    inPath))
            using (var output =
                File.Create(
                    outPath))
            {
                byte[] dataBuf =
                    new byte[
                        SECTOR_2048];
                byte[] rawBuf =
                    new byte[
                        SECTOR_2352];

                for (long lba = 0;
                     lba <
                     numSectors;
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
        // (strips sync + headers,
        //  keeps only user data)
        // ═══════════════════════════════
        static void
        Convert2352To2048(
            string inPath,
            string outPath)
        {
            long origSize =
                new FileInfo(
                    inPath).Length;
            long numSectors =
                origSize /
                SECTOR_2352;

            using (var input =
                File.OpenRead(
                    inPath))
            using (var output =
                File.Create(
                    outPath))
            {
                byte[] rawBuf =
                    new byte[
                        SECTOR_2352];
                byte[] dataBuf =
                    new byte[
                        SECTOR_2048];

                for (long lba = 0;
                     lba <
                     numSectors;
                     lba++)
                {
                    input.Read(
                        rawBuf, 0,
                        SECTOR_2352);

                    // Extract only
                    // user data
                    // (skip sync +
                    // header + EDC
                    // + ECC)
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

        static void
        BuildRawSector(
            byte[] raw,
            byte[] data,
            int lba)
        {
            Array.Clear(
                raw, 0,
                SECTOR_2352);

            Array.Copy(
                SYNC_PATTERN, 0,
                raw, 0, 12);

            int minutes =
                (lba + 150)
                / (60 * 75);
            int seconds =
                ((lba + 150) / 75)
                % 60;
            int frames =
                (lba + 150) % 75;

            raw[12] = ToBcd(
                minutes);
            raw[13] = ToBcd(
                seconds);
            raw[14] = ToBcd(
                frames);
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

        class Blob
        {
            public int Sector;
            public int Offset;
            public byte[] Data;
        }

        static List<Blob>
        GetEmbeddedBlobs()
        {
            byte[] raw =
                Convert.FromBase64String(
                    BLOBS_B64);

            var result = new
                List<Blob>();

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

        static
        Dictionary<int, byte[]>
        GetEmbeddedEcc()
        {
            byte[] raw =
                Convert.FromBase64String(
                    ECC_B64);

            var result = new
                Dictionary<int,
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
                    ecc, 0,
                    ECC_LEN);
                pos += ECC_LEN;

                result[sec] = ecc;
            }
            return result;
        }

        static byte[]
        BuildLogoSector(
            int lba,
            List<Blob> blobs)
        {
            byte[] data = new byte[
                DATA_LEN];

            for (int i = 0;
                 i < DATA_LEN; i++)
                data[i] = PADDING;

            foreach (var b in blobs)
            {
                if (b.Sector != lba)
                    continue;
                int copyLen =
                    Math.Min(
                        b.Data.Length,
                        DATA_LEN -
                        b.Offset);
                if (copyLen > 0 &&
                    b.Offset >= 0 &&
                    b.Offset <
                    DATA_LEN)
                {
                    Array.Copy(
                        b.Data, 0,
                        data,
                        b.Offset,
                        copyLen);
                }
            }
            return data;
        }

        static void
        WriteSectorData(
            FileStream fs, int lba,
            byte[] data)
        {
            long pos =
                (long)lba *
                SECTOR_2352 +
                DATA_OFF;
            fs.Position = pos;
            fs.Write(data, 0,
                     DATA_LEN);
        }

        static void
        PatchMasterSector(
            FileStream fs, int lba)
        {
            long dataPos =
                (long)lba *
                SECTOR_2352 +
                DATA_OFF;

            byte[] sec = new byte[
                DATA_LEN];

            fs.Position = dataPos;
            fs.Read(sec, 0,
                    DATA_LEN);

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
            fs.Write(sec, 0,
                     DATA_LEN);
        }

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
            fs.Write(ecc, 0,
                     ECC_LEN);
        }

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

        static int
        DetectSectorSize(
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
        // EMBEDDED DATA - KEEP YOUR
        // BASE64 VALUES!
        // ═══════════════════════════════

        const string BLOBS_B64 =
           "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";

        const string ECC_B64 =
            "AADk5OTk5CQnuukBMGhorTtmPj6O7Cl4hyAgh720kumJONmRcnJycnJycnJycnKvHh4eHh5l6oSe8gKGdmwiOEGxpLOpwbhItXVTDJMC9JVycnJye4TTYDk5OTk5Ny4yoeqbol96zN7n55V3UiTaHR3aAU6KXFvV0ZNycnJycnJycnJycrjh4eHh4dnxHsRLm36udNUPV4dQ5z1yKvonZQjXvOf3FXJycnJ81kdu+cnfVACCMawutVB9BGWCaeM4zeCziy9bV2vfF6SMXNrgUIu6/eNGUksQ8Vvo2OsDg9vHRQWO3bEw97ljIt/bsY8uWS+PuopCP1GgL2CXlq1OdR9c9hb2IuJzGjRuLiuXccPMo482zM0BAKgOPpWIkpKSkjAhDG2ZmXGd6d3HH8uefimWXsPxATxj2Kt7YSXow3PWtE+M0vaagnL2AEB2cHC+zB57e3t71LGKecIpKD/UD0j65JcdWCL9nDm/9yg5TLjAEqrQ0NDQ3jUYg7e3qAwZ7ZesPWcCCVbvBpsuWuGYMZU0GzQ/4iWRFrJlMxiicvoWlueBgTgCcTQ0NDT7voZUkv0SJ2P+rJ8MuxLIk+OQFZEWN6H253HpNxndtt4csteS8gmZFfGLcCOnnQ7GgvWvvVUOeBCF2smu9w+FQOVD+dDRfUGJAXaaD9kzP8OTJC/+W/IRaqi2A+1gF87gwYQgYyQY9c3xqX49B9Ce2bhBpUaw7s7Xu9aA25iN8wIAX7hxqIv69Ks88zYNBhmu9pEbGlpCNK0JZ9KD3Ye0jyc0ZuTvEs2pHY6i5hMyqj4wSlSW2buctk6QhdmBUs8RsgKeuNYO042ErRgBAfzj8ChZi9J8GkrLrGYJb92V8+EXSK4PLa7hdn2BYX2Bqutm+G4xzRwLR13+HxCpkvKvJAtKXCcHZ5OPfW78gzdJxJDk5Y4luvFzmPP8fMfBAYRwmwEB3KNDUaSO6BBLALjMcx0F5kDaGwdPK1sK6XrSHeVhQdEviN7GrypehxbzZMH/wICR/D3quB6P1TJVTwqOkWXYuf6bs834JTU3HgeP+bPHixEevABD27KxNSS8rK2Am5jO2xl1ia8CwSsQKG8h44xzrjp7AwBi3rb+4gcpk+0J/TqAPJ4Q4yJ6zfvvnfRDP5AVFS1UcqNTXf1eYxFgTeDWD5CzFWOhxiA080JtsKmnS6FJx5Pw8nd2aFf8w+OSH9RDTF3XfB7fIaH08UEydDaKGyDR7TPKe4aYf182I/gFxwpuu9557RXHEkA3rn7imckp8VSywzmAayRnyTAAb/gTdFMTSxEO78OG/rjD7Hx975p0lcHH97X2UjTeEhcFRl3381MzLvLy5zjvFV4KGTfJdUmDk8FDgJQgrO3HOuVv/E6bMAt2FChDxpLglGGPChx7C3GS12ENM/Lon7y2SxlqBwh8z0AJIhhIHHtjpLY+qGlpo2ifbWgNl57zv7mxTDbGNBA9vzunZR0EAN2sYk+9WXKjA+A8mnSesxWjYFdYlDT0lw2m/D4vuEfqCJ5fMVb4/a70BCD3EqQerdJuqKi/IFbNrfkHplTeF5jO/wN0e4jdShc5Pq068Y/eJi+8z07bEcXZgTSqIYsv9wfc3TF/sIp4XdNgaTFD6/S3bjpEiQwbeW+PZOMsgoD4kzBtdhMxCawRQMIBqTSta4XjdJ/ld1VIkNJlwUpJW79apLrnOc8TC6V1hMoDYewGCfGsoz9MOdwipvByIkbM+V+H4IDZDx8zRZJUGq7A7VWT8JxMch2NH5ovr4TEVVqoMLS22jTAsDgdSE2mILWupeeZ0+guIywfRuaAna9/oYdtd76pHa9D4j+ckduVbppphgqIfwUArrgNuLLY6xXU3MY67U7HVAOL8F7gbKIZHDlrJ9LUEErh4T/xj07avCDwq0bLKASM14acbNmE0lzEoi92AW2Ts4fhAtcM7gLz+jN6ON0TCPU61SMW3/BFIEJXUH+VWwj6HJjFZiLg8UB16WkZijPSOq2naYuB+fNyhDMmjETY+cpmty4mjLS41WrpayNiiJFxLQ+xHMMaNQoW6TyMsN803vI/eyxJcKM3c0/fOnCyd3vjnhqHQ4rNs++bF6BrWqDvKh7Zm/ImEDx284qmGion6WjktfmnFvDAMDKbK5b9JEFBk6njcTLFW7rIiPmsuXcxuKCymFKI+VV5tl3Ab/25wzBx+tg5ldG3LJdK4v1OJhP0pY0LBgCeT6IWJZiRON6Jkh277r2FO8R+8LXYMpuc7bGffqog4HkXF/syOuUmlxtH9wlC+sTPtU937F5Fv5UQ27mruH1zqPsoh6bc0O1Qb2XjsZ/ROALWP2X+OrUMe9DaJhGfJMdPr38PCyvl1r1Zc3W7WIQVfpo/tLR+Q6QTeqqCccg825Zah5x+EPF4er9b9OGtiMGm6ZSU2mWoOQBKk7+gH1kPL1/ylLyDp5WLERcF7JleW6DNnmwZ5TvWnD6QcDIhofOYcDKxf2zcDZoxTTw0LnvOAVTrjSWt05+BtjU66bThUgQvPLgJwlSA3syTYyUZxHSdoKCKxrnAqJjMKPs7VVsXWWDNYqlePtovcKdlph6IYaBeJ+sHACoYGoS1+c+obr0GlEARUaeWHrL6WluIZD6J4/DOAos98JBuC/KUaveYkJLZjT1WYAv6PyeX1jmfRqvWPqawkFBBEsFgCBSdBPsV9dQtKy5GC5dcIagOPTh91Jo/g0kx1C9p5OJTaWKK9KqbDAnSfIWIreFEB6W8+3KlMa8qckL7cDS0jWExsg5ARHI62Ttsv4p9KYb8c0Wb5D8/8+/NFIGCp4Pz0GzAKzwuot+W8tfsCweDml3X8bXcthsK5FEfDCVJmWDZUUx27DNZn+iuolKbHDRxxw3/+u1Ccdr66X61BaOUvIMntX5R0FAx8tj7HYx+O6xCPdgraG4UAdS+kp7ggAb74fInkqTuH9R8CiLMh/D6/ggAH1Dl04Nld3H9kx0Q6la/TR9YaTsx1yaEgbRbKSr8RibQ/y1ek2UbAFASOD1twYLTUrKyjcvcS0ALdHAbx8s8r7UlXEWHsKLJn2vYrUq1FdgUbM6X1rPmsy75PG60D81tfMWtaTnRkxO1cFyAtcjhR+7Cp2d5R/eFt7wIktTA5VEU6stFBFKkyGKZsAm+YcInQlv+PE0eA3eyO0FJf/PIda+1EYvYMpmjW7qGk0G7PVBQ8SEuekzs7wOzzb+vLT/8UYljl3H17BP5XUdKTNMS8KtQG/HxfOnANxZeTGs5IPizdPv7aaDWX57wSvnS4QDQqLpxr24qj/mSiVSGHktup+AM1yAzXLzAL/kdOw1Y/n6pPa8ECQB/tFZz4h3LICAgIM5rjFpaWlpm0f7+/v7+/v7+/v7+/v7+/v7+/jiw4uIorUfB4fb29vboHMrw8NydPo2NjY2NjY2NjY2NjW61JiZWjY2NjY2NnUBd4zGbHjFDhnkCAgICyYn95eXl5VFzvr6+vr6+vr6+vr6+vr6+vr6+OspdXfcSVgLYCQkJCdn5tg8PQ4R/ISEhISEhISEhISEh4nkKCrohISEhISHKXEoyw0meRvqVi/AeqDI3TOxyLrhQJodyt729dXWGKwS4/ninKgd0nx/SULRqtVCwzveDAeLM8FeKGsItEUJChU2Qoar6uz9eA2xoqPuMjEREkG3KVRxzzqLgr2Q1Jt704S8DZIG97p9OwwQKACBNeg4ODju6XE4CApQdvHJycnJycnJycnJycnJycnJycnJycnJycnJDFJCQkJCXOwJZWVlZOIlycnJycnJycnJycnJycnJycnJycnJycnJyTiGlfK9y0bc7LCwsm26orCAgawPacnJycnJycnJycnJycnJycnJycnJycnJycgNHb29vb6GbcaampqYOyHJycnJycnJycnJycnJycnJycnJycnJycnJKfTNWP7TOBMC7oZfx8aPAUoyKihK8C2ls2Z2/7QI8LD43goLn5y0t0KSDBwLqXxmWA8vkIYH/TueFQ8T7+01B09NSZgHbqKjlRDL41DMz4/dvfQ7jzV9fOjrw8MN5ELW2NshK+uqNe6rwinvrRwsA5OTkITWI6+uIXuTkcnJycnJycnJycnJycnJycnJycnJycnJycnJycnJycr3OaUBptJFmMtHR0YwRcnJycnJycnJycnJycnJycnJycnJycnJycpS3Jrg5OTmf0MlKSsksOTlycnJycnJycnJycnJycnJycnJycnJycnJycnJycnJyngLlLOUY1SntLi4u95JycnJycnJycnJycnJycnJycnJycnJycnJyPfTh/tgdltppknZMGUQft01dMK5eTTT8JmNPT2BgtWRpzDT3LQakpKur9fXvR6eaZ3LwHCYij9bGYezAqTwIubhJ+vaQ4yo10bacaLQ4kpK9vcS92J0lpvD4eXl2dr6+g/2FXXuwhwoEfyMuDgDGxsbGxsb19fX1QzRWt1JWAuMURLuNa1Ei1nt1Iq0yu7lEvLXHfgrFzNFSmkc8tb4oNLdHtNHORnLpJVPb0X2OMEbR0dHR0dHR0dHR0dHR0dHR20Evkebm5ubm5vX19fVi69ok2NrwDvvFxwlAnDLEaicyuEK96B3Pcu+/eleC0TyQFGjwpjhQ/hT30b/EJKxCNrXRbIAiVtHR0dHR0dHR0dHR0dHR0dHdMY5agZ7OOc3v3PJwVhLHwjP0BfC4YW94X0t5/rcktgShsbx8Lmm3R9TcRP23CVyyTMZVvil3ImW8ANcz4oh01EuXp3V0PqCGD6+GfgYKWOa5fTbPw3Ljo2Lb/TzBJkpxJ2YhTl/zYJTj8XMPAMbGxsbGxvX19fVDNFa3UlYC4xREu41rUSLWe3UirTK7uUS8tcd+CsXM0VKaRzy1vig0t0e00c5GcuklU9vRfY4wRtHR0dHR0dHR0dHR0dHR0dHbQS+R5ubm5ubm9fX19WLr2iTY2vAO+8XHCUCcMsRqJzK4Qr3oHc9y7796V4LRPJAUaPCmOFD+FPfRv8QkrEI2tdFsgCJW0dHR0dHR0dHR0dHR0dHR0d0xjlqBns45ze/c8nBWEsfCM/QF8Lhhb3hfS3n+tyS2BKGxvHwuabdH1NxE/bcJXLJMxlW+KXciZbwA1zPiiHTUS5endXQ+oIYPr4Z+BgpY5rl9Ns/DcuOjYtv9PMEmSnEnZiFOX/NglOPxcw==";
    }
}
