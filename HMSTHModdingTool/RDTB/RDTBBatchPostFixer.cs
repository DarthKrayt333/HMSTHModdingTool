using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// Post-fixer for cbatches output.
    /// Fixes 3 bugs WITHOUT touching
    /// RDTBBatchReplacer internals:
    ///
    /// FIX 1: Copies original game
    ///   normals into ALL new batch
    ///   VIF blocks by nearest-neighbor
    ///   from the _source.rdtb.
    ///
    /// FIX 2: Applies OBJ UV coords
    ///   to ALL batches, including
    ///   those whose tri count matched
    ///   the original (previously
    ///   silently skipped).
    ///
    /// FIX 3: When --forcenew is set,
    ///   uses OBJ normals as-is on
    ///   ALL batches instead of
    ///   transferring game originals.
    ///
    /// Usage (add to Program.cs):
    ///   postfix &lt;batch_folder&gt;
    ///           &lt;output_rdtb&gt;
    ///           [--forcenew]
    ///
    /// Run AFTER cbatches completes.
    /// </summary>
    public static class RDTBBatchPostFixer
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private const float EPS = 0.0001f;

        // ─────────────────────────────
        // ENTRY POINT
        // ─────────────────────────────
        public static void Apply(
            string folderPath,
            string outputRdtbPath,
            bool forceNewNormals)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTBBatchPostFixer"
                + " v1.0");
            Console.ResetColor();
            Console.WriteLine(
                "    Folder  : " +
                folderPath);
            Console.WriteLine(
                "    Output  : " +
                outputRdtbPath);
            Console.WriteLine(
                "    Normals : " +
                (forceNewNormals
                    ? "OBJ (forcenew)"
                    : "GAME (default)"));

            // ── Validate paths ────────
            if (!Directory.Exists(
                    folderPath))
            {
                TextOut.PrintError(
                    "Folder not found: "
                    + folderPath);
                return;
            }
            if (!File.Exists(
                    outputRdtbPath))
            {
                TextOut.PrintError(
                    "Output RDTB not"
                    + " found: " +
                    outputRdtbPath);
                return;
            }

            string srcRdtbPath =
                Path.Combine(
                    folderPath,
                    "_source.rdtb");
            if (!File.Exists(srcRdtbPath))
            {
                TextOut.PrintError(
                    "_source.rdtb not"
                    + " found in: " +
                    folderPath);
                return;
            }

            // ── Load files ────────────
            byte[] srcRdtb =
                File.ReadAllBytes(
                    srcRdtbPath);
            byte[] outRdtb =
                File.ReadAllBytes(
                    outputRdtbPath);

            // ── Scan batch OBJ files ──
            var batchObjs =
                new SortedDictionary<
                    int, string>();
            foreach (string md in
                Directory
                    .GetDirectories(
                        folderPath,
                        "model_*"))
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
                    int bi;
                    if (int.TryParse(
                            fn.Substring(6),
                            out bi))
                        batchObjs[bi] = f;
                }
            }

            if (batchObjs.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No batch OBJ"
                    + " files found."
                    + " Nothing to fix.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine(
                "    Batch OBJs: " +
                batchObjs.Count);

            // ── Get mesh chunk info ───
            // from OUTPUT rdtb
            var outOffs =
                ReadChunkOffsets(outRdtb);
            int outMeshIdx =
                FindMeshChunkIdx(
                    outRdtb, outOffs);
            if (outMeshIdx < 0)
            {
                TextOut.PrintError(
                    "No mesh chunk in"
                    + " output RDTB");
                return;
            }

            int outMeshStart =
                outOffs[outMeshIdx];
            int outMeshEnd =
                (outMeshIdx + 1 <
                    outOffs.Count)
                ? outOffs[
                    outMeshIdx + 1]
                : outRdtb.Length;

            // ── Get mesh chunk info ───
            // from SOURCE rdtb (for
            // original normals)
            var srcOffs =
                ReadChunkOffsets(srcRdtb);
            int srcMeshIdx =
                FindMeshChunkIdx(
                    srcRdtb, srcOffs);

            // Source mesh chunk for
            // normal sampling
            byte[] srcMeshChunk = null;
            int srcNPtrs = 0;
            if (srcMeshIdx >= 0)
            {
                int srcStart =
                    srcOffs[srcMeshIdx];
                int srcEnd =
                    (srcMeshIdx + 1 <
                        srcOffs.Count)
                    ? srcOffs[
                        srcMeshIdx + 1]
                    : srcRdtb.Length;
                srcMeshChunk =
                    new byte[
                        srcEnd - srcStart];
                Array.Copy(srcRdtb,
                    srcStart,
                    srcMeshChunk, 0,
                    srcMeshChunk.Length);
                uint sf = BitConverter
                    .ToUInt32(
                        srcMeshChunk, 0);
                if (sf > 0 &&
                    sf < (uint)
                        srcMeshChunk.Length)
                    srcNPtrs =
                        (int)(sf / 4);
            }

            // Pointer count in output
            // mesh chunk
            uint outFirst =
                BitConverter.ToUInt32(
                    outRdtb,
                    outMeshStart);
            int outNPtrs =
                (outFirst > 0 &&
                 outFirst < (uint)
                     (outMeshEnd -
                      outMeshStart))
                ? (int)(outFirst / 4)
                : 0;

            if (outNPtrs == 0)
            {
                TextOut.PrintError(
                    "Output mesh chunk"
                    + " has no valid"
                    + " pointer table");
                return;
            }

            // ── Read original normals ─
            // For each batch in batchObjs,
            // sample the SOURCE rdtb
            // normals at that batch slot
            var origNormals =
                new Dictionary<int,
                    List<(float[] pos,
                          float[] norm)>>();

            if (!forceNewNormals &&
                srcMeshChunk != null)
            {
                foreach (int bi in
                    batchObjs.Keys)
                {
                    var samples =
                        ReadBatchNormals(
                            srcMeshChunk,
                            bi,
                            srcNPtrs);
                    if (samples.Count > 0)
                        origNormals[bi] =
                            samples;
                }
                Console.WriteLine(
                    "    Sampled normals"
                    + " for " +
                    origNormals.Count +
                    " batches from"
                    + " source RDTB");
            }

            // ── Process each batch ────
            int fixedNormals = 0;
            int fixedUVs = 0;
            int skipped = 0;

            foreach (var kv in batchObjs)
            {
                int bi = kv.Key;
                string objPath = kv.Value;

                // Parse OBJ
                var verts =
                    new List<float[]>();
                var normals =
                    new List<float[]>();
                var uvs =
                    new List<float[]>();
                var tris =
                    new List<int[]>();

                ParseObj(objPath,
                    verts, normals,
                    uvs, tris);

                if (verts.Count == 0)
                {
                    skipped++;
                    continue;
                }

                // ── FIX 1 + FIX 3:
                // Normal transfer ──────
                List<float[]>
                    finalNormals;

                if (forceNewNormals)
                {
                    // FIX 3: Use OBJ
                    // normals as-is
                    finalNormals =
                        normals;
                }
                else if (
                    origNormals
                        .ContainsKey(bi) &&
                    origNormals[bi]
                        .Count > 0)
                {
                    // FIX 1: Nearest-
                    // neighbor transfer
                    // from source RDTB
                    finalNormals =
                        TransferNormals(
                            verts,
                            origNormals[bi]);
                    fixedNormals++;
                }
                else
                {
                    // No source normals
                    // available, keep OBJ
                    finalNormals =
                        normals;
                }

                // ── FIX 2: Apply UVs ──
                // Convert OBJ UVs back
                // to PS2 space (flip V)
                // UVs are already
                // flipped in ParseObj
                // so we use them directly

                // ── Write to output ───
                // Find batch in output
                // mesh chunk and write
                // normals + UVs
                bool written =
                    WriteBatchData(
                        outRdtb,
                        outMeshStart,
                        outMeshEnd,
                        bi,
                        outNPtrs,
                        verts,
                        finalNormals,
                        uvs,
                        tris);

                if (written)
                    fixedUVs++;
                else
                    skipped++;
            }

            // ── Save output ───────────
            File.WriteAllBytes(
                outputRdtbPath, outRdtb);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Post-fix complete!");
            Console.ResetColor();
            Console.WriteLine(
                "     Normals fixed : " +
                fixedNormals);
            Console.WriteLine(
                "     UVs fixed     : " +
                fixedUVs);
            Console.WriteLine(
                "     Skipped       : " +
                skipped);
            Console.WriteLine(
                "     Output        : " +
                outputRdtbPath);
        }

        // ─────────────────────────────
        // WRITE BATCH DATA
        // Directly patches normals
        // and UVs into output RDTB
        // bytes at the correct VIF
        // block positions for batch bi.
        // This is the KEY fix for Bug 2:
        // we ALWAYS write UVs, regardless
        // of whether tri count matched.
        // ─────────────────────────────
        private static bool WriteBatchData(
            byte[] data,
            int meshStart,
            int meshEnd,
            int batchIdx,
            int nPtrs,
            List<float[]> objVerts,
            List<float[]> finalNormals,
            List<float[]> objUvs,
            List<int[]> tris)
        {
            if (batchIdx >= nPtrs)
                return false;

            // Get batch pointer
            int poff =
                meshStart +
                batchIdx * 4;
            if (poff + 4 > data.Length)
                return false;

            uint bPtr =
                BitConverter.ToUInt32(
                    data, poff);
            if (bPtr == 0)
                return false;

            // Find next valid pointer
            uint nPtr =
                (uint)(meshEnd -
                       meshStart);
            for (int j = batchIdx + 1;
                 j < nPtrs; j++)
            {
                int np =
                    meshStart + j * 4;
                if (np + 4 >
                    data.Length)
                    break;
                uint c =
                    BitConverter
                        .ToUInt32(
                            data, np);
                if (c > bPtr &&
                    c < (uint)
                        (meshEnd -
                         meshStart))
                {
                    nPtr = c;
                    break;
                }
            }

            int batchAbsStart =
                meshStart + (int)bPtr;
            int batchAbsEnd =
                meshStart + (int)nPtr;

            if (batchAbsStart >= data.Length
                || batchAbsEnd >
                    data.Length)
                return false;

            // Scan VIF blocks in
            // this batch range
            var blocks =
                new List<(int absPos,
                    int vc)>();
            int pos = batchAbsStart;

            while (pos + 16 <=
                batchAbsEnd)
            {
                if (data[pos] == VIF_B0 &&
                    data[pos + 1] == VIF_B1 &&
                    data[pos + 3] == VIF_B3)
                {
                    int vc = data[pos + 4];
                    if (vc >= 1 &&
                        vc <= 96)
                    {
                        blocks.Add(
                            (pos, vc));
                        int bSize =
                            16 +
                            3 * vc * 16 +
                            16;
                        if (pos + bSize
                            + 16 <=
                            batchAbsEnd)
                        {
                            uint eof =
                                BitConverter
                                    .ToUInt32(
                                        data,
                                        pos +
                                        bSize);
                            if (eof ==
                                0x70000000)
                                bSize +=
                                    16;
                        }
                        pos += bSize;
                        continue;
                    }
                }
                pos += 4;
            }

            if (blocks.Count == 0)
                return false;

            // Determine if this is a
            // pure-tri batch (each
            // block has vc=3, one
            // triangle per block) or
            // a strip batch (longer
            // blocks)
            bool isPureTri =
                blocks.Count ==
                    tris.Count &&
                blocks.All(
                    b => b.vc == 3);

            if (isPureTri &&
                tris.Count > 0)
            {
                // ── PURE-TRI PATH ─────
                // Each block[i] = tri[i]
                // Write normals and UVs
                // using face vertex
                // indices from OBJ tris
                for (int ti = 0;
                     ti < blocks.Count
                     && ti < tris.Count;
                     ti++)
                {
                    int absPos =
                        blocks[ti].absPos;
                    int vStart =
                        absPos + 16;
                    int nStart =
                        vStart + 3 * 16;
                    int uStart =
                        nStart + 3 * 16;

                    int[] tri = tris[ti];

                    for (int vi = 0;
                         vi < 3; vi++)
                    {
                        int vIdx = tri[vi];

                        // Write normal
                        int nOff =
                            nStart +
                            vi * 16;
                        if (nOff + 16 <=
                            data.Length &&
                            vIdx <
                                finalNormals
                                    .Count)
                        {
                            float[] n =
                                finalNormals[
                                    vIdx];
                            WriteF(data,
                                nOff + 4,
                                n[0]);
                            WriteF(data,
                                nOff + 8,
                                n[1]);
                            WriteF(data,
                                nOff + 12,
                                n[2]);
                        }

                        // ── FIX 2:
                        // Write UV ────
                        int uOff =
                            uStart +
                            vi * 16;
                        if (uOff + 12 <=
                            data.Length &&
                            vIdx <
                                objUvs
                                    .Count)
                        {
                            float[] uv =
                                objUvs[vIdx];
                            WriteF(data,
                                uOff + 4,
                                uv[0]);
                            WriteF(data,
                                uOff + 8,
                                uv[1]);
                        }
                    }
                }
            }
            else
            {
                // ── STRIP PATH ────────
                // Sequential vertex
                // write across all
                // blocks
                int vertIdx = 0;
                foreach (var (absPos, vc)
                    in blocks)
                {
                    int vStart =
                        absPos + 16;
                    int nStart =
                        vStart + vc * 16;
                    int uStart =
                        nStart + vc * 16;

                    for (int i = 0;
                         i < vc; i++)
                    {
                        int vi =
                            vertIdx + i;

                        // Write normal
                        int nOff =
                            nStart +
                            i * 16;
                        if (nOff + 16 <=
                            data.Length &&
                            vi <
                                finalNormals
                                    .Count)
                        {
                            float[] n =
                                finalNormals[vi];
                            WriteF(data,
                                nOff + 4,
                                n[0]);
                            WriteF(data,
                                nOff + 8,
                                n[1]);
                            WriteF(data,
                                nOff + 12,
                                n[2]);
                        }

                        // ── FIX 2:
                        // Write UV ────
                        int uOff =
                            uStart +
                            i * 16;
                        if (uOff + 12 <=
                            data.Length &&
                            vi <
                                objUvs
                                    .Count)
                        {
                            float[] uv =
                                objUvs[vi];
                            WriteF(data,
                                uOff + 4,
                                uv[0]);
                            WriteF(data,
                                uOff + 8,
                                uv[1]);
                        }
                    }
                    vertIdx += vc;
                }
            }

            return true;
        }

        // ─────────────────────────────
        // TRANSFER NORMALS
        // Nearest-neighbor from source
        // RDTB samples to OBJ verts.
        // FIX 1: Applied to ALL batches
        // unconditionally.
        // ─────────────────────────────
        private static List<float[]>
            TransferNormals(
                List<float[]> objVerts,
                List<(float[] pos,
                      float[] norm)>
                    samples)
        {
            var result =
                new List<float[]>();

            foreach (float[] v in
                objVerts)
            {
                float bestD =
                    float.MaxValue;
                float[] bestN =
                    new float[]
                    { 0f, 1f, 0f };

                foreach (var s in
                    samples)
                {
                    float dx =
                        v[0] - s.pos[0];
                    float dy =
                        v[1] - s.pos[1];
                    float dz =
                        v[2] - s.pos[2];
                    float d =
                        dx * dx +
                        dy * dy +
                        dz * dz;
                    if (d < bestD)
                    {
                        bestD = d;
                        bestN = s.norm;
                    }
                }
                result.Add(bestN);
            }
            return result;
        }

        // ─────────────────────────────
        // READ BATCH NORMALS
        // Reads position+normal pairs
        // from source mesh chunk at
        // a given batch slot.
        // ─────────────────────────────
        private static
            List<(float[] pos,
                  float[] norm)>
            ReadBatchNormals(
                byte[] chunk,
                int batchIdx,
                int nPtrs)
        {
            var result =
                new List<(float[],
                    float[])>();

            if (batchIdx >= nPtrs ||
                chunk == null)
                return result;

            uint bPtr =
                BitConverter.ToUInt32(
                    chunk,
                    batchIdx * 4);
            if (bPtr == 0)
                return result;

            uint nPtr =
                (uint)chunk.Length;
            for (int j = batchIdx + 1;
                 j < nPtrs; j++)
            {
                if (j * 4 + 4 >
                    chunk.Length)
                    break;
                uint c =
                    BitConverter
                        .ToUInt32(
                            chunk,
                            j * 4);
                if (c > bPtr &&
                    c < (uint)
                        chunk.Length)
                {
                    nPtr = c;
                    break;
                }
            }

            int pos = (int)bPtr;
            int end = (int)nPtr;

            while (pos + 16 <= end)
            {
                if (chunk[pos] !=
                        VIF_B0 ||
                    chunk[pos + 1] !=
                        VIF_B1 ||
                    chunk[pos + 3] !=
                        VIF_B3)
                {
                    pos += 4;
                    continue;
                }

                int vc = chunk[pos + 4];
                if (vc < 1 || vc > 96)
                {
                    pos += 4;
                    continue;
                }

                int vStart = pos + 16;
                int nStart =
                    vStart + vc * 16;

                if (nStart + vc * 16
                    > end)
                {
                    pos += 4;
                    continue;
                }

                for (int i = 0;
                     i < vc; i++)
                {
                    int vOff =
                        vStart + i * 16;
                    int nOff =
                        nStart + i * 16;
                    if (nOff + 16 > end)
                        break;

                    float[] p =
                    {
                        BitConverter
                            .ToSingle(
                                chunk,
                                vOff + 4),
                        BitConverter
                            .ToSingle(
                                chunk,
                                vOff + 8),
                        BitConverter
                            .ToSingle(
                                chunk,
                                vOff + 12)
                    };
                    float[] n =
                    {
                        BitConverter
                            .ToSingle(
                                chunk,
                                nOff + 4),
                        BitConverter
                            .ToSingle(
                                chunk,
                                nOff + 8),
                        BitConverter
                            .ToSingle(
                                chunk,
                                nOff + 12)
                    };
                    result.Add((p, n));
                }

                int bSize =
                    16 + 3 * vc * 16 +
                    16;
                if (pos + bSize + 16
                    <= end)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                chunk,
                                pos + bSize);
                    if (eof == 0x70000000)
                        bSize += 16;
                }
                pos += bSize;
            }

            return result;
        }

        // ─────────────────────────────
        // OBJ PARSER
        // Parses verts, normals, UVs,
        // and tri face indices.
        // V-flip applied to UVs for PS2.
        // ─────────────────────────────
        private static void ParseObj(
            string path,
            List<float[]> verts,
            List<float[]> normals,
            List<float[]> uvs,
            List<int[]> tris)
        {
            var rawV =
                new List<float[]>();
            var rawN =
                new List<float[]>();
            var rawT =
                new List<float[]>();
            var comboMap =
                new Dictionary<
                    string, int>();

            var ci =
                System.Globalization
                    .CultureInfo
                    .InvariantCulture;

            foreach (string line in
                File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t)
                    || t[0] == '#')
                    continue;

                string[] p = t.Split(
                    new[]
                    { ' ', '\t' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
                if (p.Length == 0)
                    continue;

                string h =
                    p[0].ToLower();

                if (h == "v" &&
                    p.Length >= 4)
                {
                    rawV.Add(
                        new float[]
                        {
                            float.Parse(
                                p[1], ci),
                            float.Parse(
                                p[2], ci),
                            float.Parse(
                                p[3], ci)
                        });
                }
                else if (h == "vn" &&
                    p.Length >= 4)
                {
                    rawN.Add(
                        new float[]
                        {
                            float.Parse(
                                p[1], ci),
                            float.Parse(
                                p[2], ci),
                            float.Parse(
                                p[3], ci)
                        });
                }
                else if (h == "vt" &&
                    p.Length >= 3)
                {
                    rawT.Add(
                        new float[]
                        {
                            float.Parse(
                                p[1], ci),
                            // FIX 2:
                            // V-flip for PS2
                            1f - float.Parse(
                                p[2], ci)
                        });
                }
                else if (h == "f" &&
                    p.Length >= 4)
                {
                    int[] idx =
                        new int[3];
                    for (int fi = 0;
                         fi < 3; fi++)
                    {
                        string raw =
                            p[fi + 1]
                            + "//";
                        string[] parts =
                            raw.Split('/');

                        int vi = int.Parse(
                            parts[0]) - 1;
                        int ti =
                            (parts.Length > 1
                             && !string
                                .IsNullOrEmpty(
                                    parts[1]))
                            ? int.Parse(
                                parts[1]) - 1
                            : vi;
                        int ni =
                            (parts.Length > 2
                             && !string
                                .IsNullOrEmpty(
                                    parts[2]))
                            ? int.Parse(
                                parts[2]) - 1
                            : vi;

                        string key =
                            vi + "/" +
                            ti + "/" + ni;
                        int newIdx;
                        if (!comboMap
                                .TryGetValue(
                                    key,
                                    out newIdx))
                        {
                            newIdx =
                                verts.Count;
                            comboMap[key] =
                                newIdx;
                            verts.Add(
                                vi >= 0 &&
                                vi < rawV.Count
                                ? rawV[vi]
                                : new float[]
                                  { 0, 0, 0 });
                            uvs.Add(
                                ti >= 0 &&
                                ti < rawT.Count
                                ? rawT[ti]
                                : new float[]
                                  { 0, 0 });
                            normals.Add(
                                ni >= 0 &&
                                ni < rawN.Count
                                ? rawN[ni]
                                : new float[]
                                  { 0, 1, 0 });
                        }
                        idx[fi] = newIdx;
                    }
                    tris.Add(idx);
                }
            }

            while (normals.Count <
                verts.Count)
                normals.Add(
                    new float[]
                    { 0, 1, 0 });
            while (uvs.Count <
                verts.Count)
                uvs.Add(
                    new float[]
                    { 0, 0 });
        }

        // ─────────────────────────────
        // CHUNK LAYOUT HELPERS
        // ─────────────────────────────
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
                if (v == 0 ||
                    v == 0xFFFFFFFF ||
                    v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            offs.Sort();
            return offs
                .Distinct()
                .ToList();
        }

        private static int
            FindMeshChunkIdx(
                byte[] data,
                List<int> offs)
        {
            // Raw slot 11 is always
            // the primary mesh chunk
            if (data.Length <
                0x10 + 12 * 4)
                return -1;

            uint meshRaw =
                BitConverter.ToUInt32(
                    data,
                    0x10 + 11 * 4);

            if (meshRaw == 0 ||
                meshRaw == 0xFFFFFFFF)
                return -1;

            int idx =
                offs.IndexOf(
                    (int)meshRaw);
            if (idx >= 0)
                return idx;

            // Fallback: chunk with
            // most VIF blocks
            int bestIdx = -1;
            int bestVif = 0;
            for (int ci = 0;
                 ci < offs.Count; ci++)
            {
                int s = offs[ci];
                int e =
                    (ci + 1 <
                        offs.Count)
                    ? offs[ci + 1]
                    : data.Length;
                int vifCount = 0;
                for (int i = s;
                     i + 16 <= e;
                     i += 4)
                {
                    if (data[i] ==
                            VIF_B0 &&
                        data[i + 1] ==
                            VIF_B1 &&
                        data[i + 3] ==
                            VIF_B3)
                        vifCount++;
                }
                if (vifCount > bestVif)
                {
                    bestVif = vifCount;
                    bestIdx = ci;
                }
            }
            return bestIdx;
        }

        // ─────────────────────────────
        // WRITE FLOAT
        // ─────────────────────────────
        private static void WriteF(
            byte[] data, int off,
            float v)
        {
            byte[] b =
                BitConverter.GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }
    }
}
