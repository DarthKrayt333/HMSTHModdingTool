using HMSTHModdingTool.IO;
using HMSTHModdingTool.GDTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.SRDB
{
    public static class SRDBBatchExtractor
    {
        static uint RU32(byte[] d, int o)
        {
            if (o + 4 > d.Length)
                return 0;
            return BitConverter
                .ToUInt32(d, o);
        }
        static ushort RU16(
            byte[] d, int o)
        {
            if (o + 2 > d.Length)
                return 0;
            return BitConverter
                .ToUInt16(d, o);
        }
        static float RF32(
            byte[] d, int o)
        {
            if (o + 4 > d.Length)
                return 0f;
            return BitConverter
                .ToSingle(d, o);
        }
        static void WU32(
            byte[] buf, int o,
            uint v)
        {
            byte[] b = BitConverter
                .GetBytes(v);
            Array.Copy(
                b, 0, buf, o, 4);
        }

        // ═════════════════════
        // SRDB MASTER TABLE
        // ═════════════════════
        static List<byte[]>
            ParseMasterTable(
                byte[] data)
        {
            if (data.Length < 4 ||
                data[0] != 0x53 ||
                data[1] != 0x52 ||
                data[2] != 0x44 ||
                data[3] != 0x42)
                throw new
                    InvalidDataException(
                    "Not SRDB");

            uint firstOff = RU32(
                data, 0x0C);
            var chunkOffs =
                new List<uint>();
            int pos = 0x0C;
            while (pos + 4 <=
                (int)firstOff)
            {
                uint v = RU32(
                    data, pos);
                if (v == 0) break;
                if (v > (uint)
                    data.Length)
                    break;
                chunkOffs.Add(v);
                pos += 4;
            }
            if (chunkOffs.Count < 3)
                throw new
                    InvalidDataException(
                    "SRDB < 3 chunks");

            uint c2Start =
                chunkOffs[2];
            uint masterSize =
                RU32(data,
                     (int)c2Start);
            if (masterSize == 0)
                throw new
                    InvalidDataException(
                    "Empty master "
                    + "table");

            var masterPtrs =
                new List<uint>();
            pos = (int)c2Start;
            while (pos <
                (int)(c2Start +
                      masterSize))
            {
                uint v = RU32(
                    data, pos);
                if (v == 0) break;
                masterPtrs.Add(v);
                pos += 4;
            }

            var rdtbs =
                new List<byte[]>();
            for (int i = 0;
                 i < masterPtrs
                     .Count; i++)
            {
                uint s = c2Start +
                    masterPtrs[i];
                uint e;
                if (i + 1 <
                    masterPtrs.Count)
                    e = c2Start +
                        masterPtrs[
                            i + 1];
                else
                    e = (uint)
                        data.Length;
                int sz =
                    (int)(e - s);
                if (sz <= 0)
                    continue;
                byte[] rdtb =
                    new byte[sz];
                Array.Copy(
                    data, (int)s,
                    rdtb, 0, sz);
                rdtbs.Add(rdtb);
            }
            return rdtbs;
        }

        // ═════════════════════
        // SRDB REBUILDER
        // ═════════════════════
        static byte[] RebuildSRDBBytes(
            byte[] original,
            List<byte[]> newRdtbs)
        {
            uint firstOff = RU32(
                original, 0x0C);
            var chunkOffs =
                new List<uint>();
            int pos = 0x0C;
            while (pos + 4 <=
                (int)firstOff)
            {
                uint v = RU32(
                    original, pos);
                if (v == 0) break;
                if (v > (uint)
                    original.Length)
                    break;
                chunkOffs.Add(v);
                pos += 4;
            }

            int headerSize =
                (int)chunkOffs[0];
            byte[] chunk0 =
                new byte[
                    chunkOffs[1] -
                    chunkOffs[0]];
            Array.Copy(original,
                (int)chunkOffs[0],
                chunk0, 0,
                chunk0.Length);
            byte[] chunk1 =
                new byte[
                    chunkOffs[2] -
                    chunkOffs[1]];
            Array.Copy(original,
                (int)chunkOffs[1],
                chunk1, 0,
                chunk1.Length);

            uint masterSize =
                RU32(original,
                     (int)chunkOffs[2]);

            var nm = new List<int>();
            int cursor =
                (int)masterSize;
            foreach (var rdtb in
                newRdtbs)
            {
                nm.Add(cursor);
                cursor +=
                    rdtb.Length;
            }

            byte[] nc2 =
                new byte[cursor];
            for (int i = 0;
                 i < nm.Count; i++)
                WU32(nc2, i * 4,
                     (uint)nm[i]);
            for (int i = 0;
                 i < newRdtbs.Count;
                 i++)
                Array.Copy(
                    newRdtbs[i], 0,
                    nc2, nm[i],
                    newRdtbs[i]
                        .Length);

            int total = headerSize
                + chunk0.Length
                + chunk1.Length
                + nc2.Length;
            byte[] result =
                new byte[total];
            Array.Copy(original,
                0, result, 0, 12);

            int[] newOffs = {
                headerSize,
                headerSize +
                    chunk0.Length,
                headerSize +
                    chunk0.Length +
                    chunk1.Length,
            };
            int hp = 0x0C;
            foreach (int off in
                newOffs)
            {
                if (hp + 4 >
                    headerSize)
                    break;
                WU32(result, hp,
                     (uint)off);
                hp += 4;
            }
            Array.Copy(chunk0, 0,
                result, newOffs[0],
                chunk0.Length);
            Array.Copy(chunk1, 0,
                result, newOffs[1],
                chunk1.Length);
            Array.Copy(nc2, 0,
                result, newOffs[2],
                nc2.Length);
            return result;
        }

        // ═════════════════════
        // EXTRACT ONE RDTB
        // Uses RAW slot offsets
        // directly — not index
        // into offs list.
        // This is the key fix.
        // ═════════════════════
        static int ExtractRDTBBatches(
            byte[] rdtb,
            string subPath,
            string tmpTex)
        {
            if (rdtb.Length < 0x48)
                return 0;
            if (rdtb[0] != 0x52 ||
                rdtb[1] != 0x44 ||
                rdtb[2] != 0x54 ||
                rdtb[3] != 0x42)
                return 0;

            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
                rawSlots[i] = RU32(
                    rdtb, 0x10 + i * 4);

            uint c8Off = rawSlots[8];
            if (c8Off == 0 ||
                c8Off == 0xFFFFFFFF ||
                c8Off >= (uint)rdtb.Length)
                return 0;

            uint c11Off = rawSlots[11];
            if (c11Off == 0 ||
                c11Off == 0xFFFFFFFF ||
                c11Off >= (uint)rdtb.Length)
                return 0;

            uint c8End =
                (uint)rdtb.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c8Off &&
                    v < c8End &&
                    v != 0xFFFFFFFF &&
                    v != 0)
                    c8End = v;
            }

            uint c11End =
                (uint)rdtb.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c11Off &&
                    v < c11End &&
                    v != 0xFFFFFFFF &&
                    v != 0)
                    c11End = v;
            }

            int c8Len =
                (int)(c8End - c8Off);
            if (c8Len < 4) return 0;

            if (rdtb[(int)c8Off] == 0x00
                && rdtb[(int)c8Off + 1]
                    == 0x80
                && c8Len > 3
                && rdtb[(int)c8Off + 3]
                    == 0x6C)
                return 0;

            uint matFirst = RU32(
                rdtb, (int)c8Off);
            if (matFirst == 0 ||
                matFirst > (uint)c8Len)
                return 0;

            int bc = (int)(matFirst / 4);
            if (bc > 10000) return 0;

            var batchTex =
                new Dictionary<int, int>();
            for (int i = 0; i < bc; i++)
            {
                int ptrOff =
                    (int)c8Off + i * 4;
                if (ptrOff + 4 >
                    rdtb.Length) break;
                uint ptr = RU32(
                    rdtb, ptrOff);
                int recOff =
                    (int)c8Off + (int)ptr;
                if (recOff + 8 >
                    rdtb.Length) continue;
                int tex = RU16(
                    rdtb, recOff + 6);
                batchTex[i] = tex;
            }

            var texGroups =
                new SortedDictionary<
                    int, List<int>>();
            foreach (var kv in batchTex)
            {
                if (!texGroups
                        .ContainsKey(
                            kv.Value))
                    texGroups[kv.Value] =
                        new List<int>();
                texGroups[kv.Value]
                    .Add(kv.Key);
            }

            int mcStart = (int)c11Off;
            int mcEnd = (int)c11End;
            byte[] meshChunk =
                new byte[mcEnd - mcStart];
            Array.Copy(rdtb, mcStart,
                meshChunk, 0,
                meshChunk.Length);

            uint mFirst = RU32(
                meshChunk, 0);
            if (mFirst == 0 ||
                mFirst > (uint)
                    meshChunk.Length ||
                mFirst < 4)
                return 0;

            int nPtrs =
                (int)(mFirst / 4);
            int safeBc =
                Math.Min(bc, nPtrs);

            uint[] batchPtrs =
                new uint[safeBc];
            for (int i = 0; i < safeBc; i++)
                batchPtrs[i] = RU32(
                    meshChunk, i * 4);

            var sortedPtrs =
                batchPtrs
                    .Where(p => p > 0 &&
                        p < (uint)
                            meshChunk
                                .Length)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToList();

            // ── AUTO-SCALE ──────────────
            // If model bounding box >
            // 250 units, scale down to
            // 100 units for Blender.
            // cbatches inverts this so
            // game gets exact original
            // coordinates back.
            float autoScale = 1.0f;
            {
                float mnx = float.MaxValue;
                float mxx = float.MinValue;
                float mny = float.MaxValue;
                float mxy = float.MinValue;
                float mnz = float.MaxValue;
                float mxz = float.MinValue;
                bool any = false;

                for (int i = 0;
                     i < safeBc; i++)
                {
                    uint bPtr =
                        batchPtrs[i];
                    if (bPtr == 0 ||
                        bPtr >= (uint)
                            meshChunk.Length)
                        continue;

                    uint nPtr =
                        (uint)
                        meshChunk.Length;
                    foreach (uint sp in
                        sortedPtrs)
                    {
                        if (sp > bPtr)
                        {
                            nPtr = sp;
                            break;
                        }
                    }

                    int bSz =
                        (int)(nPtr - bPtr);
                    if (bSz <= 16) continue;

                    byte[] bd =
                        new byte[bSz];
                    Array.Copy(
                        meshChunk,
                        (int)bPtr,
                        bd, 0, bSz);

                    int pos = 0;
                    while (pos + 16 <=
                        bd.Length)
                    {
                        if (bd[pos] != 0x00
                            || bd[pos + 1]
                                != 0x80
                            || bd[pos + 3]
                                != 0x6C)
                        {
                            pos += 4;
                            continue;
                        }
                        int vcb =
                            bd[pos + 4];
                        if (vcb < 1 ||
                            vcb > 96)
                        {
                            pos += 4;
                            continue;
                        }
                        int vS = pos + 16;
                        if (vS + vcb * 16 >
                            bd.Length)
                        {
                            pos += 4;
                            continue;
                        }
                        for (int vi = 0;
                             vi < vcb; vi++)
                        {
                            int vo =
                                vS + vi * 16;
                            if (vo + 16 >
                                bd.Length)
                                break;
                            float vx = RF32(
                                bd, vo + 4);
                            float vy = RF32(
                                bd, vo + 8);
                            float vz = RF32(
                                bd, vo + 12);
                            if (float.IsNaN(vx)
                                || float
                                    .IsNaN(vy)
                                || float
                                    .IsNaN(vz)
                                || float
                                    .IsInfinity(
                                        vx)
                                || float
                                    .IsInfinity(
                                        vy)
                                || float
                                    .IsInfinity(
                                        vz))
                                continue;
                            if (vx < mnx)
                                mnx = vx;
                            if (vx > mxx)
                                mxx = vx;
                            if (vy < mny)
                                mny = vy;
                            if (vy > mxy)
                                mxy = vy;
                            if (vz < mnz)
                                mnz = vz;
                            if (vz > mxz)
                                mxz = vz;
                            any = true;
                        }
                        int bSize =
                            16 +
                            3 * vcb * 16 +
                            16;
                        pos += bSize;
                    }
                }

                if (any)
                {
                    float dx = mxx - mnx;
                    float dy = mxy - mny;
                    float dz = mxz - mnz;
                    float maxDim =
                        Math.Max(dx,
                        Math.Max(dy, dz));
                    const float TARGET =
                        100f;
                    const float THRESHOLD =
                        250f;
                    if (maxDim > THRESHOLD)
                    {
                        autoScale =
                            TARGET / maxDim;
                        Console.WriteLine(
                            "    [auto-scale]"
                            + " " +
                            autoScale
                                .ToString(
                                    "F4") +
                            "x (was " +
                            maxDim
                                .ToString(
                                    "F0") +
                            " units)");
                    }
                }
            }

            int written = 0;
            foreach (var kv in texGroups)
            {
                int texId = kv.Key;
                string modelDir =
                    Path.Combine(
                        subPath,
                        "model_" +
                        texId.ToString(
                            "D2"));
                Directory
                    .CreateDirectory(
                        modelDir);

                string srcBmp =
                    Path.Combine(
                        tmpTex,
                        "texture_" +
                        texId.ToString(
                            "D2") +
                        ".bmp");
                string dstBmp =
                    Path.Combine(
                        modelDir,
                        "texture_" +
                        texId.ToString(
                            "D2") +
                        ".bmp");
                if (File.Exists(srcBmp))
                    File.Copy(srcBmp,
                        dstBmp, true);

                string texFn =
                    "texture_" +
                    texId.ToString("D2") +
                    ".bmp";

                foreach (int bi in kv.Value)
                {
                    if (bi >= safeBc)
                        continue;
                    uint bPtr =
                        batchPtrs[bi];
                    if (bPtr == 0 ||
                        bPtr >= (uint)
                            meshChunk.Length)
                        continue;

                    uint nPtr =
                        (uint)
                        meshChunk.Length;
                    foreach (uint sp in
                        sortedPtrs)
                    {
                        if (sp > bPtr)
                        {
                            nPtr = sp;
                            break;
                        }
                    }

                    int batchSize =
                        (int)(nPtr - bPtr);
                    if (batchSize <= 16 ||
                        batchSize >
                            meshChunk.Length)
                        continue;

                    byte[] bdata =
                        new byte[batchSize];
                    Array.Copy(
                        meshChunk,
                        (int)bPtr,
                        bdata, 0,
                        batchSize);

                    bool hasVif = false;
                    for (int vi = 0;
                         vi + 4 <=
                         bdata.Length;
                         vi += 4)
                    {
                        if (bdata[vi]
                                == 0x00
                            && bdata[vi + 1]
                                == 0x80
                            && bdata[vi + 3]
                                == 0x6C)
                        {
                            hasVif = true;
                            break;
                        }
                    }
                    if (!hasVif) continue;

                    string objFile =
                        Path.Combine(
                            modelDir,
                            "batch_" +
                            bi.ToString(
                                "D4") +
                            ".obj");
                    string mtlFile =
                        Path.Combine(
                            modelDir,
                            "batch_" +
                            bi.ToString(
                                "D4") +
                            ".mtl");

                    if (WriteBatchObj(
                            bdata,
                            objFile,
                            mtlFile,
                            bi, texId,
                            texFn,
                            autoScale))
                        written++;
                }
            }

            // Save auto-scale to _info.txt
            // so cbatches can invert it
            if (autoScale != 1.0f)
            {
                string infoPath =
                    Path.Combine(
                        subPath,
                        "_info.txt");
                File.AppendAllText(
                    infoPath,
                    "Auto Scale: " +
                    autoScale.ToString(
                        "F6",
                        System
                            .Globalization
                            .CultureInfo
                            .InvariantCulture)
                    + "\n",
                    Encoding.UTF8);
            }

            return texGroups.Count;
        }

        // ═════════════════════
        // WRITE BATCH OBJ
        // ═════════════════════
        static bool WriteBatchObj(
            byte[] bdata,
            string objPath,
            string mtlPath,
            int batchIdx,
            int texId,
            string texFn,
            float autoScale = 1.0f)
        {
            var allV =
                new List<float[]>();
            var allN =
                new List<float[]>();
            var allU =
                new List<float[]>();
            var layouts =
                new List<List<int>>();

            int pos = 0;
            while (pos + 16 <=
                bdata.Length)
            {
                if (bdata[pos] != 0x00
                    || bdata[pos + 1]
                        != 0x80
                    || bdata[pos + 3]
                        != 0x6C)
                {
                    pos += 4;
                    continue;
                }
                int vcb =
                    bdata[pos + 4];
                if (vcb < 1 ||
                    vcb > 96)
                {
                    pos += 4;
                    continue;
                }
                int vS = pos + 16;
                int nS = vS + vcb * 16;
                int uS = nS + vcb * 16;
                if (uS + vcb * 16
                    > bdata.Length)
                {
                    pos += 4;
                    continue;
                }
                int bs = allV.Count;
                for (int i = 0;
                     i < vcb; i++)
                {
                    int vo =
                        vS + i * 16;
                    int no =
                        nS + i * 16;
                    int uo =
                        uS + i * 16;
                    if (uo + 16 >
                        bdata.Length)
                        break;

                    // Apply auto-scale
                    // to vertex positions
                    allV.Add(
                        new float[]
                        {
                    RF32(bdata,
                        vo + 4)
                        * autoScale,
                    RF32(bdata,
                        vo + 8)
                        * autoScale,
                    RF32(bdata,
                        vo + 12)
                        * autoScale,
                    RU32(bdata, vo)
                        });
                    allN.Add(
                        new float[]
                        {
                    RF32(bdata,
                        no + 4),
                    RF32(bdata,
                        no + 8),
                    RF32(bdata,
                        no + 12)
                        });
                    allU.Add(
                        new float[]
                        {
                    RF32(bdata,
                        uo + 4),
                    RF32(bdata,
                        uo + 8)
                        });
                }
                var lay =
                    new List<int>();
                for (int j = bs;
                     j < allV.Count;
                     j++)
                    lay.Add(j);
                layouts.Add(lay);
                int bSize =
                    16 + 3 * vcb * 16
                    + 16;
                if (pos + bSize + 16
                    <= bdata.Length &&
                    RU32(bdata,
                        pos + bSize) ==
                    0x70000000)
                    bSize += 16;
                pos += bSize;
            }

            var faces =
                new List<int[]>();
            foreach (var layout
                in layouts)
            {
                int nn = layout.Count;
                for (int i = 0;
                     i < nn - 2; i++)
                {
                    int a = layout[i];
                    int b, c;
                    if (i % 2 == 0)
                    {
                        b = layout[i + 1];
                        c = layout[i + 2];
                    }
                    else
                    {
                        b = layout[i + 2];
                        c = layout[i + 1];
                    }
                    if (a == b ||
                        b == c ||
                        a == c)
                        continue;
                    float[] v0 = allV[a];
                    float[] v1 = allV[b];
                    float[] v2 = allV[c];
                    float ax =
                        v1[0] - v0[0];
                    float ay =
                        v1[1] - v0[1];
                    float az =
                        v1[2] - v0[2];
                    float bx =
                        v2[0] - v0[0];
                    float by =
                        v2[1] - v0[1];
                    float bz =
                        v2[2] - v0[2];
                    float cx =
                        ay * bz - az * by;
                    float cy =
                        az * bx - ax * bz;
                    float cz =
                        ax * by - ay * bx;
                    if (cx * cx + cy * cy
                        + cz * cz > 1e-10f)
                        faces.Add(
                            new int[]
                            { a, b, c });
                }
            }

            if (allV.Count == 0 ||
                faces.Count == 0)
                return false;

            string mn =
                "batch_" +
                batchIdx.ToString("D4");

            using (var sw =
                new StreamWriter(
                    mtlPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine("# " + mn);
                sw.WriteLine();
                sw.WriteLine(
                    "newmtl " + mn);
                sw.WriteLine("Ka 1 1 1");
                sw.WriteLine("Kd 1 1 1");
                sw.WriteLine("Ks 0 0 0");
                sw.WriteLine("Ns 10");
                sw.WriteLine("illum 2");
                sw.WriteLine(
                    "map_Kd " + texFn);
            }

            using (var sw =
                new StreamWriter(
                    objPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine(
                    "# Batch " +
                    batchIdx +
                    " (tex " +
                    texId + ")");
                sw.WriteLine(
                    "mtllib " +
                    Path.GetFileName(
                        mtlPath));
                sw.WriteLine();
                for (int i = 0;
                     i < allV.Count; i++)
                {
                    float[] v = allV[i];
                    sw.WriteLine(
                        "v " +
                        v[0].ToString(
                            "F6") + " " +
                        v[1].ToString(
                            "F6") + " " +
                        v[2].ToString(
                            "F6"));
                    sw.WriteLine(
                        "#vw " +
                        ((uint)v[3])
                            .ToString(
                                "X8"));
                }
                sw.WriteLine();
                foreach (var uv in allU)
                    sw.WriteLine(
                        "vt " +
                        uv[0].ToString(
                            "F6") + " " +
                        (1f - uv[1])
                            .ToString(
                                "F6"));
                sw.WriteLine();
                foreach (var n in allN)
                    sw.WriteLine(
                        "vn " +
                        n[0].ToString(
                            "F6") + " " +
                        n[1].ToString(
                            "F6") + " " +
                        n[2].ToString(
                            "F6"));
                sw.WriteLine();
                sw.WriteLine("g " + mn);
                sw.WriteLine(
                    "usemtl " + mn);
                foreach (int[] f in faces)
                {
                    int a1 = f[0] + 1;
                    int b1 = f[1] + 1;
                    int c1 = f[2] + 1;
                    sw.WriteLine(
                        "f " +
                        a1 + "/" + a1 +
                        "/" + a1 + " " +
                        b1 + "/" + b1 +
                        "/" + b1 + " " +
                        c1 + "/" + c1 +
                        "/" + c1);
                }
            }
            return true;
        }

        // ═════════════════════
        // XSRDBBATCHES
        // ═════════════════════
        public static void ExtractBatches(
            string srdbPath,
            string gdtbPath,
            string baseName)
        {
            string outDir =
                baseName + "_3d_batches_obj";

            byte[] srdbData =
                File.ReadAllBytes(srdbPath);
            byte[] gdtbData =
                File.ReadAllBytes(gdtbPath);

            Directory.CreateDirectory(outDir);

            // ── Step 1: Save source files ──
            File.WriteAllBytes(
                Path.Combine(outDir,
                    "_source.srdb"),
                srdbData);
            File.WriteAllBytes(
                Path.Combine(outDir,
                    "_source.gdtb"),
                gdtbData);
            File.WriteAllText(
                Path.Combine(outDir,
                    "_srdb_info.txt"),
                "Source SRDB: " +
                Path.GetFileName(srdbPath)
                + "\n" +
                "Source GDTB: " +
                Path.GetFileName(gdtbPath)
                + "\n",
                Encoding.UTF8);

            // ── Step 2: Extract textures
            // to shared temp folder ────────
            string tmpTex = Path.Combine(
                outDir, "_tex_tmp");
            Directory.CreateDirectory(tmpTex);
            Console.WriteLine(
                "\n[+] Extract textures...");
            try
            {
                GDTBArchive.Extract(
                    gdtbPath, tmpTex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "    [!] " + ex.Message);
            }

            // ── Step 3: Extract each
            // embedded RDTB using xsrdb ────
            // (preserves original offsets
            //  for byte-identical csrdb)
            var rdtbs = ParseMasterTable(
                srdbData);

            Console.WriteLine(
                "\n[+] Extract SRDB");
            Console.WriteLine(
                "    SRDB: " +
                Path.GetFileName(srdbPath));
            Console.WriteLine(
                "    Out:  " + outDir + "\n");

            for (int idx = 0;
                 idx < rdtbs.Count; idx++)
            {
                byte[] rdtb = rdtbs[idx];
                string sub = "embedded_" +
                    idx.ToString("D2");
                string subPath = Path.Combine(
                    outDir, sub);
                Directory.CreateDirectory(
                    subPath);

                // Save the raw RDTB blob
                // exactly as xsrdb would
                string rdtbBlobPath =
                    Path.Combine(subPath,
                        "_source.rdtb");
                File.WriteAllBytes(
                    rdtbBlobPath, rdtb);

                // Save shared GDTB ref
                File.WriteAllBytes(
                    Path.Combine(subPath,
                        "_source.gdtb"),
                    gdtbData);

                // Write _info.txt with
                // source names
                // (cbatches reads this for
                //  output file names)
                using (var sw = new StreamWriter(
                    Path.Combine(subPath,
                        "_info.txt")))
                {
                    sw.WriteLine(
                        "HMSTH Batch Folder");
                    sw.WriteLine(
                        "Source RDTB:"
                        + " _source.rdtb");
                    sw.WriteLine(
                        "Source GDTB: " +
                        Path.GetFileName(
                            gdtbPath));
                }

                // ── Step 4: Run xbatches
                // on this embedded RDTB
                // exactly like the working
                // manual workflow ──────────
                // This calls the EXACT SAME
                // code path as:
                //   xbatches embedded_09.rdtb
                //             gdtb outfolder
                // which you confirmed works.
                int mc = ExtractRDTBBatches(
                    rdtb, subPath, tmpTex);

                Console.WriteLine(
                    "  [" +
                    idx.ToString("D2") +
                    "] " + sub + "  " +
                    rdtb.Length
                        .ToString("N0") +
                    " B  " + mc +
                    " models");
            }

            try
            {
                Directory.Delete(
                    tmpTex, true);
            }
            catch { }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "\n[OK] Extracted " +
                rdtbs.Count +
                " RDTBs to " + outDir);
            Console.ResetColor();
        }

        // ═════════════════════
        // CSRDBBATCHES
        // ═════════════════════
        public static void
            RebuildSRDB(
                string inDir,
                string outFolder,
                string outGdtbName)
        {
            string src =
                Path.Combine(inDir,
                    "_source.srdb");
            if (!File.Exists(src))
                throw new
                    FileNotFoundException(
                    "_source.srdb "
                    + "not found");

            byte[] original =
                File.ReadAllBytes(src);
            var rdtbs =
                ParseMasterTable(
                    original);

            Directory
                .CreateDirectory(
                    outFolder);

            // ── FIX: Read output SRDB
            // name from _srdb_info.txt
            // which was written during
            // extraction. This is the
            // most reliable method.
            string outSrdbName = null;

            string srdbInfoPath =
                Path.Combine(inDir,
                    "_srdb_info.txt");
            if (File.Exists(srdbInfoPath))
            {
                foreach (string line in
                    File.ReadAllLines(
                        srdbInfoPath))
                {
                    string t = line.Trim();
                    if (t.StartsWith(
                        "Source SRDB:"))
                    {
                        outSrdbName =
                            t.Substring(12)
                                .Trim();
                    }
                    if (t.StartsWith(
                        "Source GDTB:") &&
                        string.IsNullOrEmpty(
                            outGdtbName))
                    {
                        outGdtbName =
                            t.Substring(12)
                                .Trim();
                    }
                }
            }

            // ── Fallback: derive name
            // from the folder name by
            // stripping the _3d_batches
            // suffix to get base name,
            // then check if parent dir
            // has a matching SRDB file
            if (outSrdbName == null)
            {
                string inDirName =
                    Path.GetFileName(
                        Path.GetFullPath(
                            inDir));
                string parentDir =
                    Path.GetDirectoryName(
                        Path.GetFullPath(
                            inDir));

                string baseName = inDirName;
                string[] suffixes =
                {
                    "_3d_batches_obj",
                    "_3d_batches",
                    "_batches",
                };
                foreach (string sfx in
                    suffixes)
                {
                    if (baseName.EndsWith(
                            sfx,
                            StringComparison
                                .OrdinalIgnoreCase))
                    {
                        baseName =
                            baseName
                                .Substring(0,
                                    baseName
                                        .Length -
                                    sfx.Length);
                        break;
                    }
                }

                string[] tryNames =
                {
                    baseName + "_00000.srdb",
                    baseName + ".srdb",
                    baseName + "_00000.SRDB",
                    baseName + ".SRDB",
                };
                foreach (string tn in
                    tryNames)
                {
                    string fp =
                        Path.Combine(
                            parentDir, tn);
                    if (File.Exists(fp))
                    {
                        outSrdbName = tn;
                        break;
                    }
                }

                // Final fallback
                if (outSrdbName == null)
                    outSrdbName =
                        baseName +
                        "_00000.srdb";
            }

            // ── Read GDTB name from
            // embedded folder _info.txt
            // if still not found
            if (string.IsNullOrEmpty(
                    outGdtbName))
            {
                for (int i = 0;
                     i < rdtbs.Count; i++)
                {
                    string infoP =
                        Path.Combine(
                            inDir,
                            "embedded_" +
                            i.ToString("D2"),
                            "_info.txt");
                    if (File.Exists(infoP))
                    {
                        foreach (string line
                            in File
                                .ReadAllLines(
                                    infoP))
                        {
                            string t =
                                line.Trim();
                            if (t.StartsWith(
                                "Source GDTB:"))
                            {
                                outGdtbName =
                                    t.Substring(
                                        12)
                                    .Trim();
                                break;
                            }
                        }
                        if (!string
                                .IsNullOrEmpty(
                                    outGdtbName))
                            break;
                    }
                }
            }

            string outSrdb =
                Path.Combine(
                    outFolder,
                    outSrdbName);
            string outGdtb =
                Path.Combine(
                    outFolder,
                    !string
                        .IsNullOrEmpty(
                            outGdtbName)
                    ? outGdtbName
                    : "output.gdtb");

            Console.WriteLine(
                "\n[+] Rebuild SRDB");
            Console.WriteLine(
                "    In:  " + inDir);
            Console.WriteLine(
                "    Out: " +
                outFolder + "\n");
            Console.WriteLine(
                "    SRDB name: " +
                outSrdbName);
            Console.WriteLine(
                "    GDTB name: " +
                outGdtbName);

            var finalRdtbs =
                new List<byte[]>();
            int moddedCount = 0;

            for (int idx = 0;
                idx < rdtbs.Count; idx++)
            {
                string sub =
                    "embedded_" +
                    idx.ToString("D2");
                string subPath =
                    Path.Combine(inDir, sub);
                string moddedPath =
                    Path.Combine(subPath,
                        "_modded.rdtb");

                byte[] rdtbBytes;
                string status;

                if (File.Exists(moddedPath))
                {
                    // User explicitly placed
                    // _modded.rdtb here
                    rdtbBytes =
                        File.ReadAllBytes(
                            moddedPath);
                    status = "MODDED";
                    moddedCount++;
                }
                else if (
                    Directory.Exists(subPath) &&
                    HasBatchOBJs(subPath))
                {
                    // Has batch OBJs.
                    // Run cbatches and use result.
                    // The HasUserEdits check was
                    // removed because floating point
                    // comparison with large auto-scale
                    // invert values (e.g. x1050)
                    // caused false positives making
                    // ALL blobs rebuild unnecessarily.
                    //
                    // Instead we always rebuild from
                    // batch OBJs when they exist, but
                    // use the original blob bytes when
                    // cbatches produces identical
                    // output. This is safe because
                    // cbatches with "match" format
                    // preserves the source RDTB format
                    // exactly when no edits exist.
                    try
                    {
                        string tempOut =
                            Path.Combine(
                                subPath,
                                "_rebuild_tmp");

                        // Silence per-blob rebuild spam
                        var savedOut = Console.Out;
                        Console.SetOut(TextWriter.Null);
                        try
                        {
                            RDTB
                                .RDTBBatchFolder
                                .BuildFromBatchFolder(
                                    subPath,
                                    tempOut,
                                    "match",
                                    null,
                                    false,
                                    "mirrored",
                                    null);
                        }
                        finally
                        {
                            Console.SetOut(savedOut);
                        }

                        string foundRdtb = null;
                        if (Directory.Exists(
                                tempOut))
                        {
                            foreach (string f in
                                Directory.GetFiles(
                                    tempOut,
                                    "*.rdtb"))
                            {
                                foundRdtb = f;
                                break;
                            }
                        }

                        if (foundRdtb != null &&
                            File.Exists(foundRdtb))
                        {
                            byte[] rebuilt =
                                File.ReadAllBytes(
                                    foundRdtb);

                            if (IsOnlySlotTableDiff(
                                    rebuilt,
                                    rdtbs[idx]))
                            {
                                rdtbBytes = rdtbs[idx];
                                status = "source";
                            }
                            else
                            {
                                rdtbBytes = rebuilt;
                                status = "REBUILT";
                                moddedCount++;
                            }
                        }

                        else
                        {
                            rdtbBytes = rdtbs[idx];
                            status = "source(no output)";
                        }

                        try
                        {
                            Directory.Delete(
                                tempOut, true);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "    [" +
                            idx.ToString("D2") +
                            "] rebuild err: " +
                            ex.Message);
                        Console.ResetColor();
                        rdtbBytes = rdtbs[idx];
                        status = "source(err)";
                    }
                }
                else
                {
                    // No batch OBJs and no
                    // _modded.rdtb. Use original.
                    rdtbBytes = rdtbs[idx];
                    status = "source";
                }

                // ── SRDB UV PATCH ────────────────────
                // Runs for ALL blobs including
                // "source" ones. embedded_09
                // has no batch OBJs so it fell
                // through to "source" above,
                // but it may still have UV edits
                // in its model_XX folders that
                // were placed manually.
                if (Directory.Exists(subPath))
                {
                    float autoScale =
                        SRDBUVPatcher
                            .ReadAutoScale(
                                subPath);

                    // ── FIX: Use scale=1.0 floor ──
                    // If no auto-scale in _info.txt
                    // still run patch with scale=1.0
                    // so UV-only edits are not skipped
                    float patchScale =
                        autoScale > 0f
                        ? autoScale
                        : 1.0f;

                    byte[] patchTarget =
                        new byte[
                            rdtbBytes.Length];
                    Array.Copy(
                        rdtbBytes,
                        patchTarget,
                        rdtbBytes.Length);

                    int uvChanges =
                        SRDBUVPatcher.PatchUVs(
                            patchTarget,
                            subPath,
                            patchScale);  // ← was: autoScale

                    if (uvChanges > 0)
                    {
                        rdtbBytes =
                            patchTarget;
                        Console.ForegroundColor
                            = ConsoleColor
                                .Green;
                        Console.WriteLine(
                            "    [UV fix] "
                            + sub + " "
                            + uvChanges
                            + " UV pairs"
                            + " patched");
                        Console.ResetColor();

                        if (status
                            == "source")
                        {
                            status =
                                "UV-PATCHED";
                            moddedCount++;
                        }
                    }
                }
                // ── END SRDB UV PATCH ────────────

                int pad = (16 -
                    rdtbBytes.Length % 16) % 16;
                if (pad > 0)
                {
                    var padded =
                        new byte[
                            rdtbBytes.Length + pad];
                    Array.Copy(rdtbBytes, padded,
                        rdtbBytes.Length);
                    rdtbBytes = padded;
                }

                finalRdtbs.Add(rdtbBytes);

                int change =
                    rdtbBytes.Length -
                    rdtbs[idx].Length;
                string csv =
                    change == 0
                    ? "same"
                    : (change > 0
                       ? "+" + change + " B"
                       : change + " B");

                Console.WriteLine(
                    "  [" +
                    idx.ToString("D2") +
                    "] " +
                    status.PadRight(20) +
                    " " +
                    rdtbs[idx].Length
                        .ToString().PadLeft(7) +
                    " B -> " +
                    rdtbBytes.Length
                        .ToString().PadLeft(7) +
                    " B  (" + csv + ")");
            }


            Console.WriteLine(
                "\n  Total modded: "
                + moddedCount);

            byte[] result =
                RebuildSRDBBytes(
                    original,
                    finalRdtbs);

            File.WriteAllBytes(
                outSrdb, result);

            int delta =
                result.Length -
                original.Length;
            string dstr =
                delta > 0
                ? "+" + delta
                : (delta < 0
                   ? delta.ToString()
                   : "same");

            Console.ForegroundColor
                = ConsoleColor.Green;
            Console.WriteLine(
                "\n[OK] SRDB: "
                + outSrdb);
            Console.ResetColor();
            Console.WriteLine(
                "    Orig: " +
                original.Length
                    .ToString("N0")
                + " B");
            Console.WriteLine(
                "    New:  " +
                result.Length
                    .ToString("N0")
                + " B (" +
                dstr + ")");

            // ── Build unified GDTB ───────────
            // Collect ALL textures from ALL
            // embedded dirs. Detect modded ones
            // by comparing file hashes across
            // copies of the same texture index.
            // Use modded version if found,
            // original otherwise.
            // This ensures the shared GDTB
            // contains all textures, not just
            // the subset of one embedded RDTB.
            BuildUnifiedGdtb(
                inDir, outGdtb, outFolder,
                rdtbs.Count);
        }

        static bool HasBatchOBJs(
            string folderPath)
        {
            if (!Directory.Exists(
                    folderPath))
                return false;
            foreach (string entry
                in Directory
                    .GetDirectories(
                        folderPath,
                        "model_*"))
            {
                foreach (string fn
                    in Directory
                        .GetFiles(
                            entry,
                            "batch_*.obj"))
                    return true;
            }
            return false;
        }

        // ═════════════════════════════════
        // BUILD UNIFIED GDTB
        // Collects all textures from all
        // embedded_NN/model_XX/ subfolders.
        // For each texture index, uses the
        // modded version if it differs from
        // the majority of copies. Otherwise
        // uses the original.
        // Builds one complete GDTB with all
        // textures in correct index order.
        // ═════════════════════════════════
        private static void BuildUnifiedGdtb(
            string inDir,
            string outGdtb,
            string outFolder,
            int nEmbedded)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Building unified GDTB"
                + " (all textures)");
            Console.ResetColor();

            // Step 1: Collect all copies of
            // each texture index across all
            // embedded_NN/model_XX/ folders.
            // Key = tex_idx
            // Value = list of (file_path,
            //                  file_size)
            var texCopies =
                new Dictionary<int,
                    List<string>>();

            for (int ei = 0;
                 ei < nEmbedded; ei++)
            {
                string sub = Path.Combine(
                    inDir,
                    "embedded_" +
                    ei.ToString("D2"));
                if (!Directory.Exists(sub))
                    continue;

                foreach (string mDir in
                    Directory.GetDirectories(
                        sub, "model_*"))
                {
                    foreach (string bmp in
                        Directory.GetFiles(
                            mDir,
                            "texture_*.bmp"))
                    {
                        string fn =
                            Path.GetFileName(
                                bmp);
                        // texture_XX.bmp
                        string idxStr =
                            fn.Length > 12
                            ? fn.Substring(
                                8, fn.Length
                                   - 12)
                            : "";
                        int tidx;
                        if (!int.TryParse(
                                idxStr,
                                out tidx))
                            continue;

                        if (!texCopies
                                .ContainsKey(
                                    tidx))
                            texCopies[tidx] =
                                new List<
                                    string>();
                        texCopies[tidx]
                            .Add(bmp);
                    }
                }
            }

            if (texCopies.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [!] No textures"
                    + " found in any"
                    + " embedded folder."
                    + " Copying source"
                    + " GDTB.");
                Console.ResetColor();
                string srcG = Path.Combine(
                    inDir, "_source.gdtb");
                if (File.Exists(srcG))
                    File.Copy(
                        srcG, outGdtb, true);
                return;
            }

            Console.WriteLine(
                "    Texture indices found: ["
                + string.Join(", ",
                    texCopies.Keys
                        .OrderBy(k => k))
                + "]");

            // Step 2: For each texture index,
            // pick the best copy.
            // If all copies have the same
            // byte size AND content hash,
            // they are unmodded — use any.
            // If one copy has a different
            // hash from the majority, it is
            // modded — use it.
            string tempDir = Path.Combine(
                inDir, "_unified_tex_tmp");
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(
                        tempDir, true);
                Directory.CreateDirectory(
                    tempDir);

                int modded = 0;
                int unmodded = 0;

                foreach (int tidx in
                    texCopies.Keys
                        .OrderBy(k => k))
                {
                    var copies =
                        texCopies[tidx];
                    string chosen = null;

                    if (copies.Count == 1)
                    {
                        // Only one copy —
                        // use it regardless
                        chosen = copies[0];
                        unmodded++;
                    }
                    else
                    {
                        // Multiple copies.
                        // Compute file sizes
                        // as fast comparator.
                        var groups =
                            new Dictionary<
                                long,
                                List<string>>();
                        foreach (string c in
                            copies)
                        {
                            long sz;
                            try
                            {
                                sz = new FileInfo(
                                    c).Length;
                            }
                            catch
                            {
                                sz = 0;
                            }
                            if (!groups
                                    .ContainsKey(
                                        sz))
                                groups[sz] =
                                    new List<
                                        string>();
                            groups[sz].Add(c);
                        }

                        if (groups.Count == 1)
                        {
                            // All same size.
                            // Do byte compare
                            // of first vs rest.
                            string first =
                                copies[0];
                            byte[] refBytes =
                                null;
                            try
                            {
                                refBytes =
                                    File
                                        .ReadAllBytes(
                                            first);
                            }
                            catch { }

                            string different =
                                null;
                            if (refBytes != null)
                            {
                                foreach (
                                    string c in
                                    copies)
                                {
                                    if (c == first)
                                        continue;
                                    try
                                    {
                                        byte[] cb =
                                            File
                                                .ReadAllBytes(
                                                    c);
                                        if (!refBytes
                                                .SequenceEqual(
                                                    cb))
                                        {
                                            different = c;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }

                            if (different !=
                                null)
                            {
                                // Found a copy
                                // with same size
                                // but different
                                // bytes = modded
                                chosen =
                                    different;
                                modded++;
                                Console
                                    .ForegroundColor
                                    = ConsoleColor
                                        .Green;
                                Console.WriteLine(
                                    "    [MODDED]"
                                    + " texture_" +
                                    tidx.ToString(
                                        "D2") +
                                    ".bmp");
                                Console
                                    .ResetColor();
                            }
                            else
                            {
                                chosen = first;
                                unmodded++;
                            }
                        }
                        else
                        {
                            // Different sizes.
                            // Majority size is
                            // original. Lone
                            // size = modded.
                            long majoritySize =
                                groups
                                    .OrderByDescending(
                                        kv =>
                                        kv.Value
                                            .Count)
                                    .First()
                                    .Key;

                            // Find the lone
                            // (modded) copy
                            string modCopy =
                                null;
                            foreach (var kv in
                                groups)
                            {
                                if (kv.Key !=
                                    majoritySize
                                    && kv.Value
                                        .Count
                                        < groups[
                                            majoritySize]
                                        .Count)
                                {
                                    modCopy =
                                        kv.Value[
                                            0];
                                    break;
                                }
                            }

                            if (modCopy != null)
                            {
                                chosen = modCopy;
                                modded++;
                                Console
                                    .ForegroundColor
                                    = ConsoleColor
                                        .Green;
                                Console.WriteLine(
                                    "    [MODDED]"
                                    + " texture_" +
                                    tidx.ToString(
                                        "D2") +
                                    ".bmp"
                                    + " (size"
                                    + " differs)");
                                Console
                                    .ResetColor();
                            }
                            else
                            {
                                // All different
                                // sizes, no clear
                                // majority.
                                // Use first.
                                chosen =
                                    copies[0];
                                unmodded++;
                            }
                        }
                    }

                    if (chosen == null)
                    {
                        chosen = copies[0];
                        unmodded++;
                    }

                    // Copy chosen texture to
                    // unified temp folder
                    string dst = Path.Combine(
                        tempDir,
                        "texture_" +
                        tidx.ToString("D2") +
                        ".bmp");
                    try
                    {
                        File.Copy(
                            chosen, dst, true);
                    }
                    catch (Exception ex)
                    {
                        Console
                            .ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "    [!] Copy"
                            + " texture_" +
                            tidx.ToString("D2")
                            + ": " +
                            ex.Message);
                        Console.ResetColor();
                    }
                }

                Console.WriteLine(
                    "    Total textures : "
                    + texCopies.Count);
                Console.WriteLine(
                    "    Modded          : "
                    + modded);
                Console.WriteLine(
                    "    Unmodded        : "
                    + unmodded);

                // Step 3: Build GDTB from
                // the unified temp folder
                try
                {
                    GDTBArchive.Create(
                        tempDir, outGdtb);
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "\n[OK] Unified GDTB: "
                        + outGdtb);
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] GDTB build"
                        + " failed: " +
                        ex.Message);
                    Console.ResetColor();
                    // Fallback to source
                    string srcG = Path.Combine(
                        inDir, "_source.gdtb");
                    if (File.Exists(srcG))
                    {
                        File.Copy(
                            srcG, outGdtb,
                            true);
                        Console
                            .ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "    Copied source"
                            + " GDTB as"
                            + " fallback.");
                        Console.ResetColor();
                    }
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(
                            tempDir))
                        Directory.Delete(
                            tempDir, true);
                }
                catch { }
            }
        }

        // ═════════════════════════════════
        // HAS USER EDITS
        // Detects if any batch OBJ in this
        // embedded folder was actually
        // modified by the user vs being an
        // unmodded extraction roundtrip.
        //
        // Compares OBJ vertex positions
        // (accounting for auto-scale) against
        // the original RDTB blob bytes.
        // If any vertex differs by >= EPS,
        // the folder was user-edited.
        //
        // Returns false for pure roundtrips
        // so unmodded blobs use original bytes
        // and stay byte-identical.
        // ═════════════════════════════════
        private static bool HasUserEdits(
            string subPath,
            byte[] originalBlob)
        {
            const float EPS = 0.002f;

            // Read auto-scale from _info.txt
            float autoScale = 1.0f;
            string infoPath = Path.Combine(
                subPath, "_info.txt");
            if (File.Exists(infoPath))
            {
                foreach (string line in
                    File.ReadAllLines(infoPath))
                {
                    string t = line.Trim();
                    if (t.StartsWith("Auto Scale:"))
                    {
                        float sc;
                        if (float.TryParse(
                                t.Substring(11)
                                    .Trim(),
                                System.Globalization
                                    .NumberStyles
                                    .Float,
                                System.Globalization
                                    .CultureInfo
                                    .InvariantCulture,
                                out sc) && sc > 0f)
                            autoScale = sc;
                        break;
                    }
                }
            }

            // autoScaleInvert converts OBJ
            // display-space back to game-space
            float inv = autoScale != 0f
                ? 1.0f / autoScale
                : 1.0f;

            // Get mesh chunk bounds from blob
            uint[] rawSlots = new uint[14];
            for (int i = 0; i < 14; i++)
            {
                int o = 0x10 + i * 4;
                if (o + 4 > originalBlob.Length)
                    break;
                rawSlots[i] = RU32(
                    originalBlob, o);
            }

            uint c11Off = rawSlots[11];
            if (c11Off == 0 ||
                c11Off == 0xFFFFFFFF ||
                c11Off >= (uint)originalBlob.Length)
                return false;

            uint c11End =
                (uint)originalBlob.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c11Off &&
                    v < c11End &&
                    v != 0xFFFFFFFF &&
                    v != 0)
                    c11End = v;
            }

            int mcLen = (int)(c11End - c11Off);
            if (mcLen < 32) return false;

            byte[] mc = new byte[mcLen];
            Array.Copy(originalBlob,
                (int)c11Off, mc, 0, mcLen);

            uint mFirst = RU32(mc, 0);
            if (mFirst == 0 ||
                mFirst > (uint)mcLen ||
                mFirst < 4)
                return false;

            int nPtrs = (int)(mFirst / 4);

            // Build sorted batch pointer list
            var sortedPtrs = new List<uint>();
            for (int i = 0; i < nPtrs; i++)
            {
                uint p = RU32(mc, i * 4);
                if (p > 0 && p < (uint)mcLen)
                    sortedPtrs.Add(p);
            }
            sortedPtrs = sortedPtrs
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // For each batch, collect original
            // vertex positions
            var origVerts =
                new Dictionary<int,
                    List<float[]>>();

            for (int bi = 0; bi < nPtrs; bi++)
            {
                uint bPtr = RU32(mc, bi * 4);
                if (bPtr == 0 ||
                    bPtr >= (uint)mcLen)
                    continue;

                uint nPtr = (uint)mcLen;
                foreach (uint sp in sortedPtrs)
                {
                    if (sp > bPtr)
                    {
                        nPtr = sp;
                        break;
                    }
                }

                var verts = new List<float[]>();
                int pos = (int)bPtr;
                int end = (int)nPtr;

                while (pos + 16 <= end)
                {
                    if (mc[pos] == 0x00 &&
                        mc[pos + 1] == 0x80 &&
                        mc[pos + 3] == 0x6C)
                    {
                        int vc = mc[pos + 4];
                        if (vc >= 1 && vc <= 96)
                        {
                            int vs = pos + 16;
                            if (vs + vc * 16 <= end)
                            {
                                for (int vi = 0;
                                     vi < vc; vi++)
                                {
                                    int vo =
                                        vs + vi * 16;
                                    verts.Add(
                                        new float[]
                                        {
                                    RF32(mc,
                                        vo + 4),
                                    RF32(mc,
                                        vo + 8),
                                    RF32(mc,
                                        vo + 12)
                                        });
                                }
                            }
                            int bsz = 16 +
                                3 * vc * 16 + 16;
                            if (pos + bsz + 16
                                    <= end &&
                                RU32(mc,
                                    pos + bsz)
                                    == 0x70000000)
                                bsz += 16;
                            pos += bsz;
                            continue;
                        }
                    }
                    pos += 4;
                }

                if (verts.Count > 0)
                    origVerts[bi] = verts;
            }

            // Scan batch OBJ files and compare
            foreach (string modelDir in
                Directory.GetDirectories(
                    subPath, "model_*"))
            {
                foreach (string objFile in
                    Directory.GetFiles(
                        modelDir,
                        "batch_*.obj"))
                {
                    string fn =
                        Path.GetFileNameWithoutExtension(
                            objFile);
                    if (!fn.StartsWith("batch_"))
                        continue;
                    int bi;
                    if (!int.TryParse(
                            fn.Substring(6),
                            out bi))
                        continue;

                    if (!origVerts
                            .ContainsKey(bi))
                        continue;

                    var orig = origVerts[bi];

                    // Read OBJ verts
                    var objV =
                        new List<float[]>();
                    var ci = System.Globalization
                        .CultureInfo
                        .InvariantCulture;
                    foreach (string line in
                        File.ReadAllLines(
                            objFile))
                    {
                        string t = line.Trim();
                        if (t.Length < 2 ||
                            t[0] != 'v' ||
                            t[1] != ' ')
                            continue;
                        string[] p = t.Split(
                            new char[]
                                { ' ', '\t' },
                            StringSplitOptions
                                .RemoveEmptyEntries);
                        if (p.Length < 4 ||
                            p[0] != "v")
                            continue;
                        float x, y, z;
                        if (float.TryParse(
                                p[1],
                                System.Globalization
                                    .NumberStyles
                                    .Float, ci,
                                out x) &&
                            float.TryParse(
                                p[2],
                                System.Globalization
                                    .NumberStyles
                                    .Float, ci,
                                out y) &&
                            float.TryParse(
                                p[3],
                                System.Globalization
                                    .NumberStyles
                                    .Float, ci,
                                out z))
                        {
                            objV.Add(
                                new float[]
                                    { x, y, z });
                        }
                    }

                    if (objV.Count != orig.Count)
                        return true; // vert count changed = modded

                    // Compare verts
                    for (int vi = 0;
                         vi < objV.Count; vi++)
                    {
                        // OBJ is in display-space
                        // (autoScale applied).
                        // Convert back to game-space
                        // using inv before comparing
                        // against original bytes.
                        float gx =
                            objV[vi][0] * inv;
                        float gy =
                            objV[vi][1] * inv;
                        float gz =
                            objV[vi][2] * inv;

                        if (Math.Abs(gx -
                                orig[vi][0]) > EPS ||
                            Math.Abs(gy -
                                orig[vi][1]) > EPS ||
                            Math.Abs(gz -
                                orig[vi][2]) > EPS)
                            return true; // vertex moved = modded
                    }
                }
            }

            return false; // no edits detected
        }

        // ═════════════════════════════════
        // IS ONLY SLOT TABLE DIFF
        // Returns true if the only difference
        // between rebuilt and original RDTB
        // is in the 4 mirror slots (9,10,12,13)
        // changing from 0xFFFFFFFF to real
        // offsets. This is the ApplySlotMirror
        // signature on an unmodded roundtrip.
        // If true, the blob was not user-edited
        // and we should use original bytes.
        // ═════════════════════════════════
        private static bool IsOnlySlotTableDiff(
            byte[] rebuilt,
            byte[] original)
        {
            if (rebuilt.Length != original.Length)
                return false;

            int diffCount = 0;
            int firstDiff = -1;
            int lastDiff = -1;

            for (int i = 0;
                 i < rebuilt.Length; i++)
            {
                if (rebuilt[i] != original[i])
                {
                    diffCount++;
                    if (firstDiff < 0)
                        firstDiff = i;
                    lastDiff = i;
                }
            }

            if (diffCount == 0)
                return true; // identical

            // Slot table is at 0x10..0x47
            // (14 slots × 4 bytes = 56 bytes)
            // The 4 mirror slots are at:
            //   slot 9  = 0x10 + 9*4  = 0x34
            //   slot 10 = 0x10 + 10*4 = 0x38
            //   slot 12 = 0x10 + 12*4 = 0x40
            //   slot 13 = 0x10 + 13*4 = 0x44
            // Each slot is 4 bytes.
            // So diffs must be within
            // 0x34..0x47 only.
            // Max 16 bytes of diffs
            // (4 slots × 4 bytes).

            if (diffCount > 16)
                return false;

            if (firstDiff < 0x34)
                return false;

            if (lastDiff > 0x47)
                return false;

            // All diffs are within the
            // mirror slot range
            return true;
        }
    }
}
