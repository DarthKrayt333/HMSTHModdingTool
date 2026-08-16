using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.SRDB
{
    /// <summary>
    /// SRDB-only UV patcher.
    /// Fixes UV changes not being applied
    /// when vertex count is unchanged but
    /// auto-scale is active (large buildings
    /// and map objects in SRDB files).
    ///
    /// Called ONLY from SRDBBatchExtractor
    /// .RebuildSRDB() after the normal
    /// cbatches rebuild. Does NOT touch
    /// standalone RDTB files.
    ///
    /// Root cause of the bug:
    ///   PatchKeptBatchUvs() in
    ///   RDTBBatchReplacer calls
    ///   VerifyVertexOrder() which compares
    ///   OBJ verts (display-space, scaled)
    ///   against RDTB verts (game-space).
    ///   When autoScaleInvert != 1.0 the
    ///   positions never match so the UV
    ///   patch is silently skipped.
    ///
    /// This class fixes that by using a
    /// scale-aware comparison and applying
    /// UV patches directly to the RDTB blob
    /// bytes before SRDB assembly.
    /// </summary>
    public static class SRDBUVPatcher
    {
        // ─── VIF constants ────────────────
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private const uint EOF_FLAG =
            0x70000000;
        private const float UV_EPS = 0.00001f;

        // ═════════════════════════════════════
        // PUBLIC ENTRY POINT
        // Called from SRDBBatchExtractor
        // .RebuildSRDB() per embedded blob.
        //
        // Parameters:
        //   rdtbBlob   - the RDTB blob bytes
        //                to patch (modified
        //                in-place)
        //   subPath    - path to the
        //                embedded_NN folder
        //                containing model_XX/
        //                batch_XXXX.obj files
        //   autoScale  - the scale that was
        //                applied at extraction
        //                time (from _info.txt)
        //
        // Returns number of UV pairs changed.
        // Returns 0 if autoScale == 1.0 (not
        // the bug condition - C# tool already
        // handles that path correctly).
        // ═════════════════════════════════════
        public static int PatchUVs(
            byte[] rdtbBlob,
            string subPath,
            float autoScale)
        {
            // Only fix the SRDB auto-scale bug.
            // Normal-scale blobs are handled
            // correctly by PatchKeptBatchUvs
            // in RDTBBatchReplacer already.
            if (autoScale == 1.0f)
                return 0;

            if (rdtbBlob == null ||
                rdtbBlob.Length < 0x48)
                return 0;

            if (!Directory.Exists(subPath))
                return 0;

            float autoScaleInv =
                autoScale > 0f
                ? 1.0f / autoScale
                : 1.0f;

            // Read material table
            var batchTex =
                GetBatchTexMap(rdtbBlob);
            if (batchTex.Count == 0)
                return 0;

            // Get mesh chunk bounds
            int meshStart, meshEnd;
            if (!GetMeshChunkBounds(
                    rdtbBlob,
                    out meshStart,
                    out meshEnd))
                return 0;

            // Get batch ranges
            var batchRanges =
                GetBatchRanges(
                    rdtbBlob,
                    meshStart,
                    meshEnd);
            if (batchRanges.Count == 0)
                return 0;

            int totalChanged = 0;

            // Scan model_XX subdirs
            string[] modelDirs =
                Directory
                    .GetDirectories(
                        subPath, "model_*")
                    .OrderBy(d => d)
                    .ToArray();

            foreach (string modelDir in
                modelDirs)
            {
                string[] objFiles =
                    Directory
                        .GetFiles(
                            modelDir,
                            "batch_*.obj")
                        .OrderBy(f => f)
                        .ToArray();

                foreach (string objPath in
                    objFiles)
                {
                    string fn =
                        Path
                        .GetFileNameWithoutExtension(
                            objPath)
                        .ToLower();

                    if (!fn.StartsWith(
                            "batch_"))
                        continue;

                    int bi;
                    if (!int.TryParse(
                            fn.Substring(6),
                            out bi))
                        continue;

                    if (!batchRanges
                            .ContainsKey(bi))
                        continue;

                    int absStart =
                        batchRanges[bi].Item1;
                    int absEnd =
                        batchRanges[bi].Item2;

                    // Count physical verts
                    // in RDTB blob
                    int physVc =
                        CountPhysicalVerts(
                            rdtbBlob,
                            absStart,
                            absEnd);
                    if (physVc == 0)
                        continue;

                    // Parse OBJ
                    List<float[]> comboVerts;
                    List<float[]> comboUvs;
                    ParseObjVertsUvs(
                        objPath,
                        out comboVerts,
                        out comboUvs);

                    if (comboUvs.Count == 0)
                        continue;

                    // Only apply when vertex
                    // count is unchanged.
                    // If vert count changed,
                    // cbatches already wrote
                    // correct UVs into the
                    // new VIF data.
                    if (comboUvs.Count !=
                        physVc)
                        continue;

                    // Verify vertex order
                    // using scale-aware check.
                    // This is the fix for the
                    // bug: original code used
                    // exact position match
                    // which failed because OBJ
                    // is in display-space but
                    // RDTB is in game-space.
                    if (!VerifyVertexOrderScaled(
                            rdtbBlob,
                            absStart, absEnd,
                            comboVerts,
                            autoScale,
                            checkCount: 5))
                    {
                        Console.ForegroundColor
                            = ConsoleColor
                                .DarkGray;
                        Console.WriteLine(
                            "      [UV skip]"
                            + " batch_" +
                            bi.ToString("D4")
                            + " order mismatch");
                        Console.ResetColor();
                        continue;
                    }

                    // Patch UV rows
                    int nChanged =
                        PatchUvsDirect(
                            rdtbBlob,
                            absStart, absEnd,
                            comboUvs);

                    if (nChanged > 0)
                    {
                        Console
                            .ForegroundColor =
                            ConsoleColor.Green;
                        Console.WriteLine(
                            "      [UV patch]"
                            + " batch_" +
                            bi.ToString("D4")
                            + " " + nChanged
                            + " UV pairs");
                        Console.ResetColor();
                        totalChanged +=
                            nChanged;
                    }
                }
            }

            return totalChanged;
        }

        // ═════════════════════════════════════
        // READ AUTO SCALE FROM _info.txt
        // ═════════════════════════════════════
        public static float ReadAutoScale(
            string subPath)
        {
            string infoPath =
                Path.Combine(
                    subPath, "_info.txt");
            if (!File.Exists(infoPath))
                return 1.0f;

            foreach (string line in
                File.ReadAllLines(infoPath))
            {
                string t = line.Trim();
                if (!t.StartsWith(
                        "Auto Scale:"))
                    continue;
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
                    return sc;
            }
            return 1.0f;
        }

        // ─────────────────────────────────────
        // GET BATCH TEX MAP
        // Reads chunk 8 material table.
        // Returns {batch_idx -> tex_id}
        // ─────────────────────────────────────
        private static
            Dictionary<int, int>
            GetBatchTexMap(byte[] rdtb)
        {
            var result =
                new Dictionary<int, int>();
            if (rdtb.Length < 0x48)
                return result;
            if (rdtb[0] != 'R' ||
                rdtb[1] != 'D' ||
                rdtb[2] != 'T' ||
                rdtb[3] != 'B')
                return result;

            uint[] rawSlots = new uint[14];
            for (int i = 0; i < 14; i++)
                rawSlots[i] =
                    BitConverter.ToUInt32(
                        rdtb, 0x10 + i * 4);

            uint c8Off = rawSlots[8];
            if (c8Off == 0 ||
                c8Off == 0xFFFFFFFF ||
                c8Off >= (uint)rdtb.Length)
                return result;

            uint c8End = (uint)rdtb.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c8Off &&
                    v < c8End &&
                    v != 0xFFFFFFFF &&
                    v != 0)
                    c8End = v;
            }

            int c8Len = (int)(c8End - c8Off);
            if (c8Len < 4)
                return result;

            // Skip VIF-tagged chunks
            if (rdtb[c8Off] == VIF_B0 &&
                rdtb[c8Off + 1] == VIF_B1 &&
                c8Len > 3 &&
                rdtb[c8Off + 3] == VIF_B3)
                return result;

            uint matFirst =
                BitConverter.ToUInt32(
                    rdtb, (int)c8Off);
            if (matFirst == 0 ||
                matFirst > (uint)c8Len)
                return result;

            int bc = (int)(matFirst / 4);
            for (int i = 0; i < bc; i++)
            {
                int ptrOff =
                    (int)c8Off + i * 4;
                if (ptrOff + 4 > rdtb.Length)
                    break;
                uint ptr =
                    BitConverter.ToUInt32(
                        rdtb, ptrOff);
                int recOff =
                    (int)c8Off + (int)ptr;
                if (recOff + 8 > rdtb.Length)
                    continue;
                int texId =
                    BitConverter.ToUInt16(
                        rdtb, recOff + 6);
                result[i] = texId;
            }

            return result;
        }

        // ─────────────────────────────────────
        // GET MESH CHUNK BOUNDS
        // Uses raw slot 11 (LOD0).
        // Falls back to last active slot
        // for small RDTBs.
        // ─────────────────────────────────────
        private static bool
            GetMeshChunkBounds(
                byte[] rdtb,
                out int meshStart,
                out int meshEnd)
        {
            meshStart = 0;
            meshEnd = 0;

            if (rdtb.Length < 0x48)
                return false;

            uint[] rawSlots = new uint[14];
            for (int i = 0; i < 14; i++)
                rawSlots[i] =
                    BitConverter.ToUInt32(
                        rdtb, 0x10 + i * 4);

            // Slot 11 = LOD0 mesh
            uint c11 = rawSlots[11];
            if (c11 == 0 ||
                c11 == 0xFFFFFFFF)
            {
                // Small RDTB: use last
                // active slot
                var active =
                    rawSlots
                        .Where(v =>
                            v != 0 &&
                            v != 0xFFFFFFFF &&
                            v >= 0x48 &&
                            v < (uint)
                                rdtb.Length)
                        .OrderBy(v => v)
                        .ToList();
                if (active.Count == 0)
                    return false;
                c11 = active.Last();
            }

            if (c11 >= (uint)rdtb.Length)
                return false;

            uint c11End = (uint)rdtb.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c11 &&
                    v < c11End &&
                    v != 0xFFFFFFFF &&
                    v != 0)
                    c11End = v;
            }

            meshStart = (int)c11;
            meshEnd = (int)c11End;
            return meshEnd > meshStart;
        }

        // ─────────────────────────────────────
        // GET BATCH RANGES
        // Returns batch_idx ->
        //   (abs_start, abs_end)
        // using the pointer table at the
        // start of the mesh chunk.
        // ─────────────────────────────────────
        private static
            Dictionary<int,
                Tuple<int, int>>
            GetBatchRanges(
                byte[] rdtb,
                int meshStart,
                int meshEnd)
        {
            var result =
                new Dictionary<int,
                    Tuple<int, int>>();

            if (meshEnd <= meshStart)
                return result;

            uint firstPtr =
                BitConverter.ToUInt32(
                    rdtb, meshStart);
            if (firstPtr == 0 ||
                firstPtr >
                    (uint)(meshEnd -
                           meshStart) ||
                firstPtr < 4)
                return result;

            int nPtrs =
                (int)(firstPtr / 4);

            // Read all pointers
            var ptrs =
                new List<Tuple<int, uint>>();
            for (int i = 0; i < nPtrs; i++)
            {
                int pOff =
                    meshStart + i * 4;
                if (pOff + 4 > rdtb.Length)
                    break;
                uint ptr =
                    BitConverter.ToUInt32(
                        rdtb, pOff);
                if (ptr != 0 &&
                    ptr < (uint)(meshEnd -
                                 meshStart))
                    ptrs.Add(
                        Tuple.Create(i, ptr));
            }

            // Sort by pointer value
            var sorted =
                ptrs.OrderBy(t => t.Item2)
                    .ToList();

            for (int k = 0;
                 k < sorted.Count; k++)
            {
                int batchIdx =
                    sorted[k].Item1;
                int absStart =
                    meshStart +
                    (int)sorted[k].Item2;
                int absEnd =
                    (k + 1 < sorted.Count)
                    ? meshStart +
                      (int)sorted[k + 1]
                          .Item2
                    : meshEnd;

                result[batchIdx] =
                    Tuple.Create(
                        absStart, absEnd);
            }

            return result;
        }

        // ─────────────────────────────────────
        // COUNT PHYSICAL VERTS IN VIF BLOCKS
        // ─────────────────────────────────────
        private static int
            CountPhysicalVerts(
                byte[] data,
                int start, int end)
        {
            int total = 0;
            int pos = start;
            while (pos + 16 <= end)
            {
                if (data[pos] == VIF_B0 &&
                    data[pos + 1] == VIF_B1 &&
                    data[pos + 3] == VIF_B3)
                {
                    int vc = data[pos + 4];
                    if (vc >= 1 && vc <= 96)
                    {
                        int uStart =
                            pos + 16 +
                            vc * 16 +
                            vc * 16;
                        if (uStart +
                            vc * 16 <= end)
                            total += vc;
                        int bSize =
                            16 +
                            3 * vc * 16 +
                            16;
                        if (pos + bSize +
                            16 <= end &&
                            BitConverter
                                .ToUInt32(
                                    data,
                                    pos +
                                    bSize)
                            == EOF_FLAG)
                            bSize += 16;
                        pos += bSize;
                        continue;
                    }
                }
                pos += 4;
            }
            return total;
        }

        // ─────────────────────────────────────
        // PARSE OBJ VERTS AND UVS
        // Resolves to per-unique-combo lists.
        // ─────────────────────────────────────
        private static void ParseObjVertsUvs(
            string objPath,
            out List<float[]> comboVerts,
            out List<float[]> comboUvs)
        {
            comboVerts = new List<float[]>();
            comboUvs = new List<float[]>();

            var rawV = new List<float[]>();
            var rawVT = new List<float[]>();
            var comboMap =
                new Dictionary<
                    long, int>();

            var ci =
                System.Globalization
                    .CultureInfo
                    .InvariantCulture;

            foreach (string line in
                File.ReadAllLines(
                    objPath,
                    Encoding.UTF8))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(t)
                    || t[0] == '#')
                    continue;

                string[] parts = t.Split(
                    new char[]
                    { ' ', '\t' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                string h =
                    parts[0].ToLower();

                if (h == "v" &&
                    parts.Length >= 4)
                {
                    float x, y, z;
                    if (float.TryParse(
                            parts[1],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out x) &&
                        float.TryParse(
                            parts[2],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out y) &&
                        float.TryParse(
                            parts[3],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out z))
                    {
                        rawV.Add(
                            new float[]
                            { x, y, z });
                    }
                }
                else if (h == "vt" &&
                    parts.Length >= 3)
                {
                    float u, v;
                    if (float.TryParse(
                            parts[1],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out u) &&
                        float.TryParse(
                            parts[2],
                            System.Globalization
                                .NumberStyles
                                .Float,
                            ci, out v))
                    {
                        rawVT.Add(
                            new float[]
                            { u, v });
                    }
                }
                else if (h == "f" &&
                    parts.Length >= 4)
                {
                    for (int fi = 1;
                         fi <= 3; fi++)
                    {
                        string raw =
                            parts[fi] + "//";
                        string[] sp =
                            raw.Split('/');
                        int vi =
                            int.Parse(
                                sp[0]) - 1;
                        int ti =
                            (sp.Length > 1 &&
                             !string
                                 .IsNullOrEmpty(
                                     sp[1]))
                            ? int.Parse(
                                sp[1]) - 1
                            : vi;

                        // Pack vi, ti into
                        // one long key
                        long key =
                            ((long)vi << 32)
                            | (uint)ti;

                        if (!comboMap
                                .ContainsKey(
                                    key))
                        {
                            comboMap[key] =
                                comboVerts
                                    .Count;

                            float[] v3 =
                                (vi >= 0 &&
                                 vi <
                                 rawV.Count)
                                ? rawV[vi]
                                : new float[]
                                  { 0, 0, 0 };
                            comboVerts.Add(
                                v3);

                            float[] uv =
                                (ti >= 0 &&
                                 ti <
                                 rawVT.Count)
                                ? rawVT[ti]
                                : new float[]
                                  { 0, 0 };
                            comboUvs.Add(uv);
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────
        // VERIFY VERTEX ORDER - SCALE AWARE
        //
        // The bug fix is here.
        // Original VerifyVertexOrder in
        // RDTBBatchReplacer compares OBJ verts
        // (display-space) directly against VIF
        // verts (game-space). This fails when
        // autoScale != 1.0 because:
        //   OBJ_v  = game_v * autoScale
        //   game_v = OBJ_v  / autoScale
        //
        // We compare:
        //   game_v * autoScale ≈ OBJ_v
        //
        // This correctly identifies whether
        // vertices are in the expected order.
        // ─────────────────────────────────────
        private static bool
            VerifyVertexOrderScaled(
                byte[] data,
                int start, int end,
                List<float[]> objVerts,
                float autoScale,
                int checkCount = 5)
        {
            if (autoScale <= 0f)
                autoScale = 1.0f;

            // Allow 5% of display-range
            // as position tolerance
            float posEps = 0.05f * autoScale;

            int matched = 0;
            int checked_ = 0;
            int vertIdx = 0;
            int pos = start;

            while (pos + 16 <= end &&
                   checked_ < checkCount)
            {
                if (data[pos] == VIF_B0 &&
                    data[pos + 1] == VIF_B1 &&
                    data[pos + 3] == VIF_B3)
                {
                    int vc = data[pos + 4];
                    if (vc >= 1 && vc <= 96)
                    {
                        int vStart =
                            pos + 16;
                        int uStart =
                            vStart +
                            vc * 16 +
                            vc * 16;
                        if (uStart +
                            vc * 16 <= end)
                        {
                            for (int i = 0;
                                 i < vc &&
                                 checked_ <
                                 checkCount;
                                 i++)
                            {
                                if (vertIdx
                                    + i >=
                                    objVerts
                                        .Count)
                                    break;

                                int vro =
                                    vStart +
                                    i * 16;

                                // Game-space
                                // vertex
                                float gx =
                                    BitConverter
                                        .ToSingle(
                                            data,
                                            vro + 4);
                                float gy =
                                    BitConverter
                                        .ToSingle(
                                            data,
                                            vro + 8);
                                float gz =
                                    BitConverter
                                        .ToSingle(
                                            data,
                                            vro + 12);

                                // Scale to
                                // display-space
                                // for comparison
                                float sx =
                                    gx * autoScale;
                                float sy =
                                    gy * autoScale;
                                float sz =
                                    gz * autoScale;

                                float[] ov =
                                    objVerts[
                                        vertIdx
                                        + i];

                                if (
                                    Math.Abs(
                                        sx - ov[0])
                                    < posEps &&
                                    Math.Abs(
                                        sy - ov[1])
                                    < posEps &&
                                    Math.Abs(
                                        sz - ov[2])
                                    < posEps)
                                    matched++;

                                checked_++;
                            }
                        }

                        vertIdx += vc;

                        int bSize =
                            16 +
                            3 * vc * 16 +
                            16;
                        if (pos + bSize +
                            16 <= end &&
                            BitConverter
                                .ToUInt32(
                                    data,
                                    pos + bSize)
                            == EOF_FLAG)
                            bSize += 16;
                        pos += bSize;
                        continue;
                    }
                }
                pos += 4;
            }

            if (checked_ == 0)
                return false;

            float ratio =
                (float)matched / checked_;
            return ratio >= 0.8f;
        }

        // ─────────────────────────────────────
        // PATCH UVS DIRECT
        // Writes combo_uvs[i] into VIF UV
        // row[i] in physical vertex order.
        //
        // OBJ UV convention:
        //   Extractor writes: vt U (1-V)
        //   So OBJ has V already flipped.
        //   PS2 stores raw V.
        //   Therefore: ps2_V = 1 - obj_V
        //
        // UV values are NOT affected by
        // autoScale (scale is positional
        // only, UV coordinates are in
        // texture-space 0..1).
        // ─────────────────────────────────────
        private static int PatchUvsDirect(
            byte[] data,
            int start, int end,
            List<float[]> objUvs)
        {
            int changed = 0;
            int vertIdx = 0;
            int pos = start;

            while (pos + 16 <= end)
            {
                if (data[pos] == VIF_B0 &&
                    data[pos + 1] == VIF_B1 &&
                    data[pos + 3] == VIF_B3)
                {
                    int vc = data[pos + 4];
                    if (vc >= 1 && vc <= 96)
                    {
                        int vStart =
                            pos + 16;
                        int nStart =
                            vStart + vc * 16;
                        int uStart =
                            nStart + vc * 16;

                        if (uStart +
                            vc * 16 <= end)
                        {
                            for (int i = 0;
                                 i < vc; i++)
                            {
                                if (vertIdx
                                    + i >=
                                    objUvs
                                        .Count)
                                    break;

                                int uro =
                                    uStart +
                                    i * 16;
                                if (uro + 12
                                    > data
                                        .Length)
                                    break;

                                // Current
                                // stored UV
                                float curU =
                                    BitConverter
                                        .ToSingle(
                                            data,
                                            uro + 4);
                                float curV =
                                    BitConverter
                                        .ToSingle(
                                            data,
                                            uro + 8);

                                // New UV from
                                // OBJ.
                                // OBJ V is
                                // already 1-V
                                // (flipped at
                                // extraction).
                                // PS2 stores
                                // raw V so
                                // flip back:
                                // ps2_V = 1 - obj_V
                                float newU =
                                    objUvs[
                                        vertIdx
                                        + i][0];
                                float newV =
                                    1.0f -
                                    objUvs[
                                        vertIdx
                                        + i][1];

                                if (
                                    Math.Abs(
                                        curU -
                                        newU)
                                    >= UV_EPS
                                    ||
                                    Math.Abs(
                                        curV -
                                        newV)
                                    >= UV_EPS)
                                {
                                    byte[] bu =
                                        BitConverter
                                            .GetBytes(
                                                newU);
                                    byte[] bv =
                                        BitConverter
                                            .GetBytes(
                                                newV);
                                    data[uro + 4]
                                        = bu[0];
                                    data[uro + 5]
                                        = bu[1];
                                    data[uro + 6]
                                        = bu[2];
                                    data[uro + 7]
                                        = bu[3];
                                    data[uro + 8]
                                        = bv[0];
                                    data[uro + 9]
                                        = bv[1];
                                    data[uro + 10]
                                        = bv[2];
                                    data[uro + 11]
                                        = bv[3];
                                    changed++;
                                }
                            }
                        }

                        vertIdx += vc;

                        int bSize =
                            16 +
                            3 * vc * 16 +
                            16;
                        if (pos + bSize +
                            16 <= end &&
                            BitConverter
                                .ToUInt32(
                                    data,
                                    pos + bSize)
                            == EOF_FLAG)
                            bSize += 16;
                        pos += bSize;
                        continue;
                    }
                }
                pos += 4;
            }

            return changed;
        }
    }
}
