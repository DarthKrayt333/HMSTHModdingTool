using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// Converts RDTB between all
    /// three formats:
    ///   - BIG    (14 active chunks
    ///             with 3 LOD meshes)
    ///   - SMALL  (10 active chunks
    ///             with 1 mesh,
    ///             slots 9/10/12/13
    ///             = 0xFFFFFFFF)
    ///   - MIRRORED (14 slots, but
    ///               9/10 point to
    ///               same offset as
    ///               8, and 12/13
    ///               point to same
    ///               offset as 11)
    /// </summary>
    public static class RDTBConverter
    {
        public enum RDTBFormat
        {
            BIG,
            SMALL,
            MIRRORED,
            UNKNOWN
        }

        // ═════════════════════════════
        // DETECT FORMAT
        // ═════════════════════════════
        public static RDTBFormat
            DetectFormat(string rdtbPath)
        {
            if (!File.Exists(rdtbPath))
                return RDTBFormat.UNKNOWN;

            byte[] data =
                File.ReadAllBytes(
                    rdtbPath);
            if (data.Length < 0x48 ||
                data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
                return RDTBFormat.UNKNOWN;

            uint[] slots = new uint[14];
            for (int i = 0; i < 14; i++)
            {
                slots[i] =
                    BitConverter.ToUInt32(
                        data,
                        0x10 + i * 4);
            }

            // Check for SMALL:
            // slots 9, 10, 12, 13
            // all = 0xFFFFFFFF
            bool isSmall =
                slots[9] == 0xFFFFFFFF
                && slots[10] == 0xFFFFFFFF
                && slots[12] == 0xFFFFFFFF
                && slots[13] == 0xFFFFFFFF;
            if (isSmall)
                return RDTBFormat.SMALL;

            // Check for MIRRORED:
            // slots 9,10 same as 8
            // AND slots 12,13 same
            // as 11
            bool isMirrored =
                slots[9] == slots[8]
                && slots[10] == slots[8]
                && slots[12] == slots[11]
                && slots[13] == slots[11]
                && slots[11] != 0
                && slots[11] != 0xFFFFFFFF;
            if (isMirrored)
                return RDTBFormat.MIRRORED;

            // Check for BIG:
            // all 14 slots have
            // valid unique offsets
            bool allValid = true;
            for (int i = 0; i < 14; i++)
            {
                if (slots[i] == 0 ||
                    slots[i] ==
                        0xFFFFFFFF ||
                    slots[i] < 0x48 ||
                    slots[i] >
                        (uint)data.Length)
                {
                    allValid = false;
                    break;
                }
            }
            if (allValid)
                return RDTBFormat.BIG;

            return RDTBFormat.UNKNOWN;
        }

        // ═════════════════════════════
        // INFO COMMAND
        // ═════════════════════════════
        public static void ShowFormat(
            string rdtbPath)
        {
            if (!File.Exists(rdtbPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + rdtbPath);
                return;
            }

            RDTBFormat fmt =
                DetectFormat(rdtbPath);

            byte[] data =
                File.ReadAllBytes(
                    rdtbPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Format: " +
                fmt.ToString());
            Console.ResetColor();
            Console.WriteLine(
                "    File: " +
                Path.GetFileName(
                    rdtbPath));
            Console.WriteLine(
                "    Size: " +
                data.Length.ToString(
                    "N0") + " B");
            Console.WriteLine();
            Console.WriteLine(
                "    Slot table:");
            for (int i = 0; i < 14; i++)
            {
                uint v =
                    BitConverter
                        .ToUInt32(data,
                            0x10 + i * 4);
                string tag = "";
                if (v == 0)
                    tag = " (unused)";
                else if (v == 0xFFFFFFFF)
                    tag = " (FFFFFFFF)";
                Console.WriteLine(
                    "      [" +
                    i.ToString("D2") +
                    "] 0x" +
                    v.ToString("X8") +
                    tag);
            }
            Console.WriteLine();
            switch (fmt)
            {
                case RDTBFormat.BIG:
                    Console.WriteLine(
                        "    => BIG"
                        + " RDTB: 14"
                        + " active"
                        + " chunks, 3"
                        + " LOD meshes");
                    break;
                case RDTBFormat.SMALL:
                    Console.WriteLine(
                        "    => SMALL"
                        + " RDTB: 10"
                        + " chunks,"
                        + " single mesh");
                    break;
                case RDTBFormat.MIRRORED:
                    Console.WriteLine(
                        "    => MIRRORED"
                        + " RDTB: 14"
                        + " slots,"
                        + " slot pairs"
                        + " share data");
                    break;
                default:
                    Console.WriteLine(
                        "    => UNKNOWN"
                        + " format");
                    break;
            }
        }

        // ═════════════════════════════
        // BIG -> SMALL
        // Port of rdtb_big_to_small_
        // full_v9.py
        // ═════════════════════════════
        public static void BigToSmall(
            string inPath,
            string outPath)
        {
            if (!File.Exists(inPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + inPath);
                return;
            }

            byte[] data =
                File.ReadAllBytes(
                    inPath);
            if (data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
            {
                TextOut.PrintError(
                    "Not RDTB: "
                    + inPath);
                return;
            }

            RDTBFormat fmt =
                DetectFormat(inPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Big->Small");
            Console.ResetColor();
            Console.WriteLine(
                "    Input : " +
                Path.GetFileName(
                    inPath));
            Console.WriteLine(
                "    Output: " +
                Path.GetFileName(
                    outPath));
            Console.WriteLine(
                "    Source format: "
                + fmt.ToString());

            // Read raw slots
            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
                rawSlots[i] =
                    BitConverter
                        .ToUInt32(data,
                            0x10 + i * 4);

            // Get all valid offsets
            // in original order
            List<int> offsets =
                new List<int>();
            foreach (uint v in rawSlots)
            {
                if (v == 0) break;
                if (v < 0x48) break;
                if (v == 0xFFFFFFFF)
                    continue;
                if (v > (uint)data.Length)
                    break;
                offsets.Add((int)v);
            }

            if (offsets.Count < 14 &&
                fmt != RDTBFormat
                    .MIRRORED)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] Already"
                    + " has < 14 chunks"
                    + " (" +
                    offsets.Count + ")");
                Console.ResetColor();
            }

            // Extract chunks (use
            // distinct offsets only)
            List<int> distinctOffs =
                offsets.Distinct()
                    .OrderBy(x => x)
                    .ToList();

            // Map raw slot -> chunk
            // data by offset
            Dictionary<int, byte[]>
                offsetToChunk =
                new Dictionary<int,
                    byte[]>();
            for (int i = 0;
                 i < distinctOffs.Count;
                 i++)
            {
                int s =
                    distinctOffs[i];
                int e =
                    (i + 1 <
                        distinctOffs
                            .Count
                    ? distinctOffs[i + 1]
                    : data.Length);
                byte[] cd =
                    new byte[e - s];
                Array.Copy(data, s,
                    cd, 0, e - s);
                offsetToChunk[s] = cd;
            }

            // For BIG/MIRRORED:
            // chunks 0-7 are unique
            // chunk 8 is the
            //   material/lookup chunk
            // chunk 11 is LOD0 mesh
            // We keep 0-7, 8, and 11
            // for SMALL output

            byte[] c8Data = null;
            byte[] c11Data = null;
            byte[][] chunks07 =
                new byte[8][];

            for (int i = 0; i < 8; i++)
            {
                uint slotVal =
                    rawSlots[i];
                if (slotVal == 0 ||
                    slotVal ==
                        0xFFFFFFFF)
                    continue;
                if (offsetToChunk
                    .ContainsKey(
                        (int)slotVal))
                    chunks07[i] =
                        offsetToChunk[
                            (int)slotVal];
            }

            if (rawSlots[8] != 0 &&
                rawSlots[8] !=
                    0xFFFFFFFF &&
                offsetToChunk
                    .ContainsKey(
                        (int)rawSlots[8]))
            {
                c8Data =
                    offsetToChunk[
                        (int)rawSlots[8]];
            }

            if (rawSlots[11] != 0 &&
                rawSlots[11] !=
                    0xFFFFFFFF &&
                offsetToChunk
                    .ContainsKey(
                        (int)rawSlots[11]))
            {
                c11Data =
                    offsetToChunk[
                        (int)rawSlots[11]];
            }

            if (c8Data == null ||
                c11Data == null)
            {
                TextOut.PrintError(
                    "Missing chunk 8"
                    + " or 11");
                return;
            }

            // Apply chunk 8 flag
            // fix (clear bit 7)
            int flagsChanged;
            byte[] c8Fixed =
                FixChunk8Flags(c8Data,
                    out flagsChanged);

            Console.WriteLine(
                "    Chunk 8 flags"
                + " cleared: " +
                flagsChanged);

            // Build new file
            byte[][] kept = new byte[][]
            {
                chunks07[0], chunks07[1],
                chunks07[2], chunks07[3],
                chunks07[4], chunks07[5],
                chunks07[6], chunks07[7],
                c8Fixed,
                c11Data
            };

            int HEADER = 0x48;
            int cursor = HEADER;
            int[] physOff =
                new int[kept.Length];
            for (int i = 0;
                 i < kept.Length; i++)
            {
                physOff[i] = cursor;
                cursor += kept[i].Length;
            }

            int slot8Abs = physOff[8];
            int slot11Abs = physOff[9];

            byte[] header =
                new byte[HEADER];
            // Magic + version
            Array.Copy(data, 0,
                header, 0, 12);
            // ptr_count + bone_count
            Array.Copy(data, 0x0C,
                header, 0x0C, 4);

            // Slots 0-7
            for (int i = 0; i < 8; i++)
                WriteU32(header,
                    0x10 + i * 4,
                    (uint)physOff[i]);
            // Slot 8
            WriteU32(header,
                0x10 + 8 * 4,
                (uint)slot8Abs);
            // Slots 9, 10 = FFFFFFFF
            WriteU32(header,
                0x10 + 9 * 4,
                0xFFFFFFFF);
            WriteU32(header,
                0x10 + 10 * 4,
                0xFFFFFFFF);
            // Slot 11
            WriteU32(header,
                0x10 + 11 * 4,
                (uint)slot11Abs);
            // Slots 12, 13 = FFFFFFFF
            WriteU32(header,
                0x10 + 12 * 4,
                0xFFFFFFFF);
            WriteU32(header,
                0x10 + 13 * 4,
                0xFFFFFFFF);

            byte[] result =
                new byte[cursor];
            Array.Copy(header, 0,
                result, 0, HEADER);
            for (int i = 0;
                 i < kept.Length; i++)
            {
                Array.Copy(kept[i], 0,
                    result,
                    physOff[i],
                    kept[i].Length);
            }

            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        outPath));
            if (!string.IsNullOrEmpty(
                    outDir))
                Directory.CreateDirectory(
                    outDir);

            File.WriteAllBytes(
                outPath, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Saved: " +
                outPath);
            Console.ResetColor();
            Console.WriteLine(
                "     In  : " +
                data.Length.ToString(
                    "N0") + " B");
            Console.WriteLine(
                "     Out : " +
                result.Length.ToString(
                    "N0") + " B");
            Console.WriteLine(
                "     Save: " +
                (data.Length -
                    result.Length)
                    .ToString("N0") +
                " B");
        }

        // ═════════════════════════════
        // SMALL -> BIG
        // Port of rdtb_small_to_big_
        // v1.py
        // ═════════════════════════════
        public static void SmallToBig(
            string inPath,
            string outPath)
        {
            if (!File.Exists(inPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + inPath);
                return;
            }

            byte[] data =
                File.ReadAllBytes(
                    inPath);
            if (data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
            {
                TextOut.PrintError(
                    "Not RDTB");
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Small->Big");
            Console.ResetColor();
            Console.WriteLine(
                "    Input : " +
                Path.GetFileName(
                    inPath));
            Console.WriteLine(
                "    Output: " +
                Path.GetFileName(
                    outPath));

            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
                rawSlots[i] =
                    BitConverter
                        .ToUInt32(data,
                            0x10 + i * 4);

            // Check it's small
            if (rawSlots[9] !=
                0xFFFFFFFF)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] slot 9"
                    + " is not"
                    + " FFFFFFFF");
                Console.ResetColor();
            }
            if (rawSlots[11] ==
                0xFFFFFFFF)
            {
                TextOut.PrintError(
                    "No mesh chunk"
                    + " (slot 11 is"
                    + " FFFFFFFF)");
                return;
            }

            uint slot8Off =
                rawSlots[8];
            uint slot11Off =
                rawSlots[11];

            int c8Size =
                (int)(slot11Off -
                      slot8Off);
            int c11Size =
                data.Length -
                (int)slot11Off;

            byte[] c8Data =
                new byte[c8Size];
            Array.Copy(data,
                (int)slot8Off,
                c8Data, 0, c8Size);
            byte[] c11Data =
                new byte[c11Size];
            Array.Copy(data,
                (int)slot11Off,
                c11Data, 0, c11Size);

            int chunks07Start =
                (int)rawSlots[0];
            int chunks07End =
                (int)rawSlots[8];
            byte[] chunks07 =
                new byte[chunks07End -
                         chunks07Start];
            Array.Copy(data,
                chunks07Start,
                chunks07, 0,
                chunks07.Length);

            Console.WriteLine(
                "    chunks 0-7: " +
                chunks07.Length
                    .ToString("N0") +
                " B");
            Console.WriteLine(
                "    chunk 8   : " +
                c8Size.ToString("N0") +
                " B");
            Console.WriteLine(
                "    chunk 11  : " +
                c11Size.ToString("N0") +
                " B");

            int HEADER = 0x48;
            uint[] newSlots =
                new uint[14];

            // Chunks 0-7 at original
            // positions (relative to
            // chunks07 buffer)
            uint cursor = (uint)HEADER;
            for (int i = 0; i < 8; i++)
            {
                newSlots[i] = cursor;
                if (i < 7)
                {
                    uint chunkLen =
                        rawSlots[i + 1] -
                        rawSlots[i];
                    cursor += chunkLen;
                }
                else
                {
                    uint chunkLen =
                        rawSlots[8] -
                        rawSlots[7];
                    cursor += chunkLen;
                }
            }

            // Chunk 8
            newSlots[8] = cursor;
            cursor += (uint)c8Size;
            // Chunk 9 = copy of 8
            newSlots[9] = cursor;
            cursor += (uint)c8Size;
            // Chunk 10 = copy of 8
            newSlots[10] = cursor;
            cursor += (uint)c8Size;
            // Chunk 11
            newSlots[11] = cursor;
            cursor += (uint)c11Size;
            // Chunk 12 = copy of 11
            newSlots[12] = cursor;
            cursor += (uint)c11Size;
            // Chunk 13 = copy of 11
            newSlots[13] = cursor;
            cursor += (uint)c11Size;

            int newSize = (int)cursor;
            byte[] result =
                new byte[newSize];

            // Header
            Array.Copy(data, 0, result,
                0, HEADER);
            for (int i = 0; i < 14; i++)
                WriteU32(result,
                    0x10 + i * 4,
                    newSlots[i]);

            // Write chunks 0-7
            Array.Copy(chunks07, 0,
                result, HEADER,
                chunks07.Length);
            // Write chunk 8
            Array.Copy(c8Data, 0,
                result,
                (int)newSlots[8],
                c8Size);
            // Write chunk 9 (copy)
            Array.Copy(c8Data, 0,
                result,
                (int)newSlots[9],
                c8Size);
            // Write chunk 10 (copy)
            Array.Copy(c8Data, 0,
                result,
                (int)newSlots[10],
                c8Size);
            // Write chunk 11
            Array.Copy(c11Data, 0,
                result,
                (int)newSlots[11],
                c11Size);
            // Write chunk 12 (copy)
            Array.Copy(c11Data, 0,
                result,
                (int)newSlots[12],
                c11Size);
            // Write chunk 13 (copy)
            Array.Copy(c11Data, 0,
                result,
                (int)newSlots[13],
                c11Size);

            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        outPath));
            if (!string.IsNullOrEmpty(
                    outDir))
                Directory.CreateDirectory(
                    outDir);

            File.WriteAllBytes(
                outPath, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Saved: " +
                outPath);
            Console.ResetColor();
            Console.WriteLine(
                "     In  : " +
                data.Length.ToString(
                    "N0") + " B");
            Console.WriteLine(
                "     Out : " +
                result.Length.ToString(
                    "N0") + " B");
        }

        // ═════════════════════════════
        // SMALL -> MIRRORED
        // Same as small but doesn't
        // duplicate chunk data, just
        // points slots 9/10 to 8 and
        // 12/13 to 11. File stays
        // same size.
        // ═════════════════════════════
        public static void SmallToMirrored(
            string inPath,
            string outPath)
        {
            if (!File.Exists(inPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + inPath);
                return;
            }

            byte[] data =
                File.ReadAllBytes(
                    inPath);
            if (data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
            {
                TextOut.PrintError(
                    "Not RDTB");
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Small->"
                + "Mirrored");
            Console.ResetColor();
            Console.WriteLine(
                "    Input : " +
                Path.GetFileName(
                    inPath));
            Console.WriteLine(
                "    Output: " +
                Path.GetFileName(
                    outPath));

            byte[] result =
                new byte[data.Length];
            Array.Copy(data, result,
                data.Length);

            uint slot8 =
                BitConverter.ToUInt32(
                    data,
                    0x10 + 8 * 4);
            uint slot11 =
                BitConverter.ToUInt32(
                    data,
                    0x10 + 11 * 4);

            if (slot11 == 0xFFFFFFFF)
            {
                TextOut.PrintError(
                    "No mesh chunk in"
                    + " slot 11");
                return;
            }

            // Mirror slots
            WriteU32(result,
                0x10 + 9 * 4, slot8);
            WriteU32(result,
                0x10 + 10 * 4, slot8);
            WriteU32(result,
                0x10 + 12 * 4, slot11);
            WriteU32(result,
                0x10 + 13 * 4, slot11);

            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        outPath));
            if (!string.IsNullOrEmpty(
                    outDir))
                Directory.CreateDirectory(
                    outDir);

            File.WriteAllBytes(
                outPath, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Saved: " +
                outPath);
            Console.ResetColor();
            Console.WriteLine(
                "     Slots 9,10 ->"
                + " same as slot 8");
            Console.WriteLine(
                "     Slots 12,13 ->"
                + " same as slot 11");
            Console.WriteLine(
                "     Size unchanged: "
                + result.Length
                    .ToString("N0") +
                " B");
        }

        // ═════════════════════════════
        // MIRRORED -> SMALL
        // Just sets mirrored slots
        // back to 0xFFFFFFFF
        // ═════════════════════════════
        public static void MirroredToSmall(
            string inPath,
            string outPath)
        {
            if (!File.Exists(inPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + inPath);
                return;
            }

            byte[] data =
                File.ReadAllBytes(
                    inPath);
            if (data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
            {
                TextOut.PrintError(
                    "Not RDTB");
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Mirrored->"
                + "Small");
            Console.ResetColor();
            Console.WriteLine(
                "    Input : " +
                Path.GetFileName(
                    inPath));
            Console.WriteLine(
                "    Output: " +
                Path.GetFileName(
                    outPath));

            byte[] result =
                new byte[data.Length];
            Array.Copy(data, result,
                data.Length);

            // Set mirrored slots
            // to FFFFFFFF
            WriteU32(result,
                0x10 + 9 * 4,
                0xFFFFFFFF);
            WriteU32(result,
                0x10 + 10 * 4,
                0xFFFFFFFF);
            WriteU32(result,
                0x10 + 12 * 4,
                0xFFFFFFFF);
            WriteU32(result,
                0x10 + 13 * 4,
                0xFFFFFFFF);

            // Apply chunk 8 flag fix
            uint slot8 =
                BitConverter.ToUInt32(
                    result,
                    0x10 + 8 * 4);
            uint slot11 =
                BitConverter.ToUInt32(
                    result,
                    0x10 + 11 * 4);

            if (slot8 != 0 &&
                slot8 != 0xFFFFFFFF &&
                slot11 != 0 &&
                slot11 != 0xFFFFFFFF)
            {
                int c8Size =
                    (int)(slot11 -
                          slot8);
                byte[] c8 =
                    new byte[c8Size];
                Array.Copy(result,
                    (int)slot8,
                    c8, 0, c8Size);

                int changed;
                byte[] c8Fixed =
                    FixChunk8Flags(c8,
                        out changed);
                Array.Copy(c8Fixed, 0,
                    result,
                    (int)slot8,
                    c8Size);

                Console.WriteLine(
                    "    Chunk 8 flags"
                    + " cleared: " +
                    changed);
            }

            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        outPath));
            if (!string.IsNullOrEmpty(
                    outDir))
                Directory.CreateDirectory(
                    outDir);

            File.WriteAllBytes(
                outPath, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Saved: " +
                outPath);
            Console.ResetColor();
        }

        // ═════════════════════════════
        // BIG -> MIRRORED
        // Convert big to small first,
        // then small to mirrored.
        // ═════════════════════════════
        public static void BigToMirrored(
            string inPath,
            string outPath)
        {
            string tempPath =
                outPath + ".tmp";
            try
            {
                BigToSmall(inPath,
                    tempPath);
                SmallToMirrored(
                    tempPath,
                    outPath);
            }
            finally
            {
                if (File.Exists(
                        tempPath))
                {
                    try
                    {
                        File.Delete(
                            tempPath);
                    }
                    catch { }
                }
            }
        }

        // ═════════════════════════════
        // MIRRORED -> BIG
        // Mirrored already has all 14
        // slots filled, just need to
        // duplicate the data so each
        // slot has unique chunk bytes
        // ═════════════════════════════
        public static void MirroredToBig(
            string inPath,
            string outPath)
        {
            // Mirrored has data only
            // at slots 8 and 11. We
            // need to duplicate it
            // so all 14 slots have
            // unique storage. Easiest:
            // mirrored -> small,
            // then small -> big.
            string tempPath =
                outPath + ".tmp";
            try
            {
                MirroredToSmall(inPath,
                    tempPath);
                SmallToBig(tempPath,
                    outPath);
            }
            finally
            {
                if (File.Exists(
                        tempPath))
                {
                    try
                    {
                        File.Delete(
                            tempPath);
                    }
                    catch { }
                }
            }
        }

        // ═════════════════════════════
        // BIG -> MIRRORED (direct)
        // ═════════════════════════════
        // (handled by BigToMirrored
        //  above as small intermediate)

        // ═════════════════════════════
        // HELPERS
        // ═════════════════════════════
        private static void WriteU32(
            byte[] data, int off,
            uint v)
        {
            byte[] b =
                BitConverter.GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }

        /// <summary>
        /// Clear bit 7 of the flags
        /// u32 (bytes 4-7) of every
        /// QW lookup record in
        /// chunk 8. This tells the
        /// game "no external LOD
        /// chunks - single mesh
        /// only".
        /// </summary>
        private static byte[]
            FixChunk8Flags(
                byte[] chunk8,
                out int changed)
        {
            changed = 0;
            byte[] d =
                new byte[chunk8.Length];
            Array.Copy(chunk8, d,
                chunk8.Length);

            if (d.Length < 4)
                return d;

            uint firstPtr =
                BitConverter.ToUInt32(
                    d, 0);
            if (firstPtr == 0 ||
                firstPtr > (uint)
                    d.Length)
                return d;

            int n = (int)(firstPtr / 4);

            for (int i = 0; i < n; i++)
            {
                int ptrOff = i * 4;
                if (ptrOff + 4 >
                    d.Length)
                    break;
                uint recOff =
                    BitConverter
                        .ToUInt32(d,
                            ptrOff);
                if (recOff + 8 >
                    (uint)d.Length)
                    continue;

                uint flags =
                    BitConverter
                        .ToUInt32(d,
                            (int)recOff
                            + 4);
                uint newFlags =
                    flags & ~(uint)
                        0x00000080;

                if (newFlags != flags)
                {
                    WriteU32(d,
                        (int)recOff
                        + 4,
                        newFlags);
                    changed++;
                }
            }

            return d;
        }
    }
}
