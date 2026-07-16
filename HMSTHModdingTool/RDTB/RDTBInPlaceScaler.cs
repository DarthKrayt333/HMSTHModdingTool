using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// Standalone in-place scale/move
    /// applier for batches with UNCHANGED
    /// vertex count.
    ///
    /// Runs AFTER cbatches has finished
    /// building the RDTB. Scans the output
    /// folder for batch OBJ files, compares
    /// each vertex to the original RDTB
    /// bytes, and writes ONLY the changed
    /// XYZ floats at exact VIF byte offsets.
    ///
    /// Preserves everything else exactly:
    ///   - VIF headers
    ///   - GIF tags
    ///   - EOF terminators
    ///   - Vertex flags (bone weights)
    ///   - Normals
    ///   - UVs
    ///   - Batch pointer tables
    ///   - LOD chunk structure
    ///
    /// Works with BIG, SMALL, MIRRORED
    /// RDTBs, and SRDB embedded RDTBs.
    /// For BIG RDTBs, applies the same
    /// edit to all 3 LOD mesh chunks
    /// (11/12/13) automatically.
    ///
    /// PRE-FLIGHT DETECTION:
    /// Does a quick scan first. If no
    /// unchanged-vertex batch has any
    /// scaled/moved vertices, exits
    /// silently with zero side effects.
    /// Full run only activates when
    /// real edits are detected.
    ///
    /// Does NOT touch batches whose
    /// vertex count differs from the
    /// original — those go through the
    /// standard cbatches recompile
    /// pipeline.
    /// </summary>
    public static class RDTBInPlaceScaler
    {
        // Change detection threshold.
        // Values differing from original
        // by less than this are considered
        // unchanged. Tuned tight so even
        // small Blender nudges register
        // as edits.
        private const float EPS = 0.001f;

        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;

        // ═════════════════════════════════
        // APPLY
        // Called by cbatches after the
        // main rebuild completes. Uses
        // pre-flight check to exit
        // silently when nothing to do.
        // ═════════════════════════════════
        public static void Apply(
            string folderPath,
            string outRdtbPath)
        {
            if (!File.Exists(outRdtbPath))
                return;
            if (!Directory.Exists(
                    folderPath))
                return;

            string srcRdtb = Path.Combine(
                folderPath,
                "_source.rdtb");
            if (!File.Exists(srcRdtb))
                return;

            // ─── PRE-FLIGHT DETECTION ───
            // Only proceed if at least one
            // batch OBJ has (a) same vertex
            // count as original AND (b) at
            // least one vertex changed by
            // >= EPS. Otherwise exit
            // silently — no console output,
            // no rewrites, no risk.
            if (!HasUnchangedVertBatchEdits(
                    folderPath, srcRdtb))
                return;

            byte[] originalRdtb =
                File.ReadAllBytes(srcRdtb);
            byte[] outputRdtb =
                File.ReadAllBytes(
                    outRdtbPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] In-Place Scale/Move"
                + " Applier");
            Console.ResetColor();
            Console.WriteLine(
                "    Detected"
                + " scaled/moved batches"
                + " with unchanged vertex"
                + " count. Applying...");

            // Find all batch OBJ files
            string[] modelDirs =
                Directory.GetDirectories(
                    folderPath,
                    "model_*");

            var batchObjs =
                new SortedDictionary<int,
                    string>();
            foreach (string md in
                modelDirs)
            {
                foreach (string f in
                    Directory.GetFiles(
                        md,
                        "batch_*.obj"))
                {
                    string fn = Path
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
                            batchObjs[bi]
                                = f;
                    }
                }
            }

            if (batchObjs.Count == 0)
                return;

            // Detect mesh chunks in
            // original RDTB (before
            // cbatches touched it)
            List<int> origOffsets =
                ReadChunkOffsets(
                    originalRdtb);
            List<int> origMeshChunks =
                DetectMeshChunks(
                    originalRdtb,
                    origOffsets);

            // Detect mesh chunks in
            // output RDTB (positions
            // may have shifted due
            // to cbatches replacing
            // other batches)
            List<int> outOffsets =
                ReadChunkOffsets(
                    outputRdtb);
            List<int> outMeshChunks =
                DetectMeshChunks(
                    outputRdtb,
                    outOffsets);

            if (origMeshChunks.Count == 0
                || outMeshChunks.Count
                == 0)
                return;

            int totalScaled = 0;
            int totalUnchanged = 0;
            int totalSkippedDiffVc = 0;

            // Process each mesh chunk.
            // BIG: chunks 11/12/13.
            // SMALL: single mesh chunk.
            // MIRRORED: same as BIG
            // before slot mirror is
            // applied.
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
                    ? origOffsets[
                        origCi + 1]
                    : originalRdtb.Length;

                int outStart =
                    outOffsets[outCi];
                int outEnd =
                    (outCi + 1 <
                        outOffsets.Count)
                    ? outOffsets[
                        outCi + 1]
                    : outputRdtb.Length;

                // Parse batches from
                // BOTH original and
                // output. Match by
                // batch index.
                var origBatches =
                    ParseChunkBatches(
                        originalRdtb,
                        origStart,
                        origEnd);
                var outBatches =
                    ParseChunkBatches(
                        outputRdtb,
                        outStart,
                        outEnd);

                int cScaled = 0;
                int cUnchanged = 0;
                int cSkipped = 0;

                foreach (var kv in
                    origBatches)
                {
                    int batchIdx = kv.Key;
                    var origVertOffs =
                        kv.Value;

                    if (!batchObjs
                            .ContainsKey(
                                batchIdx))
                        continue;

                    if (!outBatches
                            .ContainsKey(
                                batchIdx))
                        continue;

                    var outVertOffs =
                        outBatches[
                            batchIdx];

                    // In-place overwrite
                    // requires matching
                    // vertex count in
                    // BOTH original and
                    // output. If output
                    // count differs,
                    // cbatches already
                    // recompiled — don't
                    // touch it.
                    if (origVertOffs.Count
                        != outVertOffs
                            .Count)
                    {
                        cSkipped++;
                        continue;
                    }

                    var objVerts =
                        LoadObjVerts(
                            batchObjs[
                                batchIdx]);

                    // OBJ must match too
                    if (objVerts.Count !=
                        origVertOffs.Count)
                    {
                        cSkipped++;
                        continue;
                    }

                    // Per-vertex compare
                    // vs ORIGINAL bytes,
                    // write to OUTPUT
                    // where real change
                    // exists.
                    int changed =
                        ApplyToVerts(
                            outputRdtb,
                            originalRdtb,
                            outVertOffs,
                            origVertOffs,
                            objVerts);

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
                        "scaled=" +
                        cScaled +
                        " unchanged=" +
                        cUnchanged +
                        " skipped(diff-vc)="
                        + cSkipped);
                }

                totalScaled += cScaled;
                totalUnchanged +=
                    cUnchanged;
                totalSkippedDiffVc +=
                    cSkipped;
            }

            if (totalScaled > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    [OK] "
                    + totalScaled +
                    " batch(es)"
                    + " scaled/moved"
                    + " in-place");
                Console.ResetColor();

                File.WriteAllBytes(
                    outRdtbPath,
                    outputRdtb);
            }
        }

        // ═════════════════════════════════
        // PRE-FLIGHT DETECTION
        // Quick scan: does any batch OBJ
        // have (a) same vertex count as
        // the original AND (b) at least
        // one vertex changed by >= EPS?
        // If not, caller skips this whole
        // module — no console output, no
        // rewrites, no risk.
        // ═════════════════════════════════
        private static bool
            HasUnchangedVertBatchEdits(
                string folderPath,
                string srcRdtbPath)
        {
            string[] modelDirs =
                Directory.GetDirectories(
                    folderPath,
                    "model_*");
            if (modelDirs.Length == 0)
                return false;

            var batchObjs =
                new SortedDictionary<int,
                    string>();
            foreach (string md in
                modelDirs)
            {
                foreach (string f in
                    Directory.GetFiles(
                        md,
                        "batch_*.obj"))
                {
                    string fn = Path
                        .GetFileNameWithoutExtension(
                            f);
                    if (!fn.StartsWith(
                            "batch_"))
                        continue;
                    string ns =
                        fn.Substring(6);
                    int bi;
                    if (int.TryParse(
                            ns, out bi))
                        batchObjs[bi] = f;
                }
            }
            if (batchObjs.Count == 0)
                return false;

            byte[] originalRdtb =
                File.ReadAllBytes(
                    srcRdtbPath);

            List<int> offs =
                ReadChunkOffsets(
                    originalRdtb);
            List<int> meshChunks =
                DetectMeshChunks(
                    originalRdtb, offs);
            if (meshChunks.Count == 0)
                return false;

            // Only check FIRST mesh
            // chunk (LOD0 for BIG,
            // single chunk for SMALL).
            // If any batch there has
            // an unchanged-vertex edit,
            // full run is needed.
            int ci = meshChunks[0];
            int cStart = offs[ci];
            int cEnd = (ci + 1 <
                offs.Count)
                ? offs[ci + 1]
                : originalRdtb.Length;

            var origBatches =
                ParseChunkBatches(
                    originalRdtb,
                    cStart, cEnd);

            foreach (var kv in
                origBatches)
            {
                int batchIdx = kv.Key;
                var vertOffsets =
                    kv.Value;

                if (!batchObjs
                        .ContainsKey(
                            batchIdx))
                    continue;

                var objVerts =
                    LoadObjVerts(
                        batchObjs[
                            batchIdx]);

                // Skip if vertex count
                // differs — cbatches
                // handles those
                if (objVerts.Count !=
                    vertOffsets.Count)
                    continue;

                // Check if ANY vertex
                // actually moved
                for (int i = 0;
                     i < vertOffsets
                        .Count; i++)
                {
                    int row =
                        vertOffsets[i];
                    float[] v =
                        objVerts[i];

                    if (row + 16 >
                        originalRdtb
                            .Length)
                        break;

                    float ox =
                        BitConverter
                            .ToSingle(
                                originalRdtb,
                                row + 4);
                    float oy =
                        BitConverter
                            .ToSingle(
                                originalRdtb,
                                row + 8);
                    float oz =
                        BitConverter
                            .ToSingle(
                                originalRdtb,
                                row + 12);

                    if (Math.Abs(v[0] - ox)
                            >= EPS ||
                        Math.Abs(v[1] - oy)
                            >= EPS ||
                        Math.Abs(v[2] - oz)
                            >= EPS)
                    {
                        // Found at least
                        // one scaled/moved
                        // vertex — full
                        // run needed
                        return true;
                    }
                }
            }

            // Scanned everything, no
            // edits detected
            return false;
        }

        // ═════════════════════════════════
        // Read chunk offsets from RDTB
        // slot table (skip zero + FFFFFFFF)
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
                if (v == 0xFFFFFFFF)
                    continue;
                if (v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            offs.Sort();
            offs = offs.Distinct()
                .ToList();
            return offs;
        }

        // ═════════════════════════════════
        // Detect mesh chunks (chunks with
        // valid pointer table + VIF data).
        // Excludes material chunk (slot 8).
        // ═════════════════════════════════
        private static List<int>
            DetectMeshChunks(
                byte[] data,
                List<int> offs)
        {
            var result = new List<int>();
            int matIdx = 8;

            for (int ci = 0;
                 ci < offs.Count; ci++)
            {
                if (ci == matIdx)
                    continue;

                int cs = offs[ci];
                int ce = (ci + 1 <
                    offs.Count)
                    ? offs[ci + 1]
                    : data.Length;
                int sz = ce - cs;
                if (sz < 64) continue;

                uint first =
                    BitConverter.ToUInt32(
                        data, cs);
                if (first == 0 ||
                    first > (uint)sz ||
                    first < 4)
                    continue;

                bool hasVif = false;
                for (int i = cs;
                     i + 16 <= ce;
                     i += 4)
                {
                    if (data[i] == VIF_B0
                        && data[i + 1]
                            == VIF_B1
                        && data[i + 3]
                            == VIF_B3)
                    {
                        hasVif = true;
                        break;
                    }
                }

                if (hasVif)
                    result.Add(ci);
            }
            return result;
        }

        // ═════════════════════════════════
        // Parse a mesh chunk into batches.
        // For each batch idx, returns the
        // absolute byte offsets of every
        // vertex row's flag byte (so +4/8/
        // 12 = X/Y/Z floats).
        // ═════════════════════════════════
        private static
            SortedDictionary<int,
                List<int>>
            ParseChunkBatches(
                byte[] data,
                int chunkStart,
                int chunkEnd)
        {
            var result =
                new SortedDictionary<int,
                    List<int>>();

            uint firstPtr =
                BitConverter.ToUInt32(
                    data, chunkStart);
            if (firstPtr == 0 ||
                firstPtr > (uint)
                    (chunkEnd - chunkStart)
                || firstPtr < 4)
                return result;

            int nPtrs =
                (int)(firstPtr / 4);

            var batchStarts =
                new List<(int idx,
                    uint ptr)>();
            for (int i = 0; i < nPtrs;
                 i++)
            {
                int poff = chunkStart
                    + i * 4;
                if (poff + 4 >
                    data.Length) break;
                uint ptr =
                    BitConverter.ToUInt32(
                        data, poff);
                if (ptr == 0) continue;
                batchStarts.Add(
                    (i, ptr));
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
                uint bEnd = (si + 1 <
                    sortedByOffset.Count)
                    ? sortedByOffset[
                        si + 1].ptr
                    : (uint)
                        (chunkEnd -
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
        // Walk a batch's VIF blocks and
        // return absolute file byte offset
        // of each vertex row (points to
        // the flag u32, so +4/+8/+12 =
        // X/Y/Z floats).
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
                if (data[pos] != VIF_B0
                    || data[pos + 1]
                        != VIF_B1
                    || data[pos + 3]
                        != VIF_B3)
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
                int nStart = vStart
                    + vc * 16;
                int uStart = nStart
                    + vc * 16;
                if (uStart + vc * 16 > end)
                {
                    pos += 4;
                    continue;
                }
                for (int i = 0; i < vc;
                     i++)
                {
                    offsets.Add(
                        vStart + i * 16);
                }
                int blockSize = 16 +
                    3 * vc * 16 + 16;
                if (pos + blockSize + 16
                    <= end)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                data,
                                pos +
                                blockSize);
                    if (eof == 0x70000000)
                        blockSize += 16;
                }
                pos += blockSize;
            }
            return offsets;
        }

        // ═════════════════════════════════
        // Load OBJ vertex positions in
        // file order. Only reads 'v '
        // lines — normals and UVs are
        // ignored (this script never
        // touches them).
        // ═════════════════════════════════
        private static List<float[]>
            LoadObjVerts(string path)
        {
            var verts =
                new List<float[]>();
            var ci = System
                .Globalization
                .CultureInfo
                .InvariantCulture;

            using (var fh =
                new StreamReader(
                    path, Encoding.UTF8))
            {
                string line;
                while ((line = fh.ReadLine())
                       != null)
                {
                    string t = line.Trim();
                    if (string.IsNullOrEmpty(
                            t)) continue;
                    if (t[0] == '#')
                        continue;
                    if (t.Length < 2)
                        continue;
                    if (t[0] != 'v' ||
                        t[1] != ' ')
                        continue;

                    string[] p = t.Split(
                        new char[]
                        { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                    if (p.Length < 4)
                        continue;
                    if (p[0].ToLower()
                        != "v") continue;

                    float x, y, z;
                    if (float.TryParse(
                            p[1],
                            System
                                .Globalization
                                .NumberStyles
                                .Float,
                            ci, out x) &&
                        float.TryParse(
                            p[2],
                            System
                                .Globalization
                                .NumberStyles
                                .Float,
                            ci, out y) &&
                        float.TryParse(
                            p[3],
                            System
                                .Globalization
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

        // ═════════════════════════════════
        // Per-vertex compare + write.
        // Compares each OBJ vertex against
        // the ORIGINAL RDTB bytes. If any
        // XYZ float differs by >= EPS,
        // writes new floats at the OUTPUT
        // byte offset. Preserves vertex
        // flags, normals, UVs, and all
        // VIF structure exactly.
        // Returns count of vertices that
        // actually changed.
        // ═════════════════════════════════
        private static int ApplyToVerts(
            byte[] outputData,
            byte[] originalData,
            List<int> outVertOffs,
            List<int> origVertOffs,
            List<float[]> objVerts)
        {
            int changed = 0;
            int n = Math.Min(
                Math.Min(
                    outVertOffs.Count,
                    origVertOffs.Count),
                objVerts.Count);

            for (int i = 0; i < n; i++)
            {
                int outRow =
                    outVertOffs[i];
                int origRow =
                    origVertOffs[i];
                float[] v = objVerts[i];

                int oxOff = origRow + 4;
                int oyOff = origRow + 8;
                int ozOff = origRow + 12;

                int wxOff = outRow + 4;
                int wyOff = outRow + 8;
                int wzOff = outRow + 12;

                if (ozOff + 4 >
                    originalData.Length)
                    break;
                if (wzOff + 4 >
                    outputData.Length)
                    break;

                float ox =
                    BitConverter.ToSingle(
                        originalData,
                        oxOff);
                float oy =
                    BitConverter.ToSingle(
                        originalData,
                        oyOff);
                float oz =
                    BitConverter.ToSingle(
                        originalData,
                        ozOff);

                bool xDiff =
                    Math.Abs(v[0] - ox)
                    >= EPS;
                bool yDiff =
                    Math.Abs(v[1] - oy)
                    >= EPS;
                bool zDiff =
                    Math.Abs(v[2] - oz)
                    >= EPS;

                if (xDiff || yDiff ||
                    zDiff)
                {
                    byte[] xb =
                        BitConverter
                            .GetBytes(v[0]);
                    byte[] yb =
                        BitConverter
                            .GetBytes(v[1]);
                    byte[] zb =
                        BitConverter
                            .GetBytes(v[2]);

                    outputData[wxOff]
                        = xb[0];
                    outputData[wxOff + 1]
                        = xb[1];
                    outputData[wxOff + 2]
                        = xb[2];
                    outputData[wxOff + 3]
                        = xb[3];

                    outputData[wyOff]
                        = yb[0];
                    outputData[wyOff + 1]
                        = yb[1];
                    outputData[wyOff + 2]
                        = yb[2];
                    outputData[wyOff + 3]
                        = yb[3];

                    outputData[wzOff]
                        = zb[0];
                    outputData[wzOff + 1]
                        = zb[1];
                    outputData[wzOff + 2]
                        = zb[2];
                    outputData[wzOff + 3]
                        = zb[3];

                    changed++;
                }
            }
            return changed;
        }
    }
}
