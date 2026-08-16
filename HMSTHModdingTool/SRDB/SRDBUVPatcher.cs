using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.SRDB
{
    public static class SRDBUVPatcher
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private const uint EOF_FLAG =
            0x70000000;
        private const float UV_EPS = 0.00001f;

        public static int PatchUVs(
            byte[] rdtbBlob,
            string subPath,
            float autoScale)
        {
            // ── FIX v2: Remove the early
            // return on scale=1.0.
            // UV-only edits must be applied
            // even when no auto-scale was
            // used during extraction.
            // The scale is only used for
            // vertex ORDER verification,
            // not for UV patching itself.
            //
            // OLD (broken):
            //   if (autoScale == 1.0f)
            //       return 0;
            //
            // NEW: always proceed, use
            // scale=1.0 as neutral value

            if (rdtbBlob == null ||
                rdtbBlob.Length < 0x48)
                return 0;

            if (!Directory.Exists(subPath))
                return 0;

            var batchTex =
                GetBatchTexMap(rdtbBlob);
            if (batchTex.Count == 0)
                return 0;

            int meshStart, meshEnd;
            if (!GetMeshChunkBounds(
                    rdtbBlob,
                    out meshStart,
                    out meshEnd))
                return 0;

            var batchRanges =
                GetBatchRanges(
                    rdtbBlob,
                    meshStart,
                    meshEnd);
            if (batchRanges.Count == 0)
                return 0;

            int totalChanged = 0;

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

                    int physVc =
                        CountPhysicalVerts(
                            rdtbBlob,
                            absStart,
                            absEnd);

                    List<float[]> comboVerts;
                    List<float[]> comboUvs;
                    ParseObjVertsUvs(
                        objPath,
                        out comboVerts,
                        out comboUvs);

                    if (comboUvs.Count == 0)
                        continue;

                    // Allow up to 2 vertex difference
                    // caused by Blender seam welding.
                    if (Math.Abs(
                            comboUvs.Count -
                            physVc) > 2)
                        continue;

                    // FIX v3: No vertex order check
                    // needed. Position-based UV matching
                    // works regardless of vertex order,
                    // so we can accept any batch where
                    // vertex count is close enough.
                    // This fixes the scrambled UV bug
                    // on the car and waterwell where
                    // Blender reordered vertices during
                    // UV editing.

                    // ── FIX v3: Use position-based
                    // matching instead of index-based.
                    // Direct index mapping fails when
                    // Blender reorders vertices during
                    // UV editing (causes scrambled UVs
                    // like on car and waterwell).
                    float scaleInvert =
                        autoScale > 0f
                        ? 1.0f / autoScale
                        : 1.0f;

                    int nChanged =
                        PatchUvsByPosition(
                            rdtbBlob,
                            absStart, absEnd,
                            comboVerts,
                            comboUvs,
                            scaleInvert);

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

            uint c11 = rawSlots[11];
            if (c11 == 0 ||
                c11 == 0xFFFFFFFF)
            {
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
                new Dictionary<long, int>();

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

            // ── FIX v2: Scale-aware epsilon
            // When autoScale is large (e.g.
            // 0.05 for huge map geometry),
            // scaleInvert is large too and
            // floating point error compounds.
            // Use a wider epsilon that scales
            // with the invert factor so
            // valid batches are not rejected.
            float scaleInvert =
                1.0f / autoScale;
            float posEps = Math.Max(
                0.05f,
                0.1f * scaleInvert);  // ← was: 0.05f * autoScale

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

                                // ── FIX v2:
                                // Convert OBJ
                                // display-space
                                // back to game-
                                // space using
                                // scaleInvert
                                float[] ov =
                                    objVerts[
                                        vertIdx
                                        + i];
                                float sx =
                                    ov[0] *
                                    scaleInvert;
                                float sy =
                                    ov[1] *
                                    scaleInvert;
                                float sz =
                                    ov[2] *
                                    scaleInvert;

                                if (
                                    Math.Abs(
                                        gx - sx)
                                    <= posEps &&
                                    Math.Abs(
                                        gy - sy)
                                    <= posEps &&
                                    Math.Abs(
                                        gz - sz)
                                    <= posEps)
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

            if (checked_ == 0)
                return false;

            float ratio =
                (float)matched / checked_;
            return ratio >= 0.6f;
        }

        // ═════════════════════════════════════
        // PATCH UVS BY NEAREST VERTEX POSITION
        // ═════════════════════════════════════
        // For each VIF vertex in the RDTB batch,
        // find the OBJ vertex whose position
        // matches (accounting for auto-scale)
        // and copy that OBJ vertex's UV into
        // the VIF UV slot.
        //
        // This handles the case where Blender
        // reorders vertices during UV editing.
        // Direct index mapping breaks when the
        // OBJ vertex order does not match the
        // original RDTB extraction order, which
        // causes scrambled UVs like the car
        // texture you saw.
        // ═════════════════════════════════════
        private static int PatchUvsDirect(
            byte[] data,
            int start, int end,
            List<float[]> objUvs)
        {
            // This overload kept for backward
            // compat but should not be called
            // for SRDB embedded batches.
            // It does index-based mapping which
            // corrupts UVs when Blender reorders
            // vertices during editing.
            return PatchUvsByPosition(
                data, start, end,
                null, objUvs, 1.0f);
        }

        // ═════════════════════════════════════
        // NEW: POSITION-BASED UV PATCH
        // Matches each VIF vertex to the
        // nearest OBJ vertex by position and
        // copies that OBJ vertex's UV. Safe
        // against Blender vertex reordering.
        // ═════════════════════════════════════
        // ═════════════════════════════════════
        // PATCH UVS BY POSITION + UV HINT v4
        // ═════════════════════════════════════
        // For each VIF vertex, find OBJ vertex
        // matching BOTH position AND closest
        // original UV. This handles UV seams
        // where multiple OBJ vertices share
        // the same 3D position but have
        // different UVs.
        //
        // Score = position_distance * 1000
        //       + uv_distance
        //
        // Position dominates (1000x weight)
        // so only vertices at the same spot
        // compete, and among those the one
        // with UV closest to original wins.
        // This picks the correct seam side.
        // ═════════════════════════════════════
        private static int PatchUvsByPosition(
            byte[] data,
            int start, int end,
            List<float[]> objVerts,
            List<float[]> objUvs,
            float scaleInvert)
        {
            if (objVerts == null ||
                objUvs == null ||
                objVerts.Count == 0 ||
                objUvs.Count == 0)
                return 0;

            int nObj = Math.Min(
                objVerts.Count,
                objUvs.Count);

            // Pre-scale OBJ positions once
            // for speed
            float[] osx = new float[nObj];
            float[] osy = new float[nObj];
            float[] osz = new float[nObj];
            for (int j = 0; j < nObj; j++)
            {
                osx[j] = objVerts[j][0]
                    * scaleInvert;
                osy[j] = objVerts[j][1]
                    * scaleInvert;
                osz[j] = objVerts[j][2]
                    * scaleInvert;
            }

            // Pre-flip OBJ UVs to PS2 space
            // (OBJ V is flipped, PS2 is not)
            float[] ouU = new float[nObj];
            float[] ouV = new float[nObj];
            for (int j = 0; j < nObj; j++)
            {
                ouU[j] = objUvs[j][0];
                ouV[j] = 1.0f - objUvs[j][1];
            }

            int changed = 0;
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
                int nStart = vStart + vc * 16;
                int uStart = nStart + vc * 16;

                if (uStart + vc * 16 > end)
                {
                    pos += 4;
                    continue;
                }

                for (int i = 0; i < vc; i++)
                {
                    int vro = vStart + i * 16;
                    int uro = uStart + i * 16;
                    if (uro + 12 > data.Length)
                        break;

                    // Read VIF position
                    float gx = BitConverter
                        .ToSingle(data, vro + 4);
                    float gy = BitConverter
                        .ToSingle(data, vro + 8);
                    float gz = BitConverter
                        .ToSingle(data, vro + 12);

                    // Read original VIF UV
                    // (used as tiebreaker
                    //  hint for seam verts)
                    float origU = BitConverter
                        .ToSingle(data, uro + 4);
                    float origV = BitConverter
                        .ToSingle(data, uro + 8);

                    // Find OBJ vertex with
                    // best combined score
                    float bestScore =
                        float.MaxValue;
                    int bestIdx = -1;

                    for (int j = 0; j < nObj; j++)
                    {
                        float dx = gx - osx[j];
                        float dy = gy - osy[j];
                        float dz = gz - osz[j];
                        float posD = dx * dx
                            + dy * dy
                            + dz * dz;

                        float du = origU
                            - ouU[j];
                        float dv = origV
                            - ouV[j];
                        float uvD = du * du
                            + dv * dv;

                        // Position dominates.
                        // UV breaks ties among
                        // co-located verts.
                        float score =
                            posD * 1000.0f
                            + uvD;

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestIdx = j;
                        }
                    }

                    if (bestIdx < 0) continue;

                    float newU = ouU[bestIdx];
                    float newV = ouV[bestIdx];

                    if (Math.Abs(origU - newU)
                            >= UV_EPS ||
                        Math.Abs(origV - newV)
                            >= UV_EPS)
                    {
                        byte[] bu = BitConverter
                            .GetBytes(newU);
                        byte[] bv = BitConverter
                            .GetBytes(newV);
                        data[uro + 4] = bu[0];
                        data[uro + 5] = bu[1];
                        data[uro + 6] = bu[2];
                        data[uro + 7] = bu[3];
                        data[uro + 8] = bv[0];
                        data[uro + 9] = bv[1];
                        data[uro + 10] = bv[2];
                        data[uro + 11] = bv[3];
                        changed++;
                    }
                }

                int bSize = 16 + 3 * vc * 16 + 16;
                if (pos + bSize + 16 <= end &&
                    BitConverter.ToUInt32(
                        data, pos + bSize)
                    == EOF_FLAG)
                    bSize += 16;
                pos += bSize;
            }

            return changed;
        }
    }
}
