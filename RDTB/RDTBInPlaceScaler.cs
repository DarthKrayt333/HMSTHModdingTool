using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    public static class RDTBInPlaceScaler
    {
        private const float EPS = 0.001f;
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;

        public static void Apply(
            string folderPath,
            string outRdtbPath)
        {
            if (!File.Exists(outRdtbPath))
                return;
            if (!Directory.Exists(folderPath))
                return;

            string srcRdtb = Path.Combine(
                folderPath, "_source.rdtb");
            if (!File.Exists(srcRdtb))
                return;

            if (!HasUnchangedVertBatchEdits(
                    folderPath, srcRdtb))
                return;

            byte[] originalRdtb =
                File.ReadAllBytes(srcRdtb);
            byte[] outputRdtb =
                File.ReadAllBytes(outRdtbPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] In-Place Scale/Move"
                + " Applier v4"
                + " (centroid delta)");
            Console.ResetColor();

            string[] modelDirs =
                Directory.GetDirectories(
                    folderPath, "model_*");

            var batchObjs =
                new SortedDictionary<
                    int, string>();
            foreach (string md in modelDirs)
            {
                foreach (string f in
                    Directory.GetFiles(
                        md, "batch_*.obj"))
                {
                    string fn =
                        Path
                        .GetFileNameWithoutExtension(
                            f);
                    if (fn.StartsWith(
                            "batch_"))
                    {
                        string ns =
                            fn.Substring(6);
                        int bi;
                        if (int.TryParse(
                                ns, out bi))
                            batchObjs[bi] = f;
                    }
                }
            }

            if (batchObjs.Count == 0)
                return;

            List<int> origOffsets =
                ReadChunkOffsets(
                    originalRdtb);
            List<int> origMeshChunks =
                DetectMeshChunks(
                    originalRdtb,
                    origOffsets);

            List<int> outOffsets =
                ReadChunkOffsets(outputRdtb);
            List<int> outMeshChunks =
                DetectMeshChunks(
                    outputRdtb, outOffsets);

            if (origMeshChunks.Count == 0
                || outMeshChunks.Count == 0)
                return;

            int totalScaled = 0;
            int totalUnchanged = 0;
            int totalSkipped = 0;

            int chunkCount = Math.Min(
                origMeshChunks.Count,
                outMeshChunks.Count);

            for (int mi = 0;
                 mi < chunkCount; mi++)
            {
                int origCi =
                    origMeshChunks[mi];
                int outCi =
                    outMeshChunks[mi];

                int origStart =
                    origOffsets[origCi];
                int origEnd =
                    (origCi + 1 <
                        origOffsets.Count)
                    ? origOffsets[origCi + 1]
                    : originalRdtb.Length;

                int outStart =
                    outOffsets[outCi];
                int outEnd =
                    (outCi + 1 <
                        outOffsets.Count)
                    ? outOffsets[outCi + 1]
                    : outputRdtb.Length;

                var origBatches =
                    ParseChunkBatches(
                        originalRdtb,
                        origStart, origEnd);
                var outBatches =
                    ParseChunkBatches(
                        outputRdtb,
                        outStart, outEnd);

                int cScaled = 0;
                int cUnchanged = 0;
                int cSkipped = 0;

                foreach (var kv in origBatches)
                {
                    int batchIdx = kv.Key;
                    var origVertOffs = kv.Value;

                    if (!batchObjs
                            .ContainsKey(batchIdx))
                        continue;

                    if (!outBatches
                            .ContainsKey(batchIdx))
                    {
                        if (batchIdx == 5)
                            Console.WriteLine(
                                "    [DBG] batch 5"
                                + " NOT in outBatches");
                        continue;
                    }

                    var outVertOffs =
                        outBatches[batchIdx];

                    if (origVertOffs.Count
                        != outVertOffs.Count)
                    {
                        if (batchIdx == 5)
                            Console.WriteLine(
                                "    [DBG] batch 5"
                                + " count mismatch: orig="
                                + origVertOffs.Count
                                + " out="
                                + outVertOffs.Count);
                        cSkipped++;
                        continue;
                    }

                    var objVerts =
                        LoadObjVerts(
                            batchObjs[batchIdx]);

                    if (objVerts.Count == 0)
                    {
                        if (batchIdx == 5)
                            Console.WriteLine(
                                "    [DBG] batch 5"
                                + " obj empty");
                        cSkipped++;
                        continue;
                    }

                    if (batchIdx == 5)
                    {
                        // Compute bboxes and show
                        float gMinX = float.MaxValue;
                        float gMaxX = float.MinValue;
                        for (int i = 0;
                             i < origVertOffs.Count; i++)
                        {
                            int row = origVertOffs[i];
                            if (row + 16 >
                                originalRdtb.Length) break;
                            float x =
                                BitConverter.ToSingle(
                                    originalRdtb, row + 4);
                            if (x < gMinX) gMinX = x;
                            if (x > gMaxX) gMaxX = x;
                        }
                        float gCx = (gMinX + gMaxX) * 0.5f;

                        float oMinX = float.MaxValue;
                        float oMaxX = float.MinValue;
                        foreach (var o in objVerts)
                        {
                            if (o[0] < oMinX) oMinX = o[0];
                            if (o[0] > oMaxX) oMaxX = o[0];
                        }
                        float oCx = (oMinX + oMaxX) * 0.5f;

                        Console.WriteLine(
                            "    [DBG] batch 5:"
                            + " gCx=" + gCx
                            + " oCx=" + oCx
                            + " moveX=" + (oCx - gCx)
                            + " origVerts="
                            + origVertOffs.Count
                            + " objVerts="
                            + objVerts.Count);
                    }

                    int changed =
                        ApplyToVerts(
                            outputRdtb,
                            originalRdtb,
                            outVertOffs,
                            origVertOffs,
                            objVerts);

                    if (batchIdx == 5)
                        Console.WriteLine(
                            "    [DBG] batch 5"
                            + " changed=" + changed);

                    if (changed > 0)
                        cScaled++;
                    else
                        cUnchanged++;
                }

                if (cScaled > 0
                    || cSkipped > 0)
                {
                    Console.WriteLine(
                        "    Chunk " +
                        outCi + ": " +
                        "scaled=" + cScaled +
                        " unchanged=" +
                        cUnchanged +
                        " skipped=" +
                        cSkipped);
                }

                totalScaled += cScaled;
                totalUnchanged += cUnchanged;
                totalSkipped += cSkipped;
            }

            if (totalScaled > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    [OK] " +
                    totalScaled +
                    " batch(es) scaled/moved"
                    + " (centroid delta)");
                Console.ResetColor();

                File.WriteAllBytes(
                    outRdtbPath, outputRdtb);
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.DarkGray;
                Console.WriteLine(
                    "    (No real changes"
                    + " detected)");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════
        // PRE-FLIGHT
        // ═════════════════════════════════
        private static bool
            HasUnchangedVertBatchEdits(
                string folderPath,
                string srcRdtbPath)
        {
            string[] modelDirs =
                Directory.GetDirectories(
                    folderPath, "model_*");
            if (modelDirs.Length == 0)
                return false;

            var batchObjs =
                new SortedDictionary<int,
                    string>();
            foreach (string md in modelDirs)
            {
                foreach (string f in
                    Directory.GetFiles(md,
                        "batch_*.obj"))
                {
                    string fn = Path
                        .GetFileNameWithoutExtension(
                            f);
                    if (!fn.StartsWith("batch_"))
                        continue;
                    int bi;
                    if (int.TryParse(
                            fn.Substring(6),
                            out bi))
                        batchObjs[bi] = f;
                }
            }
            if (batchObjs.Count == 0)
                return false;

            byte[] originalRdtb =
                File.ReadAllBytes(srcRdtbPath);
            List<int> offs =
                ReadChunkOffsets(originalRdtb);
            List<int> meshChunks =
                DetectMeshChunks(
                    originalRdtb, offs);
            if (meshChunks.Count == 0)
                return false;

            int ci = meshChunks[0];
            int cStart = offs[ci];
            int cEnd = (ci + 1 < offs.Count)
                ? offs[ci + 1]
                : originalRdtb.Length;

            var origBatches =
                ParseChunkBatches(
                    originalRdtb, cStart, cEnd);

            foreach (var kv in origBatches)
            {
                int batchIdx = kv.Key;
                var vertOffsets = kv.Value;

                if (!batchObjs.ContainsKey(
                        batchIdx))
                    continue;

                var objVerts =
                    LoadObjVerts(
                        batchObjs[batchIdx]);
                if (objVerts.Count == 0)
                    continue;

                int n = Math.Min(
                    vertOffsets.Count,
                    objVerts.Count);
                if (n == 0) continue;

                // Centroid delta move detection
                // (coordinate-space agnostic)
                double gCx2 = 0, gCy2 = 0,
                    gCz2 = 0;
                double oCx2 = 0, oCy2 = 0,
                    oCz2 = 0;
                for (int i = 0; i < n; i++)
                {
                    int row = vertOffsets[i];
                    if (row + 16 >
                        originalRdtb.Length)
                        break;
                    gCx2 += BitConverter.ToSingle(
                        originalRdtb, row + 4);
                    gCy2 += BitConverter.ToSingle(
                        originalRdtb, row + 8);
                    gCz2 += BitConverter.ToSingle(
                        originalRdtb, row + 12);
                    oCx2 += objVerts[i][0];
                    oCy2 += objVerts[i][1];
                    oCz2 += objVerts[i][2];
                }
                float dcx =
                    (float)((oCx2 - gCx2) / n);
                float dcy =
                    (float)((oCy2 - gCy2) / n);
                float dcz =
                    (float)((oCz2 - gCz2) / n);

                if (Math.Abs(dcx) > 0.005f
                    || Math.Abs(dcy) > 0.005f
                    || Math.Abs(dcz) > 0.005f)
                    return true;

                // Also check for scale
                // (bbox size ratio)
                float gMinX = float.MaxValue;
                float gMaxX = float.MinValue;
                float oMinX = float.MaxValue;
                float oMaxX = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    int row = vertOffsets[i];
                    if (row + 16 >
                        originalRdtb.Length)
                        break;
                    float gx = BitConverter
                        .ToSingle(
                            originalRdtb,
                            row + 4);
                    if (gx < gMinX) gMinX = gx;
                    if (gx > gMaxX) gMaxX = gx;
                    if (objVerts[i][0] < oMinX)
                        oMinX = objVerts[i][0];
                    if (objVerts[i][0] > oMaxX)
                        oMaxX = objVerts[i][0];
                }
                float gSx = gMaxX - gMinX;
                float oSx = oMaxX - oMinX;
                if (gSx > 0.001f)
                {
                    float ratio = oSx / gSx;
                    if (Math.Abs(ratio - 1.0f)
                        > 0.005f)
                        return true;
                }
            }
            return false;
        }

        // ═════════════════════════════════
        // APPLY TO VERTS v4
        // Centroid-delta method.
        // Completely ignores extractor
        // coordinate bugs. Only measures
        // scale and move you applied in
        // Blender and applies that to the
        // original correct game vertices.
        // ═════════════════════════════════
        private static int ApplyToVerts(
            byte[] outputData,
            byte[] originalData,
            List<int> outVertOffs,
            List<int> origVertOffs,
            List<float[]> objVerts)
        {
            if (outVertOffs.Count == 0
                || origVertOffs.Count == 0
                || objVerts.Count == 0)
                return 0;

            int n = Math.Min(
                Math.Min(
                    outVertOffs.Count,
                    origVertOffs.Count),
                objVerts.Count);
            if (n == 0) return 0;

            // Read all original game verts
            List<float[]> gameVerts =
                new List<float[]>();
            for (int i = 0; i < n; i++)
            {
                int row = origVertOffs[i];
                if (row + 16 >
                    originalData.Length) break;
                gameVerts.Add(new float[]
                {
            BitConverter.ToSingle(
                originalData, row + 4),
            BitConverter.ToSingle(
                originalData, row + 8),
            BitConverter.ToSingle(
                originalData, row + 12)
                });
            }

            int cnt = Math.Min(
                gameVerts.Count, n);
            if (cnt == 0) return 0;

            // ── COMPUTE CENTROIDS ──────────
            // Centroid of original game verts
            double gSumX = 0, gSumY = 0,
                gSumZ = 0;
            for (int i = 0; i < cnt; i++)
            {
                gSumX += gameVerts[i][0];
                gSumY += gameVerts[i][1];
                gSumZ += gameVerts[i][2];
            }
            float gCentX =
                (float)(gSumX / cnt);
            float gCentY =
                (float)(gSumY / cnt);
            float gCentZ =
                (float)(gSumZ / cnt);

            // Centroid of OBJ verts
            double oSumX = 0, oSumY = 0,
                oSumZ = 0;
            for (int i = 0; i < cnt; i++)
            {
                oSumX += objVerts[i][0];
                oSumY += objVerts[i][1];
                oSumZ += objVerts[i][2];
            }
            float oCentX =
                (float)(oSumX / cnt);
            float oCentY =
                (float)(oSumY / cnt);
            float oCentZ =
                (float)(oSumZ / cnt);

            // ── COMPUTE BBOX FOR SCALE ─────
            float gMinX = float.MaxValue;
            float gMaxX = float.MinValue;
            float gMinY = float.MaxValue;
            float gMaxY = float.MinValue;
            float gMinZ = float.MaxValue;
            float gMaxZ = float.MinValue;
            float oMinX = float.MaxValue;
            float oMaxX = float.MinValue;
            float oMinY = float.MaxValue;
            float oMaxY = float.MinValue;
            float oMinZ = float.MaxValue;
            float oMaxZ = float.MinValue;

            for (int i = 0; i < cnt; i++)
            {
                float[] g = gameVerts[i];
                float[] o = objVerts[i];
                if (g[0] < gMinX) gMinX = g[0];
                if (g[0] > gMaxX) gMaxX = g[0];
                if (g[1] < gMinY) gMinY = g[1];
                if (g[1] > gMaxY) gMaxY = g[1];
                if (g[2] < gMinZ) gMinZ = g[2];
                if (g[2] > gMaxZ) gMaxZ = g[2];
                if (o[0] < oMinX) oMinX = o[0];
                if (o[0] > oMaxX) oMaxX = o[0];
                if (o[1] < oMinY) oMinY = o[1];
                if (o[1] > oMaxY) oMaxY = o[1];
                if (o[2] < oMinZ) oMinZ = o[2];
                if (o[2] > oMaxZ) oMaxZ = o[2];
            }

            float gSx = gMaxX - gMinX;
            float gSy = gMaxY - gMinY;
            float gSz = gMaxZ - gMinZ;
            float oSx = oMaxX - oMinX;
            float oSy = oMaxY - oMinY;
            float oSz = oMaxZ - oMinZ;

            float scaleX =
                (gSx > 0.001f)
                ? (oSx / gSx) : 1.0f;
            float scaleY =
                (gSy > 0.001f)
                ? (oSy / gSy) : 1.0f;
            float scaleZ =
                (gSz > 0.001f)
                ? (oSz / gSz) : 1.0f;

            bool scaled =
                Math.Abs(scaleX - 1.0f)
                    > 0.005f
                || Math.Abs(scaleY - 1.0f)
                    > 0.005f
                || Math.Abs(scaleZ - 1.0f)
                    > 0.005f;

            // ── CENTROID-TO-CENTROID MOVE ──
            // This works in any coordinate
            // space because it measures the
            // RELATIVE shift of the cloud
            // center — bone offset cancels
            // out since it is the same
            // constant added to all verts
            // in both OBJ and game space.
            float moveX = oCentX - gCentX;
            float moveY = oCentY - gCentY;
            float moveZ = oCentZ - gCentZ;

            bool moved =
                Math.Abs(moveX) > 0.005f
                || Math.Abs(moveY) > 0.005f
                || Math.Abs(moveZ) > 0.005f;

            if (!scaled && !moved)
            {
                // No real edit detected.
                // Restore original bytes.
                for (int i = 0;
                     i < cnt; i++)
                {
                    int outRow =
                        outVertOffs[i];
                    Array.Copy(
                        originalData,
                        origVertOffs[i] + 4,
                        outputData,
                        outRow + 4, 12);
                }
                return 0;
            }

            // ── APPLY TRANSFORM ───────────
            // Scale around game bbox center,
            // then translate by centroid delta
            float gBbCx =
                (gMinX + gMaxX) * 0.5f;
            float gBbCy =
                (gMinY + gMaxY) * 0.5f;
            float gBbCz =
                (gMinZ + gMaxZ) * 0.5f;

            int changed = 0;
            for (int i = 0; i < cnt; i++)
            {
                float[] g = gameVerts[i];
                float newX, newY, newZ;

                if (scaled)
                {
                    // Scale around bbox center
                    // of game verts, then
                    // add centroid move delta
                    newX = (g[0] - gBbCx)
                        * scaleX + gBbCx
                        + moveX;
                    newY = (g[1] - gBbCy)
                        * scaleY + gBbCy
                        + moveY;
                    newZ = (g[2] - gBbCz)
                        * scaleZ + gBbCz
                        + moveZ;
                }
                else
                {
                    // Pure translation:
                    // add centroid delta
                    // to each original
                    // game vert
                    newX = g[0] + moveX;
                    newY = g[1] + moveY;
                    newZ = g[2] + moveZ;
                }

                int outRow = outVertOffs[i];
                WriteFloat(outputData,
                    outRow + 4, newX);
                WriteFloat(outputData,
                    outRow + 8, newY);
                WriteFloat(outputData,
                    outRow + 12, newZ);
                changed++;
            }
            return changed;
        }

        // ═════════════════════════════════
        // WRITE FLOAT
        // ═════════════════════════════════
        private static void WriteFloat(
            byte[] data, int off, float v)
        {
            byte[] b =
                BitConverter.GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }

        // ═════════════════════════════════
        // READ CHUNK OFFSETS
        // ═════════════════════════════════
        private static List<int>
            ReadChunkOffsets(byte[] data)
        {
            var offs = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                if (0x10 + i * 4 + 4 >
                    data.Length)
                    break;
                uint v =
                    BitConverter.ToUInt32(
                        data,
                        0x10 + i * 4);
                if (v == 0) continue;
                if (v == 0xFFFFFFFF) continue;
                if (v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            offs.Sort();
            offs = offs.Distinct().ToList();
            return offs;
        }

        // ═════════════════════════════════
        // DETECT MESH CHUNKS
        // ═════════════════════════════════
        private static List<int>
            DetectMeshChunks(
                byte[] data,
                List<int> offs)
        {
            var result = new List<int>();

            // Read raw slot 11 to find the
            // real mesh chunk offset.
            // Slot 11 is always the mesh
            // (LOD0) in BIG, SMALL and
            // MIRROR RDTBs.
            if (data.Length < 0x10 + 12 * 4)
                return result;
            uint meshRawOff =
                BitConverter.ToUInt32(
                    data, 0x10 + 11 * 4);

            if (meshRawOff == 0 ||
                meshRawOff == 0xFFFFFFFF)
                return result;

            // Find its index in offs
            int meshIdx =
                offs.IndexOf((int)meshRawOff);
            if (meshIdx >= 0)
                result.Add(meshIdx);

            return result;
        }

        // ═════════════════════════════════
        // PARSE CHUNK BATCHES
        // ═════════════════════════════════
        private static
            SortedDictionary<int, List<int>>
            ParseChunkBatches(
                byte[] data,
                int chunkStart,
                int chunkEnd)
        {
            var result =
                new SortedDictionary<
                    int, List<int>>();

            uint firstPtr =
                BitConverter.ToUInt32(
                    data, chunkStart);
            if (firstPtr == 0 ||
                firstPtr > (uint)
                    (chunkEnd - chunkStart) ||
                firstPtr < 4)
                return result;

            int nPtrs =
                (int)(firstPtr / 4);

            var batchStarts =
                new List<(int idx,
                    uint ptr)>();
            for (int i = 0; i < nPtrs; i++)
            {
                int poff =
                    chunkStart + i * 4;
                if (poff + 4 > data.Length)
                    break;
                uint ptr =
                    BitConverter.ToUInt32(
                        data, poff);
                if (ptr == 0) continue;
                batchStarts.Add((i, ptr));
            }

            var sortedByOffset =
                batchStarts
                    .OrderBy(b => b.ptr)
                    .ToList();

            for (int si = 0;
                 si < sortedByOffset.Count;
                 si++)
            {
                var (batchIdx, bPtr) =
                    sortedByOffset[si];
                uint bEnd =
                    (si + 1 <
                        sortedByOffset.Count)
                    ? sortedByOffset[
                        si + 1].ptr
                    : (uint)(chunkEnd -
                              chunkStart);

                int absBatchStart =
                    chunkStart + (int)bPtr;
                int absBatchEnd =
                    chunkStart + (int)bEnd;

                var vertOffsets =
                    ParseBatchVertOffsets(
                        data,
                        absBatchStart,
                        absBatchEnd);

                if (vertOffsets.Count > 0)
                    result[batchIdx] =
                        vertOffsets;
            }
            return result;
        }

        // ═════════════════════════════════
        // PARSE BATCH VERTEX OFFSETS
        // ═════════════════════════════════
        private static List<int>
            ParseBatchVertOffsets(
                byte[] data,
                int start,
                int end)
        {
            var offsets = new List<int>();
            int pos = start;

            while (pos + 16 <= end)
            {
                if (data[pos] != VIF_B0 ||
                    data[pos + 1] != VIF_B1 ||
                    data[pos + 3] != VIF_B3)
                {
                    pos += 4;
                    continue;
                }
                int vc = data[pos + 4];
                if (vc < 1 || vc > 96)
                {
                    pos += 4;
                    continue;
                }
                int vStart = pos + 16;
                int nStart =
                    vStart + vc * 16;
                int uStart =
                    nStart + vc * 16;
                if (uStart + vc * 16 > end)
                {
                    pos += 4;
                    continue;
                }
                for (int i = 0; i < vc; i++)
                    offsets.Add(
                        vStart + i * 16);

                int blockSize =
                    16 + 3 * vc * 16 + 16;
                if (pos + blockSize + 16
                    <= end)
                {
                    uint eof =
                        BitConverter.ToUInt32(
                            data,
                            pos + blockSize);
                    if (eof == 0x70000000)
                        blockSize += 16;
                }
                pos += blockSize;
            }
            return offsets;
        }

        // ═════════════════════════════════
        // LOAD OBJ VERTS
        // ═════════════════════════════════
        private static List<float[]>
            LoadObjVerts(string path)
        {
            var verts = new List<float[]>();
            var ci =
                System.Globalization
                    .CultureInfo
                    .InvariantCulture;

            using (var fh =
                new StreamReader(
                    path, Encoding.UTF8))
            {
                string line;
                while ((line =
                    fh.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (string.IsNullOrEmpty(t))
                        continue;
                    if (t[0] == '#') continue;
                    if (t.Length < 2) continue;
                    if (t[0] != 'v' ||
                        t[1] != ' ')
                        continue;

                    string[] p = t.Split(
                        new char[]
                        { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                    if (p.Length < 4) continue;
                    if (p[0].ToLower() != "v")
                        continue;

                    float x, y, z;
                    if (float.TryParse(
                            p[1],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out x) &&
                        float.TryParse(
                            p[2],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out y) &&
                        float.TryParse(
                            p[3],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out z))
                    {
                        verts.Add(
                            new float[]
                            { x, y, z });
                    }
                }
            }
            return verts;
        }
    }
}
