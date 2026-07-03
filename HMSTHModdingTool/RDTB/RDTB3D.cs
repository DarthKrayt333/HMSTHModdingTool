using HMSTHModdingTool.GDTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace HMSTHModdingTool.RDTB
{
    internal struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z)
        { X = x; Y = y; Z = z; }
        public static Vec3 Zero =>
            new Vec3(0, 0, 0);
    }

    internal struct Vec2
    {
        public float U, V;
        public Vec2(float u, float v)
        { U = u; V = v; }
    }

    internal struct Tri
    {
        public int A, B, C;
        public Tri(int a, int b, int c)
        { A = a; B = b; C = c; }
    }

    internal class VIFBlockInfo
    {
        public int OffsetInChunk { get; set; }
        public int VertexCount { get; set; }
        public int FirstVertex { get; set; }
    }

    internal class MeshBatch
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public int TexId { get; set; }
        public int SourceChunkIdx { get; set; }
        public Vec3 BoneOffset { get; set; }
        public Vec3 SpreadOffset { get; set; }
        public int ObjVertStart { get; set; }
        public int ObjVertEnd { get; set; }
        public List<Vec3> Verts =
            new List<Vec3>();
        public List<Vec3> Normals =
            new List<Vec3>();
        public List<Vec2> UVs =
            new List<Vec2>();
        public List<Tri> Faces =
            new List<Tri>();
        public List<VIFBlockInfo> Blocks =
            new List<VIFBlockInfo>();
    }

    internal class LODSiblingInfo
    {
        public int ChunkIdx { get; set; }
            = -1;
        public int BatchIndex { get; set; }
            = -1;
        public int ChunkOffset { get; set; }
            = 0;
        public int VertexCount { get; set; }
            = 0;
        public List<(int offset, int vc)>
            VifBlocks =
            new List<(int, int)>();
    }

    internal class ManifestBlock3D
    {
        public int ChunkOffset { get; set; }
        public int VertexCount { get; set; }
        public int FirstVertex { get; set; }
    }

    internal class ManifestBatch3D
    {
        public int Index { get; set; }
        public int TexId { get; set; }
        public int SourceChunk { get; set; }
        public int ChunkOffset { get; set; }
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
        public int ObjVertStart { get; set; }
        public int ObjVertEnd { get; set; }
        public Vec3 SpreadOffset { get; set; }
        public Vec3 BoneOffset { get; set; }
        public Vec3 LocalCentroid { get; set; }
            = Vec3.Zero;
        public List<ManifestBlock3D> Blocks =
            new List<ManifestBlock3D>();
        public List<LODSiblingInfo>
            LodSiblings =
            new List<LODSiblingInfo>();
    }

    internal class ManifestData3D
    {
        public string Version { get; set; }
        public string SourceRdtb { get; set; }
        public string SourceGdtb { get; set; }
        public string OriginalRdtbName { get; set; }
        public string OriginalGdtbName { get; set; }
        public int SourceSize { get; set; }
        public int Chunk11Offset { get; set; }
        public int Chunk11Size { get; set; }
        public int MeshChunkIdx { get; set; }
        public List<Dictionary<string, int>>
            AllMeshChunks =
            new List<Dictionary<string, int>>();
        public List<ManifestBatch3D> Batches =
            new List<ManifestBatch3D>();
    }

    internal class ParsedObj
    {
        public List<Vec3> Verts =
            new List<Vec3>();
        public List<Vec3> Normals =
            new List<Vec3>();
        public List<Vec2> UVs =
            new List<Vec2>();
        public List<Tri> AllFaces =
            new List<Tri>();
        public Dictionary<string, List<Tri>>
            FacesByGroup =
            new Dictionary<string, List<Tri>>();

        // NEW: When true, vertices were
        // sourced from per-batch
        // subfolder files where each
        // batch was already centered to
        // local origin. Do NOT subtract
        // spread/bone offsets during
        // rebuild for these.
        public bool IsLocalOriginBatches
            = false;

        // NEW: Per-batch centroid that was
        // subtracted during extract. Must
        // be re-added during rebuild.
        // Keyed by group name like
        // "batch_0049".
        public Dictionary<string, Vec3>
            BatchCentroids =
            new Dictionary<string, Vec3>();

        internal List<Vec3> _rawV =
            new List<Vec3>();
        internal List<Vec2> _rawVT =
            new List<Vec2>();
        internal List<Vec3> _rawVN =
            new List<Vec3>();
        internal Dictionary<
            (int vi, int ti, int ni), int>
            _comboMap =
            new Dictionary<
                (int, int, int), int>();
    }

    public static class Model3D
    {
        public static void Extract(
            string rdtbPath,
            string gdtbPath,
            string baseName)
        {
            new RDTB3DExtractor().DoExtract(
                rdtbPath, gdtbPath,
                baseName, false);
        }

        // NEW overload with scale
        public static void Extract(
            string rdtbPath,
            string gdtbPath,
            string baseName,
            float scale)
        {
            var ex = new RDTB3DExtractor();
            ex.ExtractScale = scale;
            ex.DoExtract(
                rdtbPath, gdtbPath,
                baseName, false);
        }

        public static void ExtractSplit(
            string rdtbPath,
            string gdtbPath,
            string baseName)
        {
            new RDTB3DExtractor().DoExtract(
                rdtbPath, gdtbPath,
                baseName, true);
        }

        // NEW overload with scale for split mode
        public static void ExtractSplit(
            string rdtbPath,
            string gdtbPath,
            string baseName,
            float scale)
        {
            var ex = new RDTB3DExtractor();
            ex.ExtractScale = scale;
            ex.DoExtract(
                rdtbPath, gdtbPath,
                baseName, true);
        }

        public static void Create(
            string folder,
            string outFolder,
            float scale = 1.0f)
        {
            new RDTB3DCreator().DoCreate(
                folder, outFolder, scale);
        }
    }

    internal static class LODPairer
    {
        public static readonly int[]
            LOD_CHUNK_INDICES =
            new int[] { 11, 12, 13 };

        // Distance threshold for body parts
        // (tight - same body part across LODs
        // should be at very similar position)
        private const double BODY_MAX_DIST
            = 200.0;

        // Distance threshold for tools/items
        // (loose - tools live in hand-bone
        // space, can be transformed wildly
        // differently across LOD chunks)
        private const double TOOLS_MAX_DIST
            = 5000.0;

        public static Dictionary<int,
            List<LODSiblingInfo>>
            PairBatches(
                Dictionary<int,
                    List<MeshBatch>>
                    allBatchesByChunk)
        {
            var result =
                new Dictionary<int,
                    List<LODSiblingInfo>>();

            // Find primary (highest LOD) chunk
            int primaryChunk = -1;
            foreach (int ci in
                LOD_CHUNK_INDICES)
            {
                if (allBatchesByChunk
                        .ContainsKey(ci))
                {
                    primaryChunk = ci;
                    break;
                }
            }
            if (primaryChunk < 0)
                return result;

            var primaryBatches =
                allBatchesByChunk[
                    primaryChunk];
            var siblingChunks =
                new List<int>();
            foreach (int ci in
                LOD_CHUNK_INDICES)
            {
                if (ci != primaryChunk &&
                    allBatchesByChunk
                        .ContainsKey(ci))
                    siblingChunks.Add(ci);
            }
            if (siblingChunks.Count == 0)
                return result;

            // Group primary batches by tex
            var primaryByTex =
                new Dictionary<int,
                    List<MeshBatch>>();
            foreach (var b in primaryBatches)
            {
                if (!primaryByTex
                        .ContainsKey(b.TexId))
                    primaryByTex[b.TexId] =
                        new List<MeshBatch>();
                primaryByTex[b.TexId].Add(b);
            }

            // Group sibling batches by chunk
            // then by tex
            var siblingsByTex =
                new Dictionary<int,
                    Dictionary<int,
                        List<MeshBatch>>>();
            foreach (int sci in siblingChunks)
            {
                var sbt =
                    new Dictionary<int,
                        List<MeshBatch>>();
                foreach (var b in
                    allBatchesByChunk[sci])
                {
                    if (!sbt.ContainsKey(
                            b.TexId))
                        sbt[b.TexId] =
                            new List<MeshBatch>();
                    sbt[b.TexId].Add(b);
                }
                siblingsByTex[sci] = sbt;
            }

            // Identify the "tools" texture id
            // = the texture group with the
            // MOST batches (since one texture
            // sheet holds many tool items)
            int toolsTid =
                DetectToolsTid(primaryByTex);

            foreach (var tk in primaryByTex)
            {
                int tid = tk.Key;
                var pList = tk.Value;
                bool isTools =
                    (tid == toolsTid);
                double maxDist = isTools
                    ? TOOLS_MAX_DIST
                    : BODY_MAX_DIST;

                for (int i = 0;
                     i < pList.Count; i++)
                {
                    var pBatch = pList[i];
                    var pairList =
                        new List<LODSiblingInfo>();

                    foreach (int sci in
                        siblingChunks)
                    {
                        if (!siblingsByTex[sci]
                                .ContainsKey(tid))
                            continue;
                        var sList =
                            siblingsByTex[sci]
                                [tid];
                        if (sList.Count == 0)
                            continue;

                        MeshBatch sBatch =
                            FindBestSibling(
                                pBatch, sList,
                                i, isTools,
                                maxDist);
                        if (sBatch == null)
                            continue;

                        var info =
                            new LODSiblingInfo
                            {
                                ChunkIdx = sci,
                                BatchIndex =
                                    sBatch.Index,
                                ChunkOffset =
                                    sBatch.Offset,
                            };
                        int vcSum = 0;
                        foreach (var blk in
                            sBatch.Blocks)
                            vcSum +=
                                blk.VertexCount;
                        info.VertexCount =
                            vcSum;
                        foreach (var blk in
                            sBatch.Blocks)
                            info.VifBlocks.Add(
                                (blk.OffsetInChunk,
                                 blk.VertexCount));
                        pairList.Add(info);
                    }

                    if (pairList.Count > 0)
                        result[pBatch.Index] =
                            pairList;
                }
            }

            return result;
        }

        // Detect which texture id is the
        // "tools/items" sheet by finding the
        // texture group with the largest
        // batch count (tools sheet typically
        // contains 8+ small item meshes)
        private static int DetectToolsTid(
            Dictionary<int, List<MeshBatch>>
                byTex)
        {
            int toolsTid = -1;
            int maxCount = 0;
            foreach (var kv in byTex)
            {
                if (kv.Value.Count > maxCount)
                {
                    maxCount = kv.Value.Count;
                    toolsTid = kv.Key;
                }
            }
            // If only one tex group exists or
            // all groups have <= 3 batches,
            // there are no tools to special
            // case
            if (byTex.Count <= 1 ||
                maxCount < 4)
                toolsTid = -1;
            return toolsTid;
        }

        // Smart sibling matcher:
        // For body parts: use centroid dist
        //   (positional match across LODs)
        // For tools: use INDEX match first
        //   (tools[0] in chunk11 = tools[0]
        //    in chunk12 in original game
        //    layout), fall back to vertex
        //    count similarity if index out
        //    of range
        private static MeshBatch
            FindBestSibling(
                MeshBatch primary,
                List<MeshBatch> siblings,
                int primaryIndex,
                bool isTools,
                double maxDist)
        {
            if (siblings.Count == 0)
                return null;

            if (isTools)
            {
                // Strategy 1: same index slot
                if (primaryIndex <
                    siblings.Count)
                {
                    var byIdx =
                        siblings[primaryIndex];
                    double d =
                        CentroidDistance(
                            primary, byIdx);
                    if (d <= maxDist)
                        return byIdx;
                }

                // Strategy 2: closest centroid
                // among siblings (positional)
                MeshBatch bestPos = null;
                double bestDist =
                    double.MaxValue;
                foreach (var s in siblings)
                {
                    double d =
                        CentroidDistance(
                            primary, s);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestPos = s;
                    }
                }
                if (bestPos != null &&
                    bestDist <= maxDist)
                    return bestPos;

                // Strategy 3: closest vertex
                // count (last resort - same
                // mesh topology hint)
                int pvc = 0;
                foreach (var blk in
                    primary.Blocks)
                    pvc += blk.VertexCount;

                MeshBatch bestVc = null;
                int bestDiff = int.MaxValue;
                foreach (var s in siblings)
                {
                    int svc = 0;
                    foreach (var blk in
                        s.Blocks)
                        svc += blk.VertexCount;
                    int diff =
                        Math.Abs(svc - pvc);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestVc = s;
                    }
                }
                return bestVc;
            }
            else
            {
                // Body parts: prefer index
                // match with tight distance
                // check
                if (primaryIndex <
                    siblings.Count)
                {
                    var byIdx =
                        siblings[primaryIndex];
                    double d =
                        CentroidDistance(
                            primary, byIdx);
                    if (d <= maxDist)
                        return byIdx;
                }

                // Fallback: closest centroid
                MeshBatch bestPos = null;
                double bestDist =
                    double.MaxValue;
                foreach (var s in siblings)
                {
                    double d =
                        CentroidDistance(
                            primary, s);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestPos = s;
                    }
                }
                if (bestPos != null &&
                    bestDist <= maxDist)
                    return bestPos;

                return null;
            }
        }

        private static double
            CentroidDistance(
                MeshBatch b1, MeshBatch b2)
        {
            if (b1.Verts.Count == 0 ||
                b2.Verts.Count == 0)
                return 0.0;
            var c1 = Centroid(b1.Verts);
            var c2 = Centroid(b2.Verts);
            double dx = c1.x - c2.x;
            double dy = c1.y - c2.y;
            double dz = c1.z - c2.z;
            return Math.Sqrt(
                dx * dx + dy * dy + dz * dz);
        }

        private static (double x,
            double y, double z)
            Centroid(List<Vec3> verts)
        {
            if (verts.Count == 0)
                return (0.0, 0.0, 0.0);
            double sx = 0, sy = 0, sz = 0;
            foreach (var v in verts)
            {
                sx += v.X;
                sy += v.Y;
                sz += v.Z;
            }
            int n = verts.Count;
            return (sx / n, sy / n,
                    sz / n);
        }
    }

    internal static class LODResampler
    {
        public static (List<Vec3> v,
            List<Vec3> n, List<Vec2> u)
            Resample(
                List<Vec3> srcVerts,
                List<Vec3> srcNormals,
                List<Vec2> srcUvs,
                int targetCount)
        {
            int srcN = srcVerts.Count;
            if (srcN == 0 || targetCount == 0)
                return (new List<Vec3>(),
                        new List<Vec3>(),
                        new List<Vec2>());

            if (srcN == targetCount)
                return (
                    new List<Vec3>(srcVerts),
                    new List<Vec3>(srcNormals),
                    new List<Vec2>(srcUvs));

            if (targetCount > srcN)
            {
                var rv =
                    new List<Vec3>(srcVerts);
                var rn =
                    new List<Vec3>(srcNormals);
                var ru =
                    new List<Vec2>(srcUvs);
                while (rv.Count < targetCount)
                {
                    rv.Add(
                        rv.Count > 0
                        ? rv[rv.Count - 1]
                        : Vec3.Zero);
                    rn.Add(
                        rn.Count > 0
                        ? rn[rn.Count - 1]
                        : new Vec3(0, 1, 0));
                    ru.Add(
                        ru.Count > 0
                        ? ru[ru.Count - 1]
                        : new Vec2(0, 0));
                }
                return (rv, rn, ru);
            }

            var picked = new List<int> { 0 };
            var used = new HashSet<int> { 0 };

            var minDist =
                new double[srcN];
            for (int i = 0; i < srcN; i++)
                minDist[i] = DistSq(
                    srcVerts[i],
                    srcVerts[0]);
            minDist[0] = -1.0;

            while (picked.Count < targetCount)
            {
                int bestI = -1;
                double bestD = -1.0;
                for (int i = 0; i < srcN; i++)
                {
                    if (used.Contains(i))
                        continue;
                    if (minDist[i] > bestD)
                    {
                        bestD = minDist[i];
                        bestI = i;
                    }
                }
                if (bestI < 0) break;
                picked.Add(bestI);
                used.Add(bestI);
                for (int i = 0; i < srcN; i++)
                {
                    if (used.Contains(i))
                        continue;
                    double d = DistSq(
                        srcVerts[i],
                        srcVerts[bestI]);
                    if (d < minDist[i])
                        minDist[i] = d;
                }
            }

            picked.Sort();

            var rV = new List<Vec3>();
            var rN = new List<Vec3>();
            var rU = new List<Vec2>();
            foreach (int i in picked)
            {
                rV.Add(srcVerts[i]);
                rN.Add(
                    i < srcNormals.Count
                    ? srcNormals[i]
                    : new Vec3(0, 1, 0));
                rU.Add(
                    i < srcUvs.Count
                    ? srcUvs[i]
                    : new Vec2(0, 0));
            }
            return (rV, rN, rU);
        }

        private static double DistSq(
            Vec3 a, Vec3 b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public static List<(int offset,
            List<Vec3> bv, List<Vec3> bn,
            List<Vec2> bu)>
            ResampleToSibling(
                List<Vec3> newVerts,
                List<Vec3> newNormals,
                List<Vec2> newUvs,
                LODSiblingInfo sib)
        {
            var results =
                new List<(int, List<Vec3>,
                    List<Vec3>, List<Vec2>)>();

            int totalTarget = 0;
            foreach (var b in sib.VifBlocks)
                totalTarget += b.vc;

            var (rv, rn, ru) = Resample(
                newVerts, newNormals,
                newUvs, totalTarget);

            int cursor = 0;
            foreach (var (offset, vc) in
                sib.VifBlocks)
            {
                int take = Math.Min(
                    vc, rv.Count - cursor);
                var blockV = new List<Vec3>();
                var blockN = new List<Vec3>();
                var blockU = new List<Vec2>();
                for (int j = 0; j < take; j++)
                {
                    blockV.Add(rv[cursor + j]);
                    blockN.Add(rn[cursor + j]);
                    blockU.Add(ru[cursor + j]);
                }
                cursor += take;
                while (blockV.Count < vc)
                {
                    blockV.Add(
                        blockV.Count > 0
                        ? blockV[
                            blockV.Count - 1]
                        : Vec3.Zero);
                    blockN.Add(
                        blockN.Count > 0
                        ? blockN[
                            blockN.Count - 1]
                        : new Vec3(0, 1, 0));
                    blockU.Add(
                        blockU.Count > 0
                        ? blockU[
                            blockU.Count - 1]
                        : new Vec2(0, 0));
                }
                results.Add(
                    (offset, blockV,
                     blockN, blockU));
            }
            return results;
        }
    }

    internal class RDTB3DExtractor
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private const uint FLAG_EOF = 0x70000000;

        private string _rdtbPath;
        private string _gdtbPath;
        private byte[] _data;
        private int _ptrCount;
        private int _boneCount;
        private List<int> _chunkOffsets =
            new List<int>();
        private List<byte[]> _chunks =
            new List<byte[]>();
        private List<Vec3> _boneWorldT =
            new List<Vec3>();
        private List<int> _allMeshChunks =
            new List<int>();
        private Dictionary<int, List<MeshBatch>>
            _batchesPerChunk =
            new Dictionary<int, List<MeshBatch>>();
        private Dictionary<int, List<LODSiblingInfo>>
            _lodPairings =
            new Dictionary<int, List<LODSiblingInfo>>();
        private int _toolsTid = -1;

        // ADDITIVE: True for small/embedded RDTBs
        // (like SRDB embedded entries). When true,
        // output ONE combined OBJ instead of per-tex
        // split — matches SRDB extractor format.
        private bool _isEmbedded = false;

        // ADDITIVE: per-vertex scale factor.
        // Default 1.0 = no scaling. Use < 1.0
        // to shrink (e.g., 0.05 for items).
        public float ExtractScale { get; set; }
            = 1.0f;

        public void DoExtract(string rdtbPath,
            string gdtbPath, string baseName,
            bool splitMode)
        {
            if (!File.Exists(rdtbPath))
                throw new FileNotFoundException(
                    "RDTB not found: " + rdtbPath);
            if (!File.Exists(gdtbPath))
                throw new FileNotFoundException(
                    "GDTB not found: " + gdtbPath);

            _rdtbPath = rdtbPath;
            _gdtbPath = gdtbPath;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] HMSTH 3D Extractor"
                + " v2.2 (viewer)");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 60));
            Console.WriteLine(
                "    RDTB : " +
                Path.GetFileName(rdtbPath));
            Console.WriteLine(
                "    GDTB : " +
                Path.GetFileName(gdtbPath));
            Console.WriteLine(
                "    Base : " + baseName);
            Console.WriteLine(
                "    Mode : VIEW-ONLY"
                + " (use xbatches for"
                + " modding)");
            Console.WriteLine(
                new string('=', 60));

            LoadRDTB(rdtbPath);
            Console.WriteLine(
                "    Bones  : " + _boneCount);
            Console.WriteLine(
                "    Chunks : " +
                _chunkOffsets.Count);

            // Detect small/embedded RDTB
            _isEmbedded =
                _chunkOffsets.Count <= 11;
            if (_isEmbedded)
            {
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "    Mode   : EMBEDDED"
                    + " (one combined OBJ)");
                Console.ResetColor();
            }

            _boneWorldT = ComputeBoneWorldT();

            string dr = Path.GetDirectoryName(
                Path.GetFullPath(rdtbPath));

            // Only ONE folder now: _all_obj
            string fAllObj = Path.Combine(
                dr, baseName + "_all_obj");
            Directory.CreateDirectory(fAllObj);
            Directory.CreateDirectory(
                Path.Combine(fAllObj, "textures"));

            var tpAllObj = ExtrTex(gdtbPath,
                Path.Combine(fAllObj, "textures"),
                "[all_obj]");

            var batches = LoadMatsAndBatches(
                out int mci);

            // Auto-scale oversized models
            if (batches.Count > 0)
            {
                float mnx = float.MaxValue;
                float mxx = float.MinValue;
                float mny = float.MaxValue;
                float mxy = float.MinValue;
                float mnz = float.MaxValue;
                float mxz = float.MinValue;
                bool any = false;
                foreach (var b in batches)
                    foreach (var v in b.Verts)
                    {
                        if (v.X < mnx) mnx = v.X;
                        if (v.X > mxx) mxx = v.X;
                        if (v.Y < mny) mny = v.Y;
                        if (v.Y > mxy) mxy = v.Y;
                        if (v.Z < mnz) mnz = v.Z;
                        if (v.Z > mxz) mxz = v.Z;
                        any = true;
                    }
                if (any)
                {
                    float dx = mxx - mnx;
                    float dy = mxy - mny;
                    float dz = mxz - mnz;
                    float maxDim = dx;
                    if (dy > maxDim) maxDim = dy;
                    if (dz > maxDim) maxDim = dz;

                    const float TARGET = 100f;
                    const float THRESHOLD = 250f;

                    if (maxDim > THRESHOLD)
                    {
                        float autoScale =
                            TARGET / maxDim;
                        Console.ForegroundColor =
                            ConsoleColor.Cyan;
                        Console.WriteLine(
                            "    Auto-scale: " +
                            autoScale.ToString(
                                "F4") +
                            "x (model was " +
                            maxDim.ToString(
                                "F0") +
                            " units, scaling"
                            + " to " + TARGET +
                            ")");
                        Console.ResetColor();
                        foreach (var b in
                            batches)
                        {
                            var newV =
                                new List<Vec3>();
                            foreach (var v in
                                b.Verts)
                                newV.Add(
                                    new Vec3(
                                        v.X *
                                        autoScale,
                                        v.Y *
                                        autoScale,
                                        v.Z *
                                        autoScale));
                            b.Verts = newV;
                            b.BoneOffset =
                                new Vec3(
                                    b.BoneOffset
                                        .X *
                                    autoScale,
                                    b.BoneOffset
                                        .Y *
                                    autoScale,
                                    b.BoneOffset
                                        .Z *
                                    autoScale);
                        }
                    }
                }
            }

            AssignRanges(batches);
            var groups = GroupByTex(batches);

            // LOD pairing (still useful for
            // viewing per-LOD if needed)
            _lodPairings =
                new Dictionary<int,
                    List<LODSiblingInfo>>();
            if (_batchesPerChunk.Count >= 2)
            {
                try
                {
                    _lodPairings =
                        LODPairer.PairBatches(
                            _batchesPerChunk);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [!] LOD pairing"
                        + " failed: " +
                        ex.Message);
                    Console.ResetColor();
                    _lodPairings =
                        new Dictionary<int,
                            List<LODSiblingInfo>>();
                }
            }

            if (groups.Count > 0)
            {
                ComputeSpread(groups);

                if (_isEmbedded)
                {
                    WriteEmbeddedSingleObj(
                        fAllObj, baseName,
                        groups, tpAllObj);
                }
                else
                {
                    WriteAllObj(fAllObj,
                        baseName, groups,
                        tpAllObj);
                }
            }

            // Manifest (still useful for
            // reference)
            WriteManifest(fAllObj, baseName,
                rdtbPath, gdtbPath,
                batches, groups, mci);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Extraction complete!");
            Console.ResetColor();
            Console.WriteLine(
                "     Folder: " + fAllObj);
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "     For modding,"
                + " use:");
            Console.WriteLine(
                "       xbatches "
                + Path.GetFileName(rdtbPath)
                + " "
                + Path.GetFileName(gdtbPath)
                + " " + baseName);
            Console.ResetColor();
        }

        private void LoadRDTB(string path)
        {
            _data = File.ReadAllBytes(path);
            if (_data[0] != 'R' ||
                _data[1] != 'D' ||
                _data[2] != 'T' ||
                _data[3] != 'B')
                throw new InvalidDataException(
                    "Not RDTB: " + path);
            _ptrCount = BitConverter.ToUInt16(
                _data, 0x0C);
            _boneCount = BitConverter.ToUInt16(
                _data, 0x0E);
            _chunkOffsets.Clear();
            for (int i = 0; i < 14; i++)
            {
                int v = BitConverter.ToInt32(
                    _data, 0x10 + i * 4);
                if (v == 0 || v < 0x48 ||
                    v > _data.Length)
                    break;
                if (v == -1) continue;
                _chunkOffsets.Add(v);
            }

            _chunks.Clear();
            for (int i = 0;
                 i < _chunkOffsets.Count; i++)
            {
                int s = _chunkOffsets[i];
                // For mirrored RDTBs,
                // find next DIFFERENT
                // offset
                int e = _data.Length;
                for (int ci = i + 1;
                     ci < _chunkOffsets
                         .Count; ci++)
                {
                    if (_chunkOffsets[ci]
                        != s)
                    {
                        e = _chunkOffsets[
                            ci];
                        break;
                    }
                }
                if (e <= s) continue;
                byte[] c = new byte[e - s];

                Array.Copy(_data, s, c, 0, e - s);
                _chunks.Add(c);
            }
        }

        private List<Vec3> ComputeBoneWorldT()
        {
            var world = new List<Vec3>();
            for (int i = 0; i < _boneCount; i++)
                world.Add(Vec3.Zero);
            if (_chunks.Count == 0 ||
                _boneCount == 0)
                return world;
            byte[] c0 = _chunks[0];
            int rowsStart = _boneCount * 4;
            var parents = new int[_boneCount];
            var localT = new Vec3[_boneCount];
            for (int b = 0; b < _boneCount; b++)
            {
                int o = rowsStart + b * 16;
                if (o + 16 > c0.Length) break;
                byte pb = c0[o + 3];
                // Mask off bit 7 (which is a
                // flag, possibly mirror or
                // LR-twin marker). The actual
                // bone index is in bits 0-6.
                int parentIdx = pb & 0x7F;

                if (pb == 0xFF || pb == 0x00)
                {
                    // Root bone:
                    // 0xFF = explicit "no parent"
                    // 0x00 = PS2 sentinel for
                    //        "this IS the root"
                    //        (parent=bone0 would
                    //        be self-reference)
                    parents[b] = -1;
                }
                else if (parentIdx >= _boneCount)
                {
                    // Still out of range even
                    // after masking - treat as
                    // root
                    parents[b] = -1;
                }
                else
                {
                    parents[b] = parentIdx;
                }

                localT[b] = new Vec3(
                    BitConverter.ToSingle(c0, o + 4),
                    BitConverter.ToSingle(c0, o + 8),
                    BitConverter.ToSingle(c0, o + 12));
            }
            for (int b = 0; b < _boneCount; b++)
            {
                float wx = localT[b].X;
                float wy = localT[b].Y;
                float wz = localT[b].Z;
                var visited = new HashSet<int> { b };
                int p = parents[b];
                while (p >= 0)
                {
                    if (visited.Contains(p)) break;
                    visited.Add(p);
                    wx += localT[p].X;
                    wy += localT[p].Y;
                    wz += localT[p].Z;
                    p = parents[p];
                }
                world[b] = new Vec3(wx, wy, wz);
            }
            return world;
        }

        private bool IsVIF(byte[] d, int o)
        {
            return o + 16 <= d.Length &&
                   d[o] == VIF_B0 &&
                   d[o + 1] == VIF_B1 &&
                   d[o + 3] == VIF_B3;
        }

        private List<int> FindVIFs(byte[] c)
        {
            var r = new List<int>();
            int i = 0;
            while (i + 16 <= c.Length)
            {
                if (IsVIF(c, i))
                { r.Add(i); i += 16; }
                else i += 4;
            }
            return r;
        }

        private List<(uint flag, float x,
            float y, float z)>
            ParseRows(byte[] c, int ds, int de)
        {
            var rows = new List<(uint, float,
                float, float)>();
            int o = ds;
            while (o + 16 <= de)
            {
                uint flag = BitConverter.ToUInt32(
                    c, o);
                if (flag == FLAG_EOF) break;
                if (o + 4 < de &&
                    c[o] == VIF_B0 &&
                    c[o + 1] == VIF_B1 &&
                    c[o + 3] == VIF_B3)
                    break;
                float x = BitConverter.ToSingle(
                    c, o + 4);
                float y = BitConverter.ToSingle(
                    c, o + 8);
                float z = BitConverter.ToSingle(
                    c, o + 12);
                if (!float.IsNaN(x) &&
                    !float.IsNaN(y) &&
                    !float.IsNaN(z) &&
                    !float.IsInfinity(x) &&
                    !float.IsInfinity(y) &&
                    !float.IsInfinity(z))
                    rows.Add((flag, x, y, z));
                o += 16;
            }
            return rows;
        }

        private List<Tri> MakeStrip(int n)
        {
            var r = new List<Tri>();
            for (int i = 0; i < n - 2; i++)
            {
                if (i % 2 == 0)
                    r.Add(new Tri(i, i + 1, i + 2));
                else
                    r.Add(new Tri(i, i + 2, i + 1));
            }
            return r;
        }

        private List<Tri> FilterDegen(
            List<Tri> faces, List<Vec3> verts)
        {
            var g = new List<Tri>();
            foreach (var t in faces)
            {
                if (t.A >= verts.Count ||
                    t.B >= verts.Count ||
                    t.C >= verts.Count)
                    continue;
                Vec3 v0 = verts[t.A];
                Vec3 v1 = verts[t.B];
                Vec3 v2 = verts[t.C];
                float ax = v1.X - v0.X;
                float ay = v1.Y - v0.Y;
                float az = v1.Z - v0.Z;
                float bx = v2.X - v0.X;
                float by = v2.Y - v0.Y;
                float bz = v2.Z - v0.Z;
                float cx = ay * bz - az * by;
                float cy = az * bx - ax * bz;
                float cz = ax * by - ay * bx;
                if (cx * cx + cy * cy + cz * cz
                    > 1e-10f)
                    g.Add(t);
            }
            return g;
        }

        private List<int> DetectMeshChunks()
        {
            int total = _chunks.Count;
            if (total > 11)
            {
                var found = new List<int>();
                foreach (int ci in
                    new[] { 11, 12, 13 })
                {
                    if (ci < total &&
                        FindVIFs(_chunks[ci])
                            .Count >= 2)
                        found.Add(ci);
                }
                if (found.Count > 0)
                    return found;
            }
            int last = total - 1;
            if (last >= 0 &&
                FindVIFs(_chunks[last])
                    .Count >= 1)
            {
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "    [Small RDTB: mesh" +
                    " in chunk " + last + "]");
                Console.ResetColor();
                return new List<int> { last };
            }
            for (int ci = total - 1;
                 ci >= 0; ci--)
            {
                if (_chunks[ci].Length < 32)
                    continue;
                if (FindVIFs(_chunks[ci])
                        .Count >= 1)
                    return new List<int> { ci };
            }
            int fb = Math.Min(8, total - 1);
            return new List<int> { fb };
        }

        private List<(int boneIdx, int texId,
            byte[] sig)>
            ParseChunk8(byte[] c8)
        {
            var r = new List<(int, int,
                byte[])>();
            if (c8 == null || c8.Length < 4)
                return r;
            if (c8[0] == VIF_B0 &&
                c8[1] == VIF_B1 &&
                c8.Length > 3 &&
                c8[3] == VIF_B3)
                return r;
            uint first = BitConverter
                .ToUInt32(c8, 0);
            if (first == 0 ||
                first > (uint)c8.Length)
                return r;
            int bc = (int)(first / 4);
            if (bc > 10000) return r;
            for (int i = 0; i < bc; i++)
            {
                int poff = i * 4;
                if (poff + 4 > c8.Length)
                    break;
                uint ptr = BitConverter
                    .ToUInt32(c8, poff);
                if (ptr + 8 > (uint)c8.Length)
                {
                    r.Add((0, 0, new byte[8]));
                    continue;
                }
                int bone = BitConverter
                    .ToUInt16(c8, (int)ptr);
                int tex = BitConverter
                    .ToUInt16(c8, (int)ptr + 6);
                byte[] sig = new byte[8];
                Array.Copy(c8, (int)ptr,
                    sig, 0, 8);
                r.Add((bone, tex, sig));
            }
            return r;
        }

        private List<MeshBatch>
            ParseBatchesSmall(byte[] chunk)
        {
            var batches = new List<MeshBatch>();
            var allVif = FindVIFs(chunk);
            if (allVif.Count == 0)
                return batches;
            for (int vi = 0;
                 vi < allVif.Count; vi++)
            {
                int vo = allVif[vi];
                int ve = (vi + 1 < allVif.Count)
                    ? allVif[vi + 1]
                    : chunk.Length;
                int vc = chunk[vo + 2];
                var rows = ParseRows(
                    chunk, vo + 16, ve);
                if (rows.Count < 3) continue;
                int n = (vc >= 3 &&
                    vc * 3 <= rows.Count)
                    ? vc : rows.Count / 3;
                if (n < 3) continue;
                var b = new MeshBatch
                {
                    Index = vi,
                    Offset = vo,
                    TexId = 0,
                };
                b.Blocks.Add(new VIFBlockInfo
                {
                    OffsetInChunk = vo,
                    VertexCount = n,
                    FirstVertex = 0,
                });
                for (int i = 0; i < n; i++)
                {
                    float vx = rows[i].x;
                    // W-flag 0xFFFFFFFF means
                    // "mirror X" — the PS2
                    // uses this for left/right
                    // body symmetry. Negate X
                    // to produce the correct
                    // mirrored position.
                    if (rows[i].flag == 0xFFFFFFFF)
                        vx = -vx;
                    b.Verts.Add(new Vec3(
                        vx, rows[i].y,
                        rows[i].z));
                }
                for (int i = 0; i < n; i++)
                {
                    float vx = rows[i].x;
                    if (rows[i].flag == 0xFFFFFFFF)
                        vx = -vx;
                    b.Verts.Add(new Vec3(
                        vx, rows[i].y,
                        rows[i].z));
                }
                for (int i = 2 * n; i < 3 * n; i++)
                    b.UVs.Add(new Vec2(
                        rows[i].x,
                        1.0f - rows[i].y));
                while (b.Normals.Count < n)
                    b.Normals.Add(
                        new Vec3(0, 1, 0));
                while (b.UVs.Count < n)
                    b.UVs.Add(new Vec2(0, 0));
                foreach (var t in MakeStrip(n))
                    b.Faces.Add(t);
                b.Faces = FilterDegen(
                    b.Faces, b.Verts);
                if (b.Verts.Count > 0 &&
                    b.Faces.Count > 0)
                    batches.Add(b);
            }
            return batches;
        }

        private List<MeshBatch> ParseBatches(
            byte[] c11,
                List<(int boneIdx, int texId,
            byte[] sig)> mats)
        {
            var batches = new List<MeshBatch>();
            if (c11.Length < 32)
                return batches;
            int exp = mats.Count;
            var p2bi =
                new Dictionary<int, int>();
            var opt = new List<int>();
            for (int i = 0; i < exp; i++)
            {
                int poff = i * 4;
                if (poff + 4 > c11.Length)
                    break;
                int ptr = BitConverter
                    .ToInt32(c11, poff);
                if (ptr >= 0 &&
                    ptr < c11.Length &&
                    IsVIF(c11, ptr) &&
                    !p2bi.ContainsKey(ptr))
                {
                    p2bi[ptr] = i;
                    opt.Add(ptr);
                }
            }
            opt.Sort();
            var allVif = FindVIFs(c11);

            for (int bi = 0;
                bi < opt.Count; bi++)
            {
                int bs = opt[bi];
                int be = (bi + 1 < opt.Count)
                    ? opt[bi + 1] : c11.Length;
                var lv = allVif
                    .Where(v => v >= bs &&
                                v < be)
                    .ToList();
                if (lv.Count == 0) continue;
                var b = new MeshBatch
                {
                    Index = bi,
                    Offset = bs,
                };
                for (int vi = 0;
                     vi < lv.Count; vi++)
                {
                    int vo = lv[vi];
                    int ve = (vi + 1 <
                        lv.Count)
                        ? lv[vi + 1] : be;
                    int vc = c11[vo + 2];
                    var rows = ParseRows(
                        c11, vo + 16, ve);
                    if (rows.Count < 3)
                        continue;
                    int n = (vc > 0 &&
                        vc * 3 <= rows.Count)
                        ? vc : rows.Count / 3;
                    if (n < 1 ||
                        n * 3 > rows.Count)
                        continue;
                    int bv = b.Verts.Count;
                    b.Blocks.Add(
                        new VIFBlockInfo
                        {
                            OffsetInChunk = vo,
                            VertexCount = n,
                            FirstVertex = bv,
                        });
                    for (int i = 0; i < n; i++)
                        b.Verts.Add(new Vec3(
                            rows[i].x,
                            rows[i].y,
                            rows[i].z));
                    for (int i = n;
                         i < 2 * n; i++)
                        b.Normals.Add(new Vec3(
                            rows[i].x,
                            rows[i].y,
                            rows[i].z));
                    for (int i = 2 * n;
                         i < 3 * n; i++)
                        b.UVs.Add(new Vec2(
                            rows[i].x,
                            1.0f - rows[i].y));
                    foreach (var t in
                        MakeStrip(n))
                        b.Faces.Add(new Tri(
                            bv + t.A,
                            bv + t.B,
                            bv + t.C));
                }

                int mi = p2bi.TryGetValue(
                    bs, out int mv) ? mv : bi;
                if (mats.Count > mi)
                {
                    b.TexId = mats[mi].texId;
                    int boneIdx =
                        mats[mi].boneIdx;
                    if (boneIdx >= 0 &&
                        boneIdx < _boneCount &&
                        _boneWorldT.Count > 0)
                    {
                        Vec3 wt =
                            _boneWorldT[boneIdx];
                        b.BoneOffset = wt;
                        var newV =
                            new List<Vec3>();
                        foreach (var v in
                            b.Verts)
                            newV.Add(new Vec3(
                                v.X + wt.X,
                                v.Y + wt.Y,
                                v.Z + wt.Z));
                        b.Verts = newV;
                    }
                }
                b.Faces = FilterDegen(
                    b.Faces, b.Verts);
                if (b.Verts.Count > 0 &&
                    b.Faces.Count > 0)
                    batches.Add(b);
            }
            return batches;
        }

        private List<MeshBatch>
            LoadMatsAndBatches(out int mci)
        {
            var mc = DetectMeshChunks();
            mci = mc.Count > 0 ? mc[0] : 0;

            int c8Idx = Math.Min(
                8, _chunks.Count - 1);
            byte[] c8 = c8Idx >= 0
                ? _chunks[c8Idx]
                : new byte[0];

            bool startsWithVif =
                c8.Length >= 4 &&
                c8[0] == VIF_B0 &&
                c8[1] == VIF_B1 &&
                c8[3] == VIF_B3;
            bool isSmall =
                mc.Contains(c8Idx) &&
                _chunks.Count <= 11 &&
                startsWithVif;

            var mats = isSmall
                ? new List<(int, int, byte[])>()
                : ParseChunk8(c8);

            Console.WriteLine(
                "[DIAG] c8Idx=" + c8Idx +
                " c8.Length=" + c8.Length +
                " isSmall=" + isSmall +
                " startsWithVif=" + startsWithVif +
                " mats.Count=" + mats.Count +
                " mc.Count=" + mc.Count +
                " mci=" + mci);

            if (isSmall)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [Small RDTB: no" +
                    " material table]");
                Console.ResetColor();
            }

            _allMeshChunks = new List<int>(mc);
            _batchesPerChunk =
                new Dictionary<int,
                    List<MeshBatch>>();

            var batches = new List<MeshBatch>();
            int boc = 0;

            foreach (int ci in mc)
            {
                if (ci >= _chunks.Count)
                    continue;
                List<MeshBatch> cb;

                if (ci == c8Idx &&
                    _chunks.Count <= 11)
                {
                    cb = ParseBatchesSmall(
                        _chunks[ci]);

                    // ADDITIVE FIX: assign correct
                    // TexId per batch using SRDB
                    // fallback logic exactly:
                    //   batch[i] -> mats[i] when
                    //   i < mats.Count, otherwise
                    //   TexId stays at default 0.
                    // This matches SRDB output
                    // byte-for-byte.
                    if (mats.Count > 0
                        && cb.Count > 0)
                    {
                        for (int bIdx = 0;
                             bIdx < cb.Count;
                             bIdx++)
                        {
                            if (bIdx < mats.Count)
                            {
                                cb[bIdx].TexId =
                                    mats[bIdx]
                                        .Item2;
                                int boneId =
                                    mats[bIdx]
                                        .Item1;
                                if (boneId >= 0 &&
                                    boneId <
                                        _boneCount
                                    &&
                                    _boneWorldT
                                        .Count > 0)
                                {
                                    Vec3 wt =
                                        _boneWorldT[
                                            boneId];
                                    cb[bIdx]
                                        .BoneOffset
                                        = wt;
                                    var newV =
                                        new List
                                            <Vec3>();
                                    foreach (
                                        var v in
                                        cb[bIdx]
                                            .Verts)
                                        newV.Add(
                                            new Vec3(
                                                v.X
                                                + wt.X,
                                                v.Y
                                                + wt.Y,
                                                v.Z
                                                + wt.Z));
                                    cb[bIdx]
                                        .Verts
                                        = newV;
                                }
                            }
                            // else: leave TexId at
                            // default 0 — matches
                            // SRDB behavior exactly.
                            // Batches beyond mats
                            // count use texture 0
                            // (which is what the
                            // game's renderer does).
                        }
                    }
                }
                else
                    cb = ParseBatches(
                        _chunks[ci], mats);

                var chunkBatches =
                    new List<MeshBatch>();
                foreach (var b in cb)
                {
                    b.Index = boc++;
                    b.SourceChunkIdx = ci;
                    chunkBatches.Add(b);
                }
                batches.AddRange(chunkBatches);
                _batchesPerChunk[ci] =
                    chunkBatches;
            }
            return batches;
        }

        private void AssignRanges(
            List<MeshBatch> batches)
        {
            var texOff =
                new Dictionary<int, int>();
            foreach (var b in batches)
            {
                if (!texOff.ContainsKey(
                        b.TexId))
                    texOff[b.TexId] = 0;
                b.ObjVertStart =
                    texOff[b.TexId];
                b.ObjVertEnd =
                    b.ObjVertStart +
                    b.Verts.Count;
                texOff[b.TexId] =
                    b.ObjVertEnd;
            }
        }

        private SortedDictionary<int,
            List<MeshBatch>>
            GroupByTex(
                List<MeshBatch> batches)
        {
            var r =
                new SortedDictionary<int,
                    List<MeshBatch>>();
            foreach (var b in batches)
            {
                if (!r.ContainsKey(b.TexId))
                    r[b.TexId] =
                        new List<MeshBatch>();
                r[b.TexId].Add(b);
            }
            return r;
        }

        private void ComputeSpread(
            SortedDictionary<int,
        List<MeshBatch>> groups)
        {
            // Tools mode ONLY for BOY (player
            // character). Detected by checking
            // the RDTB filename. Every other
            // character (NPCs, items, etc.)
            // uses standard single OBJ output
            // with no subfolder.
            int toolsTid = -1;
            string fname = Path
                .GetFileNameWithoutExtension(
                    _rdtbPath ?? "")
                .ToUpperInvariant();
            bool isBoy =
                fname.StartsWith("BOY");
            if (isBoy)
            {
                int maxB = 0;
                foreach (var kv in groups)
                {
                    if (kv.Value.Count > maxB)
                    {
                        maxB = kv.Value.Count;
                        toolsTid = kv.Key;
                    }
                }
                if (groups.Count <= 1)
                    toolsTid = -1;
            }
            _toolsTid = toolsTid;

            var yOffsets =
               new Dictionary<int, float>
               {
                    {0,  5f}, {1, 20f},
                    {2, 40f}, {3, 42f},
                    {4, 39f}, {6, -2f},
               };

            // Embedded RDTBs write raw verts
            // with no spread. Skip offsets.
            if (_isEmbedded)
            {
                foreach (var kv in groups)
                    foreach (var b in kv.Value)
                        b.SpreadOffset =
                            Vec3.Zero;
                return;
            }

            foreach (var kv in groups)
            {
                int tid = kv.Key;
                var bl = kv.Value;
                if (tid != toolsTid)
                {
                    float y =
                        yOffsets.TryGetValue(
                            tid, out float yv)
                        ? yv : 0f;
                    foreach (var b in bl)
                        b.SpreadOffset =
                            new Vec3(0, y, 0);
                    continue;
                }

                const float GAP = 5f;
                float cx = 0f;
                var bndList = new List<float[]>();
                foreach (var b in bl)
                {
                    if (b.Verts.Count == 0)
                    {
                        bndList.Add(new float[6]);
                        continue;
                    }
                    float mnx = float.MaxValue;
                    float mxx = float.MinValue;
                    float mny = float.MaxValue;
                    float mxy = float.MinValue;
                    float mnz = float.MaxValue;
                    float mxz = float.MinValue;
                    foreach (var v in b.Verts)
                    {
                        if (v.X < mnx) mnx = v.X;
                        if (v.X > mxx) mxx = v.X;
                        if (v.Y < mny) mny = v.Y;
                        if (v.Y > mxy) mxy = v.Y;
                        if (v.Z < mnz) mnz = v.Z;
                        if (v.Z > mxz) mxz = v.Z;
                    }
                    bndList.Add(new float[]
                    {
                        mnx, mxx, mny,
                        mxy, mnz, mxz,
                    });
                }
                var offs = new List<Vec3>();
                for (int pi = 0;
                     pi < bl.Count; pi++)
                {
                    if (bl[pi].Verts.Count == 0)
                    {
                        offs.Add(Vec3.Zero);
                        continue;
                    }
                    float[] bnd = bndList[pi];
                    float w = bnd[1] - bnd[0];
                    float ccx =
                        (bnd[0] + bnd[1]) * 0.5f;
                    float ccy =
                        (bnd[2] + bnd[3]) * 0.5f;
                    float ccz =
                        (bnd[4] + bnd[5]) * 0.5f;
                    float tcx = cx + w * 0.5f;
                    offs.Add(new Vec3(
                        tcx - ccx, -ccy, -ccz));
                    cx += w + GAP;
                }
                float tw = cx - GAP;
                float sh = tw * 0.5f;
                for (int pi = 0;
                     pi < bl.Count; pi++)
                {
                    Vec3 o = offs[pi];
                    bl[pi].SpreadOffset =
                        new Vec3(
                            o.X - sh,
                            o.Y, o.Z);
                }
            }
        }

        private List<string> ExtrTex(
            string gdtbPath,
            string outFolder,
            string label)
        {
            var result = new List<string>();
            if (!File.Exists(gdtbPath))
                return result;
            try
            {
                Directory.CreateDirectory(
                    outFolder);
                GDTBArchive.Extract(
                    gdtbPath, outFolder);
                var files = Directory.GetFiles(
                    outFolder, "texture_*.bmp")
                    .OrderBy(f => f).ToList();
                result.AddRange(files);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    " + label +
                    " [!] " + ex.Message);
                Console.ResetColor();
            }
            return result;
        }

        private string RelPath(
            string target, string fromFile)
        {
            try
            {
                string t = Path.GetFullPath(
                    target);
                string fd = Path.GetDirectoryName(
                    Path.GetFullPath(fromFile));
                Uri tUri = new Uri(t);
                Uri fUri = new Uri(fd +
                    Path.DirectorySeparatorChar);
                return Uri.UnescapeDataString(
                    fUri.MakeRelativeUri(tUri)
                        .ToString()
                        .Replace('\\', '/'));
            }
            catch
            {
                return Path.GetFileName(target);
            }
        }

        private static string G(float v)
            => v.ToString("G9",
                System.Globalization
                    .CultureInfo
                    .InvariantCulture);

        private void WriteObjPerTex(
            string outDir,
            SortedDictionary<int,
            List<MeshBatch>> groups,
        List<string> tpaths)
        {
            int toolsTid = _toolsTid;
            foreach (var kv in groups)
            {
                int tn = kv.Key;
                var bl = kv.Value;
                string tp = tn < tpaths.Count
                    ? tpaths[tn] : null;
                string mn =
                    $"model_{tn:D2}";
                string ojp = Path.Combine(
                    outDir, mn + ".obj");
                string mtp = Path.Combine(
                    outDir, mn + ".mtl");
                if (tn == toolsTid)
                {
                    // Tools group: ONLY write
                    // subfolder with per-batch
                    // OBJ files. Do NOT write
                    // model_NN.obj at root.
                }
                else
                {
                    WriteSingleGroupObj(
                        ojp, mtp, mn, bl, tp);
                    continue;
                }

                string sub = Path.Combine(
                    outDir, mn);
                string subTex = Path.Combine(
                    sub, "textures");
                Directory.CreateDirectory(subTex);

                Console.WriteLine(
                  "    [debug] tp=" +
                  (tp ?? "null") +
                  " exists=" +
                  (tp != null &&
                   File.Exists(tp)));
                string subTp = null;
                if (tp != null &&
                    File.Exists(tp))
                {
                    string dst = Path.Combine(
                        subTex,
                        Path.GetFileName(tp));
                    if (!File.Exists(dst))
                        File.Copy(tp, dst);
                    subTp = dst;
                }
                for (int idx = 0;
                     idx < bl.Count; idx++)
                {
                    var b = bl[idx];
                    if (b.Verts.Count == 0 ||
                        b.Faces.Count == 0)
                        continue;
                    WriteBatchObj(
                        sub, idx, b,
                        subTp, tn);
                }
            }
        }

        private void WriteSingleGroupObj(
            string ojp, string mtp,
            string mn,
            List<MeshBatch> bl, string tp)
        {
            using (var sw = new StreamWriter(
                mtp, false, Encoding.UTF8))
            {
                sw.WriteLine($"# {mn}");
                sw.WriteLine();
                sw.WriteLine($"newmtl {mn}");
                sw.WriteLine("Ka 1 1 1");
                sw.WriteLine("Kd 1 1 1");
                sw.WriteLine("Ks 0 0 0");
                sw.WriteLine("Ns 10");
                sw.WriteLine("illum 2");
                if (tp != null &&
                    File.Exists(tp))
                    sw.WriteLine(
                        "map_Kd " +
                        RelPath(tp, ojp));
            }
            using (var sw = new StreamWriter(
                ojp, false, Encoding.UTF8))
            {
                sw.WriteLine($"# HMSTH {mn}");
                sw.WriteLine("mtllib " +
                    Path.GetFileName(mtp));
                sw.WriteLine();
                foreach (var b in bl)
                    foreach (var v in b.Verts)
                        sw.WriteLine("v " +
                            G(v.X +
                              b.SpreadOffset.X)
                            + " " +
                            G(v.Y +
                              b.SpreadOffset.Y)
                            + " " +
                            G(v.Z +
                              b.SpreadOffset.Z));
                sw.WriteLine();
                foreach (var b in bl)
                    foreach (var uv in b.UVs)
                        sw.WriteLine("vt " +
                            G(uv.U) + " " +
                            G(uv.V));
                sw.WriteLine();
                foreach (var b in bl)
                    foreach (var n in b.Normals)
                        sw.WriteLine("vn " +
                            G(n.X) + " " +
                            G(n.Y) + " " +
                            G(n.Z));
                sw.WriteLine();

                int vb = 1, ub = 1, nb = 1;
                foreach (var b in bl)
                {
                    sw.WriteLine(
                        $"g batch_" +
                        $"{b.Index:D4}");
                    sw.WriteLine(
                        $"usemtl {mn}");
                    foreach (var t in b.Faces)
                    {
                        int a = t.A + vb;
                        int bb = t.B + vb;
                        int c = t.C + vb;
                        int au = t.A + ub;
                        int bu = t.B + ub;
                        int cu = t.C + ub;
                        int an = t.A + nb;
                        int bn = t.B + nb;
                        int cn = t.C + nb;
                        sw.WriteLine(
                            $"f {a}/{au}/{an}" +
                            $" {bb}/{bu}/{bn}" +
                            $" {c}/{cu}/{cn}");
                    }
                    vb += b.Verts.Count;
                    ub += b.UVs.Count;
                    nb += b.Normals.Count;
                }
            }
        }

        // Stores per-batch centroid for the
        // manifest writer to pick up later.
        // Key = batch.Index (manifest global
        // index), Value = (cx, cy, cz)
        private Dictionary<int, Vec3>
            _batchCentroids =
            new Dictionary<int, Vec3>();

        private void WriteBatchObj(
            string subDir, int idx,
            MeshBatch b, string subTp, int tn)
        {
            string bname = $"batch_{idx:D3}";
            string bojp = Path.Combine(
                subDir, bname + ".obj");
            string bmtp = Path.Combine(
                subDir, bname + ".mtl");

            // Compute centroid of verts AFTER
            // removing spread offset, so the
            // centroid represents only the
            // per-batch local position. This
            // way the rebuild reload can
            // re-add centroid first to get
            // back to "spread-applied" space,
            // then the standard rebuild loop
            // subtracts spread+bone normally.
            float cx_ = 0f, cy_ = 0f, cz_ = 0f;
            if (b.Verts.Count > 0)
            {
                float mnx = float.MaxValue;
                float mxx = float.MinValue;
                float mny = float.MaxValue;
                float mxy = float.MinValue;
                float mnz = float.MaxValue;
                float mxz = float.MinValue;
                foreach (var v in b.Verts)
                {
                    if (v.X < mnx) mnx = v.X;
                    if (v.X > mxx) mxx = v.X;
                    if (v.Y < mny) mny = v.Y;
                    if (v.Y > mxy) mxy = v.Y;
                    if (v.Z < mnz) mnz = v.Z;
                    if (v.Z > mxz) mxz = v.Z;
                }
                cx_ = (mnx + mxx) * 0.5f;
                cy_ = (mny + mxy) * 0.5f;
                cz_ = (mnz + mxz) * 0.5f;
            }
            _batchCentroids[b.Index] =
                new Vec3(cx_, cy_, cz_);

            using (var sw = new StreamWriter(
                bmtp, false, Encoding.UTF8))
            {
                sw.WriteLine($"# model_{tn:D2}");
                sw.WriteLine();
                sw.WriteLine($"newmtl {bname}");
                sw.WriteLine("Ka 1 1 1");
                sw.WriteLine("Kd 1 1 1");
                sw.WriteLine("Ks 0 0 0");
                sw.WriteLine("Ns 10");
                sw.WriteLine("illum 2");
                if (subTp != null &&
                    File.Exists(subTp))
                    sw.WriteLine("map_Kd " +
                        RelPath(subTp, bojp));
            }

            using (var sw = new StreamWriter(
                bojp, false, Encoding.UTF8))
            {
                sw.WriteLine(
                    $"# {bname} (tex_{tn:D2})");
                sw.WriteLine("mtllib " +
                    Path.GetFileName(bmtp));
                sw.WriteLine();

                // Center verts using bone-relative
                // centroid: remove spread first
                // (matches centroid space), then
                // subtract centroid for centering
                foreach (var v in b.Verts)
                    sw.WriteLine("v " +
                        G(v.X - cx_) + " " +
                        G(v.Y - cy_) + " " +
                        G(v.Z - cz_));
                sw.WriteLine();
                foreach (var uv in b.UVs)
                    sw.WriteLine("vt " +
                        G(uv.U) + " " +
                        G(uv.V));
                sw.WriteLine();
                foreach (var n in b.Normals)
                    sw.WriteLine("vn " +
                        G(n.X) + " " +
                        G(n.Y) + " " +
                        G(n.Z));
                sw.WriteLine();
                sw.WriteLine($"g {bname}");
                sw.WriteLine($"usemtl {bname}");
                int vb = 1;
                foreach (var t in b.Faces)
                {
                    int a = t.A + vb;
                    int bb = t.B + vb;
                    int c = t.C + vb;
                    sw.WriteLine(
                        $"f {a}/{a}/{a}" +
                        $" {bb}/{bb}/{bb}" +
                        $" {c}/{c}/{c}");
                }
            }
        }

        private void WriteAllObj(
            string outDir, string baseName,
            SortedDictionary<int,
                List<MeshBatch>> groups,
            List<string> tpaths)
        {
            int toolsTid = _toolsTid;
            if (toolsTid < 0)
            {
                WriteGrpObj(outDir,
                    baseName + "_body",
                    groups, tpaths);
                return;
            }
            // Write body combined file
            var body =
                new SortedDictionary<int,
                    List<MeshBatch>>(
                    groups
                        .Where(kv =>
                            kv.Key != toolsTid)
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value));
            if (body.Count > 0)
                WriteGrpObj(outDir,
                    baseName + "_body",
                    body, tpaths);

            // Also write tools subfolder
            // so user can mod tools from
            // _all_obj folder too. Mirrors
            // what WriteObjPerTex does for
            // the _obj folder.
            if (groups.ContainsKey(toolsTid))
            {
                var toolsBatches =
                    groups[toolsTid];
                string toolsTp =
                    toolsTid < tpaths.Count
                    ? tpaths[toolsTid] : null;
                string mn =
                    $"model_{toolsTid:D2}";
                string sub = Path.Combine(
                    outDir, mn);
                string subTex = Path.Combine(
                    sub, "textures");
                Directory.CreateDirectory(
                    subTex);
                string subTp = null;
                if (toolsTp != null &&
                    File.Exists(toolsTp))
                {
                    string dst = Path.Combine(
                        subTex,
                        Path.GetFileName(
                            toolsTp));
                    if (!File.Exists(dst))
                        File.Copy(toolsTp, dst);
                    subTp = dst;
                }
                for (int idx = 0;
                     idx < toolsBatches.Count;
                     idx++)
                {
                    var b = toolsBatches[idx];
                    if (b.Verts.Count == 0 ||
                        b.Faces.Count == 0)
                        continue;
                    WriteBatchObj(
                        sub, idx, b,
                        subTp, toolsTid);
                }
            }
        }

        private void WriteGrpObj(
            string outDir, string name,
            SortedDictionary<int,
                List<MeshBatch>> groups,
            List<string> tpaths)
        {
            if (groups.Count == 0) return;
            string ojp = Path.Combine(
                outDir, name + ".obj");
            string mtp = Path.Combine(
                outDir, name + ".mtl");
            using (var sw = new StreamWriter(
                mtp, false, Encoding.UTF8))
            {
                sw.WriteLine($"# {name} MTL");
                sw.WriteLine();
                foreach (var kv in groups)
                {
                    int tn = kv.Key;
                    string tp = tn < tpaths.Count
                        ? tpaths[tn] : null;
                    sw.WriteLine(
                        $"newmtl mat_{tn:D2}");
                    sw.WriteLine("Ka 1 1 1");
                    sw.WriteLine("Kd 1 1 1");
                    sw.WriteLine("Ks 0 0 0");
                    sw.WriteLine("Ns 10");
                    sw.WriteLine("illum 2");
                    if (tp != null &&
                        File.Exists(tp))
                        sw.WriteLine("map_Kd " +
                            RelPath(tp, ojp));
                    sw.WriteLine();
                }
            }

            using (var sw = new StreamWriter(
            ojp, false, Encoding.UTF8))
            {
                sw.WriteLine($"# {name}");
                sw.WriteLine("mtllib " +
                    Path.GetFileName(mtp));
                sw.WriteLine();
                foreach (var kv in groups)
                    foreach (var b in kv.Value)
                        foreach (var v in b.Verts)
                            sw.WriteLine("v " +
                                G(v.X +
                                  b.SpreadOffset.X)
                                + " " +
                                G(v.Y +
                                  b.SpreadOffset.Y)
                                + " " +
                                G(v.Z +
                                  b.SpreadOffset.Z));
                sw.WriteLine();
                foreach (var kv in groups)
                    foreach (var b in kv.Value)
                        foreach (var uv in b.UVs)
                            sw.WriteLine("vt " +
                                G(uv.U) + " " +
                                G(uv.V));
                sw.WriteLine();
                foreach (var kv in groups)
                    foreach (var b in kv.Value)
                        foreach (var n in
                            b.Normals)
                            sw.WriteLine("vn " +
                                G(n.X) + " " +
                                G(n.Y) + " " +
                                G(n.Z));
                sw.WriteLine();
                int vb = 1, ub = 1, nb = 1;
                foreach (var kv in groups)
                {
                    int tn = kv.Key;
                    foreach (var b in kv.Value)
                    {
                        sw.WriteLine(
                            $"g batch_" +
                            $"{b.Index:D4}");
                        sw.WriteLine(
                            $"usemtl mat_" +
                            $"{tn:D2}");
                        foreach (var t in
                            b.Faces)
                        {
                            int a = t.A + vb;
                            int bb = t.B + vb;
                            int c = t.C + vb;
                            int au = t.A + ub;
                            int bu = t.B + ub;
                            int cu = t.C + ub;
                            int an = t.A + nb;
                            int bn = t.B + nb;
                            int cn = t.C + nb;
                            sw.WriteLine(
                                $"f {a}/{au}/{an}" +
                                $" {bb}/{bu}/{bn}" +
                                $" {c}/{cu}/{cn}");
                        }
                        vb += b.Verts.Count;
                        ub += b.UVs.Count;
                        nb += b.Normals.Count;
                    }
                }
            }
        }


        // ADDITIVE: SRDB-style single OBJ writer
        // for embedded RDTBs. Writes ONE OBJ with
        // all batches and per-batch usemtl mat_NN
        // pointing to the correct texture.
        private void WriteEmbeddedSingleObj(
            string outDir,
            string baseName,
            SortedDictionary<int,
                List<MeshBatch>> groups,
            List<string> tpaths)
        {
            string ojp = Path.Combine(
                outDir, baseName + ".obj");
            string mtp = Path.Combine(
                outDir, baseName + ".mtl");

            // Collect used tex_ids in sorted order
            var usedTex = new List<int>();
            foreach (var kv in groups)
                if (!usedTex.Contains(kv.Key))
                    usedTex.Add(kv.Key);
            usedTex.Sort();

            // Write MTL with one material per tex
            using (var sw = new StreamWriter(
                mtp, false, Encoding.UTF8))
            {
                sw.WriteLine("# " + baseName +
                    " MTL");
                sw.WriteLine();
                foreach (int tn in usedTex)
                {
                    string tp =
                        tn < tpaths.Count
                        ? tpaths[tn] : null;
                    sw.WriteLine(
                        "newmtl mat_" +
                        tn.ToString("D2"));
                    sw.WriteLine("Ka 1 1 1");
                    sw.WriteLine("Kd 1 1 1");
                    sw.WriteLine("Ks 0 0 0");
                    sw.WriteLine("Ns 10");
                    sw.WriteLine("illum 2");
                    if (tp != null &&
                        File.Exists(tp))
                        sw.WriteLine(
                            "map_Kd " +
                            RelPath(tp, ojp));
                    sw.WriteLine();
                }
            }

            // Flatten all batches in original
            // discovery order (preserve mesh
            // batch index ordering)
            var allBatches =
                new List<MeshBatch>();
            foreach (var kv in groups)
                allBatches.AddRange(kv.Value);
            allBatches.Sort((a, b) =>
                a.Index.CompareTo(b.Index));

            // Write OBJ with all verts/uvs/norms
            // then per-batch face groups using
            // correct usemtl mat_NN
            using (var sw = new StreamWriter(
                ojp, false, Encoding.UTF8))
            {
                sw.WriteLine("# " + baseName +
                    " (embedded RDTB, " +
                    allBatches.Count +
                    " batches)");
                sw.WriteLine("mtllib " +
                    Path.GetFileName(mtp));
                sw.WriteLine();

                // Vertices (no spread offset
                // for embedded - keep raw)
                foreach (var b in allBatches)
                    foreach (var v in b.Verts)
                        sw.WriteLine("v " +
                            G(v.X) + " " +
                            G(v.Y) + " " +
                            G(v.Z));
                sw.WriteLine();

                // UVs
                foreach (var b in allBatches)
                    foreach (var uv in b.UVs)
                        sw.WriteLine("vt " +
                            G(uv.U) + " " +
                            G(uv.V));
                sw.WriteLine();

                // Normals
                foreach (var b in allBatches)
                    foreach (var n in b.Normals)
                        sw.WriteLine("vn " +
                            G(n.X) + " " +
                            G(n.Y) + " " +
                            G(n.Z));
                sw.WriteLine();

                // Per-batch face groups with
                // correct usemtl
                int vb = 1, ub = 1, nb = 1;
                foreach (var b in allBatches)
                {
                    sw.WriteLine(
                        "g batch_" +
                        b.Index.ToString("D4"));
                    sw.WriteLine(
                        "usemtl mat_" +
                        b.TexId.ToString("D2"));
                    foreach (var t in b.Faces)
                    {
                        int a = t.A + vb;
                        int bb = t.B + vb;
                        int c = t.C + vb;
                        int au = t.A + ub;
                        int bu = t.B + ub;
                        int cu = t.C + ub;
                        int an = t.A + nb;
                        int bn = t.B + nb;
                        int cn = t.C + nb;
                        sw.WriteLine(
                            "f " +
                            a + "/" + au +
                            "/" + an + " " +
                            bb + "/" + bu +
                            "/" + bn + " " +
                            c + "/" + cu +
                            "/" + cn);
                    }
                    vb += b.Verts.Count;
                    ub += b.UVs.Count;
                    nb += b.Normals.Count;
                }
            }
        }

        // ADDITIVE: SRDB-style single DAE writer
        // for embedded RDTBs. Writes ONE DAE with
        // all batches grouped by texture material.
        private void WriteEmbeddedSingleDae(
            string outDir,
            string baseName,
            SortedDictionary<int,
                List<MeshBatch>> groups,
            List<string> tpaths)
        {
            string daePath = Path.Combine(
                outDir, baseName + ".dae");

            // Collect used tex_ids
            var usedTex = new List<int>();
            foreach (var kv in groups)
                if (!usedTex.Contains(kv.Key))
                    usedTex.Add(kv.Key);
            usedTex.Sort();

            // Flatten batches and collect
            // per-tex face lists
            var allBatches =
                new List<MeshBatch>();
            foreach (var kv in groups)
                allBatches.AddRange(kv.Value);
            allBatches.Sort((a, b) =>
                a.Index.CompareTo(b.Index));

            var av = new List<Vec3>();
            var an_ = new List<Vec3>();
            var au_ = new List<Vec2>();
            var facesByTex =
                new Dictionary<int,
                    List<Tri>>();
            foreach (int tid in usedTex)
                facesByTex[tid] =
                    new List<Tri>();

            int voff = 0;
            foreach (var b in allBatches)
            {
                av.AddRange(b.Verts);
                an_.AddRange(b.Normals);
                au_.AddRange(b.UVs);
                if (!facesByTex
                        .ContainsKey(b.TexId))
                    facesByTex[b.TexId] =
                        new List<Tri>();
                foreach (var t in b.Faces)
                    facesByTex[b.TexId].Add(
                        new Tri(
                            t.A + voff,
                            t.B + voff,
                            t.C + voff));
                voff += b.Verts.Count;
            }

            // Write DAE
            using (var f = new StreamWriter(
                daePath, false, Encoding.UTF8))
            {
                Action<string> W =
                    s => f.Write(s);
                string gid =
                    baseName + "-geom";

                W("<?xml version=\"1.0\"" +
                  " encoding=\"UTF-8\"?>\n");
                W("<COLLADA xmlns=" +
                  "\"http://www.collada.org" +
                  "/2005/11/COLLADASchema\"" +
                  " version=\"1.4.1\">\n");
                W("<asset><up_axis>Y_UP" +
                  "</up_axis></asset>\n");

                // Images
                var imgIds =
                    new Dictionary<int,
                        string>();
                foreach (int tid in usedTex)
                {
                    string tp =
                        tid < tpaths.Count
                        ? tpaths[tid] : null;
                    if (tp != null &&
                        File.Exists(tp))
                        imgIds[tid] =
                            "img-" +
                            tid.ToString("D2");
                }
                if (imgIds.Count > 0)
                {
                    W("<library_images>\n");
                    foreach (var kv in imgIds)
                    {
                        string tp =
                            tpaths[kv.Key];
                        string rel =
                            RelPath(tp,
                                daePath);
                        W("<image id=\"" +
                          kv.Value +
                          "\"><init_from>" +
                          rel +
                          "</init_from>" +
                          "</image>\n");
                    }
                    W("</library_images>\n");
                }

                // Effects
                W("<library_effects>\n");
                foreach (int tid in usedTex)
                {
                    string eid = "eff-" +
                        tid.ToString("D2");
                    string iid = null;
                    imgIds.TryGetValue(
                        tid, out iid);

                    W("<effect id=\"" + eid +
                      "\"><profile_COMMON>\n");
                    if (iid != null)
                    {
                        W("<newparam sid=" +
                          "\"srf" + tid +
                          "\"><surface type=" +
                          "\"2D\"><init_from>" +
                          iid +
                          "</init_from>" +
                          "</surface>" +
                          "</newparam>\n");
                        W("<newparam sid=" +
                          "\"smp" + tid +
                          "\"><sampler2D>" +
                          "<source>srf" +
                          tid + "</source>" +
                          "</sampler2D>" +
                          "</newparam>\n");
                    }
                    W("<technique sid=" +
                      "\"common\"><phong>" +
                      "<diffuse>");
                    if (iid != null)
                        W("<texture texture=" +
                          "\"smp" + tid +
                          "\" texcoord=" +
                          "\"TEX0\"/>");
                    else
                        W("<color>" +
                          "1 1 1 1</color>");
                    W("</diffuse></phong>" +
                      "</technique>\n" +
                      "</profile_COMMON>" +
                      "</effect>\n");
                }
                W("</library_effects>\n");

                // Materials
                W("<library_materials>\n");
                foreach (int tid in usedTex)
                {
                    W("<material id=\"mat-" +
                      tid.ToString("D2") +
                      "\"><instance_effect" +
                      " url=\"#eff-" +
                      tid.ToString("D2") +
                      "\"/></material>\n");
                }
                W("</library_materials>\n");

                // Geometry
                W("<library_geometries>\n");
                W("<geometry id=\"" + gid +
                  "\"><mesh>\n");

                string posStr =
                    string.Join(" ",
                        av.Select(v =>
                            G(v.X) + " " +
                            G(v.Y) + " " +
                            G(v.Z)));
                W("<source id=\"" + gid +
                  "-pos\"><float_array id=\"" +
                  gid + "-pos-arr\" count=\"" +
                  (av.Count * 3) + "\">" +
                  posStr +
                  "</float_array>" +
                  "<technique_common>" +
                  "<accessor source=\"#" +
                  gid + "-pos-arr\" count=\"" +
                  av.Count +
                  "\" stride=\"3\">" +
                  "<param name=\"X\" type=" +
                  "\"float\"/><param name=" +
                  "\"Y\" type=\"float\"/>" +
                  "<param name=\"Z\" type=" +
                  "\"float\"/></accessor>" +
                  "</technique_common>" +
                  "</source>\n");

                bool hasN = an_.Count > 0;
                bool hasU = au_.Count > 0;

                if (hasN)
                {
                    string nStr =
                        string.Join(" ",
                            an_.Select(n =>
                                G(n.X) + " " +
                                G(n.Y) + " " +
                                G(n.Z)));
                    W("<source id=\"" + gid +
                      "-nrm\"><float_array" +
                      " id=\"" + gid +
                      "-nrm-arr\" count=\"" +
                      (an_.Count * 3) +
                      "\">" + nStr +
                      "</float_array>" +
                      "<technique_common>" +
                      "<accessor source=\"#" +
                      gid + "-nrm-arr\"" +
                      " count=\"" +
                      an_.Count +
                      "\" stride=\"3\">" +
                      "<param name=\"X\"" +
                      " type=\"float\"/>" +
                      "<param name=\"Y\"" +
                      " type=\"float\"/>" +
                      "<param name=\"Z\"" +
                      " type=\"float\"/>" +
                      "</accessor>" +
                      "</technique_common>" +
                      "</source>\n");
                }

                if (hasU)
                {
                    string uStr =
                        string.Join(" ",
                            au_.Select(u =>
                                G(u.U) + " " +
                                G(u.V)));
                    W("<source id=\"" + gid +
                      "-uv\"><float_array" +
                      " id=\"" + gid +
                      "-uv-arr\" count=\"" +
                      (au_.Count * 2) +
                      "\">" + uStr +
                      "</float_array>" +
                      "<technique_common>" +
                      "<accessor source=\"#" +
                      gid + "-uv-arr\"" +
                      " count=\"" +
                      au_.Count +
                      "\" stride=\"2\">" +
                      "<param name=\"S\"" +
                      " type=\"float\"/>" +
                      "<param name=\"T\"" +
                      " type=\"float\"/>" +
                      "</accessor>" +
                      "</technique_common>" +
                      "</source>\n");
                }

                W("<vertices id=\"" + gid +
                  "-v\"><input semantic=" +
                  "\"POSITION\" source=\"#" +
                  gid + "-pos\"/>" +
                  "</vertices>\n");

                int stride = 1 +
                    (hasN ? 1 : 0) +
                    (hasU ? 1 : 0);

                foreach (int tid in usedTex)
                {
                    List<Tri> fl;
                    if (!facesByTex
                            .TryGetValue(
                                tid, out fl)
                        || fl.Count == 0)
                        continue;

                    W("<triangles count=\"" +
                      fl.Count +
                      "\" material=\"mat-" +
                      tid.ToString("D2") +
                      "\">\n");
                    W("<input semantic=" +
                      "\"VERTEX\" source=" +
                      "\"#" + gid + "-v\"" +
                      " offset=\"0\"/>\n");
                    if (hasN)
                        W("<input semantic=" +
                          "\"NORMAL\"" +
                          " source=\"#" +
                          gid + "-nrm\"" +
                          " offset=\"1\"/>\n");
                    if (hasU)
                        W("<input semantic=" +
                          "\"TEXCOORD\"" +
                          " source=\"#" +
                          gid + "-uv\"" +
                          " offset=\"" +
                          (hasN ? 2 : 1) +
                          "\" set=\"0\"/>\n");

                    var pv =
                        new StringBuilder();
                    foreach (var t in fl)
                    {
                        if (stride == 3)
                            pv.Append(
                                t.A + " " +
                                t.A + " " +
                                t.A + " " +
                                t.B + " " +
                                t.B + " " +
                                t.B + " " +
                                t.C + " " +
                                t.C + " " +
                                t.C + " ");
                        else if (stride == 2)
                            pv.Append(
                                t.A + " " +
                                t.A + " " +
                                t.B + " " +
                                t.B + " " +
                                t.C + " " +
                                t.C + " ");
                        else
                            pv.Append(
                                t.A + " " +
                                t.B + " " +
                                t.C + " ");
                    }
                    W("<p>" +
                      pv.ToString().Trim() +
                      "</p>\n" +
                      "</triangles>\n");
                }

                W("</mesh></geometry>\n" +
                  "</library_geometries>\n");

                W("<library_visual_scenes>" +
                  "<visual_scene id=" +
                  "\"Scene\">\n" +
                  "<node id=\"" + baseName +
                  "\"><instance_geometry" +
                  " url=\"#" + gid + "\">\n" +
                  "<bind_material>" +
                  "<technique_common>\n");

                foreach (int tid in usedTex)
                {
                    W("<instance_material" +
                      " symbol=\"mat-" +
                      tid.ToString("D2") +
                      "\" target=\"#mat-" +
                      tid.ToString("D2") +
                      "\">");
                    if (hasU)
                        W("<bind_vertex_input" +
                          " semantic=\"TEX0\"" +
                          " input_semantic=" +
                          "\"TEXCOORD\"" +
                          " input_set=" +
                          "\"0\"/>");
                    W("</instance_material>\n");
                }

                W("</technique_common>" +
                  "</bind_material>\n" +
                  "</instance_geometry>" +
                  "</node>\n" +
                  "</visual_scene>" +
                  "</library_visual_scenes>\n" +
                  "<scene>" +
                  "<instance_visual_scene" +
                  " url=\"#Scene\"/>" +
                  "</scene>\n" +
                  "</COLLADA>\n");
            }
        }

        private void WriteDaePerTex(
            string outDir,
            SortedDictionary<int,
            List<MeshBatch>> groups,
        List<string> tpaths)
        {
            int toolsTid = _toolsTid;
            foreach (var kv in groups)
            {
                int tn = kv.Key;
                var bl = kv.Value;
                string tp = tn < tpaths.Count
                    ? tpaths[tn] : null;
                string mn = $"model_{tn:D2}";
                string dap = Path.Combine(
                    outDir, mn + ".dae");
                var av = new List<Vec3>();
                var an_ = new List<Vec3>();
                var au_ = new List<Vec2>();
                var fc = new List<Tri>();
                int vo = 0;
                foreach (var b in bl)
                {
                    foreach (var v in b.Verts)
                        av.Add(new Vec3(
                            v.X +
                            b.SpreadOffset.X,
                            v.Y +
                            b.SpreadOffset.Y,
                            v.Z +
                            b.SpreadOffset.Z));
                    an_.AddRange(b.Normals);
                    au_.AddRange(b.UVs);
                    foreach (var t in b.Faces)
                        fc.Add(new Tri(
                            t.A + vo,
                            t.B + vo,
                            t.C + vo));
                    vo += b.Verts.Count;
                }

                if (tn != toolsTid)
                {
                    // Non-tools: write
                    // combined DAE file
                    WriteDaeSingle(
                        dap, mn, av, an_,
                        au_, fc, tp);
                }
                else
                {
                    // Tools: write per-batch
                    // DAE subfolder only
                    string sub = Path.Combine(
                        outDir, mn);
                    Directory.CreateDirectory(
                        sub);
                    string subTex =
                        Path.Combine(
                            sub, "textures");
                    Directory.CreateDirectory(
                        subTex);
                    string subTp = null;
                    if (tp != null &&
                        File.Exists(tp))
                    {
                        string dst =
                            Path.Combine(
                                subTex,
                                Path.GetFileName(
                                    tp));
                        if (!File.Exists(dst))
                            File.Copy(tp, dst);
                        subTp = dst;
                    }
                    for (int idx = 0;
                         idx < bl.Count;
                         idx++)
                    {
                        var b = bl[idx];
                        if (b.Verts.Count == 0
                            || b.Faces.Count
                            == 0)
                            continue;
                        string bname =
                            $"batch_{idx:D3}";
                        string bdap =
                            Path.Combine(
                                sub,
                                bname + ".dae");
                        float cx_ = 0f;
                        float cy_ = 0f;
                        float cz_ = 0f;
                        if (b.Verts.Count > 0)
                        {
                            float mnx =
                                float.MaxValue;
                            float mxx =
                                float.MinValue;
                            float mny =
                                float.MaxValue;
                            float mxy =
                                float.MinValue;
                            float mnz =
                                float.MaxValue;
                            float mxz =
                                float.MinValue;
                            foreach (var v in
                                b.Verts)
                            {
                                if (v.X < mnx)
                                    mnx = v.X;
                                if (v.X > mxx)
                                    mxx = v.X;
                                if (v.Y < mny)
                                    mny = v.Y;
                                if (v.Y > mxy)
                                    mxy = v.Y;
                                if (v.Z < mnz)
                                    mnz = v.Z;
                                if (v.Z > mxz)
                                    mxz = v.Z;
                            }
                            cx_ = (mnx + mxx)
                                * 0.5f;
                            cy_ = (mny + mxy)
                                * 0.5f;
                            cz_ = (mnz + mxz)
                                * 0.5f;
                        }
                        var cv =
                            new List<Vec3>();
                        foreach (var v in
                            b.Verts)
                            cv.Add(new Vec3(
                                v.X - cx_,
                                v.Y - cy_,
                                v.Z - cz_));
                        WriteDaeSingle(
                            bdap, bname,
                            cv, b.Normals,
                            b.UVs, b.Faces,
                            subTp);
                    }
                }
            }
        }

        private void WriteAllDae(
            string outDir, string baseName,
            SortedDictionary<int,
                List<MeshBatch>> groups,
            List<string> tpaths)
        {
            int toolsTid = _toolsTid;
            if (toolsTid < 0)
            {
                BuildAllDae(
                    Path.Combine(outDir,
                        baseName + "_body.dae"),
                    groups, tpaths);
                return;
            }
            // Only write body combined DAE.
            // Tools are written as per-batch
            // files in WriteDaePerTex via the
            // model_NN/ subfolder.
            var body =
                new SortedDictionary<int,
                    List<MeshBatch>>(
                    groups
                        .Where(kv =>
                            kv.Key != toolsTid)
                        .ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value));
            if (body.Count > 0)
                BuildAllDae(
                    Path.Combine(outDir,
                        baseName + "_body.dae"),
                    body, tpaths);
            // Also write tools subfolder
            // for _all_dae folder
            if (toolsTid >= 0 &&
                groups.ContainsKey(toolsTid))
            {
                var toolsBatches =
                    groups[toolsTid];
                string toolsTp =
                    toolsTid < tpaths.Count
                    ? tpaths[toolsTid] : null;
                string mn =
                    $"model_{toolsTid:D2}";
                string sub = Path.Combine(
                    outDir, mn);
                Directory.CreateDirectory(sub);
                string subTex = Path.Combine(
                    sub, "textures");
                Directory.CreateDirectory(
                    subTex);
                string subTp = null;
                if (toolsTp != null &&
                    File.Exists(toolsTp))
                {
                    string dst = Path.Combine(
                        subTex,
                        Path.GetFileName(
                            toolsTp));
                    if (!File.Exists(dst))
                        File.Copy(toolsTp, dst);
                    subTp = dst;
                }
                for (int idx = 0;
                     idx < toolsBatches.Count;
                     idx++)
                {
                    var b = toolsBatches[idx];
                    if (b.Verts.Count == 0 ||
                        b.Faces.Count == 0)
                        continue;
                    string bname =
                        $"batch_{idx:D3}";
                    string bdap = Path.Combine(
                        sub, bname + ".dae");
                    float cx_ = 0f;
                    float cy_ = 0f;
                    float cz_ = 0f;
                    if (b.Verts.Count > 0)
                    {
                        float mnx =
                            float.MaxValue;
                        float mxx =
                            float.MinValue;
                        float mny =
                            float.MaxValue;
                        float mxy =
                            float.MinValue;
                        float mnz =
                            float.MaxValue;
                        float mxz =
                            float.MinValue;
                        foreach (var v in
                            b.Verts)
                        {
                            if (v.X < mnx)
                                mnx = v.X;
                            if (v.X > mxx)
                                mxx = v.X;
                            if (v.Y < mny)
                                mny = v.Y;
                            if (v.Y > mxy)
                                mxy = v.Y;
                            if (v.Z < mnz)
                                mnz = v.Z;
                            if (v.Z > mxz)
                                mxz = v.Z;
                        }
                        cx_ = (mnx + mxx)
                            * 0.5f;
                        cy_ = (mny + mxy)
                            * 0.5f;
                        cz_ = (mnz + mxz)
                            * 0.5f;
                    }
                    var cv = new List<Vec3>();
                    foreach (var v in b.Verts)
                        cv.Add(new Vec3(
                            v.X - cx_,
                            v.Y - cy_,
                            v.Z - cz_));
                    WriteDaeSingle(
                        bdap, bname,
                        cv, b.Normals,
                        b.UVs, b.Faces,
                        subTp);
                }
            }
        }

        private void BuildAllDae(
            string path,
            SortedDictionary<int,
                List<MeshBatch>> grps,
            List<string> tpaths)
        {
            var av = new List<Vec3>();
            var an_ = new List<Vec3>();
            var au_ = new List<Vec2>();
            var ft = new Dictionary<int,
                List<Tri>>();
            int vo = 0;
            foreach (var kv in grps)
            {
                if (!ft.ContainsKey(kv.Key))
                    ft[kv.Key] =
                        new List<Tri>();
                foreach (var b in kv.Value)
                {
                    foreach (var v in b.Verts)
                        av.Add(new Vec3(
                            v.X +
                            b.SpreadOffset.X,
                            v.Y +
                            b.SpreadOffset.Y,
                            v.Z +
                            b.SpreadOffset.Z));
                    an_.AddRange(b.Normals);
                    au_.AddRange(b.UVs);
                    foreach (var t in b.Faces)
                        ft[kv.Key].Add(new Tri(
                            t.A + vo,
                            t.B + vo,
                            t.C + vo));
                    vo += b.Verts.Count;
                }
            }
            WriteDaeMulti(path, av, an_,
                au_, ft, grps, tpaths);
        }

        private void WriteDaeSingle(
            string path, string name,
            List<Vec3> verts,
            List<Vec3> normals,
            List<Vec2> uvs,
            List<Tri> faces, string tp)
        {
            string imgId = name + "-img";
            string matId = name + "-mat";
            string effId = name + "-eff";
            string geomId = name + "-geom";
            using (var f = new StreamWriter(
                path, false, Encoding.UTF8))
            {
                Action<string> W = s => f.Write(s);
                W("<?xml version=\"1.0\"" +
                  " encoding=\"UTF-8\"?>\n");
                W("<COLLADA xmlns=" +
                  "\"http://www.collada.org" +
                  "/2005/11/COLLADASchema\"" +
                  " version=\"1.4.1\">\n");
                W("<asset><up_axis>Y_UP" +
                  "</up_axis></asset>\n");
                bool hasTex = tp != null &&
                    File.Exists(tp);
                if (hasTex)
                {
                    string rel = RelPath(
                        tp, path);
                    W("<library_images>\n");
                    W($"<image id=\"{imgId}\"" +
                      $" name=\"{imgId}\">\n");
                    W($"<init_from>{rel}" +
                      "</init_from>\n");
                    W("</image>\n" +
                      "</library_images>\n");
                }
                W("<library_effects>\n");
                W($"<effect id=\"{effId}\">" +
                  "<profile_COMMON>\n");
                if (hasTex)
                {
                    W("<newparam sid=" +
                      "\"surface0\">" +
                      "<surface type=\"2D\">" +
                      $"<init_from>{imgId}" +
                      "</init_from></surface>" +
                      "</newparam>\n");
                    W("<newparam sid=" +
                      "\"sampler0\">" +
                      "<sampler2D>" +
                      "<source>surface0" +
                      "</source>" +
                      "</sampler2D>" +
                      "</newparam>\n");
                }
                W("<technique sid=\"common\">" +
                  "<phong><diffuse>");
                if (hasTex)
                    W("<texture texture=" +
                      "\"sampler0\"" +
                      " texcoord=\"TEX0\"/>");
                else
                    W("<color>1 1 1 1</color>");
                W("</diffuse></phong>" +
                  "</technique>\n" +
                  "</profile_COMMON>" +
                  "</effect>\n" +
                  "</library_effects>\n");
                W("<library_materials>\n");
                W($"<material id=\"{matId}\"" +
                  $" name=\"{matId}\">" +
                  $"<instance_effect" +
                  $" url=\"#{effId}\"/>" +
                  "</material>\n" +
                  "</library_materials>\n");

                W("<library_geometries>\n");
                W($"<geometry id=\"{geomId}\"" +
                  $" name=\"{name}\"><mesh>\n");
                string posArr = string.Join(" ",
                    verts.Select(v =>
                        G(v.X) + " " +
                        G(v.Y) + " " + G(v.Z)));
                W($"<source id=\"{geomId}-pos\">" +
                  $"<float_array id=" +
                  $"\"{geomId}-pos-arr\"" +
                  $" count=\"{verts.Count * 3}\">" +
                  posArr + "</float_array>" +
                  "<technique_common>" +
                  $"<accessor source=" +
                  $"\"#{geomId}-pos-arr\"" +
                  $" count=\"{verts.Count}\"" +
                  " stride=\"3\">" +
                  "<param name=\"X\"" +
                  " type=\"float\"/>" +
                  "<param name=\"Y\"" +
                  " type=\"float\"/>" +
                  "<param name=\"Z\"" +
                  " type=\"float\"/>" +
                  "</accessor>" +
                  "</technique_common>" +
                  "</source>\n");
                bool hasN = normals.Count > 0;
                bool hasU = uvs.Count > 0;
                if (hasN)
                {
                    string nArr = string.Join(" ",
                        normals.Select(n =>
                            G(n.X) + " " +
                            G(n.Y) + " " +
                            G(n.Z)));
                    W($"<source id=" +
                      $"\"{geomId}-nrm\">" +
                      $"<float_array id=" +
                      $"\"{geomId}-nrm-arr\"" +
                      $" count=" +
                      $"\"{normals.Count * 3}\">" +
                      nArr + "</float_array>" +
                      "<technique_common>" +
                      $"<accessor source=" +
                      $"\"#{geomId}-nrm-arr\"" +
                      $" count=" +
                      $"\"{normals.Count}\"" +
                      " stride=\"3\">" +
                      "<param name=\"X\"" +
                      " type=\"float\"/>" +
                      "<param name=\"Y\"" +
                      " type=\"float\"/>" +
                      "<param name=\"Z\"" +
                      " type=\"float\"/>" +
                      "</accessor>" +
                      "</technique_common>" +
                      "</source>\n");
                }
                if (hasU)
                {
                    string uArr = string.Join(" ",
                        uvs.Select(u =>
                            G(u.U) + " " +
                            G(u.V)));
                    W($"<source id=" +
                      $"\"{geomId}-uv\">" +
                      $"<float_array id=" +
                      $"\"{geomId}-uv-arr\"" +
                      $" count=" +
                      $"\"{uvs.Count * 2}\">" +
                      uArr + "</float_array>" +
                      "<technique_common>" +
                      $"<accessor source=" +
                      $"\"#{geomId}-uv-arr\"" +
                      $" count=" +
                      $"\"{uvs.Count}\"" +
                      " stride=\"2\">" +
                      "<param name=\"S\"" +
                      " type=\"float\"/>" +
                      "<param name=\"T\"" +
                      " type=\"float\"/>" +
                      "</accessor>" +
                      "</technique_common>" +
                      "</source>\n");
                }

                W($"<vertices id=" +
  $"\"{geomId}-verts\">" +
  "<input semantic=" +
  "\"POSITION\"" +
  $" source=\"#{geomId}-pos\"/>" +
  "</vertices>\n");
                int stride = 1 +
                    (hasN ? 1 : 0) +
                    (hasU ? 1 : 0);
                W($"<triangles count=" +
                  $"\"{faces.Count}\"" +
                  $" material=\"{matId}\">\n");
                W("<input semantic=\"VERTEX\"" +
                  $" source=\"#{geomId}-verts\"" +
                  " offset=\"0\"/>\n");
                if (hasN)
                    W("<input semantic=" +
                      "\"NORMAL\"" +
                      $" source=" +
                      $"\"#{geomId}-nrm\"" +
                      " offset=\"1\"/>\n");
                if (hasU)
                    W("<input semantic=" +
                      "\"TEXCOORD\"" +
                      $" source=" +
                      $"\"#{geomId}-uv\"" +
                      $" offset=" +
                      $"\"{(hasN ? 2 : 1)}\"" +
                      " set=\"0\"/>\n");
                var pv = new StringBuilder();
                foreach (var t in faces)
                {
                    if (stride == 3)
                        pv.Append(
                            $"{t.A} {t.A}" +
                            $" {t.A} " +
                            $"{t.B} {t.B}" +
                            $" {t.B} " +
                            $"{t.C} {t.C}" +
                            $" {t.C} ");
                    else if (stride == 2)
                        pv.Append(
                            $"{t.A} {t.A} " +
                            $"{t.B} {t.B} " +
                            $"{t.C} {t.C} ");
                    else
                        pv.Append(
                            $"{t.A} {t.B}" +
                            $" {t.C} ");
                }
                W("<p>" +
                  pv.ToString().Trim() +
                  "</p>\n</triangles>\n" +
                  "</mesh></geometry>\n" +
                  "</library_geometries>\n");
                W("<library_visual_scenes>" +
                  "<visual_scene id=" +
                  "\"Scene\" name=\"Scene\">\n" +
                  $"<node id=\"{name}\"" +
                  $" name=\"{name}\"" +
                  " type=\"NODE\">\n" +
                  "<instance_geometry url=" +
                  $"\"#{geomId}\">\n" +
                  "<bind_material>" +
                  "<technique_common>\n" +
                  $"<instance_material symbol=" +
                  $"\"{matId}\" target=" +
                  $"\"#{matId}\">\n");
                if (hasU)
                    W("<bind_vertex_input" +
                      " semantic=\"TEX0\"" +
                      " input_semantic=" +
                      "\"TEXCOORD\"" +
                      " input_set=\"0\"/>\n");
                W("</instance_material>\n" +
                  "</technique_common>" +
                  "</bind_material>\n" +
                  "</instance_geometry>\n" +
                  "</node></visual_scene>\n" +
                  "</library_visual_scenes>\n" +
                  "<scene><instance_visual_scene" +
                  " url=\"#Scene\"/>" +
                  "</scene>\n</COLLADA>\n");
            }
        }

        private void WriteDaeMulti(
    string path,
    List<Vec3> av,
    List<Vec3> an_,
    List<Vec2> au_,
    Dictionary<int, List<Tri>> ft,
    SortedDictionary<int,
        List<MeshBatch>> groups,
    List<string> tpaths)
        {
            using (var f = new StreamWriter(
                path, false, Encoding.UTF8))
            {
                Action<string> W = s => f.Write(s);
                string gid = "geom";
                W("<?xml version=\"1.0\"" +
                  " encoding=\"UTF-8\"?>\n");
                W("<COLLADA xmlns=" +
                  "\"http://www.collada.org" +
                  "/2005/11/COLLADASchema\"" +
                  " version=\"1.4.1\">\n");
                W("<asset><up_axis>Y_UP" +
                  "</up_axis></asset>\n");
                var imgIds =
                    new Dictionary<int,
                        string>();
                foreach (var kv in ft)
                {
                    int tn = kv.Key;
                    string tp = tn < tpaths.Count
                        ? tpaths[tn] : null;
                    if (tp != null &&
                        File.Exists(tp))
                        imgIds[tn] =
                            $"img-{tn:D2}";
                }
                if (imgIds.Count > 0)
                {
                    W("<library_images>\n");
                    foreach (var kv in imgIds)
                    {
                        string tp =
                            tpaths[kv.Key];
                        string rel =
                            RelPath(tp, path);
                        W($"<image id=" +
                          $"\"{kv.Value}\">" +
                          $"<init_from>{rel}" +
                          "</init_from>" +
                          "</image>\n");
                    }
                    W("</library_images>\n");
                }

                W("<library_effects>\n");
                foreach (var kv in ft)
                {
                    int tn = kv.Key;
                    string eid = $"eff-{tn:D2}";
                    string iid = null;
                    imgIds.TryGetValue(
                        tn, out iid);
                    W($"<effect id=\"{eid}\">" +
                      "<profile_COMMON>\n");
                    if (iid != null)
                    {
                        W($"<newparam sid=" +
                          $"\"srf{tn}\">" +
                          "<surface type=\"2D\">" +
                          $"<init_from>{iid}" +
                          "</init_from></surface>" +
                          "</newparam>\n");
                        W($"<newparam sid=" +
                          $"\"smp{tn}\">" +
                          "<sampler2D>" +
                          $"<source>srf{tn}" +
                          "</source>" +
                          "</sampler2D>" +
                          "</newparam>\n");
                    }
                    W("<technique sid=\"common\">" +
                      "<phong><diffuse>");
                    if (iid != null)
                        W($"<texture texture=" +
                          $"\"smp{tn}\"" +
                          " texcoord=\"TEX0\"/>");
                    else
                        W("<color>1 1 1 1" +
                          "</color>");
                    W("</diffuse></phong>" +
                      "</technique>\n" +
                      "</profile_COMMON>" +
                      "</effect>\n");
                }
                W("</library_effects>\n");
                W("<library_materials>\n");
                foreach (var kv in ft)
                {
                    int tn = kv.Key;
                    W($"<material id=" +
                      $"\"mat-{tn:D2}\">" +
                      "<instance_effect url=" +
                      $"\"#eff-{tn:D2}\"/>" +
                      "</material>\n");
                }
                W("</library_materials>\n");

                W("<library_geometries>\n");
                W($"<geometry id=\"{gid}\">" +
                  "<mesh>\n");
                string posStr = string.Join(" ",
                    av.Select(v =>
                        G(v.X) + " " +
                        G(v.Y) + " " + G(v.Z)));
                W($"<source id=\"{gid}-pos\">" +
                  $"<float_array id=" +
                  $"\"{gid}-pos-arr\"" +
                  $" count=\"{av.Count * 3}\">" +
                  posStr + "</float_array>" +
                  "<technique_common>" +
                  $"<accessor source=" +
                  $"\"#{gid}-pos-arr\"" +
                  $" count=\"{av.Count}\"" +
                  " stride=\"3\">" +
                  "<param name=\"X\"" +
                  " type=\"float\"/>" +
                  "<param name=\"Y\"" +
                  " type=\"float\"/>" +
                  "<param name=\"Z\"" +
                  " type=\"float\"/>" +
                  "</accessor>" +
                  "</technique_common>" +
                  "</source>\n");
                if (an_.Count > 0)
                {
                    string nStr = string.Join(" ",
                        an_.Select(n =>
                            G(n.X) + " " +
                            G(n.Y) + " " +
                            G(n.Z)));
                    W($"<source id=" +
                      $"\"{gid}-nrm\">" +
                      $"<float_array id=" +
                      $"\"{gid}-nrm-arr\"" +
                      $" count=\"{an_.Count * 3}\">" +
                      nStr + "</float_array>" +
                      "<technique_common>" +
                      $"<accessor source=" +
                      $"\"#{gid}-nrm-arr\"" +
                      $" count=\"{an_.Count}\"" +
                      " stride=\"3\">" +
                      "<param name=\"X\"" +
                      " type=\"float\"/>" +
                      "<param name=\"Y\"" +
                      " type=\"float\"/>" +
                      "<param name=\"Z\"" +
                      " type=\"float\"/>" +
                      "</accessor>" +
                      "</technique_common>" +
                      "</source>\n");
                }
                if (au_.Count > 0)
                {
                    string uStr = string.Join(" ",
                        au_.Select(u =>
                            G(u.U) + " " +
                            G(u.V)));
                    W($"<source id=" +
                      $"\"{gid}-uv\">" +
                      $"<float_array id=" +
                      $"\"{gid}-uv-arr\"" +
                      $" count=\"{au_.Count * 2}\">" +
                      uStr + "</float_array>" +
                      "<technique_common>" +
                      $"<accessor source=" +
                      $"\"#{gid}-uv-arr\"" +
                      $" count=\"{au_.Count}\"" +
                      " stride=\"2\">" +
                      "<param name=\"S\"" +
                      " type=\"float\"/>" +
                      "<param name=\"T\"" +
                      " type=\"float\"/>" +
                      "</accessor>" +
                      "</technique_common>" +
                      "</source>\n");
                }

                W($"<vertices id=\"{gid}-v\">" +
  "<input semantic=\"POSITION\"" +
  $" source=\"#{gid}-pos\"/>" +
  "</vertices>\n");
                bool hasN = an_.Count > 0;
                bool hasU = au_.Count > 0;
                int stride2 = 1 +
                    (hasN ? 1 : 0) +
                    (hasU ? 1 : 0);
                foreach (var kv in ft)
                {
                    int tn = kv.Key;
                    var fl = kv.Value;
                    if (fl.Count == 0) continue;
                    W($"<triangles count=" +
                      $"\"{fl.Count}\"" +
                      $" material=\"mat-" +
                      $"{tn:D2}\">\n");
                    W("<input semantic=\"VERTEX\"" +
                      $" source=\"#{gid}-v\"" +
                      " offset=\"0\"/>\n");
                    if (hasN)
                        W("<input semantic=" +
                          "\"NORMAL\" source=" +
                          $"\"#{gid}-nrm\"" +
                          " offset=\"1\"/>\n");
                    if (hasU)
                        W("<input semantic=" +
                          "\"TEXCOORD\" source=" +
                          $"\"#{gid}-uv\" offset=" +
                          $"\"{(hasN ? 2 : 1)}\"" +
                          " set=\"0\"/>\n");
                    var pv = new StringBuilder();
                    foreach (var t in fl)
                    {
                        if (stride2 == 3)
                            pv.Append(
                                $"{t.A} {t.A}" +
                                $" {t.A} " +
                                $"{t.B} {t.B}" +
                                $" {t.B} " +
                                $"{t.C} {t.C}" +
                                $" {t.C} ");
                        else if (stride2 == 2)
                            pv.Append(
                                $"{t.A} {t.A} " +
                                $"{t.B} {t.B} " +
                                $"{t.C} {t.C} ");
                        else
                            pv.Append(
                                $"{t.A} {t.B}" +
                                $" {t.C} ");
                    }
                    W("<p>" +
                      pv.ToString().Trim() +
                      "</p>\n</triangles>\n");
                }
                W("</mesh></geometry>\n" +
                  "</library_geometries>\n");
                W("<library_visual_scenes>" +
                  "<visual_scene id=\"Scene\">\n" +
                  "<node id=\"root\">" +
                  $"<instance_geometry url=" +
                  $"\"#{gid}\">\n" +
                  "<bind_material>" +
                  "<technique_common>\n");
                foreach (var kv in ft)
                {
                    int tn = kv.Key;
                    W($"<instance_material symbol=" +
                      $"\"mat-{tn:D2}\" target=" +
                      $"\"#mat-{tn:D2}\">");
                    if (hasU)
                        W("<bind_vertex_input" +
                          " semantic=\"TEX0\"" +
                          " input_semantic=" +
                          "\"TEXCOORD\"" +
                          " input_set=\"0\"/>");
                    W("</instance_material>\n");
                }
                W("</technique_common>" +
                  "</bind_material>\n" +
                  "</instance_geometry></node>\n" +
                  "</visual_scene>" +
                  "</library_visual_scenes>\n" +
                  "<scene><instance_visual_scene" +
                  " url=\"#Scene\"/>" +
                  "</scene>\n</COLLADA>\n");
            }
        }

        private void WriteManifest(
    string outFolder,
    string baseName,
    string rdtbPath,
    string gdtbPath,
    List<MeshBatch> batches,
    SortedDictionary<int,
        List<MeshBatch>> groups,
    int mci)
        {
            string mfp = Path.Combine(
                outFolder,
                "rebuild_manifest.json");
            string rc = Path.Combine(
                outFolder, "_source.rdtb");
            if (!File.Exists(rc))
                File.Copy(rdtbPath, rc);
            string gc = "";
            if (gdtbPath != null &&
                File.Exists(gdtbPath))
            {
                gc = Path.Combine(
                    outFolder, "_source.gdtb");
                if (!File.Exists(gc))
                    File.Copy(gdtbPath, gc);
            }
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(
                "  \"tool\":" +
                " \"HMSTHModdingTool v2.1\",");
            sb.AppendLine(
                "  \"version\": \"2.1\",");
            sb.AppendLine(
                "  \"lod_sync_available\": true,");
            sb.AppendLine(
                "  \"mesh_chunk_idx\": " +
                mci + ",");
            sb.AppendLine(
                "  \"all_mesh_chunks\": [");
            for (int i = 0;
                 i < _allMeshChunks.Count; i++)
            {
                int ci = _allMeshChunks[i];
                bool last = i ==
                    _allMeshChunks.Count - 1;
                int co = ci < _chunkOffsets.Count
                    ? _chunkOffsets[ci] : 0;
                int cs = ci < _chunks.Count
                    ? _chunks[ci].Length : 0;
                sb.AppendLine(
                    "    {\"index\":" + ci +
                    ",\"offset\":" + co +
                    ",\"size\":" + cs +
                    "}" + (last ? "" : ","));
            }
            sb.AppendLine("  ],");
            sb.AppendLine(
                "  \"source_rdtb\":" +
                " \"_source.rdtb\",");
            sb.AppendLine(
                "  \"source_gdtb\": \"" +
                (gc != "" ? "_source.gdtb" : "")
                + "\",");
            sb.AppendLine(
                "  \"original_rdtb_name\": \"" +
                Path.GetFileName(rdtbPath) +
                "\",");
            sb.AppendLine(
                "  \"original_gdtb_name\": \"" +
                (gdtbPath != null
                    ? Path.GetFileName(gdtbPath)
                    : "") + "\",");
            sb.AppendLine(
                "  \"source_size\": " +
                _data.Length + ",");
            int c11off = mci <
                _chunkOffsets.Count
                ? _chunkOffsets[mci] : 0;
            int c11sz = mci < _chunks.Count
                ? _chunks[mci].Length : 0;
            sb.AppendLine(
                "  \"chunk11_offset\": " +
                c11off + ",");
            sb.AppendLine(
                "  \"chunk11_size\": " +
                c11sz + ",");
            sb.AppendLine("  \"batches\": [");
            for (int i = 0;
                 i < batches.Count; i++)
            {
                var b = batches[i];
                bool last =
                    i == batches.Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine(
                    "      \"index\": " +
                    b.Index + ",");
                sb.AppendLine(
                    "      \"tex_id\": " +
                    b.TexId + ",");
                sb.AppendLine(
                    "      \"source_chunk\": " +
                    b.SourceChunkIdx + ",");
                sb.AppendLine(
                    "      \"chunk_offset\": " +
                    b.Offset + ",");
                sb.AppendLine(
                    "      \"vertex_count\": " +
                    b.Verts.Count + ",");
                sb.AppendLine(
                    "      \"face_count\": " +
                    b.Faces.Count + ",");
                sb.AppendLine(
                    "      \"obj_vert_start\":" +
                    " " + b.ObjVertStart + ",");
                sb.AppendLine(
                    "      \"obj_vert_end\":" +
                    " " + b.ObjVertEnd + ",");
                sb.AppendLine(
                    "      \"spread_offset\":" +
                    " [" +
                    G(b.SpreadOffset.X) + "," +
                    G(b.SpreadOffset.Y) + "," +
                    G(b.SpreadOffset.Z) + "],");
                sb.AppendLine(
                    "      \"bone_offset\": [" +
                    G(b.BoneOffset.X) + "," +
                    G(b.BoneOffset.Y) + "," +
                    G(b.BoneOffset.Z) + "],");

                // local_centroid goes HERE, BEFORE vif_blocks
                Vec3 lc;
                if (_batchCentroids.TryGetValue(
                        b.Index, out lc))
                {
                    sb.AppendLine(
                        "      \"local_centroid\": [" +
                        G(lc.X) + "," +
                        G(lc.Y) + "," +
                        G(lc.Z) + "],");
                }
                else
                {
                    sb.AppendLine(
                        "      \"local_centroid\":" +
                        " [0,0,0],");
                }

                sb.AppendLine(
                    "      \"vif_blocks\": [");

                for (int bi = 0;
                     bi < b.Blocks.Count; bi++)
                {
                    var blk = b.Blocks[bi];
                    bool bLast =
                        bi == b.Blocks.Count - 1;
                    sb.AppendLine(
                        "        {" +
                        "\"chunk_offset\":" +
                        blk.OffsetInChunk +
                        ",\"vertex_count\":" +
                        blk.VertexCount +
                        ",\"first_vertex\":" +
                        blk.FirstVertex +
                        "}" + (bLast ? "" : ","));
                }
                sb.AppendLine("      ],");


                List<LODSiblingInfo> sibs = null;
                _lodPairings.TryGetValue(
                    b.Index, out sibs);
                sb.AppendLine(
                    "      \"lod_siblings\": [");
                if (sibs != null &&
                    sibs.Count > 0)
                {
                    for (int si = 0;
                         si < sibs.Count; si++)
                    {
                        var s = sibs[si];
                        bool sLast =
                            si == sibs.Count - 1;
                        sb.AppendLine(
                            "        {");
                        sb.AppendLine(
                            "          " +
                            "\"chunk_idx\": " +
                            s.ChunkIdx + ",");
                        sb.AppendLine(
                            "          " +
                            "\"batch_index\": " +
                            s.BatchIndex + ",");
                        sb.AppendLine(
                            "          " +
                            "\"chunk_offset\":" +
                            " " +
                            s.ChunkOffset + ",");
                        sb.AppendLine(
                            "          " +
                            "\"vertex_count\":" +
                            " " +
                            s.VertexCount + ",");
                        sb.AppendLine(
                            "          " +
                            "\"vif_blocks\": [");
                        for (int vi = 0;
                             vi <
                             s.VifBlocks.Count;
                             vi++)
                        {
                            var (off, vc) =
                                s.VifBlocks[vi];
                            bool vLast = vi ==
                                s.VifBlocks
                                    .Count - 1;
                            sb.AppendLine(
                                "            {" +
                                "\"offset\":" +
                                off +
                                ",\"vc\":" +
                                vc + "}" +
                                (vLast ? "" : ","));
                        }
                        sb.AppendLine(
                            "          ]");
                        sb.AppendLine(
                            "        }" +
                            (sLast ? "" : ","));
                    }
                }
                sb.AppendLine("      ]");
                sb.AppendLine(
                    "    }" + (last ? "" : ","));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(
                mfp, sb.ToString(),
                Encoding.UTF8);
        }
    }

    internal class RDTB3DCreator
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private ManifestData3D _manifest;
        private float _scale = 1.0f;
        private byte[] _originalRdtb;

        public void DoCreate(
            string folder,
            string outFolder,
            float scale)
        {
            _scale = scale;
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] HMSTH 3D Creator v2.1");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 60));
            Console.WriteLine(
                "    Folder : " + folder);
            Console.WriteLine(
                "    Output : " + outFolder);
            if (scale != 1.0f)
                Console.WriteLine(
                    "    Scale  : " +
                    scale + "x");
            Console.WriteLine(
                "    LOD sync: ENABLED");
            Console.WriteLine(
                new string('=', 60));
            string mfp = Path.Combine(
                folder,
                "rebuild_manifest.json");
            if (!File.Exists(mfp))
                throw new FileNotFoundException(
                    "rebuild_manifest.json" +
                    " not found in: " +
                    folder);
            _manifest = LoadManifest(mfp);
            Console.WriteLine(
                "    Batches: " +
                _manifest.Batches.Count);
            string src = Path.Combine(
                folder,
                _manifest.SourceRdtb);
            if (!File.Exists(src))
                throw new FileNotFoundException(
                    "Source RDTB not found: " +
                    src);
            byte[] rdtbData =
                File.ReadAllBytes(src);
            _originalRdtb = rdtbData;
            // Start with byte-perfect copy
            // of original. Only modified
            // vertices will overwrite.
            var modified =
                new byte[rdtbData.Length];
            Array.Copy(rdtbData, modified,
                rdtbData.Length);
            // Track whether ANY OBJ load
            // actually contributes data
            bool anyModelLoaded = false;
            var texObjs =
                new Dictionary<int,
                    ParsedObj>();
            LoadObjFiles(folder, texObjs);
            anyModelLoaded =
                texObjs.Count > 0;
            // No OBJ files = byte-perfect
            // roundtrip (no

            int exact = 0, fixed_ = 0,
            padded = 0, skipped = 0,
            nomdl = 0, lodSynced = 0;
            var batchDataMap =
                new Dictionary<int,
                    (List<Vec3> rv,
                     List<Vec3> rn,
                     List<Vec2> ru)>();
            bool needsFullRebuild = false;

            foreach (var mb in _manifest.Batches
                .OrderBy(b => b.TexId)
                .ThenBy(b => b.Index))
            {
                if (!texObjs.TryGetValue(
                        mb.TexId, out var obj))
                {
                    nomdl++;
                    continue;
                }

                // Skip LOD-only batches that come
                // from a subfolder source (their
                // LocalCentroid is non-zero). For
                // these, the primary in chunk 11
                // already handled writing chunks
                // 12 and 13 via WriteToLodSibling
                // using direct copy (sibTotal ==
                // primCount). Processing the LOD
                // entry would clobber that write.
                // Body batches loaded from combined
                // model_NN.obj have LocalCentroid
                // zero and need their LOD entries
                // processed normally to fill in
                // the resampled gaps.
                bool isSubfolderLodOnly =
                    (mb.LodSiblings == null
                     || mb.LodSiblings.Count == 0)
                    && mb.SourceChunk != 11
                    && (mb.LocalCentroid.X != 0f
                        || mb.LocalCentroid.Y != 0f
                        || mb.LocalCentroid.Z != 0f);
                if (isSubfolderLodOnly)
                {
                    skipped++;
                    continue;
                }

                var (vs, ve) =
                    ResolveVertRange(obj, mb);
                var rv = new List<Vec3>();
                var rn = new List<Vec3>();
                var ru = new List<Vec2>();
                for (int i = vs; i < ve; i++)
                {
                    if (i >= obj.Verts.Count)
                        break;
                    Vec3 v = obj.Verts[i];
                    // Subfolder batches were already
                    // fully reconstructed to raw chunk
                    // space by MergeBatchSubfolder
                    // (v_obj + centroid = v_raw).
                    // Combined-OBJ batches still need
                    // spread+bone subtracted.
                    bool fromSubfolder =
                        (mb.LocalCentroid.X != 0f
                         || mb.LocalCentroid.Y != 0f
                         || mb.LocalCentroid.Z != 0f);

                    if (fromSubfolder)
                    {
                        rv.Add(new Vec3(
                            v.X, v.Y, v.Z));
                    }
                    else
                    {
                        rv.Add(new Vec3(
                            v.X -
                            mb.SpreadOffset.X -
                            mb.BoneOffset.X,
                            v.Y -
                            mb.SpreadOffset.Y -
                            mb.BoneOffset.Y,
                            v.Z -
                            mb.SpreadOffset.Z -
                            mb.BoneOffset.Z));
                    }
                    rn.Add(i < obj.Normals.Count
                        ? obj.Normals[i]
                        : new Vec3(0, 1, 0));
                    if (i < obj.UVs.Count)
                        ru.Add(new Vec2(
                            obj.UVs[i].U,
                            1.0f -
                            obj.UVs[i].V));
                    else
                        ru.Add(new Vec2(0, 0));
                }
                int need = mb.VertexCount;
                bool bf = false, bp = false;
                if (rv.Count != need)
                {
                    if (rv.Count > need)
                    {
                        needsFullRebuild = true;
                        var rs =
                            LODResampler.Resample(
                                rv, rn, ru, need);
                        rv = rs.v;
                        rn = rs.n;
                        ru = rs.u;
                        bf = true;
                    }
                    else
                    {
                        // Always skip when OBJ
                        // slice is short. Original
                        // bytes already in modified
                        // buffer are correct.
                        // Handles SARAH (LOD-only
                        // short) and LOUIS (primary
                        // short). No padding, no
                        // full chunk rebuild needed.
                        skipped++;
                        continue;
                    }
                }
                if (rv.Count == 0)
                {
                    skipped++;
                    continue;
                }
                batchDataMap[mb.Index] =
                    (rv, rn, ru);
                WriteBatchToChunk(
                    modified, mb,
                    rv, rn, ru);

                Console.WriteLine(
                    "[DoCreate] batch idx=" + mb.Index
                    + " tex=" + mb.TexId
                    + " siblings="
                    + (mb.LodSiblings?.Count ?? 0));

                if (mb.LodSiblings != null &&
                    mb.LodSiblings.Count > 0)
                {
                    foreach (var sib in
                        mb.LodSiblings)
                    {
                        WriteToLodSibling(
                            modified, sib,
                            rv, rn, ru);
                        lodSynced++;
                    }
                }
                if (bf) fixed_++;
                else if (bp) padded++;
                else exact++;
            }

            if (needsFullRebuild)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [+] Vertex count" +
                    " changed - full chunk" +
                    " rebuild");
                Console.ResetColor();
                var chunksToRebuild =
                    new Dictionary<int,
                        List<ManifestBatch3D>>();
                foreach (var mb in
                    _manifest.Batches)
                {
                    int ci = mb.SourceChunk;
                    if (ci < 0) continue;
                    if (!chunksToRebuild
                            .ContainsKey(ci))
                        chunksToRebuild[ci] =
                            new List<
                                ManifestBatch3D>();
                    chunksToRebuild[ci].Add(mb);
                }
                var newChunks =
                    new Dictionary<int,
                        byte[]>();
                foreach (var kv in
                    chunksToRebuild)
                {
                    int ci = kv.Key;
                    Dictionary<string, int>
                        ckInfo = null;
                    foreach (var ck in
                        _manifest.AllMeshChunks)
                    {
                        if (ck.TryGetValue(
                                "index",
                                out int cIdx) &&
                            cIdx == ci)
                        {
                            ckInfo = ck;
                            break;
                        }
                    }
                    if (ckInfo == null)
                        continue;
                    int co = ckInfo["offset"];
                    int cs = ckInfo["size"];
                    var orig = new byte[cs];
                    Array.Copy(modified, co,
                        orig, 0, cs);
                    var nd = RebuildMeshChunk(
                        orig, ci, kv.Value,
                        batchDataMap);
                    newChunks[ci] = nd;
                    Console.WriteLine(
                        "        chunk " + ci +
                        ": " + cs + " -> " +
                        nd.Length + " bytes");
                }
                modified = ReassembleRdtb(
                    modified, newChunks);
            }
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    Exact  : " + exact);
            Console.ResetColor();
            if (fixed_ > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    Fixed  : " + fixed_);
                Console.ResetColor();
            }
            if (padded > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    Padded : " + padded);
                Console.ResetColor();
            }
            if (skipped > 0)
                Console.WriteLine(
                    "    Skipped: " + skipped);
            if (nomdl > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No mdl : " + nomdl);
                Console.ResetColor();
            }
            if (lodSynced > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    LOD writes: " +
                    lodSynced);
                Console.ResetColor();
            }
            Directory.CreateDirectory(outFolder);
            string outr = Path.Combine(
                outFolder,
                _manifest.OriginalRdtbName);

            // FINAL CHECK before writing file
            if (modified.Length > 0x195780 + 8)
            {
                float finalVal =
                    BitConverter.ToSingle(
                        modified, 0x195784);
                Console.WriteLine(
                    "[FINAL CHECK] offset 0x195784"
                    + " final value=" + finalVal);
            }

            File.WriteAllBytes(outr, modified);
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Rebuild complete!");
            Console.ResetColor();
            Console.WriteLine(
                "     Output: " + outr);
            if (scale != 1.0f)
                Console.WriteLine(
                    "     Scale: " + scale +
                    "x (baked into vertices)");
            string tf = Path.Combine(
                folder, "textures");
            if (Directory.Exists(tf) &&
                !string.IsNullOrEmpty(
                    _manifest.OriginalGdtbName))
            {
                string outg = Path.Combine(
                    outFolder,
                    _manifest.OriginalGdtbName);
                try
                {
                    GDTBArchive.Create(tf, outg);
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "     GDTB: " + outg);
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] GDTB: " +
                        ex.Message);
                    Console.ResetColor();
                    string sg = Path.Combine(
                        folder,
                        _manifest.SourceGdtb);
                    if (File.Exists(sg))
                        File.Copy(sg, outg, true);
                }
            }
        }

        private void LoadObjFiles(
            string folder,
            Dictionary<int, ParsedObj>
            texObjs)
        {
            var allObjFiles =
                Directory.GetFiles(
                    folder, "*.obj");

            Console.WriteLine(
                "[LoadObjFiles] folder=" + folder
                + " objFilesFound=" + allObjFiles.Length);
            foreach (var fp in allObjFiles)
            {
                Console.WriteLine(
                    "  - "
                    + Path.GetFileName(fp));
            }

            // 1) model_NN.obj at root
            foreach (var fp in allObjFiles)
            {
                string fn =
                    Path
                    .GetFileNameWithoutExtension(
                        fp).ToLower();
                if (!fn.StartsWith("model_"))
                    continue;
                string rest = fn.Substring(6);
                if (rest.EndsWith("_all"))
                    rest = rest.Substring(
                        0, rest.Length - 4);
                int us = rest.IndexOf('_');
                if (us > 0)
                    rest =
                        rest.Substring(0, us);
                if (!int.TryParse(
                        rest, out int tid))
                    continue;
                if (texObjs.ContainsKey(tid))
                    continue;
                try
                {
                    var obj =
                        ObjParser.Parse(fp);
                    texObjs[tid] = obj;
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    model_" +
                        tid.ToString("D2") +
                        " loaded (" +
                        obj.Verts.Count + "v)");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] " +
                        Path.GetFileName(fp) +
                        ": " + ex.Message);
                    Console.ResetColor();
                }
            }

            // 2b) Embedded single OBJ - any .obj that
            // is NOT model_NN, NOT _body, NOT _all.
            // Used by embedded/small RDTBs which write
            // <baseName>.obj with all batches combined.
            // Load it as tex_id 0 if not already loaded.
            // Map batch groups by face index ranges.
            if (!texObjs.ContainsKey(0))
            {
                foreach (var fp in allObjFiles)
                {
                    string fn =
                        Path.GetFileNameWithoutExtension(
                            fp).ToLower();
                    // Skip already-handled patterns
                    if (fn.StartsWith("model_"))
                        continue;
                    if (fn.EndsWith("_body"))
                        continue;
                    if (fn.EndsWith("_all"))
                        continue;
                    // This is a standalone named OBJ
                    // (e.g. EBONY.obj, BLUEBERRY.obj)
                    try
                    {
                        var obj = ObjParser.Parse(fp);
                        if (obj.Verts.Count == 0)
                            continue;
                        

                        // Load for ALL tex_ids used
                        // in manifest that aren't
                        // already loaded. For embedded
                        // RDTBs all batches share one
                        // OBJ so we point every tex_id
                        // at the same parsed object.
                        var usedTids =
                            new HashSet<int>();
                        foreach (var mb2 in
                            _manifest.Batches)
                            usedTids.Add(mb2.TexId);

                        foreach (int tid in usedTids)
                        {
                            if (!texObjs.ContainsKey(tid))
                            {
                                texObjs[tid] = obj;
                                Console.ForegroundColor =
                                    ConsoleColor.Green;
                                Console.WriteLine(
                                    "    [embedded] " +
                                    Path.GetFileName(fp) +
                                    " -> tex_" +
                                    tid.ToString("D2") +
                                    " (" +
                                    obj.Verts.Count +
                                    "v, " +
                                    obj.FacesByGroup
                                        .Count +
                                    " groups)");
                                Console.ResetColor();
                            }
                        }
                        // Only use the first matching
                        // standalone OBJ found
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "    [!] " +
                            Path.GetFileName(fp) +
                            ": " + ex.Message);
                        Console.ResetColor();
                    }
                }
            }

            // ── EMBEDDED SINGLE OBJ ──────────
            // Small/embedded RDTBs write one
            // combined OBJ named after the
            // base name (e.g. EBONY.obj).
            // It has batch_XXXX groups but
            // does NOT start with model_,
            // end with _body or _all.
            // Load it for every tex_id used
            // in the manifest.
            foreach (var fp in allObjFiles)
            {
                string fn =
                    Path
                    .GetFileNameWithoutExtension(
                        fp).ToLower();
                if (fn.StartsWith("model_"))
                    continue;
                if (fn.EndsWith("_body"))
                    continue;
                if (fn.EndsWith("_all"))
                    continue;

                ParsedObj embObj = null;
                try
                {
                    embObj =
                        ObjParser.Parse(fp);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] " +
                        Path.GetFileName(fp) +
                        ": " + ex.Message);
                    Console.ResetColor();
                    continue;
                }

                if (embObj.Verts.Count == 0)
                    continue;
                

                // Register for every
                // tex_id in manifest
                var usedTids =
                    new HashSet<int>();
                foreach (var mb2 in
                    _manifest.Batches)
                    usedTids.Add(mb2.TexId);

                bool anyLoaded = false;
                foreach (int tid in usedTids)
                {
                    if (texObjs
                            .ContainsKey(tid))
                        continue;
                    texObjs[tid] = embObj;
                    anyLoaded = true;
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    [embedded] " +
                        Path.GetFileName(fp) +
                        " -> tex_" +
                        tid.ToString("D2") +
                        " (" +
                        embObj.Verts.Count +
                        "v " +
                        embObj.FacesByGroup
                            .Count +
                        " groups)");
                    Console.ResetColor();
                }

                // Only use first matching
                // standalone OBJ
                if (anyLoaded) break;
            }

            // Find _body / _tools / _all
            string bodyPath = null;

            string allPath = null;
            foreach (var fp in allObjFiles)
            {

                string fn =
                    Path
                    .GetFileNameWithoutExtension(
                        fp).ToLower();
                if (fn.EndsWith("_body"))
                    bodyPath = fp;
                else if (fn.EndsWith("_all"))
                    allPath = fp;
            }

            // Compute tools_tid from manifest
            int toolsTid = -1;
            var tidCounts =
                new Dictionary<int, int>();
            foreach (var mb in
                _manifest.Batches)
            {
                if (!tidCounts.ContainsKey(
                        mb.TexId))
                    tidCounts[mb.TexId] = 0;
                tidCounts[mb.TexId]++;
            }
            int maxB = 0;
            foreach (var kv in tidCounts)
            {
                if (kv.Value > maxB)
                {
                    maxB = kv.Value;
                    toolsTid = kv.Key;
                }
            }
            if (tidCounts.Count <= 1)
                toolsTid = -1;

            // 3) <base>_body.obj (split by
            // batch groups, exclude toolsTid)
            if (bodyPath != null)
            {
                try
                {
                    var bodyObj = ObjParser.Parse(bodyPath);

                    // DEBUG: show what groups parser found
                    Console.WriteLine(
                        "[LoadObjFiles] _body parsed: "
                        + bodyObj.Verts.Count + "v, "
                        + bodyObj.FacesByGroup.Count
                        + " groups");
                    int shownGroups = 0;
                    foreach (var kv in bodyObj.FacesByGroup)
                    {
                        if (shownGroups++ < 5)
                            Console.WriteLine(
                                "    group '" + kv.Key
                                + "': " + kv.Value.Count
                                + " faces");
                    }
                    var batchToTid =
                        new Dictionary<
                            int, int>();
                    foreach (var mb in
                        _manifest.Batches)
                    {
                        if (mb.TexId !=
                                toolsTid)
                            batchToTid[
                                mb.Index] =
                                mb.TexId;
                    }
                    SplitObjByBatchGroups(
                        bodyObj,
                        batchToTid,
                        texObjs,
                        "_body.obj");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] _body: " +
                        ex.Message);
                    Console.ResetColor();
                }
            }

            // 4) <base>_all.obj
            //    Single combined OBJ used for
            //    items/props. Map every batch
            //    group to its tex_id from
            //    manifest (do NOT exclude
            //    toolsTid - items only have one)
            if (allPath != null)
            {
                try
                {
                    var allObj =
                        ObjParser.Parse(
                            allPath);
                    var batchToTid =
                        new Dictionary<
                            int, int>();
                    foreach (var mb in
                        _manifest.Batches)
                    {
                        batchToTid[mb.Index] =
                            mb.TexId;
                    }
                    SplitObjByBatchGroups(
                        allObj,
                        batchToTid,
                        texObjs,
                        "_all.obj");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] _all: " +
                        ex.Message);
                    Console.ResetColor();
                }
            }

            // 5) models_NN/ and model_NN/
            //    subfolders with batch_*.obj
            var splitDirs = new List<string>();
            splitDirs.AddRange(
                Directory.GetDirectories(
                    folder, "models_*"));
            splitDirs.AddRange(
                Directory.GetDirectories(
                    folder, "model_*"));
            foreach (var sf in splitDirs)
            {
                string dn =
                    Path.GetFileName(sf)
                        .ToLower();
                string numPart;
                if (dn.StartsWith("models_"))
                    numPart = dn.Substring(7);
                else if (dn.StartsWith("model_"))
                    numPart = dn.Substring(6);
                else continue;
                if (!int.TryParse(
                        numPart, out int tid))
                    continue;
                if (texObjs.ContainsKey(tid))
                    continue;
                MergeBatchSubfolder(
                    sf, tid, texObjs);
            }
        }

        private void SplitObjByBatchGroups(
            ParsedObj srcObj,
            Dictionary<int, int> batchToTid,
            Dictionary<int, ParsedObj>
                texObjs,
            string sourceLabel)
        {
            var perTid =
                new Dictionary<int,
                    ParsedObj>();

            // PASS 1: try standard
            // 'batch_NNNN' group markers
            // (preserved when Blender
            // keeps OBJ group names)
            int batchHits = 0;
            foreach (var kv in
                srcObj.FacesByGroup)
            {
                string gname = kv.Key;
                if (!gname.StartsWith(
                        "batch_"))
                    continue;
                if (!int.TryParse(
                        gname.Substring(6),
                        out int bidx))
                    continue;
                if (!batchToTid.TryGetValue(
                        bidx, out int tid))
                    continue;
                if (kv.Value.Count == 0)
                    continue;
                if (!perTid.ContainsKey(tid))
                    perTid[tid] =
                        new ParsedObj();
                CopyGroupVertsToParsedObj(
                    srcObj,
                    kv.Value,
                    gname,
                    perTid[tid]);
                batchHits++;
            }

            // PASS 2: if no batch_ groups
            // found (Blender stripped them),
            // fall back to 'mat_NN' groups.
            // This maps all faces with a
            // given material to that tex_id.
            // Vertex ranges from manifest
            // will be used during rebuild.
            if (batchHits == 0)
            {
                foreach (var kv in
                    srcObj.FacesByGroup)
                {
                    string gname = kv.Key;
                    if (!gname.StartsWith(
                            "mat_"))
                        continue;
                    if (!int.TryParse(
                            gname.Substring(4),
                            out int tid))
                        continue;
                    if (kv.Value.Count == 0)
                        continue;
                    if (!perTid.ContainsKey(
                            tid))
                        perTid[tid] =
                            new ParsedObj();
                    CopyGroupVertsToParsedObj(
                        srcObj,
                        kv.Value,
                        gname,
                        perTid[tid]);
                }
            }

            foreach (var kv in perTid)
            {
                if (texObjs.ContainsKey(
                        kv.Key)) continue;
                if (kv.Value.Verts.Count
                        == 0) continue;
                texObjs[kv.Key] = kv.Value;
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    " + sourceLabel +
                    " -> tex_" +
                    kv.Key.ToString("D2") +
                    " (" +
                    kv.Value.Verts.Count +
                    "v)");
                Console.ResetColor();
            }
        }

        private void CopyGroupVertsToParsedObj(
            ParsedObj src,
            List<Tri> srcFaces,
            string gname,
            ParsedObj dst)
        {
            var used = new HashSet<int>();
            foreach (var tri in srcFaces)
            {
                used.Add(tri.A);
                used.Add(tri.B);
                used.Add(tri.C);
            }
            var usedSorted =
                used.OrderBy(x => x).ToList();
            var remap =
                new Dictionary<int, int>();
            int startIdx = dst.Verts.Count;
            for (int si = 0;
                 si < usedSorted.Count; si++)
            {
                int old = usedSorted[si];
                remap[old] = startIdx + si;
                dst.Verts.Add(
                    old < src.Verts.Count
                    ? src.Verts[old]
                    : Vec3.Zero);
                dst.Normals.Add(
                    old < src.Normals.Count
                    ? src.Normals[old]
                    : new Vec3(0, 1, 0));
                dst.UVs.Add(
                    old < src.UVs.Count
                    ? src.UVs[old]
                    : new Vec2(0, 0));
            }
            if (!dst.FacesByGroup
                    .ContainsKey(gname))
                dst.FacesByGroup[gname] =
                    new List<Tri>();
            foreach (var tri in srcFaces)
            {
                int ra, rb, rc;
                remap.TryGetValue(
                    tri.A, out ra);
                remap.TryGetValue(
                    tri.B, out rb);
                remap.TryGetValue(
                    tri.C, out rc);
                var nt = new Tri(ra, rb, rc);
                dst.FacesByGroup[gname].Add(nt);
                dst.AllFaces.Add(nt);
            }
        }

        private void MergeBatchSubfolder(
            string subDir,
            int tid,
            Dictionary<int, ParsedObj>
                texObjs)
        {
            var batchFiles =
                Directory.GetFiles(
                    subDir, "batch_*.obj")
                    .OrderBy(x => x)
                    .ToList();
            if (batchFiles.Count == 0)
                return;

            // Get manifest's batches for this
            // tex_id ordered by global Index
            var tidBatches =
                _manifest.Batches
                    .Where(b => b.TexId == tid)
                    .OrderBy(b => b.Index)
                    .ToList();

            var merged = new ParsedObj();
            int vOffset = 0;
            int fileSlot = 0;

            foreach (var bp in batchFiles)
            {
                try
                {
                    var bo =
                        ObjParser.Parse(bp);

                    if (fileSlot >=
                        tidBatches.Count)
                    {
                        vOffset +=
                            bo.Verts.Count;
                        fileSlot++;
                        continue;
                    }
                    var mb =
                        tidBatches[fileSlot];
                    int manifestIdx = mb.Index;
                    string gname =
                        "batch_" +
                        manifestIdx
                            .ToString("D4");

                    // Add centroid back (gives v_raw +
                    // bone since bone was baked into
                    // verts during extraction). Then
                    // subtract bone to get pure v_raw.
                    Vec3 lc = mb.LocalCentroid;
                    Vec3 bo2 = mb.BoneOffset;
                    foreach (var v in bo.Verts)
                        merged.Verts.Add(
                            new Vec3(
                                v.X + lc.X - bo2.X,
                                v.Y + lc.Y - bo2.Y,
                                v.Z + lc.Z - bo2.Z));
                    merged.Normals.AddRange(
                        bo.Normals);
                    merged.UVs.AddRange(bo.UVs);

                    if (!merged.FacesByGroup
                            .ContainsKey(gname))
                        merged.FacesByGroup[
                            gname] =
                            new List<Tri>();
                    foreach (var t in
                        bo.AllFaces)
                    {
                        var nt = new Tri(
                            t.A + vOffset,
                            t.B + vOffset,
                            t.C + vOffset);
                        merged.AllFaces.Add(nt);
                        merged.FacesByGroup[
                            gname].Add(nt);
                    }
                    vOffset +=
                        bo.Verts.Count;
                    fileSlot++;
                }
                catch { }
            }
            if (merged.Verts.Count > 0)
            {
                texObjs[tid] = merged;
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    [split] tex_" +
                    tid.ToString("D2") +
                    " merged: " +
                    merged.Verts.Count +
                    "v across " +
                    merged.FacesByGroup.Count +
                    " manifest batches");
                Console.ResetColor();
            }
        }

        private (int vs, int ve)
        ResolveVertRange(
            ParsedObj obj,
            ManifestBatch3D mb)
        {
            string gname =
                "batch_" +
                mb.Index.ToString("D4");
            if (obj.FacesByGroup.TryGetValue(
                    gname, out var gf) &&
                gf.Count > 0)
            {
                var used = new HashSet<int>();
                foreach (var t in gf)
                {
                    used.Add(t.A);
                    used.Add(t.B);
                    used.Add(t.C);
                }
                if (used.Count > 0)
                {
                    int mn = int.MaxValue;
                    int mx = int.MinValue;
                    foreach (int u in used)
                    {
                        if (u < mn) mn = u;
                        if (u > mx) mx = u;
                    }
                    return (mn, mx + 1);
                }
            }
            int vs = mb.ObjVertStart;
            int ve = Math.Min(
                mb.ObjVertEnd,
                obj.Verts.Count);
            return (vs, ve);
        }

        private void WriteBatchToChunk(
            byte[] data,
            ManifestBatch3D mb,
            List<Vec3> verts,
            List<Vec3> normals,
            List<Vec2> uvs)
        {
            int chunkOffset = 0;
            foreach (var ck in
                _manifest.AllMeshChunks)
            {
                if (ck.TryGetValue(
                        "index", out int ci) &&
                    ci == mb.SourceChunk &&
                    ck.TryGetValue(
                        "offset", out int co))
                {
                    chunkOffset = co;
                    break;
                }
            }
            if (chunkOffset == 0)
                chunkOffset =
                    _manifest.Chunk11Offset;
            int vi = 0;
            const float EPS = 0.0001f;
            foreach (var blk in mb.Blocks)
            {
                int bs = chunkOffset +
                    blk.ChunkOffset;
                int n = blk.VertexCount;
                int ds = bs + 16;
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + i * 16;
                    if (vi + i >= verts.Count ||
                        ro + 16 > data.Length)
                        break;
                    Vec3 v = verts[vi + i];
                    WriteFNear(data, ro + 4,
                        v.X, EPS);
                    WriteFNear(data, ro + 8,
                        v.Y, EPS);
                    WriteFNear(data, ro + 12,
                        v.Z, EPS);
                }
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + (n + i) * 16;
                    if (vi + i >= normals.Count ||
                        ro + 16 > data.Length)
                        break;
                    Vec3 nr = normals[vi + i];
                    WriteFNear(data, ro + 4,
                        nr.X, EPS);
                    WriteFNear(data, ro + 8,
                        nr.Y, EPS);
                    WriteFNear(data, ro + 12,
                        nr.Z, EPS);
                }
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + (2 * n + i) * 16;
                    if (vi + i >= uvs.Count ||
                        ro + 12 > data.Length)
                        break;
                    Vec2 uv = uvs[vi + i];
                    WriteFNear(data, ro + 4,
                        uv.U, EPS);
                    WriteFNear(data, ro + 8,
                        uv.V, EPS);
                }
                vi += n;
            }
        }

        private void WriteFNear(
            byte[] data, int off,
            float v, float eps)
        {
            if (_originalRdtb != null &&
                off + 4 <= _originalRdtb.Length)
            {
                float originalVal =
                    BitConverter.ToSingle(
                        _originalRdtb, off);

                // Use a tighter absolute
                // epsilon AND a relative one
                // so small values near zero
                // are not falsely "changed"
                float absEps = eps;
                float relEps =
                    Math.Abs(originalVal)
                    * 0.001f;
                float useEps =
                    Math.Max(absEps, relEps);

                if (Math.Abs(originalVal - v)
                        < useEps)
                {
                    // Restore original bytes
                    // exactly - no drift
                    data[off] =
                        _originalRdtb[off];
                    data[off + 1] =
                        _originalRdtb[off + 1];
                    data[off + 2] =
                        _originalRdtb[off + 2];
                    data[off + 3] =
                        _originalRdtb[off + 3];
                    return;
                }
            }
            byte[] b =
                BitConverter.GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }

        private static void WriteF(
            byte[] data, int off, float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }

        private void WriteToLodSibling(
            byte[] data,
            LODSiblingInfo sib,
            List<Vec3> primaryVerts,
            List<Vec3> primaryNormals,
            List<Vec2> primaryUvs)
        {
            int chunkOffset = 0;
            bool found = false;
            foreach (var ck in
                _manifest.AllMeshChunks)
            {
                if (ck.TryGetValue(
                        "index", out int ci) &&
                    ci == sib.ChunkIdx &&
                    ck.TryGetValue(
                        "offset", out int co))
                {
                    chunkOffset = co;
                    found = true;
                    break;
                }
            }
            if (!found) return;

            int sibTotal = 0;
            foreach (var (off, vc) in
                sib.VifBlocks)
                sibTotal += vc;

            bool useDirectCopy =
                (primaryVerts.Count == sibTotal);

            Console.WriteLine(
                "[WTLS] sib.chunk=" + sib.ChunkIdx
                + " primCount=" + primaryVerts.Count
                + " sibTotal=" + sibTotal
                + " direct=" + useDirectCopy);

            if (useDirectCopy)
            {
                const float EPS = 0.001f;
                int vIdx = 0;

                // Find primary batch for
                // looking up original primary
                // bytes (for per-vertex match
                // detection)
                ManifestBatch3D primMb = null;
                foreach (var cmb in
                    _manifest.Batches)
                {
                    if (cmb.LodSiblings == null)
                        continue;
                    foreach (var lsib in
                        cmb.LodSiblings)
                    {
                        if (lsib.ChunkIdx
                                == sib.ChunkIdx
                            && lsib.BatchIndex
                                == sib.BatchIndex)
                        {
                            primMb = cmb;
                            break;
                        }
                    }
                    if (primMb != null) break;
                }
                int primChunkOff = 0;
                if (primMb != null)
                {
                    foreach (var ck in
                        _manifest.AllMeshChunks)
                    {
                        if (ck.TryGetValue(
                                "index",
                                out int pci)
                            && pci == primMb
                                .SourceChunk
                            && ck.TryGetValue(
                                "offset",
                                out int pco))
                        {
                            primChunkOff = pco;
                            break;
                        }
                    }
                }

                foreach (var (blockOffset, vc)
                    in sib.VifBlocks)
                {
                    int bs =
                        chunkOffset + blockOffset;
                    int ds = bs + 16;
                    int n = vc;

                    // Find matching primary
                    // block (by sequential
                    // order)
                    ManifestBlock3D primBlk =
                        null;
                    if (primMb != null
                        && primMb.Blocks.Count
                            > 0)
                    {
                        int sibBlkIdx = -1;
                        for (int bi = 0;
                             bi <
                             sib.VifBlocks.Count;
                             bi++)
                        {
                            if (sib.VifBlocks[bi]
                                    .offset
                                == blockOffset)
                            {
                                sibBlkIdx = bi;
                                break;
                            }
                        }
                        if (sibBlkIdx >= 0
                            && sibBlkIdx <
                                primMb.Blocks
                                    .Count)
                            primBlk =
                                primMb.Blocks[
                                    sibBlkIdx];
                    }

                    int primBlkStart =
                        primBlk != null
                        ? primChunkOff
                            + primBlk
                                .ChunkOffset
                            + 16
                        : -1;

                    for (int i = 0; i < n; i++)
                    {
                        int ro = ds + i * 16;
                        if (vIdx + i >=
                                primaryVerts.Count
                            || ro + 16
                                > data.Length)
                            break;

                        // Check if sibling's
                        // original vertex
                        // matches primary's
                        // original vertex
                        // (within epsilon).
                        // If they MATCH, write
                        // user's edit. If they
                        // DIFFER, preserve
                        // sibling's original
                        // (intentional LOD
                        // geometry).
                        bool preserveOrig = false;
                        if (primBlkStart >= 0
                            && _originalRdtb
                                != null)
                        {
                            int sibVOff = ro + 4;
                            int primVOff =
                                primBlkStart
                                + i * 16 + 4;
                            if (sibVOff + 12
                                    <= _originalRdtb
                                        .Length
                                && primVOff + 12
                                    <= _originalRdtb
                                        .Length)
                            {
                                float sox =
                                    BitConverter
                                        .ToSingle(
                                            _originalRdtb,
                                            sibVOff);
                                float soy =
                                    BitConverter
                                        .ToSingle(
                                            _originalRdtb,
                                            sibVOff + 4);
                                float soz =
                                    BitConverter
                                        .ToSingle(
                                            _originalRdtb,
                                            sibVOff + 8);
                                float pox =
                                    BitConverter
                                        .ToSingle(
                                            _originalRdtb,
                                            primVOff);
                                float poy =
                                    BitConverter
                                        .ToSingle(
                                            _originalRdtb,
                                            primVOff + 4);
                                float poz =
                                    BitConverter
                                        .ToSingle(
                                            _originalRdtb,
                                            primVOff + 8);
                                bool match =
                                    Math.Abs(
                                        sox - pox)
                                        < 0.001f
                                    && Math.Abs(
                                        soy - poy)
                                        < 0.001f
                                    && Math.Abs(
                                        soz - poz)
                                        < 0.001f;
                                preserveOrig =
                                    !match;
                            }
                        }

                        if (preserveOrig)
                        {
                            // Restore original
                            // sibling bytes for
                            // this vertex
                            if (_originalRdtb
                                    != null)
                            {
                                for (int k = 0;
                                     k < 12; k++)
                                {
                                    data[ro + 4
                                        + k] =
                                        _originalRdtb[
                                            ro + 4
                                            + k];
                                }
                            }
                        }
                        else
                        {
                            Vec3 v =
                                primaryVerts[
                                    vIdx + i];
                            WriteFNear(data,
                                ro + 4, v.X, EPS);
                            WriteFNear(data,
                                ro + 8, v.Y, EPS);
                            WriteFNear(data,
                                ro + 12, v.Z, EPS);
                        }
                    }

                    for (int i = 0; i < n; i++)
                    {
                        int ro =
                            ds + (n + i) * 16;
                        if (vIdx + i >=
                                primaryNormals
                                    .Count
                            || ro + 16
                                > data.Length)
                            break;
                        Vec3 nr =
                            primaryNormals[
                                vIdx + i];
                        WriteFNear(data, ro + 4,
                            nr.X, EPS);
                        WriteFNear(data, ro + 8,
                            nr.Y, EPS);
                        WriteFNear(data, ro + 12,
                            nr.Z, EPS);
                    }
                    for (int i = 0; i < n; i++)
                    {
                        int ro =
                            ds + (2 * n + i) * 16;
                        if (vIdx + i >=
                                primaryUvs.Count
                            || ro + 12
                                > data.Length)
                            break;
                        Vec2 uv =
                            primaryUvs[vIdx + i];
                        WriteFNear(data, ro + 4,
                            uv.U, EPS);
                        WriteFNear(data, ro + 8,
                            uv.V, EPS);
                    }
                    vIdx += n;
                }
                return;
            }

            // Check if the primary batch was
            // actually edited by comparing
            // primaryVerts against the chunk
            // 11 original bytes. If no edits
            // detected, preserve LOD chunk
            // bytes entirely (no resampling
            // drift). This is the common
            // unedited-roundtrip case.
            bool primaryWasEdited = false;
            if (_originalRdtb != null)
            {
                // Find primary batch & its
                // chunk 11 offset
                ManifestBatch3D primMb = null;
                foreach (var cmb in
                    _manifest.Batches)
                {
                    if (cmb.LodSiblings == null)
                        continue;
                    foreach (var lsib in
                        cmb.LodSiblings)
                    {
                        if (lsib.ChunkIdx
                                == sib.ChunkIdx
                            && lsib.BatchIndex
                                == sib.BatchIndex)
                        {
                            primMb = cmb;
                            break;
                        }
                    }
                    if (primMb != null) break;
                }
                if (primMb != null
                    && primMb.Blocks.Count > 0)
                {
                    int primChOff = 0;
                    foreach (var ck in
                        _manifest.AllMeshChunks)
                    {
                        if (ck.TryGetValue(
                                "index",
                                out int pci)
                            && pci == primMb
                                .SourceChunk
                            && ck.TryGetValue(
                                "offset",
                                out int pco))
                        {
                            primChOff = pco;
                            break;
                        }
                    }
                    // Compare a few sample verts
                    int sampleIdx = 0;
                    foreach (var primBlk in
                        primMb.Blocks)
                    {
                        int primDs = primChOff
                            + primBlk.ChunkOffset
                            + 16;
                        for (int si = 0;
                             si < primBlk
                                .VertexCount;
                             si++)
                        {
                            if (sampleIdx >=
                                primaryVerts.Count)
                                break;
                            int pro =
                                primDs + si * 16;
                            if (pro + 16
                                > _originalRdtb
                                    .Length)
                                break;
                            float ox =
                                BitConverter
                                    .ToSingle(
                                        _originalRdtb,
                                        pro + 4);
                            float oy =
                                BitConverter
                                    .ToSingle(
                                        _originalRdtb,
                                        pro + 8);
                            float oz =
                                BitConverter
                                    .ToSingle(
                                        _originalRdtb,
                                        pro + 12);
                            Vec3 pv =
                                primaryVerts[
                                    sampleIdx];
                            if (Math.Abs(pv.X - ox)
                                    > 0.01f
                                || Math.Abs(pv.Y - oy)
                                    > 0.01f
                                || Math.Abs(pv.Z - oz)
                                    > 0.01f)
                            {
                                primaryWasEdited
                                    = true;
                                break;
                            }
                            sampleIdx++;
                        }
                        if (primaryWasEdited)
                            break;
                    }
                }
            }

            if (!primaryWasEdited)
            {
                // No user edit detected.
                // Preserve original LOD bytes
                // entirely for this batch.
                return;
            }

            // Otherwise fall through to
            // normal resampler path.
            var perBlock =
                LODResampler.ResampleToSibling(
                    primaryVerts,
                    primaryNormals,
                    primaryUvs,
                    sib);
            const float EPS2 = 0.001f;
            foreach (var (blockOffset, bv,
                bn, bu) in perBlock)
            {
                int bs = chunkOffset
                    + blockOffset;
                int n = bv.Count;
                int ds = bs + 16;
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + i * 16;
                    if (ro + 16 > data.Length)
                        break;
                    Vec3 v = bv[i];
                    WriteFNear(data, ro + 4,
                        v.X, EPS2);
                    WriteFNear(data, ro + 8,
                        v.Y, EPS2);
                    WriteFNear(data, ro + 12,
                        v.Z, EPS2);
                }
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + (n + i) * 16;
                    if (ro + 16 > data.Length)
                        break;
                    Vec3 nr = bn[i];
                    WriteFNear(data, ro + 4,
                        nr.X, EPS2);
                    WriteFNear(data, ro + 8,
                        nr.Y, EPS2);
                    WriteFNear(data, ro + 12,
                        nr.Z, EPS2);
                }
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + (2 * n + i) * 16;
                    if (ro + 12 > data.Length)
                        break;
                    Vec2 uv = bu[i];
                    WriteFNear(data, ro + 4,
                        uv.U, EPS2);
                    WriteFNear(data, ro + 8,
                        uv.V, EPS2);
                }
            }
        }

        private byte[] RebuildMeshChunk(
            byte[] originalChunkData,
            int chunkIdx,
            List<ManifestBatch3D>
        batchesInChunk,
            Dictionary<int,
        (List<Vec3> rv,
         List<Vec3> rn,
         List<Vec2> ru)>
        batchDataMap)
        {
            if (batchesInChunk.Count == 0)
                return originalChunkData;
            uint firstPtr =
                BitConverter.ToUInt32(
                    originalChunkData, 0);
            if (firstPtr == 0 ||
                firstPtr >
                originalChunkData.Length)
                return originalChunkData;
            int ptrCount = (int)(firstPtr / 4);
            var newBlocksPerBatch =
                new Dictionary<int,
                    List<(List<Vec3> bv,
                          List<Vec3> bn,
                          List<Vec2> bu)>>();
            foreach (var mb in batchesInChunk)
            {
                if (!batchDataMap.TryGetValue(
                        mb.Index, out var d))
                    continue;
                var newV = d.rv;
                var newN = d.rn;
                var newU = d.ru;
                int totalOld = 0;
                foreach (var blk in mb.Blocks)
                    totalOld += blk.VertexCount;
                int totalNew = newV.Count;
                if (totalOld == 0) continue;
                var blocksRedist =
                    new List<(List<Vec3>,
                              List<Vec3>,
                              List<Vec2>)>();
                int cursor = 0;
                for (int bi = 0;
                     bi < mb.Blocks.Count;
                     bi++)
                {
                    var blk = mb.Blocks[bi];
                    int take;
                    if (bi == mb.Blocks.Count
                            - 1)
                        take = totalNew - cursor;
                    else
                    {
                        take = (int)Math.Round(
                            (double)blk
                                .VertexCount *
                            totalNew / totalOld);
                        if (take < 1) take = 1;
                        if (cursor + take >
                            totalNew)
                            take = totalNew -
                                cursor;
                    }
                    if (take < 1) take = 1;
                    int end = Math.Min(
                        cursor + take,
                        newV.Count);
                    var bv = new List<Vec3>();
                    var bn = new List<Vec3>();
                    var bu = new List<Vec2>();
                    for (int j = cursor;
                         j < end; j++)
                    {
                        bv.Add(newV[j]);
                        bn.Add(newN[j]);
                        bu.Add(newU[j]);
                    }
                    blocksRedist.Add(
                        (bv, bn, bu));
                    cursor += take;
                    if (cursor >= totalNew)
                        break;
                }
                newBlocksPerBatch[mb.Index] =
                    blocksRedist;
            }

            var outStream = new MemoryStream();
            outStream.Write(
                new byte[ptrCount * 4], 0,
                ptrCount * 4);
            var ptrToNewOff =
                new Dictionary<int, int>();
            foreach (var mb in batchesInChunk)
            {
                if (mb.Blocks.Count == 0)
                    continue;
                int firstBlkOff =
                    mb.Blocks[0].ChunkOffset;
                int ptrIdx = -1;
                for (int pi = 0;
                     pi < ptrCount; pi++)
                {
                    uint pv =
                        BitConverter.ToUInt32(
                            originalChunkData,
                            pi * 4);
                    if ((int)pv == firstBlkOff)
                    {
                        ptrIdx = pi;
                        break;
                    }
                }
                List<(List<Vec3> bv,
                      List<Vec3> bn,
                      List<Vec2> bu)> blocks;
                bool hasNew =
                    newBlocksPerBatch
                        .TryGetValue(
                            mb.Index,
                            out blocks);
                if (!hasNew)
                {
                    int blkStart =
                        (int)outStream.Length;
                    if (ptrIdx >= 0)
                        ptrToNewOff[ptrIdx] =
                            blkStart;
                    foreach (var blk in
                        mb.Blocks)
                    {
                        int srcOff =
                            blk.ChunkOffset;
                        int n = blk.VertexCount;
                        int blkSize =
                            16 + (3 * n * 16);
                        int srcEnd =
                            srcOff + blkSize;
                        if (srcEnd >
                            originalChunkData
                                .Length)
                            srcEnd =
                                originalChunkData
                                    .Length;
                        outStream.Write(
                            originalChunkData,
                            srcOff,
                            srcEnd - srcOff);
                    }
                    continue;
                }

                int blkStartInNew =
    (int)outStream.Length;
                if (ptrIdx >= 0)
                    ptrToNewOff[ptrIdx] =
                        blkStartInNew;
                for (int bi = 0;
                     bi < blocks.Count; bi++)
                {
                    if (bi >= mb.Blocks.Count)
                        break;
                    var origBlk = mb.Blocks[bi];
                    int origSrc =
                        origBlk.ChunkOffset;
                    if (origSrc + 16 >
                        originalChunkData.Length)
                        continue;
                    var (bv, bn, bu) =
                        blocks[bi];
                    int n = bv.Count;
                    // VIF header copy with
                    // updated vertex count
                    byte[] hdr = new byte[16];
                    Array.Copy(
                        originalChunkData,
                        origSrc, hdr, 0, 16);
                    hdr[2] = (byte)(n & 0xFF);
                    if (n !=
                        origBlk.VertexCount)
                    {
                        int qwd = 3 * n;
                        hdr[5] =
                            (byte)(qwd & 0xFF);
                    }
                    outStream.Write(
                        hdr, 0, 16);
                    // Vertex rows
                    for (int i = 0; i < n; i++)
                    {
                        uint flag = 0;
                        if (i < origBlk
                                .VertexCount)
                            flag = BitConverter
                                .ToUInt32(
                                originalChunkData,
                                origSrc + 16 +
                                i * 16);
                        outStream.Write(
                            BitConverter.GetBytes(
                                flag), 0, 4);
                        Vec3 v = bv[i];
                        outStream.Write(
                            BitConverter.GetBytes(
                                v.X), 0, 4);
                        outStream.Write(
                            BitConverter.GetBytes(
                                v.Y), 0, 4);
                        outStream.Write(
                            BitConverter.GetBytes(
                                v.Z), 0, 4);
                    }
                    // Normal rows
                    for (int i = 0; i < n; i++)
                    {
                        uint flag = 0;
                        if (i < origBlk
                                .VertexCount)
                            flag = BitConverter
                                .ToUInt32(
                                originalChunkData,
                                origSrc + 16 +
                                (origBlk
                                    .VertexCount
                                    + i) * 16);
                        outStream.Write(
                            BitConverter.GetBytes(
                                flag), 0, 4);
                        Vec3 nr = bn[i];
                        outStream.Write(
                            BitConverter.GetBytes(
                                nr.X), 0, 4);
                        outStream.Write(
                            BitConverter.GetBytes(
                                nr.Y), 0, 4);
                        outStream.Write(
                            BitConverter.GetBytes(
                                nr.Z), 0, 4);
                    }
                    // UV rows
                    for (int i = 0; i < n; i++)
                    {
                        uint flag = 0;
                        if (i < origBlk
                                .VertexCount)
                            flag = BitConverter
                                .ToUInt32(
                                originalChunkData,
                                origSrc + 16 +
                                (2 * origBlk
                                    .VertexCount
                                    + i) * 16);
                        outStream.Write(
                            BitConverter.GetBytes(
                                flag), 0, 4);
                        Vec2 uv = bu[i];
                        outStream.Write(
                            BitConverter.GetBytes(
                                uv.U), 0, 4);
                        outStream.Write(
                            BitConverter.GetBytes(
                                uv.V), 0, 4);
                        outStream.Write(
                            BitConverter.GetBytes(
                                (uint)0), 0, 4);
                    }
                }
            }

            int lastEnd = 0;
            foreach (var mb in batchesInChunk)
            {
                foreach (var blk in mb.Blocks)
                {
                    int end = blk.ChunkOffset +
                        16 + 3 *
                        blk.VertexCount * 16;
                    if (end > lastEnd)
                        lastEnd = end;
                }
            }
            if (lastEnd <
                originalChunkData.Length)
            {
                int tailLen =
                    originalChunkData.Length -
                    lastEnd;
                while (outStream.Length
                        % 16 != 0)
                    outStream.WriteByte(0);
                outStream.Write(
                    originalChunkData,
                    lastEnd, tailLen);
            }
            byte[] result =
                outStream.ToArray();
            foreach (var kv in ptrToNewOff)
            {
                int pi = kv.Key;
                int newOff = kv.Value;
                byte[] ob =
                    BitConverter.GetBytes(
                        (uint)newOff);
                result[pi * 4] = ob[0];
                result[pi * 4 + 1] = ob[1];
                result[pi * 4 + 2] = ob[2];
                result[pi * 4 + 3] = ob[3];
            }
            return result;
        }

        private byte[] ReassembleRdtb(
    byte[] originalRdtb,
    Dictionary<int, byte[]> newChunks)
        {
            int ptrCount =
                BitConverter.ToUInt16(
                    originalRdtb, 0x0C);
            int boneCount =
                BitConverter.ToUInt16(
                    originalRdtb, 0x0E);
            var offsets = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                int v = BitConverter.ToInt32(
                    originalRdtb,
                    0x10 + i * 4);
                if (v == 0 || v < 0x48 ||
                    v > originalRdtb.Length)
                    break;
                if (v == -1) continue;
                offsets.Add(v);
            }
            var chunks =
                new List<byte[]>();
            for (int i = 0;
                 i < offsets.Count; i++)
            {
                int s = offsets[i];
                int e = (i + 1 <
                    offsets.Count)
                    ? offsets[i + 1]
                    : originalRdtb.Length;
                byte[] c = new byte[e - s];
                Array.Copy(originalRdtb,
                    s, c, 0, e - s);
                chunks.Add(c);
            }
            foreach (var kv in newChunks)
            {
                int ci = kv.Key;
                if (ci < chunks.Count)
                    chunks[ci] = kv.Value;
            }
            // Pad each chunk to 16
            for (int i = 0;
                 i < chunks.Count; i++)
            {
                int pad = (16 -
                    chunks[i].Length % 16)
                    % 16;
                if (pad > 0)
                {
                    byte[] padded =
                        new byte[
                            chunks[i].Length +
                            pad];
                    Array.Copy(chunks[i],
                        padded,
                        chunks[i].Length);
                    chunks[i] = padded;
                }
            }
            byte[] header = new byte[0x48];
            header[0] = (byte)'R';
            header[1] = (byte)'D';
            header[2] = (byte)'T';
            header[3] = (byte)'B';
            Array.Copy(originalRdtb, 4,
                header, 4, 4);
            Array.Copy(originalRdtb, 8,
                header, 8, 4);
            byte[] pc =
                BitConverter.GetBytes(
                    (ushort)ptrCount);
            header[0x0C] = pc[0];
            header[0x0D] = pc[1];
            byte[] bc =
                BitConverter.GetBytes(
                    (ushort)boneCount);
            header[0x0E] = bc[0];
            header[0x0F] = bc[1];
            var newOffsets = new int[
                chunks.Count];
            int cursor = 0x48;
            for (int i = 0;
                 i < chunks.Count; i++)
            {
                newOffsets[i] = cursor;
                cursor += chunks[i].Length;
            }
            for (int i = 0;
                 i < newOffsets.Length; i++)
            {
                byte[] ob =
                    BitConverter.GetBytes(
                        newOffsets[i]);
                int pos = 0x10 + i * 4;
                header[pos] = ob[0];
                header[pos + 1] = ob[1];
                header[pos + 2] = ob[2];
                header[pos + 3] = ob[3];
            }
            byte[] result = new byte[cursor];
            Array.Copy(header, 0,
                result, 0, 0x48);
            for (int i = 0;
                 i < chunks.Count; i++)
                Array.Copy(chunks[i], 0,
                    result, newOffsets[i],
                    chunks[i].Length);
            return result;
        }

        private static ManifestData3D
    LoadManifest(string path)
        {
            string json = File.ReadAllText(
                path, Encoding.UTF8);
            var m = new ManifestData3D();
            m.Version = JStr(json, "version");
            m.SourceRdtb =
                JStr(json, "source_rdtb");
            m.SourceGdtb =
                JStr(json, "source_gdtb");
            m.OriginalRdtbName =
                JStr(json,
                    "original_rdtb_name");
            m.OriginalGdtbName =
                JStr(json,
                    "original_gdtb_name");
            m.SourceSize =
                JInt(json, "source_size");
            m.Chunk11Offset =
                JInt(json, "chunk11_offset");
            m.Chunk11Size =
                JInt(json, "chunk11_size");
            m.MeshChunkIdx =
                JInt(json, "mesh_chunk_idx");

            // all_mesh_chunks
            int amc = json.IndexOf(
                "\"all_mesh_chunks\":");
            if (amc >= 0)
            {
                int ab = json.IndexOf('[', amc);
                int ae = MatchBracket(json, ab);
                if (ab >= 0 && ae > ab)
                {
                    string arr =
                        json.Substring(
                            ab, ae - ab + 1);
                    int p = 0;
                    while (p < arr.Length)
                    {
                        int ob = arr.IndexOf(
                            '{', p);
                        if (ob < 0) break;
                        int oe = arr.IndexOf(
                            '}', ob);
                        if (oe < 0) break;
                        string obj =
                            arr.Substring(
                                ob, oe - ob + 1);
                        var d =
                            new Dictionary<
                                string, int>
                        {
                            { "index",
                              JInt(obj,
                                "index") },
                            { "offset",
                              JInt(obj,
                                "offset") },
                            { "size",
                              JInt(obj,
                                "size") },
                        };
                        m.AllMeshChunks.Add(d);
                        p = oe + 1;
                    }
                }
            }

            int bi = json.IndexOf(
                "\"batches\":");
            if (bi < 0) return m;
            int ba = json.IndexOf('[', bi);
            int bend = MatchBracket(json, ba);
            if (ba < 0 || bend <= ba) return m;
            string batchArr = json.Substring(
                ba, bend - ba + 1);
            int bp = 0;
            while (bp < batchArr.Length)
            {
                int ob = batchArr.IndexOf(
                    '{', bp);
                if (ob < 0) break;
                int oe = MatchBrace(
                    batchArr, ob);
                if (oe < 0) break;
                string bobj =
                    batchArr.Substring(
                        ob, oe - ob + 1);
                var mb = new ManifestBatch3D
                {
                    Index = JInt(bobj, "index"),
                    TexId = JInt(bobj, "tex_id"),
                    SourceChunk = JInt(bobj,
                        "source_chunk"),
                    ChunkOffset = JInt(bobj,
                        "chunk_offset"),
                    VertexCount = JInt(bobj,
                        "vertex_count"),
                    FaceCount = JInt(bobj,
                        "face_count"),
                    ObjVertStart = JInt(bobj,
                        "obj_vert_start"),
                    ObjVertEnd = JInt(bobj,
                        "obj_vert_end"),
                };
                float[] so = JFloatArr(bobj,
                    "spread_offset");
                if (so.Length >= 3)
                    mb.SpreadOffset = new Vec3(
                        so[0], so[1], so[2]);
                float[] bo2 = JFloatArr(bobj,
                    "bone_offset");
                if (bo2.Length >= 3)
                    mb.BoneOffset = new Vec3(
                        bo2[0], bo2[1], bo2[2]);
                float[] lc2 = JFloatArr(bobj,
                    "local_centroid");
                if (lc2.Length >= 3)
                    mb.LocalCentroid = new Vec3(
                        lc2[0], lc2[1], lc2[2]);

                // vif_blocks
                int vbi = bobj.IndexOf(
                    "\"vif_blocks\":");
                if (vbi >= 0)
                {
                    int vba = bobj.IndexOf(
                        '[', vbi);
                    int vbe = MatchBracket(
                        bobj, vba);
                    if (vba >= 0 && vbe > vba)
                    {
                        string vArr =
                            bobj.Substring(
                                vba,
                                vbe - vba + 1);
                        int vp = 0;
                        while (vp < vArr.Length)
                        {
                            int vo = vArr
                                .IndexOf('{', vp);
                            if (vo < 0) break;
                            int ve = vArr
                                .IndexOf('}', vo);
                            if (ve < 0) break;
                            string vobj =
                                vArr.Substring(
                                    vo,
                                    ve - vo + 1);
                            mb.Blocks.Add(
                                new ManifestBlock3D
                                {
                                    ChunkOffset =
                                        JInt(vobj,
                                            "chunk_offset"),
                                    VertexCount =
                                        JInt(vobj,
                                            "vertex_count"),
                                    FirstVertex =
                                        JInt(vobj,
                                            "first_vertex"),
                                });
                            vp = ve + 1;
                        }
                    }
                }

                int lsi = bobj.IndexOf(
                    "\"lod_siblings\":");
                if (lsi >= 0)
                {
                    int lsa = bobj.IndexOf(
                        '[', lsi);
                    int lse = MatchBracket(
                        bobj, lsa);
                    if (lsa >= 0 && lse > lsa)
                    {
                        string sArr =
                            bobj.Substring(
                                lsa,
                                lse - lsa + 1);
                        int sp = 0;
                        while (sp < sArr.Length)
                        {
                            int sob =
                                sArr.IndexOf(
                                    '{', sp);
                            if (sob < 0) break;
                            int soe =
                                MatchBrace(
                                    sArr, sob);
                            if (soe < 0) break;
                            string sobj =
                                sArr.Substring(
                                    sob,
                                    soe - sob + 1);
                            var sib =
                                new LODSiblingInfo
                                {
                                    ChunkIdx =
                                    JInt(sobj,
                                        "chunk_idx"),
                                    BatchIndex =
                                    JInt(sobj,
                                        "batch_index"),
                                    ChunkOffset =
                                    JInt(sobj,
                                        "chunk_offset"),
                                    VertexCount =
                                    JInt(sobj,
                                        "vertex_count"),
                                };
                            // Parse vif_blocks
                            // inside sibling
                            int svbi =
                                sobj.IndexOf(
                                    "\"vif_blocks\":");
                            if (svbi >= 0)
                            {
                                int svba =
                                    sobj.IndexOf(
                                        '[', svbi);
                                int svbe =
                                    MatchBracket(
                                        sobj,
                                        svba);
                                if (svba >= 0 &&
                                    svbe > svba)
                                {
                                    string vArr2 =
                                        sobj.Substring(
                                            svba,
                                            svbe - svba + 1);
                                    int vp2 = 0;
                                    while (vp2 <
                                        vArr2.Length)
                                    {
                                        int vo2 =
                                            vArr2.IndexOf(
                                                '{', vp2);
                                        if (vo2 < 0)
                                            break;
                                        int ve2 =
                                            vArr2.IndexOf(
                                                '}', vo2);
                                        if (ve2 < 0)
                                            break;
                                        string vobj2 =
                                            vArr2.Substring(
                                                vo2,
                                                ve2 -
                                                vo2 + 1);
                                        int off =
                                            JInt(vobj2,
                                                "offset");
                                        int vc =
                                            JInt(vobj2,
                                                "vc");
                                        sib.VifBlocks
                                            .Add(
                                            (off, vc));
                                        vp2 = ve2 + 1;
                                    }
                                }
                            }
                            mb.LodSiblings.Add(sib);
                            sp = soe + 1;
                        }
                    }
                }
                m.Batches.Add(mb);
                bp = oe + 1;
            }
            m.Batches.Sort((a, b) =>
            {
                int c = a.TexId.CompareTo(b.TexId);
                return c != 0
                    ? c
                    : a.Index.CompareTo(b.Index);
            });
            return m;
        }

        private static string JStr(
    string json, string key)
        {
            string s = "\"" + key + "\"";
            int ki = json.IndexOf(s);
            if (ki < 0) return "";
            int c = json.IndexOf(':', ki);
            if (c < 0) return "";
            int q1 = json.IndexOf('"', c + 1);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(
                q1 + 1, q2 - q1 - 1);
        }

        private static int JInt(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0) return 0;
            int vs = ki + s.Length;
            while (vs < json.Length &&
                (json[vs] == ' ' ||
                 json[vs] == '\t' ||
                 json[vs] == '\n' ||
                 json[vs] == '\r'))
                vs++;
            int ve = vs;
            while (ve < json.Length &&
                (char.IsDigit(json[ve]) ||
                 json[ve] == '-'))
                ve++;
            if (ve == vs) return 0;
            int.TryParse(
                json.Substring(vs, ve - vs),
                out int r);
            return r;
        }

        private static float[] JFloatArr(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0) return new float[0];
            int ab = json.IndexOf('[', ki);
            int ae = json.IndexOf(']', ab);
            if (ab < 0 || ae < 0)
                return new float[0];
            string inner =
                json.Substring(
                    ab + 1, ae - ab - 1);
            var parts = inner.Split(',');
            var result = new List<float>();
            foreach (var p in parts)
            {
                if (float.TryParse(
                        p.Trim(),
                        System.Globalization
                            .NumberStyles.Float,
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture,
                        out float v))
                    result.Add(v);
            }
            return result.ToArray();
        }

        private static int MatchBracket(
            string s, int start)
        {
            if (start < 0 ||
                start >= s.Length)
                return -1;
            int depth = 0;
            for (int i = start;
                 i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                if (s[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private static int MatchBrace(
            string s, int start)
        {
            if (start < 0 ||
                start >= s.Length)
                return -1;
            int depth = 0;
            for (int i = start;
                 i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string G(float v)
            => v.ToString("G9",
                System.Globalization
                    .CultureInfo
                    .InvariantCulture);
    }

    internal static class ObjParser
    {
        public static ParsedObj Parse(
            string path)
        {
            var o = new ParsedObj();
            string cg = "default";
            o.FacesByGroup[cg] =
                new List<Tri>();
            using (var fh = new StreamReader(
                path, Encoding.UTF8))
            {
                string line;
                while ((line = fh.ReadLine())
                       != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(
                            line) ||
                        line[0] == '#')
                        continue;
                    string[] p = line.Split(
                        new char[]
                        { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                    if (p.Length == 0)
                        continue;
                    string h = p[0].ToLower();
                    if (h == "v" &&
                        p.Length >= 4)
                    {
                        o._rawV.Add(new Vec3(
                            ParseF(p[1]),
                            ParseF(p[2]),
                            ParseF(p[3])));
                    }
                    else if (h == "vn" &&
                             p.Length >= 4)
                    {
                        o._rawVN.Add(new Vec3(
                            ParseF(p[1]),
                            ParseF(p[2]),
                            ParseF(p[3])));
                    }
                    else if (h == "vt" &&
                             p.Length >= 3)
                    {
                        o._rawVT.Add(new Vec2(
                            ParseF(p[1]),
                            ParseF(p[2])));
                    }
                    else if (h == "g" &&
                             p.Length >= 2)
                    {
                        cg = p[1];
                        if (!o.FacesByGroup
                                .ContainsKey(cg))
                            o.FacesByGroup[cg] =
                                new List<Tri>();
                    }

                    else if (h == "usemtl" &&
                             p.Length >= 2)
                    {
                        // Only use material as group
                        // if current group is default
                        // or empty (no g batch_ set).
                        // When g batch_ IS set, keep
                        // using that as the group so
                        // faces go into the right
                        // batch group.
                        string mname = p[1];
                        if (!o.FacesByGroup
                                .ContainsKey(mname))
                            o.FacesByGroup[mname] =
                                new List<Tri>();
                        if (cg == "default" ||
                            !cg.StartsWith("batch_"))
                            cg = mname;
                    }

                    else if (h == "f" &&
                            p.Length >= 4)
                    {
                        var idx = new int[3];
                        for (int fi = 0;
                             fi < 3; fi++)
                        {
                            string raw =
                                p[fi + 1] + "//";
                            string[] parts =
                                raw.Split('/');
                            int vi = int.Parse(
                                parts[0]) - 1;
                            int ti =
                                parts.Length > 1
                                && !string
                                    .IsNullOrEmpty(
                                        parts[1])
                                ? int.Parse(
                                    parts[1]) - 1
                                : vi;
                            int ni =
                                parts.Length > 2
                                && !string
                                    .IsNullOrEmpty(
                                        parts[2])
                                ? int.Parse(
                                    parts[2]) - 1
                                : vi;
                            var key =
                                (vi, ti, ni);
                            int newIdx;
                            if (!o._comboMap
                                    .TryGetValue(
                                        key,
                                        out newIdx))
                            {
                                newIdx =
                                    o.Verts.Count;
                                o._comboMap[key] =
                                    newIdx;
                                o.Verts.Add(
                                    vi < o._rawV
                                            .Count
                                    ? o._rawV[vi]
                                    : Vec3.Zero);
                                o.UVs.Add(
                                    ti >= 0 &&
                                    ti < o._rawVT
                                            .Count
                                    ? o._rawVT[ti]
                                    : new Vec2(
                                        0, 0));
                                o.Normals.Add(
                                    ni >= 0 &&
                                    ni < o._rawVN
                                            .Count
                                    ? o._rawVN[ni]
                                    : new Vec3(
                                        0, 1, 0));
                            }
                            idx[fi] = newIdx;
                        }
                        var t2 = new Tri(
                            idx[0],
                            idx[1],
                            idx[2]);
                        o.FacesByGroup[cg]
                            .Add(t2);
                        o.AllFaces.Add(t2);
                    }
                }
            }
            return o;
        }

        private static float ParseF(string s)
        {
            return float.Parse(s,
                System.Globalization
                    .NumberStyles.Float,
                System.Globalization
                    .CultureInfo
                    .InvariantCulture);
        }
    }
}
