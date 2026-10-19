using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// FIXED v5: Correct chunk offset
    /// reading + smart mesh chunk
    /// detection separating material
    /// from mesh chunks.
    /// </summary>
    public static class RDTBBatchTools
    {
        public static void ScanBatch(
            string rdtbPath,
            int batchIdx)
        {
            if (!File.Exists(rdtbPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + rdtbPath);
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Scan Batch "
                + batchIdx);
            Console.ResetColor();
            Console.WriteLine(
                "    RDTB: " +
                Path.GetFileName(
                    rdtbPath));

            int texId = GetBatchTexId(
                rdtbPath, batchIdx);
            if (texId < 0)
            {
                TextOut.PrintError(
                    "Batch " + batchIdx
                    + " not found");
                return;
            }

            var siblings =
                FindBatchesByTexture(
                    rdtbPath, texId);

            Console.WriteLine(
                "    Texture ID  : "
                + texId);
            Console.WriteLine(
                "    Model name  :"
                + " model_"
                + texId.ToString("D2")
                + ".obj");
            Console.WriteLine(
                "    Total batches"
                + " in this model: "
                + siblings.Count);
            Console.Write(
                "    Batch indices:"
                + " [");
            for (int i = 0;
                 i < siblings.Count;
                 i++)
            {
                if (i > 0)
                    Console.Write(", ");
                Console.Write(
                    siblings[i]);
            }
            Console.WriteLine("]");

            Console.WriteLine();
            Console.WriteLine(
                "    Sibling batches"
                + " (would be hidden"
                + " with --all):");
            var others =
                new List<int>();
            foreach (int b in siblings)
                if (b != batchIdx)
                    others.Add(b);
            if (others.Count == 0)
            {
                Console.WriteLine(
                    "      (none)");
            }
            else
            {
                Console.Write("      ");
                for (int i = 0;
                     i < others.Count;
                     i++)
                {
                    if (i > 0)
                        Console.Write(
                            ",");
                    Console.Write(
                        others[i]);
                }
                Console.WriteLine();
            }
        }

        public static void ExtractBatch(
            string rdtbPath,
            int batchIdx,
            string outObj)
        {
            if (!File.Exists(rdtbPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + rdtbPath);
                return;
            }

            int chunkIdx =
                DetectMeshChunk(
                    rdtbPath);

            if (chunkIdx < 0)
            {
                TextOut.PrintError(
                    "No mesh chunk"
                    + " found");
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extract Batch "
                + batchIdx
                + " (chunk "
                + chunkIdx + ")");
            Console.ResetColor();
            Console.WriteLine(
                "    RDTB: " +
                Path.GetFileName(
                    rdtbPath));
            Console.WriteLine(
                "    OUT : " + outObj);

            var batches =
                new List<int>
                    { batchIdx };
            ExtractBatchesAsObj(
                rdtbPath, chunkIdx,
                batches, outObj, false);
        }

        public static void ExtractModel(
            string rdtbPath,
            int batchIdx,
            string outObj)
        {
            if (!File.Exists(rdtbPath))
            {
                TextOut.PrintError(
                    "File not found: "
                    + rdtbPath);
                return;
            }

            int texId = GetBatchTexId(
                rdtbPath, batchIdx);
            if (texId < 0)
            {
                TextOut.PrintError(
                    "Batch " + batchIdx
                    + " not found");
                return;
            }

            var siblings =
                FindBatchesByTexture(
                    rdtbPath, texId);
            int chunkIdx =
                DetectMeshChunk(
                    rdtbPath);

            if (chunkIdx < 0)
            {
                TextOut.PrintError(
                    "No mesh chunk"
                    + " found");
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extract Model"
                + " model_"
                + texId.ToString("D2")
                + " (" + siblings.Count
                + " batches, chunk "
                + chunkIdx + ")");
            Console.ResetColor();
            Console.WriteLine(
                "    RDTB: " +
                Path.GetFileName(
                    rdtbPath));
            Console.WriteLine(
                "    OUT : " + outObj);

            ExtractBatchesAsObj(
                rdtbPath, chunkIdx,
                siblings, outObj, true);
        }

        // ═════════════════════════════
        // READ OFFSETS v5
        // ═════════════════════════════
        private static List<int>
            ReadOffs(byte[] data)
        {
            List<int> offs =
                new List<int>();
            for (int i = 0; i < 14; i++)
            {
                if (0x10 + i * 4 + 4 >
                    data.Length)
                    break;
                uint v =
                    BitConverter.ToUInt32(
                        data,
                        0x10 + i * 4);
                if (v == 0)
                    continue;
                if (v == 0xFFFFFFFF)
                    continue;
                if (v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            offs.Sort();
            offs = offs.Distinct().ToList();
            return offs;
        }

        // ═════════════════════════════
        // DETECT MESH CHUNK v5
        // Picks chunk with MOST VIF
        // blocks, excluding material
        // chunk at index 8.
        // ═════════════════════════════
        private static int
            DetectMeshChunk(
                string rdtbPath)
        {
            byte[] data =
                File.ReadAllBytes(
                    rdtbPath);
            var offs = ReadOffs(data);

            int matIdx = 8;
            int bestIdx = -1;
            int bestVifCount = 0;

            for (int ci = 0;
                 ci < offs.Count; ci++)
            {
                if (ci == matIdx)
                    continue;

                int cs = offs[ci];
                int ce =
                    (ci + 1 < offs.Count
                    ? offs[ci + 1]
                    : data.Length);
                int sz = ce - cs;
                if (sz < 64) continue;

                uint first =
                    BitConverter
                        .ToUInt32(
                            data, cs);
                if (first == 0 ||
                    first > (uint)sz ||
                    first < 4)
                    continue;

                int vifCount = 0;
                for (int i = cs;
                     i + 16 <= ce;
                     i += 4)
                {
                    if (data[i] == 0x00
                        && data[i + 1]
                            == 0x80
                        && data[i + 3]
                            == 0x6C)
                    {
                        vifCount++;
                    }
                }

                if (vifCount >
                    bestVifCount)
                {
                    bestVifCount =
                        vifCount;
                    bestIdx = ci;
                }
            }

            return bestIdx;
        }

        private static int
            GetBatchTexId(
                string rdtbPath,
                int batchIdx)
        {
            byte[] data = File
                .ReadAllBytes(
                    rdtbPath);
            var offs = ReadOffs(data);
            if (offs.Count < 9)
                return -1;
            int c8 = offs[8];
            int c8e = (offs.Count > 9
                ? offs[9]
                : data.Length);
            uint first = BitConverter
                .ToUInt32(data, c8);
            if (first == 0 ||
                first > (uint)(c8e
                                - c8))
                return -1;
            int bc = (int)(first / 4);
            if (batchIdx >= bc)
                return -1;
            uint ptr = BitConverter
                .ToUInt32(data,
                    c8 + batchIdx * 4);
            int rec = c8 + (int)ptr;
            if (rec + 8 > data.Length)
                return -1;
            return BitConverter
                .ToUInt16(data,
                    rec + 6);
        }

        private static List<int>
            FindBatchesByTexture(
                string rdtbPath,
                int texId)
        {
            var r = new List<int>();
            byte[] data = File
                .ReadAllBytes(
                    rdtbPath);
            var offs = ReadOffs(data);
            if (offs.Count < 9)
                return r;
            int c8 = offs[8];
            int c8e = (offs.Count > 9
                ? offs[9]
                : data.Length);
            uint first = BitConverter
                .ToUInt32(data, c8);
            if (first == 0 ||
                first > (uint)(c8e
                                - c8))
                return r;
            int bc = (int)(first / 4);
            for (int i = 0; i < bc; i++)
            {
                uint ptr = BitConverter
                    .ToUInt32(data,
                        c8 + i * 4);
                int rec = c8 + (int)ptr;
                if (rec + 8 >
                    data.Length)
                    continue;
                int t = BitConverter
                    .ToUInt16(data,
                        rec + 6);
                if (t == texId)
                    r.Add(i);
            }
            return r;
        }

        private static void
            ExtractBatchesAsObj(
                string rdtbPath,
                int chunkIdx,
                List<int> batchIndices,
                string outObj,
                bool useGroups)
        {
            byte[] data = File
                .ReadAllBytes(
                    rdtbPath);
            var offs = ReadOffs(data);
            if (chunkIdx >=
                offs.Count)
            {
                TextOut.PrintError(
                    "Chunk " + chunkIdx
                    + " not present");
                return;
            }
            int cs = offs[chunkIdx];
            int ce = (chunkIdx + 1 <
                offs.Count
                ? offs[chunkIdx + 1]
                : data.Length);
            byte[] chunk = new byte[
                ce - cs];
            Array.Copy(data, cs, chunk,
                0, ce - cs);

            uint firstPtr =
                BitConverter.ToUInt32(
                    chunk, 0);

            if (firstPtr == 0 ||
                firstPtr > (uint)
                    chunk.Length ||
                firstPtr < 4)
            {
                TextOut.PrintError(
                    "Mesh chunk no"
                    + " valid ptr table"
                    + " (firstPtr=0x" +
                    firstPtr.ToString(
                        "X8") + ")");
                return;
            }

            int nPtrs =
                (int)(firstPtr / 4);

            uint[] batchPtrs =
                new uint[nPtrs];
            for (int i = 0;
                 i < nPtrs; i++)
            {
                batchPtrs[i] =
                    BitConverter.ToUInt32(
                        chunk, i * 4);
            }
            var sortedPtrs =
                new List<uint>();
            for (int i = 0;
                 i < nPtrs; i++)
            {
                uint p = batchPtrs[i];
                if (p > 0 &&
                    p < (uint)
                        chunk.Length)
                    sortedPtrs.Add(p);
            }
            sortedPtrs.Sort();
            sortedPtrs =
                sortedPtrs.Distinct()
                    .ToList();

            var allV =
                new List<float[]>();
            var allN =
                new List<float[]>();
            var allU =
                new List<float[]>();
            var allF =
                new List<int[]>();

            int skipped = 0;

            foreach (int bi in
                batchIndices)
            {
                if (bi >= nPtrs)
                {
                    skipped++;
                    continue;
                }

                uint bp = batchPtrs[bi];

                if (bp == 0 ||
                    bp >= (uint)
                        chunk.Length)
                {
                    skipped++;
                    continue;
                }

                uint np = (uint)
                    chunk.Length;
                foreach (uint sp in
                    sortedPtrs)
                {
                    if (sp > bp)
                    {
                        np = sp;
                        break;
                    }
                }

                int batchSize =
                    (int)(np - bp);
                if (batchSize <= 16 ||
                    batchSize >
                        chunk.Length)
                {
                    skipped++;
                    continue;
                }

                byte[] bd = new byte[
                    batchSize];
                Array.Copy(chunk,
                    (int)bp, bd, 0,
                    bd.Length);
                ExtractVifBatch(bd,
                    allV, allN, allU,
                    allF, bi);
            }

            if (skipped > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] " +
                    skipped +
                    " batches skipped"
                    + " (invalid)");
                Console.ResetColor();
            }

            using (var sw =
                new StreamWriter(
                    outObj, false,
                    Encoding.UTF8))
            {
                sw.WriteLine(
                    "# HMSTH extracted"
                    + " batches: "
                    + string.Join(",",
                        batchIndices));
                sw.WriteLine();
                foreach (float[] v in
                    allV)
                    sw.WriteLine("v "
                        + v[0]
                            .ToString(
                                "F6")
                        + " "
                        + v[1]
                            .ToString(
                                "F6")
                        + " "
                        + v[2]
                            .ToString(
                                "F6"));
                sw.WriteLine();
                foreach (float[] u in
                    allU)
                    sw.WriteLine(
                        "vt " +
                        u[0]
                            .ToString(
                                "F6")
                        + " "
                        + (1f - u[1])
                        .ToString(
                            "F6"));
                sw.WriteLine();
                foreach (float[] n in
                    allN)
                    sw.WriteLine("vn "
                        + n[0]
                            .ToString(
                                "F6")
                        + " "
                        + n[1]
                            .ToString(
                                "F6")
                        + " "
                        + n[2]
                            .ToString(
                                "F6"));
                sw.WriteLine();
                int curB = -1;
                foreach (int[] f in
                    allF)
                {
                    if (useGroups &&
                        f[0] != curB)
                    {
                        sw.WriteLine(
                            "g batch_"
                            + f[0]
                                .ToString(
                                    "D4"));
                        curB = f[0];
                    }
                    int a = f[1] + 1;
                    int b = f[2] + 1;
                    int c = f[3] + 1;
                    sw.WriteLine("f "
                        + a + "/" + a
                        + "/" + a +
                        " " + b + "/"
                        + b + "/" + b
                        + " " + c +
                        "/" + c + "/"
                        + c);
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "    Verts: " +
                allV.Count);
            Console.WriteLine(
                "    Faces: " +
                allF.Count);
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] " + outObj);
            Console.ResetColor();
        }

        private static void
            ExtractVifBatch(
                byte[] bd,
                List<float[]> allV,
                List<float[]> allN,
                List<float[]> allU,
                List<int[]> allF,
                int batchIdx)
        {
            int pos = 0;
            var layouts =
                new List<List<int>>();
            while (pos + 16 <=
                bd.Length)
            {
                if (bd[pos] != 0x00 ||
                    bd[pos + 1] != 0x80
                    || bd[pos + 3]
                       != 0x6C)
                {
                    pos += 4;
                    continue;
                }
                int vcb = bd[pos + 4];

                if (vcb < 1 || vcb > 96)
                {
                    pos += 4;
                    continue;
                }

                int vS = pos + 16;
                int nS = vS + vcb * 16;
                int uS = nS + vcb * 16;

                if (uS + vcb * 16 >
                    bd.Length)
                {
                    pos += 4;
                    continue;
                }

                int bStart =
                    allV.Count;
                for (int i = 0;
                     i < vcb; i++)
                {
                    int vo = vS
                        + i * 16;
                    int no = nS
                        + i * 16;
                    int uo = uS
                        + i * 16;
                    if (uo + 16 >
                        bd.Length)
                        break;
                    allV.Add(
                        new float[]
                        {
                            BitConverter
                                .ToSingle(
                                    bd,
                                    vo + 4),
                            BitConverter
                                .ToSingle(
                                    bd,
                                    vo + 8),
                            BitConverter
                                .ToSingle(
                                    bd,
                                    vo + 12)
                        });
                    allN.Add(
                        new float[]
                        {
                            BitConverter
                                .ToSingle(
                                    bd,
                                    no + 4),
                            BitConverter
                                .ToSingle(
                                    bd,
                                    no + 8),
                            BitConverter
                                .ToSingle(
                                    bd,
                                    no + 12)
                        });
                    allU.Add(
                        new float[]
                        {
                            BitConverter
                                .ToSingle(
                                    bd,
                                    uo + 4),
                            BitConverter
                                .ToSingle(
                                    bd,
                                    uo + 8)
                        });
                }
                int bEnd =
                    allV.Count;
                var lay =
                    new List<int>();
                for (int j = bStart;
                     j < bEnd; j++)
                    lay.Add(j);
                layouts.Add(lay);
                int bSize = 16
                    + 3 * vcb * 16
                    + 16;
                if (pos + bSize + 16
                    <= bd.Length)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                bd,
                                pos +
                                bSize);
                    if (eof ==
                        0x70000000)
                        bSize += 16;
                }
                pos += bSize;
            }
            foreach (List<int> lay in
                layouts)
            {
                int nn = lay.Count;
                for (int i = 0;
                     i < nn - 2; i++)
                {
                    int a, b, c;
                    a = lay[i];
                    if (i % 2 == 0)
                    {
                        b = lay[i + 1];
                        c = lay[i + 2];
                    }
                    else
                    {
                        b = lay[i + 2];
                        c = lay[i + 1];
                    }
                    if (a == b ||
                        b == c ||
                        a == c)
                        continue;
                    float[] v0 =
                        allV[a];
                    float[] v1 =
                        allV[b];
                    float[] v2 =
                        allV[c];
                    float ax = v1[0]
                        - v0[0];
                    float ay = v1[1]
                        - v0[1];
                    float az = v1[2]
                        - v0[2];
                    float bx = v2[0]
                        - v0[0];
                    float by = v2[1]
                        - v0[1];
                    float bz = v2[2]
                        - v0[2];
                    float cx = ay * bz
                        - az * by;
                    float cy = az * bx
                        - ax * bz;
                    float cz = ax * by
                        - ay * bx;
                    if (cx * cx + cy * cy
                        + cz * cz >
                        1e-10f)
                        allF.Add(
                            new int[]
                            { batchIdx,
                              a, b, c });
                }
            }
        }
    }
}
