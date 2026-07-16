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
        public static void
            ExtractBatches(
                string srdbPath,
                string gdtbPath,
                string baseName)
        {
            if (!File.Exists(srdbPath))
                throw new
                    FileNotFoundException(
                    "SRDB: " + srdbPath);
            if (!File.Exists(gdtbPath))
                throw new
                    FileNotFoundException(
                    "GDTB: " + gdtbPath);

            string outDir =
                baseName + "_3d_batches";

            byte[] srdbData =
                File.ReadAllBytes(srdbPath);
            byte[] gdtbData =
                File.ReadAllBytes(gdtbPath);

            var rdtbs =
                ParseMasterTable(srdbData);

            Directory.CreateDirectory(
                outDir);

            File.WriteAllBytes(
                Path.Combine(outDir,
                    "_source.srdb"),
                srdbData);
            File.WriteAllBytes(
                Path.Combine(outDir,
                    "_source.gdtb"),
                gdtbData);

            // ── FIX: Write _srdb_info.txt
            // so RebuildSRDB can recover
            // the correct output SRDB and
            // GDTB names automatically
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

            string tmpTex =
                Path.Combine(outDir,
                    "_tex_tmp");
            Console.WriteLine(
                "\n[+] Extract textures...");
            try
            {
                Directory.CreateDirectory(
                    tmpTex);
                GDTBArchive.Extract(
                    gdtbPath, tmpTex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "    [!] " + ex.Message);
            }

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
                string sub =
                    "embedded_" +
                    idx.ToString("D2");
                string sp =
                    Path.Combine(outDir, sub);
                Directory.CreateDirectory(sp);

                File.WriteAllBytes(
                    Path.Combine(sp,
                        "_source.rdtb"),
                    rdtb);
                File.WriteAllBytes(
                    Path.Combine(sp,
                        "_source.gdtb"),
                    gdtbData);

                int mc =
                    ExtractRDTBBatches(
                        rdtb, sp, tmpTex);

                using (var sw =
                    new StreamWriter(
                        Path.Combine(sp,
                            "_info.txt")))
                {
                    sw.WriteLine(
                        "HMSTH Batch Folder");
                    sw.WriteLine(
                        "Source RDTB:"
                        + " embedded_" +
                        idx.ToString("D2")
                        + ".rdtb");
                    sw.WriteLine(
                        "Source GDTB: " +
                        Path.GetFileName(
                            gdtbPath));
                }

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
                    Path.Combine(
                        inDir, sub);
                string moddedPath =
                    Path.Combine(
                        subPath,
                        "_modded.rdtb");
                string sourcePath =
                    Path.Combine(
                        subPath,
                        "_source.rdtb");

                byte[] rdtbBytes;
                string status;

                if (File.Exists(
                        moddedPath))
                {
                    rdtbBytes =
                        File
                            .ReadAllBytes(
                                moddedPath);
                    status = "MODDED";
                    moddedCount++;
                }
                else if (
                    Directory.Exists(
                        subPath) &&
                    HasBatchOBJs(
                        subPath))
                {
                    try
                    {
                        string tempOut =
                            Path.Combine(
                                subPath,
                                "_rebuild_tmp");

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

                        string foundRdtb =
                            null;
                        if (Directory
                                .Exists(
                                    tempOut))
                        {
                            foreach (
                                string f in
                                Directory
                                    .GetFiles(
                                        tempOut,
                                        "*.rdtb"))
                            {
                                foundRdtb = f;
                                break;
                            }
                        }

                        if (foundRdtb !=
                                null &&
                            File.Exists(
                                foundRdtb))
                        {
                            rdtbBytes =
                                File
                                    .ReadAllBytes(
                                        foundRdtb);
                            status =
                                "REBUILT";
                            moddedCount++;
                        }
                        else
                        {
                            rdtbBytes =
                                File.Exists(
                                    sourcePath)
                                ? File
                                    .ReadAllBytes(
                                        sourcePath)
                                : rdtbs[idx];
                            status =
                                "source"
                                + "(no output)";
                        }

                        try
                        {
                            Directory
                                .Delete(
                                    tempOut,
                                    true);
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        Console
                            .ForegroundColor
                            = ConsoleColor
                                .Yellow;
                        Console.WriteLine(
                            "    [" +
                            idx.ToString(
                                "D2") +
                            "] rebuild "
                            + "err: " +
                            ex.Message);
                        Console
                            .ResetColor();
                        rdtbBytes =
                            File.Exists(
                                sourcePath)
                            ? File
                                .ReadAllBytes(
                                    sourcePath)
                            : rdtbs[idx];
                        status =
                            "source(err)";
                    }
                }
                else if (File.Exists(
                        sourcePath))
                {
                    rdtbBytes =
                        File
                            .ReadAllBytes(
                                sourcePath);
                    status = "source";
                }
                else
                {
                    rdtbBytes =
                        rdtbs[idx];
                    status = "MISSING";
                }

                int pad = (16 -
                    rdtbBytes.Length
                    % 16) % 16;
                if (pad > 0)
                {
                    var padded =
                        new byte[
                            rdtbBytes
                                .Length
                            + pad];
                    Array.Copy(
                        rdtbBytes,
                        padded,
                        rdtbBytes
                            .Length);
                    rdtbBytes = padded;
                }

                finalRdtbs.Add(
                    rdtbBytes);

                int change =
                    rdtbBytes.Length -
                    rdtbs[idx].Length;
                string csv =
                    change == 0
                    ? "same"
                    : (change > 0
                       ? "+" + change
                         + " B"
                       : change
                         + " B");

                Console.WriteLine(
                    "  [" +
                    idx.ToString("D2") +
                    "] " +
                    status.PadRight(20) +
                    " " +
                    rdtbs[idx].Length
                        .ToString()
                        .PadLeft(7) +
                    " B -> " +
                    rdtbBytes.Length
                        .ToString()
                        .PadLeft(7) +
                    " B  (" +
                    csv + ")");
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

            string srcGdtb =
                Path.Combine(inDir,
                    "_source.gdtb");
            if (File.Exists(srcGdtb))
            {
                File.Copy(srcGdtb,
                    outGdtb, true);
                Console.WriteLine(
                    "\n[OK] GDTB: "
                    + outGdtb);
            }
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
    }
}
