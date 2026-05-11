using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB3D
{
    // ═════════════════════════════════════════════
    // RDTB3D DIAGNOSTIC SUITE v2
    // Part of HMSTHModdingTool v1.4.5-Beta
    // Original HDATextTool by gdkchan
    // Upgraded by DarthKrayt333 + HMSTH Community
    //
    // Reverse-engineering tools for cracking
    // PS2 Harvest Moon: Save The Homeland (USA)
    // by Victor Interactive / NATSUME / TOYBOX
    //
    // CONFIRMED FINDINGS:
    //   - Chunk 0  = Skeleton bind poses
    //   - Chunk 7  = Scene graph (LOD groups)
    //   - Chunk 8  = Material/render state
    //   - Chunk 11 = Effect/anchor mesh data
    //                (mostly bound to ROOT)
    //   - REAL body mesh is in chunks 1-6!
    //   - Byte +0 of each VIF row = bone index
    //   - SLUS LBA at 0x162460-0x162D30 (USA)
    //
    // Diagnostics:
    //   diag       - Chunk 11 byte distribution
    //   diag2      - Chunk 8 material analysis
    //   diag3      - Chunks 7-10 bone scoring
    //   diag4      - Chunk 7 raw deep dump
    //   diag5      - Chunk 7 structured nodes
    //   diag6      - Chunk 7 scene graph
    //   diag7      - Chunk 7 grouped by ANCHOR
    //   diag8      - Chunk 11 VIF bones (old)
    //   diag8b     - Chunk 11 VIF bones (FIXED)
    //   diag9      - SLUS LBA full analyzer
    //   diag10     - ALL chunks VIF + bone scan
    //   slus       - SLUS LBA basic decoder
    //   slusfix    - SLUS LBA size-correct decoder
    //   lbaupdate  - Update LBA entry safely
    // ═════════════════════════════════════════════
    public static class RDTB3DDiagnostic
    {
        // ─────────────────────────────────────────
        // CONSTANTS
        // ─────────────────────────────────────────
        private const int HEADER_SIZE = 0x48;
        private const int OFFSET_TBL_START = 0x10;
        private const int MAX_OFFSETS = 14;

        // SLUS LBA region offsets (HMSTH 2001)
        private const int LBA_USA_START = 0x162460;
        private const int LBA_USA_END = 0x162D30;
        private const int LBA_JPN_START = 0x162360;
        private const int LBA_JPN_END = 0x162C30;

        // ═════════════════════════════════════════
        // SHARED HELPERS
        // ═════════════════════════════════════════

        private static byte[] LoadRdtb(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "RDTB not found: " + path);

            byte[] data = File.ReadAllBytes(path);

            if (data.Length < HEADER_SIZE)
                throw new InvalidDataException(
                    "File too small: " + path);

            if (data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
                throw new InvalidDataException(
                    "Not a valid RDTB: " + path);

            return data;
        }

        private static List<int> GetChunkOffsets(
            byte[] data)
        {
            var offs = new List<int>();
            for (int i = 0; i < MAX_OFFSETS; i++)
            {
                int p = OFFSET_TBL_START + i * 4;
                if (p + 4 > data.Length) break;
                int v = BitConverter.ToInt32(
                    data, p);
                if (v == 0 ||
                    v < HEADER_SIZE ||
                    v > data.Length) break;
                offs.Add(v);
            }
            return offs;
        }

        private static int GetBoneCount(byte[] data)
        {
            return BitConverter.ToUInt16(
                data, 0x0E);
        }

        private static int GetNodeCount(byte[] data)
        {
            return BitConverter.ToUInt16(
                data, 0x0C);
        }

        private static void PrintHeader(
            string title, string filename)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] " + title + ": " +
                Path.GetFileName(filename));
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
        }

        // ─────────────────────────────────────────
        // BONE NAME GUESSER (BOY skeleton labels)
        // ─────────────────────────────────────────
        private static string GuessBoneName(int idx)
        {
            if (idx < 0) return "(no bone)";
            string[] map = {
                "ROOT", "SEC_ROOT",
                "SPINE_BASE", "SPINE_MID",
                "SPINE_TOP",
                "NECK", "EYES",
                "FACE_1", "FACE_2",
                "EYE_L", "EYE_C", "EYE_R",
                "CHEST_R", "CHEST_C", "CHEST_L",
                "SHOULDER_R", "UPPER_ARM_R",
                "FOREARM_R", "WRIST_R",
                "HAND_R", "HAND_DET_R",
            };
            if (idx < map.Length) return map[idx];
            if (idx >= 21 && idx <= 31)
                return $"R_FINGER_{idx - 21}";
            if (idx == 32) return "SHOULDER_L";
            if (idx == 33) return "UPPER_ARM_L";
            if (idx == 34) return "FOREARM_L";
            if (idx == 35) return "WRIST_L";
            if (idx == 36) return "HAND_L";
            if (idx == 37) return "HAND_DET_L";
            if (idx >= 38 && idx <= 49)
                return $"L_FINGER_{idx - 38}";
            if (idx == 50) return "HIP_R";
            if (idx == 51) return "THIGH_R";
            if (idx == 52) return "SHIN_R";
            if (idx == 53) return "ANKLE_R";
            if (idx == 54) return "FOOT_R";
            if (idx == 55) return "TOEBASE_R";
            if (idx == 56) return "TOE_R";
            if (idx == 57) return "TOETIP1_R";
            if (idx == 58) return "TOETIP2_R";
            if (idx == 59) return "HIP_L";
            if (idx == 60) return "THIGH_L";
            if (idx == 61) return "SHIN_L";
            if (idx == 62) return "ANKLE_L";
            if (idx == 63) return "FOOT_L";
            if (idx == 64) return "TOEBASE_L";
            if (idx == 65) return "TOE_L";
            if (idx == 66) return "TOETIP1_L";
            if (idx == 67) return "TOETIP2_L";
            return "?";
        }

        // ═════════════════════════════════════════
        // DIAG 1: CHUNK 11 VIF ROW BYTE ANALYSIS
        // ═════════════════════════════════════════
        public static void Run(string rdtbPath)
        {
            PrintHeader(
                "Chunk 11 VIF Diagnostic",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (offs.Count < 12)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  [!] No chunk 11 in RDTB");
                Console.ResetColor();
                return;
            }

            int c11Start = offs[11];
            int c11End = (12 < offs.Count)
                ? offs[12] : data.Length;
            int c11Size = c11End - c11Start;

            Console.WriteLine(
                $"  Bone count : {boneCount}");
            Console.WriteLine(
                $"  Chunk 11   : 0x{c11Start:X6}" +
                $" - 0x{c11End:X6} " +
                $"({c11Size:N0} bytes)");
            Console.WriteLine();

            int rowCount = c11Size / 16;
            var byte0 = new int[256];
            var byte12 = new int[256];

            for (int i = 0; i < rowCount; i++)
            {
                int off = c11Start + i * 16;
                if (off + 16 > data.Length) break;
                byte0[data[off]]++;
                byte12[data[off + 12]]++;
            }

            int b0InBone = 0;
            int b0Total = 0;
            int b12InBone = 0;
            int b12Total = 0;

            for (int b = 0; b < 256; b++)
            {
                if (byte0[b] > 0)
                {
                    b0Total += byte0[b];
                    if (b < boneCount)
                        b0InBone += byte0[b];
                }
                if (byte12[b] > 0)
                {
                    b12Total += byte12[b];
                    if (b < boneCount)
                        b12InBone += byte12[b];
                }
            }

            Console.WriteLine(
                $"  VIF rows scanned: " +
                $"{rowCount:N0}");
            Console.WriteLine();
            Console.WriteLine(
                "  Byte +0 (suspected bone idx):");
            double pct0 = b0Total > 0
                ? (b0InBone * 100.0 / b0Total)
                : 0;
            Console.WriteLine(
                $"    {pct0:F1}% in bone range");

            Console.WriteLine();
            Console.WriteLine(
                "  Byte +12 (suspected tex idx):");
            double pct12 = b12Total > 0
                ? (b12InBone * 100.0 / b12Total)
                : 0;
            Console.WriteLine(
                $"    {pct12:F1}% in bone range");

            Console.WriteLine();
            Console.WriteLine(
                "  Top 10 byte +0 values:");
            var top0 = Enumerable.Range(0, 256)
                .Where(b => byte0[b] > 0)
                .OrderByDescending(b => byte0[b])
                .Take(10);
            foreach (int b in top0)
            {
                string note = b < boneCount
                    ? "(bone range)"
                    : "(out of range)";
                Console.WriteLine(
                    $"    0x{b:X2} = {byte0[b],6} " +
                    $"hits  {note}");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 2: CHUNK 8 MATERIAL ANALYSIS
        // ═════════════════════════════════════════
        public static void RunChunk8(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 8 Analysis",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (offs.Count < 9)
            {
                Console.WriteLine(
                    "  [!] No chunk 8");
                return;
            }

            int c8Start = offs[8];
            int c8End = (9 < offs.Count)
                ? offs[9] : data.Length;
            int c8Size = c8End - c8Start;

            uint firstPtr =
                BitConverter.ToUInt32(
                    data, c8Start);
            int recCount = (int)(firstPtr / 4);

            Console.WriteLine(
                $"  Bone count   : {boneCount}");
            Console.WriteLine(
                $"  Chunk 8 size : {c8Size,5} bytes");
            Console.WriteLine(
                $"  Record count : {recCount}");
            Console.WriteLine();

            Console.WriteLine(
                "  [idx]  ptr      A      B      " +
                "C    Tex     bone-A? bone-B? bone-C?");
            Console.WriteLine(
                "  " + new string('-', 76));

            int aInRange = 0;
            int bInRange = 0;
            int cInRange = 0;
            int total = 0;

            var aSet = new HashSet<int>();
            var bSet = new HashSet<int>();
            var cSet = new HashSet<int>();
            var texCount =
                new Dictionary<int, int>();

            int show = Math.Min(recCount, 80);

            for (int i = 0; i < recCount; i++)
            {
                int ptrPos = c8Start + i * 4;
                if (ptrPos + 4 > data.Length)
                    break;
                uint ptr = BitConverter.ToUInt32(
                    data, ptrPos);
                int recOff = c8Start + (int)ptr;
                if (recOff + 8 > data.Length)
                    break;

                ushort a = BitConverter.ToUInt16(
                    data, recOff);
                ushort b = BitConverter.ToUInt16(
                    data, recOff + 2);
                ushort c = BitConverter.ToUInt16(
                    data, recOff + 4);
                ushort tex = BitConverter.ToUInt16(
                    data, recOff + 6);

                bool aBone = a < boneCount;
                bool bBone = b < boneCount;
                bool cBone = c < boneCount;

                if (aBone) aInRange++;
                if (bBone) bInRange++;
                if (cBone) cInRange++;

                aSet.Add(aBone ? a : -1);
                bSet.Add(bBone ? b : -1);
                cSet.Add(cBone ? c : -1);
                total++;

                if (!texCount.ContainsKey(tex))
                    texCount[tex] = 0;
                texCount[tex]++;

                if (i < show)
                {
                    Console.WriteLine(
                        $"  [{i,3}] 0x{ptr:X4} " +
                        $"{a,6} {b,6} {c,6} " +
                        $"{tex,6}     " +
                        $"{(aBone ? "yes" : " no"),3}     " +
                        $"{(bBone ? "yes" : " no"),3}     " +
                        $"{(cBone ? "yes" : " no"),3}");
                }
            }

            if (recCount > show)
                Console.WriteLine(
                    $"  ... ({recCount - show} more)");

            Console.WriteLine();
            Console.WriteLine(
                "  Verdict (which field is bone?):");
            double pa = total > 0
                ? aInRange * 100.0 / total : 0;
            double pb = total > 0
                ? bInRange * 100.0 / total : 0;
            double pc = total > 0
                ? cInRange * 100.0 / total : 0;
            int aUnique = aSet
                .Where(x => x >= 0).Count();
            int bUnique = bSet
                .Where(x => x >= 0).Count();
            int cUnique = cSet
                .Where(x => x >= 0).Count();

            Console.WriteLine(
                $"    A : {pa:F1}% in range, " +
                $"{aUnique} unique values");
            Console.WriteLine(
                $"    B : {pb:F1}% in range, " +
                $"{bUnique} unique values");
            Console.WriteLine(
                $"    C : {pc:F1}% in range, " +
                $"{cUnique} unique values");

            Console.WriteLine();
            Console.WriteLine(
                "  Tex field distribution:");
            foreach (var kv in
                texCount.OrderBy(k => k.Key))
            {
                Console.WriteLine(
                    $"    tex {kv.Key,3} : " +
                    $"{kv.Value} batches");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 3: CHUNKS 7-10 BONE-RANGE SCORING
        // ═════════════════════════════════════════
        public static void RunRangeScan(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 7-10 Range Analysis",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            Console.WriteLine(
                $"  Bone count: {boneCount}");

            for (int ci = 7; ci <= 10; ci++)
            {
                if (ci >= offs.Count) break;
                int cStart = offs[ci];
                int cEnd = (ci + 1 < offs.Count)
                    ? offs[ci + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                Console.WriteLine();
                Console.WriteLine(
                    $"  --- Chunk {ci} " +
                    $"(offset 0x{cStart:X8}, " +
                    $"size {cSize}) ---");

                int byteIn = 0;
                int byteTotal = 0;
                var byteSet = new HashSet<int>();
                for (int i = 0; i < cSize; i++)
                {
                    byte v = data[cStart + i];
                    byteTotal++;
                    if (v < boneCount)
                    {
                        byteIn++;
                        byteSet.Add(v);
                    }
                }
                double bytePct = byteTotal > 0
                    ? byteIn * 100.0 / byteTotal
                    : 0;
                string byteVerdict = bytePct > 80
                    ? "  ← LIKELY LOOKUP" : "";
                Console.WriteLine(
                    $"    As bytes  : {bytePct,5:F1}% " +
                    $"in bone range, {byteSet.Count} " +
                    $"unique{byteVerdict}");

                int uIn = 0;
                int uTotal = 0;
                var uSet = new HashSet<int>();
                for (int i = 0;
                     i + 2 <= cSize; i += 2)
                {
                    ushort v = BitConverter
                        .ToUInt16(
                            data, cStart + i);
                    uTotal++;
                    if (v < boneCount)
                    {
                        uIn++;
                        uSet.Add(v);
                    }
                }
                double uPct = uTotal > 0
                    ? uIn * 100.0 / uTotal : 0;
                string uVerdict = uPct > 80
                    ? "  ← LIKELY LOOKUP" : "";
                Console.WriteLine(
                    $"    As ushorts: {uPct,5:F1}% " +
                    $"in bone range, {uSet.Count} " +
                    $"unique{uVerdict}");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 4: CHUNK 7 DEEP DUMP (RAW BYTES)
        // ═════════════════════════════════════════
        public static void RunDeepDump(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 7 Deep Dump",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (offs.Count < 8)
            {
                Console.WriteLine(
                    "  [!] No chunk 7");
                return;
            }

            int c7Start = offs[7];
            int c7End = (8 < offs.Count)
                ? offs[8] : data.Length;
            int c7Size = c7End - c7Start;

            int batchCount = 0;
            if (offs.Count >= 9)
            {
                uint firstBatchPtr =
                    BitConverter.ToUInt32(
                        data, offs[8]);
                batchCount =
                    (int)(firstBatchPtr / 4);
            }

            Console.WriteLine(
                $"    Bone count : {boneCount}");
            Console.WriteLine(
                $"    Chunk size : {c7Size} bytes");
            Console.WriteLine(
                $"    Batch count: {batchCount}");
            if (batchCount > 0)
                Console.WriteLine(
                    $"    Bytes/batch: " +
                    $"{(double)c7Size / batchCount:F2}");

            Console.WriteLine();
            Console.Write(
                "    First 16 bytes (hex):\n      ");
            int show16 = Math.Min(16, c7Size);
            for (int i = 0; i < show16; i++)
                Console.Write(
                    data[c7Start + i].ToString("X2") +
                    " ");
            Console.WriteLine();

            uint firstPtr = BitConverter.ToUInt32(
                data, c7Start);
            int ptrCount = (int)(firstPtr / 4);

            Console.WriteLine();
            if (firstPtr > 0 &&
                firstPtr < c7Size &&
                firstPtr % 4 == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    >> POINTER TABLE format");
                Console.WriteLine(
                    $"       First ptr: 0x{firstPtr:X4}" +
                    $" → {ptrCount} pointers");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"    >> First DWORD = " +
                    $"0x{firstPtr:X8} (unclear)");
                Console.ResetColor();
                ptrCount = 0;
            }

            if (ptrCount == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Finished!");
                return;
            }

            Console.WriteLine();
            Console.WriteLine(
                "    [idx]  ptr      first 16 bytes" +
                "                          as bones");
            Console.WriteLine(
                "    " + new string('-', 76));

            int show = Math.Min(ptrCount, 30);
            for (int i = 0; i < show; i++)
            {
                int ptrPos = c7Start + i * 4;
                if (ptrPos + 4 > data.Length)
                    break;
                uint ptr = BitConverter.ToUInt32(
                    data, ptrPos);
                int recOff = c7Start + (int)ptr;

                int recSize = 8;
                if (i + 1 < ptrCount)
                {
                    int ptrPos2 = c7Start +
                        (i + 1) * 4;
                    if (ptrPos2 + 4 <= data.Length)
                    {
                        uint nextPtr =
                            BitConverter.ToUInt32(
                                data, ptrPos2);
                        recSize =
                            (int)(nextPtr - ptr);
                        if (recSize <= 0 ||
                            recSize > 64)
                            recSize = 8;
                    }
                }

                int dumpLen =
                    Math.Min(16, recSize);
                var hex = new StringBuilder();
                var asBones = new StringBuilder();
                for (int b = 0; b < dumpLen; b++)
                {
                    if (recOff + b >= data.Length)
                        break;
                    byte v = data[recOff + b];
                    hex.Append(v.ToString("X2"));
                    hex.Append(' ');
                    asBones.Append(
                        v < boneCount
                        ? v.ToString()
                        : (v < 32 ? "." :
                           v.ToString()));
                    asBones.Append(' ');
                }

                Console.WriteLine(
                    $"    [{i,3}] 0x{ptr:X4} " +
                    $"({recSize}b)  " +
                    $"{hex.ToString().PadRight(48)} " +
                    $"  {asBones}");
            }

            if (ptrCount > show)
                Console.WriteLine(
                    $"    ... ({ptrCount - show} more)");

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 5: CHUNK 7 STRUCTURED DUMP
        // ═════════════════════════════════════════
        public static void RunStructured(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 7 STRUCTURED Dump",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);
            int nodeCount = GetNodeCount(data);

            if (offs.Count < 8)
            {
                Console.WriteLine(
                    "  [!] No chunk 7");
                return;
            }

            int c7Start = offs[7];
            int c7End = (8 < offs.Count)
                ? offs[8] : data.Length;

            uint firstPtr = BitConverter.ToUInt32(
                data, c7Start);
            int actualNodes = (int)(firstPtr / 4);

            Console.WriteLine(
                $"    Bones (chunk 0) : {boneCount}");
            Console.WriteLine(
                $"    Nodes (header)  : {nodeCount}");
            Console.WriteLine();
            Console.WriteLine(
                $"    Pointer count   : {actualNodes}");
            Console.WriteLine();
            Console.WriteLine(
                "    [node]  ptr    u16_0  u16_1  " +
                "u16_2  u16_3    bone?   notes");
            Console.WriteLine(
                "    " + new string('-', 76));

            int u3OneCount = 0;
            int show = Math.Min(actualNodes, 60);

            for (int i = 0; i < actualNodes; i++)
            {
                int ptrPos = c7Start + i * 4;
                if (ptrPos + 4 > data.Length)
                    break;
                uint ptr = BitConverter.ToUInt32(
                    data, ptrPos);
                int recOff = c7Start + (int)ptr;
                if (recOff + 8 > data.Length)
                    break;

                ushort u0 = BitConverter.ToUInt16(
                    data, recOff);
                ushort u1 = BitConverter.ToUInt16(
                    data, recOff + 2);
                ushort u2 = BitConverter.ToUInt16(
                    data, recOff + 4);
                ushort u3 = BitConverter.ToUInt16(
                    data, recOff + 6);

                if (u3 == 1) u3OneCount++;

                if (i < show)
                {
                    var notes = new StringBuilder();
                    if (u0 < boneCount)
                        notes.Append($"u0={u0}(B) ");
                    if (u1 < boneCount &&
                        u1 != 0xFFFF)
                        notes.Append($"u1={u1}(B) ");
                    if (u2 < boneCount &&
                        u2 != 0xFFFF)
                        notes.Append($"u2={u2}(B) ");
                    if (u0 == 0 && u1 == 0 &&
                        u2 == 0 && u3 == 0)
                        notes.Append("(empty)");

                    Console.WriteLine(
                        $"    [{i,3}] 0x{ptr:X4} " +
                        $"{u0,6} {u1,6} {u2,6} " +
                        $"{u3,6}    {notes}");
                }
            }

            if (actualNodes > show)
                Console.WriteLine(
                    $"    ... ({actualNodes - show} more)");

            int batchCount = 0;
            if (offs.Count >= 9)
            {
                uint fbp = BitConverter.ToUInt32(
                    data, offs[8]);
                batchCount = (int)(fbp / 4);
            }

            Console.WriteLine();
            Console.WriteLine(
                $"    Total nodes with u3==1: " +
                $"{u3OneCount}");
            Console.WriteLine(
                $"    Chunk 8 batch count: " +
                $"{batchCount}");

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 6: CHUNK 7 NODE→BATCH MAPPING
        // ═════════════════════════════════════════
        public static void RunNodeMap(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 7 NODE→BATCH Mapping",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);
            int nodeCount = GetNodeCount(data);

            if (offs.Count < 8)
            {
                Console.WriteLine(
                    "  [!] No chunk 7");
                return;
            }

            int c7Start = offs[7];
            int c7End = (8 < offs.Count)
                ? offs[8] : data.Length;

            int batchCount = 0;
            if (offs.Count >= 9)
            {
                uint fbp = BitConverter.ToUInt32(
                    data, offs[8]);
                batchCount = (int)(fbp / 4);
            }

            uint firstPtr = BitConverter.ToUInt32(
                data, c7Start);
            int actualNodes = (int)(firstPtr / 4);

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones        : {boneCount}");
            Console.WriteLine(
                $"  Header nodes : {nodeCount}");
            Console.WriteLine(
                $"  Actual nodes : {actualNodes}");
            Console.WriteLine(
                $"  Batch count  : {batchCount}");
            Console.WriteLine();

            int drawCount = 0;
            int linkCount = 0;
            int emptyCount = 0;
            int anchorCount = 0;

            var drawNodes =
                new List<(int idx, int bone, int sec)>();

            Console.WriteLine(
                "  [node]  ptr     u0     u1     u2 " +
                "    u3   type     bone");
            Console.WriteLine(
                "  " + new string('-', 70));

            for (int i = 0; i < actualNodes; i++)
            {
                int ptrPos = c7Start + i * 4;
                if (ptrPos + 4 > data.Length)
                    break;
                uint ptr = BitConverter.ToUInt32(
                    data, ptrPos);
                int recOff = c7Start + (int)ptr;
                if (recOff + 8 > data.Length)
                    break;

                ushort u0 = BitConverter.ToUInt16(
                    data, recOff);
                ushort u1 = BitConverter.ToUInt16(
                    data, recOff + 2);
                ushort u2 = BitConverter.ToUInt16(
                    data, recOff + 4);
                ushort u3 = BitConverter.ToUInt16(
                    data, recOff + 6);

                string type;
                string boneInfo = "";
                ConsoleColor col = ConsoleColor.Gray;

                if (u0 == 0 && u1 == 0 &&
                    u2 == 0 && u3 == 0)
                {
                    type = "EMPTY  ";
                    emptyCount++;
                    col = ConsoleColor.DarkGray;
                }
                else if (u0 == 140)
                {
                    type = "ANCHOR ";
                    anchorCount++;
                    col = ConsoleColor.Yellow;
                }
                else if (u2 == 1)
                {
                    type = "DRAW   ";
                    drawCount++;
                    col = ConsoleColor.Green;
                    int bone = (u0 < boneCount)
                        ? u0 : -1;
                    int sec = (u1 < boneCount &&
                               u1 != 0xFFFF)
                        ? u1 : -1;
                    boneInfo = bone >= 0
                        ? $"bone={bone:D3}" +
                          (sec >= 0
                            ? $" + bone={sec:D3}"
                            : "")
                        : "no-bone";
                    drawNodes.Add(
                        (i, bone, sec));
                }
                else
                {
                    type = "LINK   ";
                    linkCount++;
                    int bone = (u0 < boneCount)
                        ? u0 : -1;
                    int sec = (u1 < boneCount &&
                               u1 != 0xFFFF)
                        ? u1 : -1;
                    boneInfo = bone >= 0
                        ? $"link {bone:D3}→" +
                          (sec >= 0
                            ? sec.ToString("D3")
                            : "?")
                        : "?";
                }

                if (i < 30 ||
                    (i >= actualNodes - 5))
                {
                    Console.ForegroundColor = col;
                    Console.WriteLine(
                        $"  [{i,4}] 0x{ptr:X4} " +
                        $"{u0,6} {u1,6} {u2,6} " +
                        $"{u3,6}   {type}  {boneInfo}");
                    Console.ResetColor();
                }
                else if (i == 30)
                {
                    Console.WriteLine(
                        $"  ... ({actualNodes - 35}" +
                        " hidden)");
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ═══════════════════════════════════");
            Console.WriteLine(
                "  CLASSIFICATION SUMMARY");
            Console.WriteLine(
                "  ═══════════════════════════════════");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"    DRAW nodes  : {drawCount}");
            Console.ResetColor();
            Console.WriteLine(
                $"    LINK nodes  : {linkCount}");
            Console.WriteLine(
                $"    ANCHOR nodes: {anchorCount}");
            Console.WriteLine(
                $"    EMPTY nodes : {emptyCount}");
            Console.WriteLine(
                $"    Total       : " +
                (drawCount + linkCount +
                 anchorCount + emptyCount));

            Console.WriteLine();
            if (drawCount == batchCount)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"  ✓✓✓ PERFECT MATCH!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  ! DRAW count {drawCount}" +
                    $" != batch count {batchCount}");
                Console.ResetColor();
            }

            if (drawNodes.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  BATCH → BONE MAPPING:");
                Console.ResetColor();
                Console.WriteLine(
                    "  batch  bone  sec  bone_name");
                Console.WriteLine(
                    "  " + new string('-', 50));

                int sshow = Math.Min(
                    drawNodes.Count, 30);
                for (int i = 0; i < sshow; i++)
                {
                    var dn = drawNodes[i];
                    string hint = GuessBoneName(
                        dn.bone);
                    string sec = dn.sec >= 0
                        ? dn.sec.ToString("D3")
                        : "  -";
                    Console.WriteLine(
                        $"  {i,5}  {dn.bone,4}  " +
                        $"{sec,3}  {hint}");
                }
                if (drawNodes.Count > sshow)
                    Console.WriteLine(
                        $"  ... ({drawNodes.Count - sshow} more)");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 7: CHUNK 7 SCENE-GRAPH DECODER
        // Groups nodes by ANCHOR (LOD/render state)
        // ═════════════════════════════════════════
        public static void RunSceneGraph(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 7 SCENE GRAPH Decode",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (offs.Count < 8)
            {
                Console.WriteLine(
                    "  [!] No chunk 7");
                return;
            }

            int c7Start = offs[7];
            int c7End = (8 < offs.Count)
                ? offs[8] : data.Length;

            uint firstPtr = BitConverter
                .ToUInt32(data, c7Start);
            int actualNodes =
                (int)(firstPtr / 4);

            int batchCount = 0;
            if (offs.Count >= 9)
            {
                uint fbp = BitConverter
                    .ToUInt32(data, offs[8]);
                batchCount = (int)(fbp / 4);
            }

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones        : {boneCount}");
            Console.WriteLine(
                $"  Total nodes  : {actualNodes}");
            Console.WriteLine(
                $"  Batches (c8) : {batchCount}");
            Console.WriteLine();

            var groups =
                new List<List<(int idx,
                    ushort u0, ushort u1,
                    ushort u2, ushort u3,
                    string type)>>();
            List<(int, ushort, ushort,
                  ushort, ushort, string)>
                  cur = null;

            int totalDraws = 0;
            int totalLinks = 0;

            for (int i = 0;
                 i < actualNodes; i++)
            {
                int ptrPos = c7Start + i * 4;
                if (ptrPos + 4 > data.Length)
                    break;
                uint ptr = BitConverter
                    .ToUInt32(data, ptrPos);
                int recOff =
                    c7Start + (int)ptr;
                if (recOff + 8 > data.Length)
                    break;

                ushort u0 = BitConverter
                    .ToUInt16(data, recOff);
                ushort u1 = BitConverter
                    .ToUInt16(data, recOff + 2);
                ushort u2 = BitConverter
                    .ToUInt16(data, recOff + 4);
                ushort u3 = BitConverter
                    .ToUInt16(data, recOff + 6);

                string type;
                if (u0 == 0 && u1 == 0 &&
                    u2 == 0 && u3 == 0)
                    type = "EMPTY";
                else if (u0 == 140 && u2 == 1)
                {
                    type = "ANCHOR";
                    cur = new List<(int,
                        ushort, ushort,
                        ushort, ushort,
                        string)>();
                    groups.Add(cur);
                }
                else if (u2 == 1)
                {
                    type = "DRAW";
                    totalDraws++;
                }
                else
                {
                    type = "LINK";
                    totalLinks++;
                }

                if (cur == null)
                {
                    cur = new List<(int,
                        ushort, ushort,
                        ushort, ushort,
                        string)>();
                    groups.Add(cur);
                }
                cur.Add((i, u0, u1,
                    u2, u3, type));
            }

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                $"  ► Found {groups.Count} " +
                "scene groups (ANCHORs)");
            Console.WriteLine(
                $"  ► Total DRAW: {totalDraws}");
            Console.WriteLine(
                $"  ► Total LINK: {totalLinks}");
            Console.ResetColor();
            Console.WriteLine();

            for (int g = 0;
                 g < groups.Count; g++)
            {
                var grp = groups[g];
                int draws = grp.Count(
                    x => x.type == "DRAW");
                int links = grp.Count(
                    x => x.type == "LINK");

                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  ┌─ Scene Group {g + 1} " +
                    $"({grp.Count} nodes: " +
                    $"{draws} draws, " +
                    $"{links} links) ─");
                Console.ResetColor();

                foreach (var n in grp)
                {
                    ConsoleColor col =
                        n.type == "DRAW"
                        ? ConsoleColor.Green
                        : (n.type == "ANCHOR"
                          ? ConsoleColor.Yellow
                          : ConsoleColor.Gray);

                    string boneStr = "";
                    if (n.u0 < boneCount)
                        boneStr =
                            $"bone={n.u0:D3}";
                    if (n.u1 < boneCount &&
                        n.u1 != 0xFFFF &&
                        n.u1 != 0)
                        boneStr +=
                            $"→{n.u1:D3}";

                    Console.ForegroundColor =
                        col;
                    Console.WriteLine(
                        $"  │ [{n.idx,3}] " +
                        $"{n.type,-7} " +
                        $"u0={n.u0,5} " +
                        $"u1={n.u1,5} " +
                        $"u2={n.u2} " +
                        $"u3={n.u3}  " +
                        $"{boneStr}");
                    Console.ResetColor();
                }
                Console.WriteLine("  └");
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Bone usage across all groups:");
            Console.ResetColor();

            var boneUse =
                new Dictionary<int, int>();
            foreach (var g in groups)
                foreach (var n in g)
                {
                    if (n.u0 < boneCount)
                    {
                        if (!boneUse
                            .ContainsKey(n.u0))
                            boneUse[n.u0] = 0;
                        boneUse[n.u0]++;
                    }
                }

            foreach (var kv in boneUse
                .OrderByDescending(
                    k => k.Value)
                .Take(15))
            {
                string name = GuessBoneName(
                    kv.Key);
                Console.WriteLine(
                    $"    bone {kv.Key,3} " +
                    $"({name,-15}): " +
                    $"{kv.Value,3} refs");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 8: VIF VERTEX-BONE EXTRACTOR (OLD)
        // Buggy version - kept for compatibility
        // Use diag8b instead!
        // ═════════════════════════════════════════
        public static void RunVifBones(
            string rdtbPath)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[!] -diag8 has a bug. " +
                "Use -diag8b for correct results.");
            Console.ResetColor();
            RunVifBonesFixed(rdtbPath);
        }

        // ═════════════════════════════════════════
        // DIAG 8b: VIF BONE EXTRACTOR (FIXED!)
        // Uses CORRECT VIF header pattern:
        //   byte[0]=0x00, byte[1]=0x80, byte[3]=0x6C
        // CONFIRMS byte +0 = bone index!
        // ═════════════════════════════════════════
        public static void RunVifBonesFixed(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 11 VIF Bone Extractor (FIXED)",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (offs.Count < 12)
            {
                Console.WriteLine(
                    "  [!] No chunk 11");
                return;
            }

            int c11Start = offs[11];
            int c11End = (12 < offs.Count)
                ? offs[12] : data.Length;
            int c11Size = c11End - c11Start;

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones      : {boneCount}");
            Console.WriteLine(
                $"  Chunk 11   : 0x{c11Start:X6}" +
                $" ({c11Size:N0} bytes)");
            Console.WriteLine();

            var vifBlocks = new List<int>();
            for (int i = 0;
                 i + 16 <= c11Size; i += 4)
            {
                int p = c11Start + i;
                if (data[p] == 0x00 &&
                    data[p + 1] == 0x80 &&
                    data[p + 3] == 0x6C)
                    vifBlocks.Add(i);
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  ✓ VIF blocks found: " +
                $"{vifBlocks.Count}");
            Console.ResetColor();
            Console.WriteLine();

            int show = Math.Min(
                vifBlocks.Count, 10);

            for (int b = 0; b < show; b++)
            {
                int blkStart = vifBlocks[b];
                int blkEnd =
                    (b + 1 < vifBlocks.Count)
                    ? vifBlocks[b + 1]
                    : c11Size;

                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  ┌─ VIF Block {b} " +
                    $"@ 0x{c11Start + blkStart:X6}" +
                    $" (size {blkEnd - blkStart}) ─");
                Console.ResetColor();

                var bonesUsed =
                    new Dictionary<byte, int>();
                int rowOff = blkStart + 16;
                int rowsThis = 0;

                while (rowOff + 16 <= blkEnd)
                {
                    int p = c11Start + rowOff;
                    byte b0 = data[p];

                    uint flag = BitConverter
                        .ToUInt32(data, p);
                    if (flag == 0x70000000)
                        break;
                    if (flag == 0x3F800000)
                    {
                        uint m = BitConverter
                            .ToUInt32(
                                data, p + 4);
                        if (m == 0x14000000 ||
                            m == 0x17000000)
                            break;
                    }

                    if (!bonesUsed
                        .ContainsKey(b0))
                        bonesUsed[b0] = 0;
                    bonesUsed[b0]++;
                    rowsThis++;
                    rowOff += 16;
                }

                Console.WriteLine(
                    $"  │ {rowsThis} rows in block");
                Console.WriteLine(
                    "  │ Byte+0 distribution:");

                int inRange = 0;
                int total = 0;
                foreach (var kv in bonesUsed)
                {
                    total += kv.Value;
                    if (kv.Key < boneCount)
                        inRange += kv.Value;
                }

                double pct = total > 0
                    ? inRange * 100.0 / total
                    : 0;

                Console.ForegroundColor =
                    pct > 70
                    ? ConsoleColor.Green
                    : ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  │   {pct:F1}% in bone " +
                    $"range ({inRange}/{total})");
                Console.ResetColor();

                foreach (var kv in bonesUsed
                    .OrderByDescending(
                        k => k.Value)
                    .Take(8))
                {
                    string note = "";
                    if (kv.Key < boneCount)
                        note = " ← bone " +
                            GuessBoneName(kv.Key);
                    Console.WriteLine(
                        $"  │   0x{kv.Key:X2} " +
                        $"({kv.Key,3}) = " +
                        $"{kv.Value,4} hits{note}");
                }
                Console.WriteLine("  └");
                Console.WriteLine();
            }

            if (vifBlocks.Count > show)
                Console.WriteLine(
                    $"  ... ({vifBlocks.Count - show}" +
                    " more blocks)");

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ═══════════════════════════════");
            Console.WriteLine(
                "  AGGREGATE BONE USAGE (ALL BLOCKS)");
            Console.WriteLine(
                "  ═══════════════════════════════");
            Console.ResetColor();

            var allBones =
                new Dictionary<byte, int>();
            for (int b = 0;
                 b < vifBlocks.Count; b++)
            {
                int bs = vifBlocks[b];
                int be = (b + 1 < vifBlocks.Count)
                    ? vifBlocks[b + 1]
                    : c11Size;
                int ro = bs + 16;
                while (ro + 16 <= be)
                {
                    int p = c11Start + ro;
                    byte b0 = data[p];
                    uint flag = BitConverter
                        .ToUInt32(data, p);
                    if (flag == 0x70000000) break;
                    if (flag == 0x3F800000)
                    {
                        uint m = BitConverter
                            .ToUInt32(data, p + 4);
                        if (m == 0x14000000 ||
                            m == 0x17000000) break;
                    }
                    if (!allBones.ContainsKey(b0))
                        allBones[b0] = 0;
                    allBones[b0]++;
                    ro += 16;
                }
            }

            int agIn = 0;
            int agTot = 0;
            foreach (var kv in allBones)
            {
                agTot += kv.Value;
                if (kv.Key < boneCount)
                    agIn += kv.Value;
            }
            double agPct = agTot > 0
                ? agIn * 100.0 / agTot : 0;

            Console.ForegroundColor =
                agPct > 70
                ? ConsoleColor.Green
                : ConsoleColor.Yellow;
            Console.WriteLine(
                $"  Total: {agPct:F1}% in bone " +
                $"range ({agIn}/{agTot} rows)");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine(
                "  All bone-byte values found:");
            foreach (var kv in allBones
                .OrderBy(k => k.Key))
            {
                if (kv.Key >= boneCount &&
                    kv.Value < 10) continue;
                string note = kv.Key < boneCount
                    ? "← bone " +
                      GuessBoneName(kv.Key)
                    : "(out of range, " +
                      "may be flag/stride byte)";
                Console.WriteLine(
                    $"    0x{kv.Key:X2} " +
                    $"({kv.Key,3}) = " +
                    $"{kv.Value,5} hits  {note}");
            }

            Console.WriteLine();
            if (agPct > 70)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  ✓✓✓ CONFIRMED: byte +0 " +
                    "of each VIF row IS the");
                Console.WriteLine(
                    "  bone index!");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 10: SCAN ALL CHUNKS FOR VIF + BONES
        // KEY DIAGNOSTIC - finds the REAL mesh
        // chunk by counting unique bones used
        // ═════════════════════════════════════════
        public static void ScanAllChunksForVif(
            string rdtbPath)
        {
            PrintHeader(
                "ALL CHUNKS VIF + Bone Scan",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones in skeleton: {boneCount}");
            Console.WriteLine(
                $"  Chunks to scan   : {offs.Count}");
            Console.WriteLine();
            Console.WriteLine(
                "  ┌────────┬──────────┬──────────┬" +
                "─────────┬──────────┬─────────────┐");
            Console.WriteLine(
                "  │ Chunk  │ Offset   │ Size     │" +
                " VIF blks│ Bones used│ Rich mesh? │");
            Console.WriteLine(
                "  ├────────┼──────────┼──────────┼" +
                "─────────┼──────────┼─────────────┤");

            var richChunks = new List<int>();

            for (int ci = 0;
                 ci < offs.Count; ci++)
            {
                int cStart = offs[ci];
                int cEnd = (ci + 1 < offs.Count)
                    ? offs[ci + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                var vifBlocks = new List<int>();
                for (int i = 0;
                     i + 16 <= cSize; i += 4)
                {
                    int p = cStart + i;
                    if (p + 4 > data.Length)
                        break;
                    if (data[p] == 0x00 &&
                        data[p + 1] == 0x80 &&
                        data[p + 3] == 0x6C)
                        vifBlocks.Add(i);
                }

                var bonesUsed =
                    new HashSet<byte>();
                int totalRows = 0;
                int boneRows = 0;

                for (int b = 0;
                     b < vifBlocks.Count; b++)
                {
                    int bs = vifBlocks[b];
                    int be = (b + 1 <
                              vifBlocks.Count)
                        ? vifBlocks[b + 1]
                        : cSize;
                    int ro = bs + 16;

                    while (ro + 16 <= be)
                    {
                        int p = cStart + ro;
                        if (p + 16 > data.Length)
                            break;
                        byte b0 = data[p];

                        uint flag = BitConverter
                            .ToUInt32(data, p);
                        if (flag == 0x70000000)
                            break;
                        if (flag == 0x3F800000)
                        {
                            uint m = BitConverter
                                .ToUInt32(
                                    data,
                                    p + 4);
                            if (m == 0x14000000
                             || m == 0x17000000)
                                break;
                        }

                        totalRows++;
                        if (b0 < boneCount)
                        {
                            bonesUsed.Add(b0);
                            boneRows++;
                        }
                        ro += 16;
                    }
                }

                bool rich = bonesUsed.Count > 5;
                if (rich) richChunks.Add(ci);

                ConsoleColor col = rich
                    ? ConsoleColor.Green
                    : (vifBlocks.Count > 0
                       ? ConsoleColor.Gray
                       : ConsoleColor.DarkGray);

                Console.ForegroundColor = col;
                string richTag = rich
                    ? "  YES ★★★" : "no";
                Console.WriteLine(
                    $"  │  [{ci,2}]  │ " +
                    $"0x{cStart:X6} │ " +
                    $"{cSize,8:N0} │ " +
                    $"{vifBlocks.Count,7} │ " +
                    $"{bonesUsed.Count,8} │ " +
                    $"{richTag,11} │");
                Console.ResetColor();
            }

            Console.WriteLine(
                "  └────────┴──────────┴──────────┴" +
                "─────────┴──────────┴─────────────┘");

            Console.WriteLine();
            if (richChunks.Count > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  ★ RICH MESH CHUNKS " +
                    "(varied bones = body data):");
                foreach (int ci in richChunks)
                    Console.WriteLine(
                        $"      Chunk {ci}");
                Console.ResetColor();

                Console.WriteLine();
                int best = richChunks[0];
                int bestBones = 0;
                foreach (int ci in richChunks)
                {
                    int cStart = offs[ci];
                    int cEnd =
                        (ci + 1 < offs.Count)
                        ? offs[ci + 1]
                        : data.Length;
                    int cSize = cEnd - cStart;

                    var bSet =
                        new HashSet<byte>();
                    for (int i = 0;
                         i + 16 <= cSize; i += 16)
                    {
                        if (cStart + i >=
                            data.Length) break;
                        byte b0 =
                            data[cStart + i];
                        if (b0 < boneCount)
                            bSet.Add(b0);
                    }
                    if (bSet.Count > bestBones)
                    {
                        bestBones = bSet.Count;
                        best = ci;
                    }
                }

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"  ► Best mesh chunk: " +
                    $"Chunk {best} " +
                    $"(uses {bestBones} bones)");
                Console.WriteLine(
                    "  ► This is your real " +
                    "character body data!");
                Console.ResetColor();

                DumpChunkBones(
                    data, offs, best, boneCount);
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  ! No varied-bone chunks " +
                    "found. Mesh may use a");
                Console.WriteLine(
                    "    different bone-encoding " +
                    "scheme (e.g. high nibble).");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // Helper: detailed bone dump for a chunk
        private static void DumpChunkBones(
            byte[] data,
            List<int> offs,
            int ci,
            int boneCount)
        {
            int cStart = offs[ci];
            int cEnd = (ci + 1 < offs.Count)
                ? offs[ci + 1] : data.Length;
            int cSize = cEnd - cStart;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                $"  ═══ CHUNK {ci} BONE DETAIL ═══");
            Console.ResetColor();

            var counts =
                new Dictionary<byte, int>();
            int total = 0;

            var vifBlocks = new List<int>();
            for (int i = 0;
                 i + 16 <= cSize; i += 4)
            {
                int p = cStart + i;
                if (p + 4 > data.Length) break;
                if (data[p] == 0x00 &&
                    data[p + 1] == 0x80 &&
                    data[p + 3] == 0x6C)
                    vifBlocks.Add(i);
            }

            for (int b = 0;
                 b < vifBlocks.Count; b++)
            {
                int bs = vifBlocks[b];
                int be = (b + 1 < vifBlocks.Count)
                    ? vifBlocks[b + 1] : cSize;
                int ro = bs + 16;
                while (ro + 16 <= be)
                {
                    int p = cStart + ro;
                    if (p + 16 > data.Length)
                        break;
                    byte b0 = data[p];
                    uint flag = BitConverter
                        .ToUInt32(data, p);
                    if (flag == 0x70000000) break;
                    if (flag == 0x3F800000)
                    {
                        uint m = BitConverter
                            .ToUInt32(
                                data, p + 4);
                        if (m == 0x14000000 ||
                            m == 0x17000000) break;
                    }
                    if (!counts.ContainsKey(b0))
                        counts[b0] = 0;
                    counts[b0]++;
                    total++;
                    ro += 16;
                }
            }

            Console.WriteLine(
                $"  Total VIF rows: {total}");
            Console.WriteLine(
                "  Bone usage breakdown:");
            Console.WriteLine();

            foreach (var kv in counts
                .Where(k => k.Key < boneCount)
                .OrderByDescending(
                    k => k.Value))
            {
                double pct = total > 0
                    ? kv.Value * 100.0 / total
                    : 0;
                int barLen =
                    Math.Max(0,
                    Math.Min(50, (int)(pct / 2)));
                string bar = new string(
                    '█', barLen);
                Console.WriteLine(
                    $"    bone {kv.Key,3} " +
                    $"({GuessBoneName(kv.Key),-15})" +
                    $" {kv.Value,5} hits " +
                    $"({pct,5:F1}%)  {bar}");
            }

            Console.WriteLine();
        }

        // ═════════════════════════════════════════
        // SLUS LBA TABLE ANALYZER (BASIC)
        // ═════════════════════════════════════════
        public static void AnalyzeSlusLba(
            string slusPath, bool isJapanese)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SLUS LBA Table Analyzer");
            Console.WriteLine(
                "    File: " +
                Path.GetFileName(slusPath));
            Console.WriteLine(
                "    Region: " +
                (isJapanese ? "Japanese (JPN)"
                            : "USA"));
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));

            if (!File.Exists(slusPath))
            {
                Console.WriteLine(
                    "  [!] SLUS file not found!");
                return;
            }

            int lbaStart = isJapanese
                ? LBA_JPN_START : LBA_USA_START;
            int lbaEnd = isJapanese
                ? LBA_JPN_END : LBA_USA_END;
            int lbaSize = lbaEnd - lbaStart;

            byte[] slus = File.ReadAllBytes(
                slusPath);

            Console.WriteLine();
            Console.WriteLine(
                $"  SLUS file size : " +
                $"{slus.Length:N0} bytes");

            if (slus.Length < lbaEnd)
            {
                Console.WriteLine(
                    $"  [!] SLUS too small!");
                return;
            }

            Console.WriteLine(
                $"  LBA region     : " +
                $"0x{lbaStart:X6} - 0x{lbaEnd:X6}");
            Console.WriteLine(
                $"  LBA total size : " +
                $"{lbaSize} bytes (0x{lbaSize:X4})");

            int[] candidateSizes =
                { 8, 12, 16, 20, 24, 32 };
            int bestSize = 8;
            int bestScore = 0;

            Console.WriteLine();
            Console.WriteLine(
                "  Auto-detecting entry size:");

            foreach (int sz in candidateSizes)
            {
                if (lbaSize % sz != 0) continue;
                int entryCount = lbaSize / sz;
                int score = 0;

                for (int i = 0;
                     i < entryCount; i++)
                {
                    int off = lbaStart + i * sz;
                    if (off + 8 > slus.Length)
                        break;
                    uint lba =
                        BitConverter.ToUInt32(
                            slus, off);
                    uint size =
                        BitConverter.ToUInt32(
                            slus, off + 4);

                    if (lba > 0 &&
                        lba < 1_000_000 &&
                        size > 0 &&
                        size < 100_000_000)
                        score++;
                }

                double pct = entryCount > 0
                    ? score * 100.0 / entryCount
                    : 0;
                Console.WriteLine(
                    $"    {sz,2}B entries → " +
                    $"{entryCount,4} entries, " +
                    $"valid: {score}/{entryCount} " +
                    $"({pct:F1}%)");

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSize = sz;
                }
            }

            int finalCount = lbaSize / bestSize;
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  ► Best fit: {bestSize}B entries" +
                $" × {finalCount} = {lbaSize}B");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  First 30 entries:");
            Console.WriteLine(
                "  [idx]  LBA(sect)   ByteOff      " +
                "Size            Extra");
            Console.WriteLine(
                "  " + new string('-', 70));

            int show = Math.Min(30, finalCount);
            for (int i = 0; i < show; i++)
            {
                int off = lbaStart + i * bestSize;
                if (off + 8 > slus.Length) break;

                uint lba = BitConverter.ToUInt32(
                    slus, off);
                uint size = BitConverter.ToUInt32(
                    slus, off + 4);
                long byteOff = (long)lba * 2048;

                string extra = "";
                if (bestSize >= 12 &&
                    off + 12 <= slus.Length)
                {
                    uint x = BitConverter.ToUInt32(
                        slus, off + 8);
                    extra = $"0x{x:X8}";
                }

                Console.WriteLine(
                    $"  [{i,3}]  {lba,10}  " +
                    $"0x{byteOff:X9}  " +
                    $"{size,12:N0}  {extra}");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // SLUS LBA - FIXED (size = next-this)*2048
        // ═════════════════════════════════════════
        public static void AnalyzeSlusLbaFixed(
            string slusPath, bool isJapanese)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SLUS LBA Decoder (FIXED)");
            Console.WriteLine(
                "    File: " +
                Path.GetFileName(slusPath));
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));

            if (!File.Exists(slusPath))
            {
                Console.WriteLine(
                    "  [!] Not found");
                return;
            }

            int lbaStart = isJapanese
                ? LBA_JPN_START : LBA_USA_START;
            int lbaEnd = isJapanese
                ? LBA_JPN_END : LBA_USA_END;
            int lbaSize = lbaEnd - lbaStart;
            int entrySize = 8;
            int entryCount = lbaSize / entrySize;

            byte[] slus = File.ReadAllBytes(
                slusPath);

            Console.WriteLine();
            Console.WriteLine(
                $"  Entries: {entryCount}");
            Console.WriteLine(
                $"  Format : " +
                "[start_LBA(4)] [next_LBA(4)]");
            Console.WriteLine(
                $"  Size   : (next-this) × 2048");
            Console.WriteLine();

            Console.WriteLine(
                "  All entries with computed size:");
            Console.WriteLine(
                "  [idx]  start_LBA   next_LBA  " +
                "  ByteOffset    Size (B)        Notes");
            Console.WriteLine(
                "  " + new string('-', 80));

            var entries =
                new List<(int idx, uint lba,
                    uint nextLba, long byteOff,
                    long size)>();

            for (int i = 0;
                 i < entryCount; i++)
            {
                int off = lbaStart +
                    i * entrySize;
                if (off + 8 > slus.Length)
                    break;

                uint lba = BitConverter
                    .ToUInt32(slus, off);
                uint nextLba = BitConverter
                    .ToUInt32(slus, off + 4);

                long byteOff =
                    (long)lba * 2048;
                long size = (nextLba > lba)
                    ? (long)(nextLba - lba)
                        * 2048
                    : 0;

                entries.Add(
                    (i, lba, nextLba,
                     byteOff, size));
            }

            int show = Math.Min(
                30, entries.Count);
            for (int i = 0; i < show; i++)
            {
                var e = entries[i];
                string note = "";
                ConsoleColor col =
                    ConsoleColor.Gray;

                if (e.size >= 1_900_000 &&
                    e.size <= 2_200_000)
                {
                    note = "← BOY-SIZED!";
                    col = ConsoleColor.Green;
                }
                else if (e.size >= 600_000 &&
                         e.size <= 700_000)
                {
                    note = "← HAYATO-SIZED!";
                    col = ConsoleColor.Green;
                }

                Console.ForegroundColor = col;
                Console.WriteLine(
                    $"  [{e.idx,3}]  " +
                    $"{e.lba,9}  {e.nextLba,9}  " +
                    $"0x{e.byteOff:X9}  " +
                    $"{e.size,12:N0}  {note}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  All character/HDA candidates" +
                " (50KB - 5MB):");
            Console.ResetColor();
            Console.WriteLine(
                "  [idx]  start_LBA   ByteOffset" +
                "    Size (B)         Notes");
            Console.WriteLine(
                "  " + new string('-', 70));

            int matches = 0;
            foreach (var e in entries)
            {
                if (e.size < 50_000 ||
                    e.size > 5_000_000)
                    continue;

                string note = "";
                ConsoleColor col =
                    ConsoleColor.Gray;

                if (e.size >= 1_900_000 &&
                    e.size <= 2_200_000)
                {
                    note = "← BOY.HDA?";
                    col = ConsoleColor.Green;
                }
                else if (e.size >= 600_000 &&
                         e.size <= 700_000)
                {
                    note = "← HAYATO.HDA?";
                    col = ConsoleColor.Green;
                }
                else if (e.size > 800_000)
                {
                    note = "← character HDA?";
                    col = ConsoleColor.Yellow;
                }
                else
                {
                    note = "← prop/tool HDA?";
                    col = ConsoleColor.DarkCyan;
                }

                Console.ForegroundColor = col;
                Console.WriteLine(
                    $"  [{e.idx,3}]  " +
                    $"{e.lba,9}    " +
                    $"0x{e.byteOff:X9}  " +
                    $"{e.size,12:N0}  {note}");
                Console.ResetColor();

                matches++;
            }

            Console.WriteLine();
            Console.WriteLine(
                $"  Total matches: {matches}");

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 9: SLUS FULL ANALYZER + CSV EXPORT
        // ═════════════════════════════════════════
        public static void AnalyzeSlusFull(
            string slusPath, bool isJapanese)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SLUS LBA Full Analyzer");
            Console.WriteLine(
                "    File: " +
                Path.GetFileName(slusPath));
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));

            if (!File.Exists(slusPath))
            {
                Console.WriteLine(
                    "  [!] File not found");
                return;
            }

            int lbaStart = isJapanese
                ? LBA_JPN_START : LBA_USA_START;
            int lbaEnd = isJapanese
                ? LBA_JPN_END : LBA_USA_END;
            int lbaSize = lbaEnd - lbaStart;
            int entrySize = 8;
            int entryCount = lbaSize / entrySize;

            byte[] slus = File.ReadAllBytes(
                slusPath);

            var entries =
                new List<(int idx, uint lba,
                    uint nextLba, long byteOff,
                    long size)>();

            for (int i = 0; i < entryCount; i++)
            {
                int off = lbaStart +
                    i * entrySize;
                if (off + 8 > slus.Length)
                    break;

                uint lba = BitConverter
                    .ToUInt32(slus, off);
                uint nextLba = BitConverter
                    .ToUInt32(slus, off + 4);

                long byteOff =
                    (long)lba * 2048;
                long size = (nextLba > lba)
                    ? (long)(nextLba - lba) * 2048
                    : 0;

                entries.Add(
                    (i, lba, nextLba,
                     byteOff, size));
            }

            int catTiny = 0;
            int catSmall = 0;
            int catMedium = 0;
            int catLarge = 0;
            int catHuge = 0;
            int catGiant = 0;

            foreach (var e in entries)
            {
                if (e.size < 50_000) catTiny++;
                else if (e.size < 200_000)
                    catSmall++;
                else if (e.size < 500_000)
                    catMedium++;
                else if (e.size < 1_000_000)
                    catLarge++;
                else if (e.size < 3_000_000)
                    catHuge++;
                else catGiant++;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  Size distribution:");
            Console.ResetColor();
            Console.WriteLine(
                $"    Tiny    (<50KB)  : {catTiny,4} files");
            Console.WriteLine(
                $"    Small   (50-200K): {catSmall,4} files");
            Console.WriteLine(
                $"    Medium  (200-500K): {catMedium,4} files");
            Console.WriteLine(
                $"    Large   (500K-1M): {catLarge,4} files " +
                "  ← character HDAs likely");
            Console.WriteLine(
                $"    Huge    (1-3M)   : {catHuge,4} files " +
                "  ← BOY/main characters");
            Console.WriteLine(
                $"    Giant   (>3M)    : {catGiant,4} files");

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  HUGE entries (>1MB) - likely " +
                "main player characters:");
            Console.ResetColor();
            Console.WriteLine(
                "  [idx]  start_LBA   ByteOff      " +
                "Size (B)         Hint");
            Console.WriteLine(
                "  " + new string('-', 70));

            foreach (var e in entries)
            {
                if (e.size < 1_000_000) continue;
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"  [{e.idx,3}]  {e.lba,9}  " +
                    $"0x{e.byteOff:X9}  " +
                    $"{e.size,12:N0}  " +
                    $"← BOY-class character");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  LARGE entries (500KB-1MB) - " +
                "NPC characters:");
            Console.ResetColor();
            Console.WriteLine(
                "  [idx]  start_LBA   ByteOff      " +
                "Size (B)");
            Console.WriteLine(
                "  " + new string('-', 60));

            int npcCount = 0;
            foreach (var e in entries)
            {
                if (e.size < 500_000 ||
                    e.size >= 1_000_000)
                    continue;
                npcCount++;

                ConsoleColor col =
                    (e.size > 600_000 &&
                     e.size < 700_000)
                    ? ConsoleColor.Green
                    : ConsoleColor.Gray;

                Console.ForegroundColor = col;
                Console.WriteLine(
                    $"  [{e.idx,3}]  {e.lba,9}  " +
                    $"0x{e.byteOff:X9}  " +
                    $"{e.size,12:N0}");
                Console.ResetColor();
            }
            Console.WriteLine(
                $"\n  Total NPC-sized: {npcCount}");

            string csvPath =
                Path.GetDirectoryName(slusPath);
            if (string.IsNullOrEmpty(csvPath))
                csvPath = ".";
            csvPath = Path.Combine(
                csvPath, "slus_lba_table.csv");

            using (var sw =
                new StreamWriter(csvPath))
            {
                sw.WriteLine(
                    "index,start_lba,next_lba," +
                    "byte_offset,size_bytes," +
                    "size_kb,category");

                foreach (var e in entries)
                {
                    string cat;
                    if (e.size < 50_000)
                        cat = "tiny";
                    else if (e.size < 200_000)
                        cat = "small";
                    else if (e.size < 500_000)
                        cat = "medium";
                    else if (e.size < 1_000_000)
                        cat = "large_npc";
                    else if (e.size < 3_000_000)
                        cat = "huge_player";
                    else cat = "giant";

                    sw.WriteLine(
                        $"{e.idx},{e.lba}," +
                        $"{e.nextLba}," +
                        $"0x{e.byteOff:X9}," +
                        $"{e.size}," +
                        $"{e.size / 1024.0:F1}," +
                        $"{cat}");
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  ✓ Complete LBA table saved to:");
            Console.WriteLine(
                $"    {csvPath}");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // LBAUPDATE: Update SLUS LBA entry safely
        // Used after rebuilding HDA with new size
        // ═════════════════════════════════════════
        public static void UpdateSlusLba(
            string slusPath,
            int entryIndex,
            long newSizeBytes,
            bool isJapanese)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SLUS LBA Updater");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));

            int lbaStart = isJapanese
                ? LBA_JPN_START : LBA_USA_START;
            int lbaEnd = isJapanese
                ? LBA_JPN_END : LBA_USA_END;
            int lbaSize = lbaEnd - lbaStart;
            int entrySize = 8;
            int entryCount = lbaSize / entrySize;

            if (entryIndex < 0 ||
                entryIndex >= entryCount)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    $"  [!] Index {entryIndex} " +
                    $"out of range (0-{entryCount - 1})");
                Console.ResetColor();
                return;
            }

            byte[] slus = File.ReadAllBytes(
                slusPath);

            int off = lbaStart +
                entryIndex * entrySize;
            uint lba = BitConverter.ToUInt32(
                slus, off);
            uint nextLba = BitConverter.ToUInt32(
                slus, off + 4);
            long oldSize = (nextLba > lba)
                ? (long)(nextLba - lba) * 2048
                : 0;

            Console.WriteLine(
                $"  Entry [{entryIndex}]:");
            Console.WriteLine(
                $"    Start LBA: {lba}");
            Console.WriteLine(
                $"    Old next : {nextLba} " +
                $"(size: {oldSize:N0} B)");

            long sectorsNeeded =
                (newSizeBytes + 2047) / 2048;
            uint newNextLba = (uint)
                (lba + sectorsNeeded);

            Console.WriteLine(
                $"    New size : {newSizeBytes:N0} B");
            Console.WriteLine(
                $"    New next : {newNextLba}");

            long shift = newNextLba - nextLba;
            Console.WriteLine(
                $"    Sector shift: {shift}");

            if (shift == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  [i] No change needed");
                Console.ResetColor();
                return;
            }

            string backup =
                slusPath + ".backup";
            if (!File.Exists(backup))
            {
                File.Copy(slusPath, backup);
                Console.WriteLine(
                    $"  ✓ Backup: {backup}");
            }

            Console.WriteLine();
            Console.WriteLine(
                "  Shifting all subsequent " +
                "LBA entries by " + shift +
                " sectors:");

            for (int i = entryIndex;
                 i < entryCount; i++)
            {
                int eOff = lbaStart +
                    i * entrySize;
                uint cur = BitConverter
                    .ToUInt32(slus, eOff);
                uint nxt = BitConverter
                    .ToUInt32(slus, eOff + 4);

                if (i == entryIndex)
                {
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(
                            newNextLba),
                        0, slus, eOff + 4, 4);
                }
                else
                {
                    uint newCur =
                        (uint)(cur + shift);
                    uint newNxt =
                        (uint)(nxt + shift);
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(
                            newCur),
                        0, slus, eOff, 4);
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(
                            newNxt),
                        0, slus, eOff + 4, 4);
                }
            }

            File.WriteAllBytes(slusPath, slus);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  ✓ SLUS updated!");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "  ⚠ REMEMBER: You must also " +
                "physically place the new HDA");
            Console.WriteLine(
                "    file at the correct LBA " +
                "in the ISO!");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // BACKWARD COMPATIBILITY ALIASES
        // ═════════════════════════════════════════
        public static void RunChunk7(
            string rdtbPath)
        {
            RunRangeScan(rdtbPath);
        }

        public static void RunChunk7Deep(
            string rdtbPath)
        {
            RunDeepDump(rdtbPath);
        }

        public static void RunChunk7Struct(
            string rdtbPath)
        {
            RunStructured(rdtbPath);
        }

        // ═════════════════════════════════════════
        // DIAG 11: FULL VIF ROW BYTE SCAN
        // Tests bone-index location at EVERY byte
        // position (0-15) of each 16-byte VIF row
        // to find where the real bone IDs are stored
        // ═════════════════════════════════════════
        public static void RunVifByteScan(
            string rdtbPath)
        {
            PrintHeader(
                "VIF Row Byte Position Scanner",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones: {boneCount}");
            Console.WriteLine();

            // Scan chunks 11, 12, 13
            // (the actual mesh chunks)
            for (int chunkIdx = 11;
                 chunkIdx <= 13; chunkIdx++)
            {
                if (chunkIdx >= offs.Count)
                    continue;

                int cStart = offs[chunkIdx];
                int cEnd = (chunkIdx + 1 <
                            offs.Count)
                    ? offs[chunkIdx + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"\n  ═══ CHUNK {chunkIdx} " +
                    $"({cSize:N0} bytes) ═══");
                Console.ResetColor();

                // Find VIF blocks
                var vifBlocks =
                    new List<int>();
                for (int i = 0;
                     i + 16 <= cSize; i += 4)
                {
                    int p = cStart + i;
                    if (p + 4 > data.Length)
                        break;
                    if (data[p] == 0x00 &&
                        data[p + 1] == 0x80 &&
                        data[p + 3] == 0x6C)
                        vifBlocks.Add(i);
                }

                Console.WriteLine(
                    $"  VIF blocks: {vifBlocks.Count}");

                if (vifBlocks.Count == 0)
                    continue;

                // For each byte position 0-15,
                // tally how often that byte is
                // a valid bone index
                int[] inRange = new int[16];
                int[] uniqueValues = new int[16];
                var seen =
                    new HashSet<byte>[16];
                for (int j = 0; j < 16; j++)
                    seen[j] = new HashSet<byte>();

                int totalRows = 0;

                for (int b = 0;
                     b < vifBlocks.Count; b++)
                {
                    int bs = vifBlocks[b];
                    int be = (b + 1 <
                              vifBlocks.Count)
                        ? vifBlocks[b + 1]
                        : cSize;

                    // Skip 16-byte VIF header
                    int ro = bs + 16;

                    while (ro + 16 <= be)
                    {
                        int p = cStart + ro;
                        if (p + 16 >
                            data.Length) break;

                        uint flag = BitConverter
                            .ToUInt32(data, p);
                        if (flag == 0x70000000)
                            break;
                        if (flag == 0x3F800000)
                        {
                            uint m = BitConverter
                                .ToUInt32(
                                    data,
                                    p + 4);
                            if (m == 0x14000000
                             || m == 0x17000000)
                                break;
                        }

                        // Test each byte position
                        for (int bp = 0;
                             bp < 16; bp++)
                        {
                            byte v =
                                data[p + bp];
                            if (v < boneCount)
                            {
                                inRange[bp]++;
                                seen[bp].Add(v);
                            }
                        }

                        totalRows++;
                        ro += 16;
                    }
                }

                Console.WriteLine(
                    $"  Total rows scanned: " +
                    $"{totalRows:N0}");
                Console.WriteLine();
                Console.WriteLine(
                    "  Byte position results:");
                Console.WriteLine(
                    "  Pos  In-range %  Unique  " +
                    "Verdict");
                Console.WriteLine(
                    "  " + new string('-', 50));

                int bestPos = -1;
                int bestUnique = 0;

                for (int bp = 0; bp < 16; bp++)
                {
                    double pct = totalRows > 0
                        ? inRange[bp] * 100.0 /
                          totalRows
                        : 0;
                    int uniq = seen[bp].Count;

                    string verdict = "";
                    ConsoleColor col =
                        ConsoleColor.Gray;

                    if (pct > 90 && uniq > 10)
                    {
                        verdict =
                            "★★★ STRONG BONE INDEX!";
                        col = ConsoleColor.Green;
                        if (uniq > bestUnique)
                        {
                            bestUnique = uniq;
                            bestPos = bp;
                        }
                    }
                    else if (pct > 70 &&
                             uniq > 5)
                    {
                        verdict =
                            "★ possible bone";
                        col =
                            ConsoleColor.Yellow;
                    }
                    else if (uniq <= 2)
                    {
                        verdict =
                            "(constant/flag)";
                    }

                    Console.ForegroundColor =
                        col;
                    Console.WriteLine(
                        $"  +{bp,2}  {pct,8:F1}%  " +
                        $"{uniq,6}   {verdict}");
                    Console.ResetColor();
                }

                if (bestPos >= 0)
                {
                    Console.WriteLine();
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        $"  ✓ BEST CANDIDATE: " +
                        $"byte +{bestPos} " +
                        $"({bestUnique} unique " +
                        "bones used)");
                    Console.ResetColor();

                    DumpBonesAtPosition(
                        data, vifBlocks,
                        cStart, cSize,
                        bestPos, boneCount);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        private static void DumpBonesAtPosition(
            byte[] data,
            List<int> vifBlocks,
            int cStart, int cSize,
            int pos, int boneCount)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                $"  Bone usage at +{pos}:");
            Console.ResetColor();

            var counts =
                new Dictionary<byte, int>();
            int total = 0;

            for (int b = 0;
                 b < vifBlocks.Count; b++)
            {
                int bs = vifBlocks[b];
                int be = (b + 1 <
                          vifBlocks.Count)
                    ? vifBlocks[b + 1]
                    : cSize;
                int ro = bs + 16;

                while (ro + 16 <= be)
                {
                    int p = cStart + ro;
                    if (p + 16 > data.Length)
                        break;

                    uint flag = BitConverter
                        .ToUInt32(data, p);
                    if (flag == 0x70000000)
                        break;
                    if (flag == 0x3F800000)
                    {
                        uint m = BitConverter
                            .ToUInt32(
                                data, p + 4);
                        if (m == 0x14000000 ||
                            m == 0x17000000)
                            break;
                    }

                    byte v = data[p + pos];
                    if (v < boneCount)
                    {
                        if (!counts
                            .ContainsKey(v))
                            counts[v] = 0;
                        counts[v]++;
                        total++;
                    }
                    ro += 16;
                }
            }

            foreach (var kv in counts
                .OrderByDescending(
                    k => k.Value)
                .Take(20))
            {
                double pct = total > 0
                    ? kv.Value * 100.0 / total
                    : 0;
                int barLen =
                    Math.Max(0,
                    Math.Min(40, (int)pct));
                string bar = new string(
                    '█', barLen);
                Console.WriteLine(
                    $"    bone {kv.Key,3} " +
                    $"({GuessBoneName(kv.Key),-15})" +
                    $" {kv.Value,5} hits " +
                    $"({pct,5:F1}%) {bar}");
            }
        }

        // ═════════════════════════════════════════
        // DIAG 12: VIF NIBBLE SCANNER
        // Tests if bone index is encoded in
        // HIGH/LOW nibbles of any byte position
        // (some PS2 games pack 2 bones per byte)
        // ═════════════════════════════════════════
        public static void RunVifNibbleScan(
            string rdtbPath)
        {
            PrintHeader(
                "VIF Nibble Bone Scanner",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones: {boneCount}");
            Console.WriteLine();

            for (int chunkIdx = 11;
                 chunkIdx <= 13; chunkIdx++)
            {
                if (chunkIdx >= offs.Count)
                    continue;

                int cStart = offs[chunkIdx];
                int cEnd = (chunkIdx + 1 <
                            offs.Count)
                    ? offs[chunkIdx + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"\n  ═══ CHUNK {chunkIdx} ═══");
                Console.ResetColor();

                var vifBlocks =
                    new List<int>();
                for (int i = 0;
                     i + 16 <= cSize; i += 4)
                {
                    int p = cStart + i;
                    if (p + 4 > data.Length)
                        break;
                    if (data[p] == 0x00 &&
                        data[p + 1] == 0x80 &&
                        data[p + 3] == 0x6C)
                        vifBlocks.Add(i);
                }

                if (vifBlocks.Count == 0)
                    continue;

                // Test high and low nibbles
                int[] hiInRange = new int[16];
                int[] loInRange = new int[16];
                var hiSeen =
                    new HashSet<byte>[16];
                var loSeen =
                    new HashSet<byte>[16];
                for (int j = 0; j < 16; j++)
                {
                    hiSeen[j] =
                        new HashSet<byte>();
                    loSeen[j] =
                        new HashSet<byte>();
                }

                int totalRows = 0;

                for (int b = 0;
                     b < vifBlocks.Count; b++)
                {
                    int bs = vifBlocks[b];
                    int be = (b + 1 <
                              vifBlocks.Count)
                        ? vifBlocks[b + 1]
                        : cSize;
                    int ro = bs + 16;

                    while (ro + 16 <= be)
                    {
                        int p = cStart + ro;
                        if (p + 16 >
                            data.Length) break;

                        uint flag = BitConverter
                            .ToUInt32(data, p);
                        if (flag == 0x70000000)
                            break;
                        if (flag == 0x3F800000)
                        {
                            uint m = BitConverter
                                .ToUInt32(
                                    data,
                                    p + 4);
                            if (m == 0x14000000
                             || m == 0x17000000)
                                break;
                        }

                        for (int bp = 0;
                             bp < 16; bp++)
                        {
                            byte v =
                                data[p + bp];
                            byte hi =
                                (byte)((v >> 4)
                                       & 0x0F);
                            byte lo =
                                (byte)(v & 0x0F);

                            // Try x16
                            byte hiX =
                                (byte)(hi * 16);
                            byte loX =
                                (byte)(lo * 16);

                            if (hi < boneCount)
                            {
                                hiInRange[bp]++;
                                hiSeen[bp]
                                    .Add(hi);
                            }
                            if (lo < boneCount)
                            {
                                loInRange[bp]++;
                                loSeen[bp]
                                    .Add(lo);
                            }
                        }

                        totalRows++;
                        ro += 16;
                    }
                }

                Console.WriteLine(
                    $"  Rows: {totalRows}");
                Console.WriteLine();
                Console.WriteLine(
                    "  Pos  Hi-nibble  Lo-nibble" +
                    "  Hi-uniq  Lo-uniq");
                Console.WriteLine(
                    "  " + new string('-', 55));

                for (int bp = 0; bp < 16; bp++)
                {
                    double hiPct = totalRows > 0
                        ? hiInRange[bp] * 100.0 /
                          totalRows
                        : 0;
                    double loPct = totalRows > 0
                        ? loInRange[bp] * 100.0 /
                          totalRows
                        : 0;
                    int hiU = hiSeen[bp].Count;
                    int loU = loSeen[bp].Count;

                    ConsoleColor col =
                        ConsoleColor.Gray;
                    if ((hiPct > 80 && hiU > 10)
                     || (loPct > 80 && loU > 10))
                        col = ConsoleColor.Green;

                    Console.ForegroundColor =
                        col;
                    Console.WriteLine(
                        $"  +{bp,2}  {hiPct,7:F1}%   " +
                        $"{loPct,7:F1}%   " +
                        $"{hiU,7}  {loU,7}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 13: VIF ROW HEX DUMP
        // Shows raw hex of first vertex rows from
        // chunk 11/12/13 to manually inspect format
        // ═════════════════════════════════════════
        public static void RunVifHexDump(
            string rdtbPath)
        {
            PrintHeader(
                "VIF Row Raw Hex Dump",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones: {boneCount}");

            for (int chunkIdx = 11;
                 chunkIdx <= 13; chunkIdx++)
            {
                if (chunkIdx >= offs.Count)
                    continue;

                int cStart = offs[chunkIdx];
                int cEnd = (chunkIdx + 1 <
                            offs.Count)
                    ? offs[chunkIdx + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"\n  ═══ CHUNK {chunkIdx} ═══");
                Console.ResetColor();

                var vifBlocks =
                    new List<int>();
                for (int i = 0;
                     i + 16 <= cSize; i += 4)
                {
                    int p = cStart + i;
                    if (p + 4 > data.Length)
                        break;
                    if (data[p] == 0x00 &&
                        data[p + 1] == 0x80 &&
                        data[p + 3] == 0x6C)
                        vifBlocks.Add(i);
                }

                if (vifBlocks.Count == 0)
                    continue;

                // Dump first 3 VIF blocks
                int blocksToShow =
                    Math.Min(3, vifBlocks.Count);

                for (int b = 0;
                     b < blocksToShow; b++)
                {
                    int bs = vifBlocks[b];
                    int be = (b + 1 <
                              vifBlocks.Count)
                        ? vifBlocks[b + 1]
                        : cSize;

                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"\n  ┌─ Block {b} " +
                        $"@ 0x{cStart + bs:X6} ─");
                    Console.ResetColor();

                    // Show VIF header
                    Console.Write(
                        "  │ HEADER: ");
                    for (int j = 0; j < 16; j++)
                    {
                        if (cStart + bs + j >=
                            data.Length) break;
                        Console.Write(
                            data[cStart + bs + j]
                                .ToString("X2") +
                            " ");
                    }
                    Console.WriteLine();

                    // Show first 8 vertex rows
                    int ro = bs + 16;
                    int rowsShown = 0;

                    while (ro + 16 <= be &&
                           rowsShown < 8)
                    {
                        int p = cStart + ro;
                        if (p + 16 >
                            data.Length) break;

                        uint flag = BitConverter
                            .ToUInt32(data, p);
                        if (flag == 0x70000000)
                            break;
                        if (flag == 0x3F800000)
                        {
                            uint m = BitConverter
                                .ToUInt32(
                                    data,
                                    p + 4);
                            if (m == 0x14000000
                             || m == 0x17000000)
                                break;
                        }

                        // Format: hex bytes,
                        // then floats, then
                        // bone-candidates
                        Console.Write(
                            $"  │ R{rowsShown}: ");
                        for (int j = 0;
                             j < 16; j++)
                        {
                            byte v = data[p + j];
                            // Color bytes
                            // 0..boneCount in
                            // green
                            if (v < boneCount &&
                                v > 0)
                                Console
                                .ForegroundColor =
                                    ConsoleColor
                                    .Green;
                            else
                                Console
                                .ForegroundColor =
                                    ConsoleColor
                                    .Gray;
                            Console.Write(
                                v.ToString("X2") +
                                " ");
                            Console.ResetColor();
                        }

                        // Show as floats
                        float f0 = BitConverter
                            .ToSingle(
                                data, p);
                        float f1 = BitConverter
                            .ToSingle(
                                data, p + 4);
                        float f2 = BitConverter
                            .ToSingle(
                                data, p + 8);
                        float f3 = BitConverter
                            .ToSingle(
                                data, p + 12);

                        Console.Write(
                            "  | ");
                        Console
                            .ForegroundColor =
                            ConsoleColor.Cyan;
                        Console.Write(
                            $"f=({f0,7:F2},{f1,7:F2}," +
                            $"{f2,7:F2},{f3,7:F2})");
                        Console.ResetColor();
                        Console.WriteLine();

                        rowsShown++;
                        ro += 16;
                    }
                    Console.WriteLine("  └");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 14: CHUNK 1-6 FORMAT DETECTOR
        // The REAL body mesh is in chunks 1-6.
        // They don't use VIF format - this finds
        // the actual format by analyzing patterns.
        // ═════════════════════════════════════════
        public static void DetectChunk16Format(
            string rdtbPath)
        {
            PrintHeader(
                "Chunk 1-6 Format Detector",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);

            Console.WriteLine();

            for (int ci = 1; ci <= 6; ci++)
            {
                if (ci >= offs.Count) break;

                int cStart = offs[ci];
                int cEnd = (ci + 1 < offs.Count)
                    ? offs[ci + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"\n  ═══ CHUNK {ci} " +
                    $"({cSize:N0} bytes) ═══");
                Console.ResetColor();

                // First 64 bytes hex dump
                Console.WriteLine(
                    "  First 64 bytes:");
                Console.Write("  ");
                int dumpLen =
                    Math.Min(64, cSize);
                for (int i = 0; i < dumpLen; i++)
                {
                    if (cStart + i >=
                        data.Length) break;
                    if (i > 0 && i % 16 == 0)
                        Console.Write("\n  ");
                    Console.Write(
                        data[cStart + i]
                            .ToString("X2") +
                        " ");
                }
                Console.WriteLine();
                Console.WriteLine();

                // Treat first 4 bytes as
                // potential count/size
                if (cSize >= 4)
                {
                    uint dw0 = BitConverter
                        .ToUInt32(
                            data, cStart);
                    uint dw1 = (cSize >= 8)
                        ? BitConverter
                            .ToUInt32(
                                data,
                                cStart + 4)
                        : 0;
                    uint dw2 = (cSize >= 12)
                        ? BitConverter
                            .ToUInt32(
                                data,
                                cStart + 8)
                        : 0;

                    Console.WriteLine(
                        $"  First DWORDs: " +
                        $"0x{dw0:X8} " +
                        $"0x{dw1:X8} " +
                        $"0x{dw2:X8}");
                }

                // Try interpreting as floats
                Console.WriteLine();
                Console.WriteLine(
                    "  Interpreted as floats:");
                int rows = Math.Min(
                    cSize / 16, 8);
                for (int r = 0; r < rows; r++)
                {
                    int off = cStart + r * 16;
                    if (off + 16 > data.Length)
                        break;
                    float f0 = BitConverter
                        .ToSingle(data, off);
                    float f1 = BitConverter
                        .ToSingle(
                            data, off + 4);
                    float f2 = BitConverter
                        .ToSingle(
                            data, off + 8);
                    float f3 = BitConverter
                        .ToSingle(
                            data, off + 12);

                    string note = "";
                    bool looksLikePos =
                        Math.Abs(f0) < 1000 &&
                        Math.Abs(f1) < 1000 &&
                        Math.Abs(f2) < 1000;
                    if (looksLikePos)
                        note = "  ← looks like XYZ!";

                    Console.WriteLine(
                        $"    [{r}] " +
                        $"({f0,8:F3}, {f1,8:F3}, " +
                        $"{f2,8:F3}, {f3,8:F3})" +
                        note);
                }

                // Try interpreting as ushorts
                // (could be vertex indices)
                Console.WriteLine();
                Console.WriteLine(
                    "  As ushorts (first 16):");
                Console.Write("  ");
                for (int i = 0;
                     i + 2 <= Math.Min(
                         cSize, 32); i += 2)
                {
                    if (cStart + i + 2 >
                        data.Length) break;
                    ushort us = BitConverter
                        .ToUInt16(
                            data, cStart + i);
                    Console.Write(
                        $"{us,5} ");
                }
                Console.WriteLine();

                // Pattern detection: check if
                // chunk has VIFLIKE data with
                // different headers
                Console.WriteLine();
                Console.WriteLine(
                    "  Searching for ANY VIF-like" +
                    " markers:");

                int foundMarkers = 0;
                var markerTypes =
                    new Dictionary<string, int>();

                for (int i = 0;
                     i + 4 <= cSize; i += 4)
                {
                    if (cStart + i + 4 >
                        data.Length) break;
                    byte b0 = data[cStart + i];
                    byte b1 = data[cStart + i + 1];
                    byte b2 = data[cStart + i + 2];
                    byte b3 = data[cStart + i + 3];

                    // VIF code patterns:
                    // 0x6C = UNPACK V4-32
                    // 0x68 = UNPACK V4-16
                    // 0x60 = UNPACK V4-5
                    // 0x14 = MSCAL
                    // 0x17 = MSCNT
                    if (b3 == 0x6C ||
                        b3 == 0x68 ||
                        b3 == 0x60 ||
                        b3 == 0x14 ||
                        b3 == 0x17 ||
                        b3 == 0x10 ||
                        b3 == 0x11)
                    {
                        foundMarkers++;
                        string key =
                            $"...{b3:X2}";
                        if (!markerTypes
                            .ContainsKey(key))
                            markerTypes[key] = 0;
                        markerTypes[key]++;
                    }
                }

                Console.WriteLine(
                    $"  Found {foundMarkers} " +
                    "VIF-like 4-byte patterns");
                foreach (var kv in markerTypes
                    .OrderByDescending(
                        k => k.Value)
                    .Take(5))
                {
                    string desc = "";
                    if (kv.Key.EndsWith("6C"))
                        desc = " (VIF UNPACK V4-32)";
                    else if (kv.Key
                        .EndsWith("68"))
                        desc = " (VIF UNPACK V4-16)";
                    else if (kv.Key
                        .EndsWith("60"))
                        desc = " (VIF UNPACK V4-5)";
                    else if (kv.Key
                        .EndsWith("14"))
                        desc = " (VIF MSCAL)";
                    else if (kv.Key
                        .EndsWith("17"))
                        desc = " (VIF MSCNT)";
                    else if (kv.Key
                        .EndsWith("10"))
                        desc = " (VIF FLUSH)";
                    else if (kv.Key
                        .EndsWith("11"))
                        desc = " (VIF FLUSHA)";
                    Console.WriteLine(
                        $"    {kv.Key}: " +
                        $"{kv.Value,5} times{desc}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 15: BONE-INDEXED MESH FRAGMENTS
        // Chunks 1-6 are bone-indexed pointer
        // tables! This decodes one mesh fragment
        // for a specific bone to confirm format.
        // ═════════════════════════════════════════
        public static void DumpBoneMesh(
            string rdtbPath,
            int chunkIdx,
            int boneIdx)
        {
            PrintHeader(
                $"Chunk {chunkIdx} Bone {boneIdx} " +
                "Mesh Fragment",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (chunkIdx >= offs.Count)
            {
                Console.WriteLine(
                    "  [!] Chunk out of range");
                return;
            }

            int cStart = offs[chunkIdx];
            int cEnd = (chunkIdx + 1 < offs.Count)
                ? offs[chunkIdx + 1]
                : data.Length;
            int cSize = cEnd - cStart;

            // Read pointer table
            uint firstPtr = BitConverter
                .ToUInt32(data, cStart);
            int ptrCount = (int)(firstPtr / 4);

            Console.WriteLine();
            Console.WriteLine(
                $"  Chunk size      : {cSize}");
            Console.WriteLine(
                $"  Bone count      : {boneCount}");
            Console.WriteLine(
                $"  Pointer count   : {ptrCount}");

            if (boneIdx >= ptrCount)
            {
                Console.WriteLine(
                    $"  [!] Bone {boneIdx} >= " +
                    $"ptr count {ptrCount}");
                return;
            }

            // Get this bone's mesh fragment
            int ptrPos = cStart + boneIdx * 4;
            uint thisPtr = BitConverter
                .ToUInt32(data, ptrPos);
            uint nextPtr =
                (boneIdx + 1 < ptrCount)
                ? BitConverter.ToUInt32(
                    data, ptrPos + 4)
                : (uint)cSize;

            int fragStart = cStart + (int)thisPtr;
            int fragEnd = cStart + (int)nextPtr;
            int fragSize = fragEnd - fragStart;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                $"  ► Bone {boneIdx} " +
                $"({GuessBoneName(boneIdx)}) mesh:");
            Console.WriteLine(
                $"    Offset (in chunk): " +
                $"0x{thisPtr:X4}");
            Console.WriteLine(
                $"    Size            : " +
                $"{fragSize} bytes");
            Console.WriteLine(
                $"    Abs offset      : " +
                $"0x{fragStart:X8}");
            Console.ResetColor();

            if (fragSize <= 0 ||
                fragSize > cSize)
            {
                Console.WriteLine(
                    "  [!] Invalid size");
                return;
            }

            // Hex dump
            Console.WriteLine();
            Console.WriteLine(
                "  First 128 bytes hex:");
            int dumpLen =
                Math.Min(128, fragSize);
            for (int i = 0; i < dumpLen; i++)
            {
                if (fragStart + i >=
                    data.Length) break;
                if (i > 0 && i % 16 == 0)
                    Console.WriteLine();
                if (i % 16 == 0)
                    Console.Write(
                        $"  0x{i:X3}: ");
                Console.Write(
                    data[fragStart + i]
                        .ToString("X2") + " ");
            }
            Console.WriteLine();

            // Try VIF detection
            Console.WriteLine();
            Console.WriteLine(
                "  Looking for VIF blocks " +
                "in this fragment:");
            var vifBlocks =
                new List<int>();
            for (int i = 0;
                 i + 16 <= fragSize; i += 4)
            {
                int p = fragStart + i;
                if (p + 4 > data.Length) break;
                if (data[p] == 0x00 &&
                    data[p + 1] == 0x80 &&
                    data[p + 3] == 0x6C)
                    vifBlocks.Add(i);
            }
            Console.WriteLine(
                $"  Found {vifBlocks.Count} " +
                "standard VIF blocks");

            // Try to find ANY VIF unpack pattern
            int vifAny = 0;
            var vifTags = new HashSet<byte>();
            for (int i = 0;
                 i + 4 <= fragSize; i++)
            {
                if (fragStart + i + 4 >
                    data.Length) break;
                byte b3 =
                    data[fragStart + i + 3];
                if (b3 == 0x6C ||
                    b3 == 0x68 ||
                    b3 == 0x60)
                {
                    vifAny++;
                    vifTags.Add(b3);
                }
            }
            Console.WriteLine(
                $"  Found {vifAny} VIF unpack " +
                "tags (0x60/0x68/0x6C)");
            if (vifTags.Count > 0)
            {
                Console.Write("  Tag types: ");
                foreach (byte t in vifTags)
                    Console.Write(
                        $"0x{t:X2} ");
                Console.WriteLine();
            }

            // Interpret first 8 rows as floats
            Console.WriteLine();
            Console.WriteLine(
                "  First 8 rows as floats:");
            for (int r = 0; r < 8; r++)
            {
                int off = fragStart + r * 16;
                if (off + 16 > data.Length)
                    break;
                float f0 = BitConverter
                    .ToSingle(data, off);
                float f1 = BitConverter
                    .ToSingle(data, off + 4);
                float f2 = BitConverter
                    .ToSingle(data, off + 8);
                float f3 = BitConverter
                    .ToSingle(data, off + 12);

                bool looksLikePos =
                    Math.Abs(f0) < 1000 &&
                    Math.Abs(f1) < 1000 &&
                    Math.Abs(f2) < 1000 &&
                    !float.IsNaN(f0);
                string note = looksLikePos
                    ? "  ← XYZ-like"
                    : "";

                Console.WriteLine(
                    $"    [{r}] " +
                    $"({f0,8:F3}, {f1,8:F3}, " +
                    $"{f2,8:F3}, {f3,8:F3})" +
                    note);
            }

            // Interpret as int16 (compressed)
            Console.WriteLine();
            Console.WriteLine(
                "  First 16 int16 values " +
                "(compressed coords?):");
            Console.Write("  ");
            for (int i = 0;
                 i + 2 <= Math.Min(
                     fragSize, 32); i += 2)
            {
                if (fragStart + i + 2 >
                    data.Length) break;
                short s = BitConverter.ToInt16(
                    data, fragStart + i);
                Console.Write($"{s,7} ");
            }
            Console.WriteLine();

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 16: SCAN ALL BONE FRAGMENTS
        // Shows which bones in chunks 1/2/3/4/5/6
        // contain real mesh data (size > 64)
        // ═════════════════════════════════════════
        public static void ScanAllBoneFragments(
            string rdtbPath)
        {
            PrintHeader(
                "All Bone-Indexed Fragments",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            Console.WriteLine();
            Console.WriteLine(
                $"  Bones: {boneCount}");
            Console.WriteLine();

            for (int ci = 1; ci <= 6; ci++)
            {
                if (ci >= offs.Count) break;

                int cStart = offs[ci];
                int cEnd = (ci + 1 < offs.Count)
                    ? offs[ci + 1]
                    : data.Length;
                int cSize = cEnd - cStart;

                uint firstPtr = BitConverter
                    .ToUInt32(data, cStart);
                int ptrCount =
                    (int)(firstPtr / 4);

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"\n  ═══ CHUNK {ci} " +
                    $"({cSize:N0}B, " +
                    $"{ptrCount} entries) ═══");
                Console.ResetColor();

                Console.WriteLine(
                    "  Bone  Offset   Size     " +
                    "Has-Data?  VIF?  Bone Name");
                Console.WriteLine(
                    "  " + new string('-', 70));

                int totalSize = 0;
                int withData = 0;
                int withVif = 0;

                for (int b = 0;
                     b < ptrCount; b++)
                {
                    int ptrPos = cStart + b * 4;
                    if (ptrPos + 4 >
                        data.Length) break;

                    uint thisPtr = BitConverter
                        .ToUInt32(
                            data, ptrPos);
                    uint nextPtr =
                        (b + 1 < ptrCount)
                        ? BitConverter
                            .ToUInt32(
                                data,
                                ptrPos + 4)
                        : (uint)cSize;

                    int fragStart =
                        cStart + (int)thisPtr;
                    int fragSize =
                        (int)(nextPtr - thisPtr);

                    bool hasData = fragSize > 64;
                    if (hasData)
                    {
                        withData++;
                        totalSize += fragSize;
                    }

                    // Check for VIF
                    bool hasVif = false;
                    if (hasData)
                    {
                        for (int i = 0;
                             i + 4 <= fragSize;
                             i += 4)
                        {
                            int p = fragStart + i;
                            if (p + 4 >
                                data.Length)
                                break;
                            if (data[p] == 0x00 &&
                                data[p + 1] == 0x80 &&
                                data[p + 3] == 0x6C)
                            {
                                hasVif = true;
                                withVif++;
                                break;
                            }
                        }
                    }

                    if (b < 30 ||
                        hasData)
                    {
                        ConsoleColor col =
                            hasVif
                            ? ConsoleColor.Green
                            : (hasData
                              ? ConsoleColor.Yellow
                              : ConsoleColor.DarkGray);

                        Console.ForegroundColor =
                            col;
                        Console.WriteLine(
                            $"  [{b,3}]  " +
                            $"0x{thisPtr:X4}  " +
                            $"{fragSize,7}  " +
                            $"{(hasData ? "yes" : " no"),8}  " +
                            $"{(hasVif ? "VIF" : "  -"),4}  " +
                            $"{GuessBoneName(b)}");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine();
                Console.WriteLine(
                    $"  Bones with data    : " +
                    $"{withData}/{ptrCount}");
                Console.WriteLine(
                    $"  Bones with VIF     : " +
                    $"{withVif}/{ptrCount}");
                Console.WriteLine(
                    $"  Total mesh data    : " +
                    $"{totalSize:N0} bytes");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 17: DECODE VIF PACKETS IN BONE FRAG
        // The bone fragments contain MULTIPLE VIF
        // packet types (0x60, 0x68, 0x6C). This
        // walks them and shows what each packet
        // actually contains.
        // ═════════════════════════════════════════
        public static void DecodeVifPackets(
            string rdtbPath,
            int chunkIdx,
            int boneIdx)
        {
            PrintHeader(
                $"VIF Packet Decoder C{chunkIdx} B{boneIdx}",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (chunkIdx >= offs.Count)
            {
                Console.WriteLine(
                    "  [!] Chunk out of range");
                return;
            }

            int cStart = offs[chunkIdx];
            int cEnd = (chunkIdx + 1 < offs.Count)
                ? offs[chunkIdx + 1]
                : data.Length;

            uint firstPtr = BitConverter
                .ToUInt32(data, cStart);
            int ptrCount = (int)(firstPtr / 4);

            if (boneIdx >= ptrCount)
            {
                Console.WriteLine(
                    "  [!] Bone out of range");
                return;
            }

            // Get bone fragment
            int ptrPos = cStart + boneIdx * 4;
            uint thisPtr = BitConverter
                .ToUInt32(data, ptrPos);
            uint nextPtr =
                (boneIdx + 1 < ptrCount)
                ? BitConverter.ToUInt32(
                    data, ptrPos + 4)
                : (uint)(cEnd - cStart);

            int fragStart = cStart + (int)thisPtr;
            int fragEnd = cStart + (int)nextPtr;
            int fragSize = fragEnd - fragStart;

            Console.WriteLine();
            Console.WriteLine(
                $"  Bone {boneIdx} " +
                $"({GuessBoneName(boneIdx)})");
            Console.WriteLine(
                $"  Fragment: 0x{fragStart:X8}" +
                $" size {fragSize}");

            // The fragment also has a sub-ptr table
            // First DWORD = first sub-pointer
            uint subFirstPtr = BitConverter
                .ToUInt32(data, fragStart);
            int subPtrCount =
                (int)(subFirstPtr / 4);

            Console.WriteLine(
                $"  Sub-pointers: {subPtrCount}");
            Console.WriteLine();

            // Walk each sub-pointer to find VIF
            // packet starts
            Console.WriteLine(
                "  ┌─ Sub-pointer scan:");

            int packetsFound = 0;
            int v32Found = 0;
            int v16Found = 0;
            int v5Found = 0;

            int show = Math.Min(20, subPtrCount);

            for (int sp = 0;
                 sp < subPtrCount; sp++)
            {
                int spPos = fragStart + sp * 4;
                if (spPos + 4 > data.Length)
                    break;

                uint subPtr = BitConverter
                    .ToUInt32(data, spPos);
                int subOff =
                    fragStart + (int)subPtr;
                if (subOff + 4 > data.Length ||
                    subOff < fragStart ||
                    subOff > fragEnd)
                    continue;

                // Read VIF tag
                // Last byte (b3) of DWORD is op
                uint vifWord = BitConverter
                    .ToUInt32(data, subOff);
                byte op = (byte)
                    ((vifWord >> 24) & 0xFF);
                byte upper = (byte)
                    ((vifWord >> 16) & 0xFF);
                byte mid = (byte)
                    ((vifWord >> 8) & 0xFF);
                byte lower = (byte)
                    (vifWord & 0xFF);

                string opName = "";
                ConsoleColor col =
                    ConsoleColor.Gray;

                if (op == 0x6C)
                {
                    opName = "UNPACK V4-32";
                    v32Found++;
                    col = ConsoleColor.Green;
                }
                else if (op == 0x68)
                {
                    opName = "UNPACK V4-16";
                    v16Found++;
                    col = ConsoleColor.Cyan;
                }
                else if (op == 0x60)
                {
                    opName = "UNPACK V4-5";
                    v5Found++;
                    col = ConsoleColor.Magenta;
                }
                else if (op == 0x14)
                    opName = "MSCAL";
                else if (op == 0x17)
                    opName = "MSCNT";
                else if (op == 0x10)
                    opName = "FLUSH";
                else if (op == 0x11)
                    opName = "FLUSHE";
                else if (op == 0x00)
                    opName = "NOP";
                else if (op == 0x30)
                    opName = "STROW";
                else if (op == 0x31)
                    opName = "STCOL";
                else
                    opName = $"unknown(0x{op:X2})";

                packetsFound++;

                if (sp < show)
                {
                    // Number of vertices in
                    // UNPACK = upper byte
                    int vertsInPacket =
                        (op == 0x6C ||
                         op == 0x68 ||
                         op == 0x60) ? upper : 0;

                    Console.ForegroundColor =
                        col;
                    Console.WriteLine(
                        $"  │ [sub {sp,3}] " +
                        $"@ 0x{subPtr:X4}  " +
                        $"VIF=0x{op:X2} " +
                        $"({opName,-15}) " +
                        $"verts={vertsInPacket,3}  " +
                        $"raw={vifWord:X8}");
                    Console.ResetColor();
                }
            }
            if (subPtrCount > show)
                Console.WriteLine(
                    $"  │ ... ({subPtrCount - show} more)");
            Console.WriteLine("  └");

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  PACKET SUMMARY:");
            Console.ResetColor();
            Console.WriteLine(
                $"    UNPACK V4-32 (0x6C): {v32Found}");
            Console.WriteLine(
                $"    UNPACK V4-16 (0x68): {v16Found}");
            Console.WriteLine(
                $"    UNPACK V4-5  (0x60): {v5Found}");
            Console.WriteLine(
                $"    Total packets       : {packetsFound}");

            // Now decode the FIRST VIF V4-32
            // packet (uncompressed vertices)
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ► First V4-32 packet decoded:");
            Console.ResetColor();

            for (int sp = 0;
                 sp < subPtrCount; sp++)
            {
                int spPos = fragStart + sp * 4;
                if (spPos + 4 > data.Length)
                    break;

                uint subPtr = BitConverter
                    .ToUInt32(data, spPos);
                int subOff =
                    fragStart + (int)subPtr;
                if (subOff + 4 > data.Length)
                    continue;

                uint vifWord = BitConverter
                    .ToUInt32(data, subOff);
                byte op = (byte)
                    ((vifWord >> 24) & 0xFF);
                byte verts = (byte)
                    ((vifWord >> 16) & 0xFF);

                if (op == 0x6C && verts > 0)
                {
                    Console.WriteLine(
                        $"  Found at sub[{sp}]" +
                        $" offset 0x{subPtr:X4}");
                    Console.WriteLine(
                        $"  Vertices: {verts}");
                    Console.WriteLine();
                    Console.WriteLine(
                        "  Vertex data (XYZW):");

                    int dataStart = subOff + 4;
                    int rowsToShow =
                        Math.Min((int)verts, 12);

                    for (int v = 0;
                         v < rowsToShow; v++)
                    {
                        int vp = dataStart +
                            v * 16;
                        if (vp + 16 >
                            data.Length) break;

                        float x = BitConverter
                            .ToSingle(data, vp);
                        float y = BitConverter
                            .ToSingle(
                                data, vp + 4);
                        float z = BitConverter
                            .ToSingle(
                                data, vp + 8);
                        float w = BitConverter
                            .ToSingle(
                                data, vp + 12);

                        Console.WriteLine(
                            $"    v[{v,3}] " +
                            $"({x,8:F3}, {y,8:F3}," +
                            $" {z,8:F3}, {w,8:F3})");
                    }
                    break;
                }
            }

            // Decode FIRST V4-16 packet
            // (compressed positions)
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ► First V4-16 packet decoded" +
                " (compressed shorts):");
            Console.ResetColor();

            for (int sp = 0;
                 sp < subPtrCount; sp++)
            {
                int spPos = fragStart + sp * 4;
                if (spPos + 4 > data.Length)
                    break;

                uint subPtr = BitConverter
                    .ToUInt32(data, spPos);
                int subOff =
                    fragStart + (int)subPtr;
                if (subOff + 4 > data.Length)
                    continue;

                uint vifWord = BitConverter
                    .ToUInt32(data, subOff);
                byte op = (byte)
                    ((vifWord >> 24) & 0xFF);
                byte verts = (byte)
                    ((vifWord >> 16) & 0xFF);

                if (op == 0x68 && verts > 0)
                {
                    Console.WriteLine(
                        $"  Found at sub[{sp}]" +
                        $" offset 0x{subPtr:X4}");
                    Console.WriteLine(
                        $"  Vertices: {verts}");
                    Console.WriteLine();
                    Console.WriteLine(
                        "  Compressed shorts " +
                        "(divide by 256.0?):");

                    int dataStart = subOff + 4;
                    int rowsToShow =
                        Math.Min((int)verts, 12);

                    for (int v = 0;
                         v < rowsToShow; v++)
                    {
                        // V4-16 = 4 shorts = 8B
                        int vp = dataStart +
                            v * 8;
                        if (vp + 8 >
                            data.Length) break;

                        short sx = BitConverter
                            .ToInt16(data, vp);
                        short sy = BitConverter
                            .ToInt16(
                                data, vp + 2);
                        short sz = BitConverter
                            .ToInt16(
                                data, vp + 4);
                        short sw = BitConverter
                            .ToInt16(
                                data, vp + 6);

                        // Try several scales
                        float fx256 =
                            sx / 256.0f;
                        float fy256 =
                            sy / 256.0f;
                        float fz256 =
                            sz / 256.0f;

                        Console.WriteLine(
                            $"    v[{v,3}] " +
                            $"raw=({sx,6}, {sy,6}, " +
                            $"{sz,6}, {sw,6})  " +
                            $"/256=({fx256,7:F3}, " +
                            $"{fy256,7:F3}, " +
                            $"{fz256,7:F3})");
                    }
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 18: BONE FRAGMENT VERTEX DECODER
        // The "VIF tags" we saw are actually
        // FLOAT vertex data! This decodes vertex
        // blocks correctly and shows real XYZ.
        // ═════════════════════════════════════════
        public static void DecodeBoneVerts(
            string rdtbPath,
            int chunkIdx,
            int boneIdx)
        {
            PrintHeader(
                $"Bone Vertex Decoder C{chunkIdx} B{boneIdx}",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (chunkIdx >= offs.Count)
            {
                Console.WriteLine(
                    "  [!] Chunk out of range");
                return;
            }

            int cStart = offs[chunkIdx];
            int cEnd = (chunkIdx + 1 < offs.Count)
                ? offs[chunkIdx + 1]
                : data.Length;

            uint firstPtr = BitConverter
                .ToUInt32(data, cStart);
            int ptrCount = (int)(firstPtr / 4);

            if (boneIdx >= ptrCount)
            {
                Console.WriteLine(
                    "  [!] Bone out of range");
                return;
            }

            // Get bone fragment
            int ptrPos = cStart + boneIdx * 4;
            uint thisPtr = BitConverter
                .ToUInt32(data, ptrPos);
            uint nextPtr =
                (boneIdx + 1 < ptrCount)
                ? BitConverter.ToUInt32(
                    data, ptrPos + 4)
                : (uint)(cEnd - cStart);

            int fragStart = cStart + (int)thisPtr;
            int fragEnd = cStart + (int)nextPtr;
            int fragSize = fragEnd - fragStart;

            Console.WriteLine();
            Console.WriteLine(
                $"  Bone {boneIdx} " +
                $"({GuessBoneName(boneIdx)})");
            Console.WriteLine(
                $"  Fragment: 0x{fragStart:X8}" +
                $" size {fragSize}");

            // Read sub-pointer table
            uint subFirstPtr = BitConverter
                .ToUInt32(data, fragStart);
            int subPtrCount =
                (int)(subFirstPtr / 4);

            Console.WriteLine(
                $"  Sub-blocks: {subPtrCount}");
            Console.WriteLine();

            // Walk sub-blocks
            int show = Math.Min(8, subPtrCount);

            for (int sp = 0; sp < show; sp++)
            {
                int spPos = fragStart + sp * 4;
                if (spPos + 4 > data.Length)
                    break;

                uint subPtr = BitConverter
                    .ToUInt32(data, spPos);
                uint nextSubPtr =
                    (sp + 1 < subPtrCount)
                    ? BitConverter.ToUInt32(
                        data, spPos + 4)
                    : (uint)(fragEnd - fragStart);

                int blockStart =
                    fragStart + (int)subPtr;
                int blockEnd =
                    fragStart + (int)nextSubPtr;
                int blockSize =
                    blockEnd - blockStart;

                if (blockSize <= 0 ||
                    blockStart < fragStart ||
                    blockEnd > fragEnd)
                {
                    Console.WriteLine(
                        $"  [block {sp}] " +
                        "INVALID");
                    continue;
                }

                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  ┌─ Block [{sp}] " +
                    $"@ 0x{subPtr:X4}  " +
                    $"size={blockSize}B " +
                    $"({blockSize / 16} rows " +
                    "of 16B)");
                Console.ResetColor();

                // Each row = 16 bytes = 4 floats
                int rows = Math.Min(
                    blockSize / 16, 8);

                for (int r = 0; r < rows; r++)
                {
                    int rp = blockStart + r * 16;
                    if (rp + 16 > data.Length)
                        break;

                    float fx = BitConverter
                        .ToSingle(data, rp);
                    float fy = BitConverter
                        .ToSingle(
                            data, rp + 4);
                    float fz = BitConverter
                        .ToSingle(
                            data, rp + 8);
                    float fw = BitConverter
                        .ToSingle(
                            data, rp + 12);

                    bool xOk = !float.IsNaN(fx)
                        && Math.Abs(fx) < 1000;
                    bool yOk = !float.IsNaN(fy)
                        && Math.Abs(fy) < 1000;
                    bool zOk = !float.IsNaN(fz)
                        && Math.Abs(fz) < 1000;

                    string note = "";
                    if (xOk && yOk && zOk)
                    {
                        if (fw == 1.0f)
                            note = " ← XYZ pos!";
                        else if (fw == 0.0f)
                            note = " ← XYZ vec";
                        else if (Math.Abs(
                            fw - 1.0f) < 0.01f)
                            note = " ← position";
                    }

                    Console.WriteLine(
                        $"  │ r{r,2}: " +
                        $"({fx,8:F4}, {fy,8:F4}," +
                        $" {fz,8:F4}, {fw,8:F4})" +
                        note);
                }
                Console.WriteLine("  └");
            }

            // Show byte distribution at +0
            // and +12 of each row across ALL
            // blocks
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ► Block W-component analysis:");
            Console.ResetColor();

            var wValues = new Dictionary<float, int>();
            int totalRows = 0;

            for (int sp = 0;
                 sp < subPtrCount; sp++)
            {
                int spPos = fragStart + sp * 4;
                if (spPos + 4 > data.Length)
                    break;

                uint subPtr = BitConverter
                    .ToUInt32(data, spPos);
                uint nextSubPtr =
                    (sp + 1 < subPtrCount)
                    ? BitConverter.ToUInt32(
                        data, spPos + 4)
                    : (uint)(fragEnd - fragStart);

                int blockStart =
                    fragStart + (int)subPtr;
                int blockEnd =
                    fragStart + (int)nextSubPtr;
                int blockSize =
                    blockEnd - blockStart;

                if (blockSize <= 0) continue;
                int rows = blockSize / 16;

                for (int r = 0; r < rows; r++)
                {
                    int rp = blockStart + r * 16;
                    if (rp + 16 > data.Length)
                        break;
                    float fw = BitConverter
                        .ToSingle(
                            data, rp + 12);
                    if (!float.IsNaN(fw) &&
                        !float.IsInfinity(fw))
                    {
                        // Round to nearest 0.01
                        float key =
                            (float)Math.Round(
                                fw, 2);
                        if (!wValues
                            .ContainsKey(key))
                            wValues[key] = 0;
                        wValues[key]++;
                        totalRows++;
                    }
                }
            }

            Console.WriteLine(
                $"  Total rows: {totalRows}");
            Console.WriteLine(
                "  Top W-component values:");
            foreach (var kv in wValues
                .OrderByDescending(
                    k => k.Value)
                .Take(10))
            {
                double pct = totalRows > 0
                    ? kv.Value * 100.0 / totalRows
                    : 0;
                Console.WriteLine(
                    $"    W={kv.Key,7:F3} : " +
                    $"{kv.Value,5} hits " +
                    $"({pct,5:F1}%)");
            }

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }

        // ═════════════════════════════════════════
        // DIAG 19: EXTRACT ALL BONE VERTICES
        // Walks ALL bone fragments in a chunk
        // and outputs combined vertex stats
        // ═════════════════════════════════════════
        public static void ExtractAllBoneVerts(
            string rdtbPath,
            int chunkIdx)
        {
            PrintHeader(
                $"Extract ALL Bone Verts " +
                $"from Chunk {chunkIdx}",
                rdtbPath);

            byte[] data = LoadRdtb(rdtbPath);
            var offs = GetChunkOffsets(data);
            int boneCount = GetBoneCount(data);

            if (chunkIdx >= offs.Count)
            {
                Console.WriteLine(
                    "  [!] Chunk out of range");
                return;
            }

            int cStart = offs[chunkIdx];
            int cEnd = (chunkIdx + 1 < offs.Count)
                ? offs[chunkIdx + 1]
                : data.Length;

            uint firstPtr = BitConverter
                .ToUInt32(data, cStart);
            int ptrCount = (int)(firstPtr / 4);

            Console.WriteLine();
            Console.WriteLine(
                $"  Chunk size: {cEnd - cStart}");
            Console.WriteLine(
                $"  Bones in ptr table: {ptrCount}");
            Console.WriteLine();

            // Output OBJ to a file
            string outPath =
                Path.GetDirectoryName(rdtbPath);
            if (string.IsNullOrEmpty(outPath))
                outPath = ".";
            string baseName =
                Path.GetFileNameWithoutExtension(
                    rdtbPath);
            string objPath = Path.Combine(
                outPath,
                $"{baseName}_chunk{chunkIdx}_verts.obj");

            Console.WriteLine(
                $"  Writing: {Path.GetFileName(objPath)}");

            int totalVerts = 0;
            int totalBlocks = 0;

            using (var sw = new StreamWriter(
                objPath))
            {
                sw.WriteLine(
                    "# RDTB Chunk " + chunkIdx +
                    " Vertex Dump");
                sw.WriteLine(
                    "# Source: " +
                    Path.GetFileName(rdtbPath));
                sw.WriteLine();

                for (int b = 0;
                     b < ptrCount; b++)
                {
                    int ptrPos =
                        cStart + b * 4;
                    if (ptrPos + 4 >
                        data.Length) break;

                    uint thisPtr = BitConverter
                        .ToUInt32(data, ptrPos);
                    uint nextPtr =
                        (b + 1 < ptrCount)
                        ? BitConverter
                            .ToUInt32(
                                data,
                                ptrPos + 4)
                        : (uint)(cEnd - cStart);

                    int fragStart =
                        cStart + (int)thisPtr;
                    int fragEnd =
                        cStart + (int)nextPtr;
                    int fragSize =
                        fragEnd - fragStart;

                    if (fragSize <= 0 ||
                        fragSize > 100000)
                        continue;

                    sw.WriteLine();
                    sw.WriteLine(
                        $"# Bone {b} " +
                        $"({GuessBoneName(b)})" +
                        $" - {fragSize}B");
                    sw.WriteLine(
                        $"g bone_{b:D3}_" +
                        GuessBoneName(b)
                            .Replace(
                                "(", "")
                            .Replace(
                                ")", "")
                            .Replace(
                                " ", "_"));

                    // Read sub-pointers
                    uint subFirstPtr =
                        BitConverter.ToUInt32(
                            data, fragStart);
                    int subPtrCount =
                        (int)(subFirstPtr / 4);

                    int boneVerts = 0;

                    for (int sp = 0;
                         sp < subPtrCount;
                         sp++)
                    {
                        int spPos =
                            fragStart + sp * 4;
                        if (spPos + 4 >
                            data.Length) break;

                        uint subPtr =
                            BitConverter
                            .ToUInt32(
                                data, spPos);
                        uint nextSubPtr =
                            (sp + 1 <
                             subPtrCount)
                            ? BitConverter
                                .ToUInt32(
                                    data,
                                    spPos + 4)
                            : (uint)
                              (fragEnd -
                               fragStart);

                        int blockStart =
                            fragStart +
                            (int)subPtr;
                        int blockEnd =
                            fragStart +
                            (int)nextSubPtr;
                        int blockSize =
                            blockEnd -
                            blockStart;

                        if (blockSize <= 0 ||
                            blockSize > 50000)
                            continue;
                        totalBlocks++;

                        int rows =
                            blockSize / 16;

                        for (int r = 0;
                             r < rows; r++)
                        {
                            int rp =
                                blockStart +
                                r * 16;
                            if (rp + 16 >
                                data.Length)
                                break;

                            float fx =
                                BitConverter
                                .ToSingle(
                                    data, rp);
                            float fy =
                                BitConverter
                                .ToSingle(
                                    data,
                                    rp + 4);
                            float fz =
                                BitConverter
                                .ToSingle(
                                    data,
                                    rp + 8);
                            float fw =
                                BitConverter
                                .ToSingle(
                                    data,
                                    rp + 12);

                            // Only write
                            // valid-looking
                            // verts
                            if (float.IsNaN(fx)
                             || float.IsNaN(fy)
                             || float.IsNaN(fz))
                                continue;
                            if (Math.Abs(fx) > 1000
                             || Math.Abs(fy) > 1000
                             || Math.Abs(fz) > 1000)
                                continue;

                            // Check if W
                            // suggests this is
                            // a position
                            // (W=1.0 typical)
                            if (Math.Abs(fw - 1.0f)
                                < 0.5f ||
                                fw == 0.0f)
                            {
                                sw.WriteLine(
                                    "v " +
                                    fx.ToString(
                                      "F4",
                                      System.Globalization
                                      .CultureInfo
                                      .InvariantCulture)
                                    + " " +
                                    fy.ToString(
                                      "F4",
                                      System.Globalization
                                      .CultureInfo
                                      .InvariantCulture)
                                    + " " +
                                    fz.ToString(
                                      "F4",
                                      System.Globalization
                                      .CultureInfo
                                      .InvariantCulture));
                                boneVerts++;
                                totalVerts++;
                            }
                        }
                    }

                    if (boneVerts > 0 && b < 30)
                        Console.WriteLine(
                            $"    bone {b,3} " +
                            $"({GuessBoneName(b),-15})" +
                            $" : {boneVerts,5} verts");
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  ✓ Total: {totalVerts:N0} " +
                "vertices across " +
                $"{totalBlocks} blocks");
            Console.WriteLine(
                $"  ✓ OBJ written: {objPath}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(
                "  → Open this OBJ in Blender!");
            Console.WriteLine(
                "    If it shows Boy's body" +
                " shape, we've cracked it!");

            Console.WriteLine();
            Console.WriteLine("Finished!");
        }
    }
}
