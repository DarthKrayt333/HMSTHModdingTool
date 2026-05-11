using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.IO;
using HMSTHModdingTool.RDTB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB3D
{
    // ═════════════════════════════════════════════
    // VIF CONSTANTS
    // ═════════════════════════════════════════════
    internal static class VIFConstants
    {
        public const byte VIF_B0 = 0x00;
        public const byte VIF_B1 = 0x80;
        public const byte VIF_B3 = 0x6C;
        public const uint FLAG_ZERO =
            0x00000000;
        public const uint FLAG_ONE =
            0x3F800000;
        public const uint FLAG_EOF =
            0x70000000;
        public const uint MARK_END_A =
            0x14000000;
        public const uint MARK_END_B =
            0x17000000;

        public const float SPREAD_X = 60.0f;
        public const float SPREAD_Y = 80.0f;
    }

    // ═════════════════════════════════════════════
    // MATH TYPES
    // ═════════════════════════════════════════════
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z)
        { X = x; Y = y; Z = z; }
    }

    public struct Vec2
    {
        public float U, V;
        public Vec2(float u, float v)
        { U = u; V = v; }
    }

    public struct Tri
    {
        public int A, B, C;
        public Tri(int a, int b, int c)
        { A = a; B = b; C = c; }
    }

    // ═════════════════════════════════════════════
    // VIF BLOCK INFO
    // ═════════════════════════════════════════════
    public class VIFBlockInfo
    {
        public int OffsetInChunk;
        public int VertexCount;
        public int FirstVertexIndex;
    }

    // ═════════════════════════════════════════════
    // MESH BATCH
    // ═════════════════════════════════════════════
    public class MeshBatch
    {
        public int Index;
        public int Offset;
        public int TexId;
        public Vec3 SpreadOffset;
        public byte[] ChunkSig = new byte[8];

        public int ObjVertStart;
        public int ObjVertEnd;

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

    // ═════════════════════════════════════════════
    // RDTB 3D EXTRACTOR
    // ═════════════════════════════════════════════
    public class RDTB3DExtractor
    {
        public bool _useNativeLayout = false;
        public int _forceChunkIdx = -1;

        private byte[] _data;
        private List<int> _chunkOffsets;
        private List<byte[]> _chunks;
        private int _ptrCount;
        private int _boneCount;
        private string _gdtbPath;

        public static void Extract(
            string rdtbPath,
            string gdtbPath,
            string baseName)
        {
            var ex = new RDTB3DExtractor();
            ex.DoExtract(
                rdtbPath, gdtbPath, baseName);
        }

        public static void ExtractNative(
            string rdtbPath,
            string gdtbPath,
            string baseName)
        {
            var ex = new RDTB3DExtractor();
            ex._useNativeLayout = true;
            ex.DoExtract(
                rdtbPath, gdtbPath, baseName);
        }

        public static void ExtractSingleChunk(
            string rdtbPath,
            string gdtbPath,
            int chunkIdx,
            string baseName)
        {
            var ex = new RDTB3DExtractor();
            ex._useNativeLayout = true;
            ex._forceChunkIdx = chunkIdx;
            ex.DoExtract(
                rdtbPath, gdtbPath,
                baseName + "_c" + chunkIdx);
        }

        private void DoExtract(
            string rdtbPath,
            string gdtbPath,
            string baseName)
        {
            if (string.IsNullOrEmpty(rdtbPath)
                || !File.Exists(rdtbPath))
                throw new
                    FileNotFoundException(
                    "RDTB not found: " +
                    rdtbPath);

            if (string.IsNullOrEmpty(gdtbPath)
                || !File.Exists(gdtbPath))
                throw new
                    FileNotFoundException(
                    "GDTB not found: " +
                    gdtbPath);

            if (string.IsNullOrEmpty(baseName))
                throw new ArgumentException(
                    "Base name cannot be empty");

            string dir =
                Path.GetDirectoryName(
                    rdtbPath) ?? ".";
            if (string.IsNullOrEmpty(dir))
                dir = ".";
            dir = Path.GetFullPath(dir);

            _gdtbPath = gdtbPath;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] HMSTH 3D Extractor");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
            Console.WriteLine(
                "    RDTB      : " +
                Path.GetFileName(rdtbPath));
            Console.WriteLine(
                "    GDTB      : " +
                Path.GetFileName(gdtbPath));
            Console.WriteLine(
                "    Base name : " + baseName);
            Console.WriteLine(
                "    Out dir   : " + dir);

            if (_useNativeLayout)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    Mode      : NATIVE");
                Console.ResetColor();
            }

            Console.WriteLine(
                new string('=', 64));

            LoadRDTB(rdtbPath);

            string folderObj =
                Path.Combine(
                    dir, baseName + "_obj");
            string folderDae =
                Path.Combine(
                    dir, baseName + "_dae");
            string folderAllObj =
                Path.Combine(
                    dir,
                    baseName + "_all_obj");
            string folderAllDae =
                Path.Combine(
                    dir,
                    baseName + "_all_dae");

            Directory.CreateDirectory(
                folderObj);
            Directory.CreateDirectory(
                folderDae);
            Directory.CreateDirectory(
                folderAllObj);
            Directory.CreateDirectory(
                folderAllDae);

            string texObj =
                Path.Combine(
                    folderObj, "textures");
            string texDae =
                Path.Combine(
                    folderDae, "textures");
            string texAllObj =
                Path.Combine(
                    folderAllObj,
                    "textures");
            string texAllDae =
                Path.Combine(
                    folderAllDae,
                    "textures");

            Directory.CreateDirectory(texObj);
            Directory.CreateDirectory(texDae);
            Directory.CreateDirectory(
                texAllObj);
            Directory.CreateDirectory(
                texAllDae);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Extracting textures...");
            Console.WriteLine();

            List<string> texPathsObj =
                ExtractTextures(
                    _gdtbPath, texObj,
                    "[obj]");
            List<string> texPathsDae =
                ExtractTextures(
                    _gdtbPath, texDae,
                    "[dae]");
            List<string> texPathsAllObj =
                ExtractTextures(
                    _gdtbPath, texAllObj,
                    "[all_obj]");
            List<string> texPathsAllDae =
                ExtractTextures(
                    _gdtbPath, texAllDae,
                    "[all_dae]");

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    [OK] " +
                texPathsObj.Count +
                " textures extracted");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "[+] Parsing chunk 8...");

            if (_chunks.Count < 9)
                throw new InvalidDataException(
                    "RDTB has fewer than 9 " +
                    "chunks.");

            List<Chunk8Record> mats =
                ParseChunk8(_chunks[8]);
            Console.WriteLine(
                "    Records: " + mats.Count);

            int meshChunkIdx =
                (_forceChunkIdx >= 0 &&
                 _forceChunkIdx <
                 _chunks.Count)
                ? _forceChunkIdx
                : 11;

            Console.WriteLine();
            Console.WriteLine(
                "[+] Parsing chunk " +
                meshChunkIdx + " (mesh)...");

            if (_chunks.Count <= meshChunkIdx)
                throw new InvalidDataException(
                    "RDTB has fewer than " +
                    (meshChunkIdx + 1) +
                    " chunks.");

            List<MeshBatch> batches =
                ParseBatches(
                    _chunks[meshChunkIdx],
                    mats);
            Console.WriteLine(
                "    Batches: " +
                batches.Count);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Skipping dedupe " +
                "(was removing too many)");
            batches =
                DedupeByChunk8(batches);
            Console.WriteLine(
                "    Kept: " +
                batches.Count);

            AssignObjVertRanges(batches);

            var texGroups =
                GroupByTextureNumber(batches);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Model -> Texture map:");
            Console.ResetColor();

            foreach (var kvp in texGroups)
            {
                int totalV = kvp.Value
                    .Sum(b => b.Verts.Count);
                Console.WriteLine(
                    "    model_" +
                    kvp.Key.ToString("D2") +
                    ".obj  <->  texture_" +
                    kvp.Key.ToString("D2") +
                    ".bmp  (" +
                    kvp.Value.Count +
                    " batches, " +
                    totalV + " verts)");
            }

            Console.WriteLine();
            Console.WriteLine(
                "[+] Computing spread...");
            ApplySpreadLayout(texGroups);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Writing per-texture " +
                "OBJ to " +
                Path.GetFileName(folderObj));
            WritePerTextureObj(
                folderObj, texGroups,
                texPathsObj);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Writing per-texture " +
                "DAE to " +
                Path.GetFileName(folderDae));
            WritePerTextureDae(
                folderDae, texGroups,
                texPathsDae);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Writing combined OBJ to " +
                Path.GetFileName(
                    folderAllObj));
            WriteAllInOneObj(
                folderAllObj, baseName,
                texGroups, texPathsAllObj);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Writing combined DAE to " +
                Path.GetFileName(
                    folderAllDae));
            WriteAllInOneDae(
                folderAllDae, baseName,
                texGroups, texPathsAllDae);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Writing manifests...");
            WriteManifest(
                folderObj, baseName,
                rdtbPath, gdtbPath,
                batches, texGroups,
                meshChunkIdx);
            WriteManifest(
                folderDae, baseName,
                rdtbPath, gdtbPath,
                batches, texGroups,
                meshChunkIdx);
            WriteManifest(
                folderAllObj, baseName,
                rdtbPath, gdtbPath,
                batches, texGroups,
                meshChunkIdx);
            WriteManifest(
                folderAllDae, baseName,
                rdtbPath, gdtbPath,
                batches, texGroups,
                meshChunkIdx);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Extraction complete!");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void AssignObjVertRanges(
            List<MeshBatch> batches)
        {
            var texOffset =
                new Dictionary<int, int>();

            foreach (var b in batches)
            {
                if (!texOffset
                        .ContainsKey(b.TexId))
                    texOffset[b.TexId] = 0;

                b.ObjVertStart =
                    texOffset[b.TexId];
                b.ObjVertEnd =
                    b.ObjVertStart +
                    b.Verts.Count;
                texOffset[b.TexId] =
                    b.ObjVertEnd;
            }
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

            _ptrCount =
                BitConverter.ToUInt16(
                    _data, 0x0C);
            _boneCount =
                BitConverter.ToUInt16(
                    _data, 0x0E);

            _chunkOffsets = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                int v = BitConverter.ToInt32(
                    _data, 0x10 + i * 4);
                if (v == 0 ||
                    v < 0x48 ||
                    v > _data.Length)
                    break;
                _chunkOffsets.Add(v);
            }

            _chunks = new List<byte[]>();
            for (int i = 0;
                 i < _chunkOffsets.Count; i++)
            {
                int s = _chunkOffsets[i];
                int e =
                    (i + 1 <
                     _chunkOffsets.Count)
                    ? _chunkOffsets[i + 1]
                    : _data.Length;
                byte[] c = new byte[e - s];
                Array.Copy(
                    _data, s, c, 0, e - s);
                _chunks.Add(c);
            }
        }

        private List<string> ExtractTextures(
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

                var files = Directory
                    .GetFiles(
                        outFolder,
                        "texture_*.bmp")
                    .OrderBy(f =>
                    {
                        string name =
                            Path
                            .GetFileNameWithoutExtension(
                                f);
                        string num =
                            name.Replace(
                                "texture_", "");
                        int n;
                        int.TryParse(
                            num, out n);
                        return n;
                    })
                    .ToList();

                result.AddRange(files);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    " + label + " " +
                    result.Count +
                    " textures:");
                foreach (var f in files)
                    Console.WriteLine(
                        "      " +
                        Path.GetFileName(f));
                Console.ResetColor();
            }
            catch (Exception e)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    " + label +
                    " [!] " + e.Message);
                Console.ResetColor();
            }

            return result;
        }

        // ═════════════════════════════════════════
        // CHUNK 8
        // ═════════════════════════════════════════
        public class Chunk8Record
        {
            public ushort A, B, C, Tex;
            public byte[] Sig = new byte[8];
        }

        private List<Chunk8Record>
            ParseChunk8(byte[] c8)
        {
            var r =
                new List<Chunk8Record>();
            if (c8.Length < 4) return r;

            uint first =
                BitConverter.ToUInt32(c8, 0);
            if (first == 0 ||
                first > (uint)c8.Length)
                return r;

            int bc = (int)(first / 4);
            for (int i = 0; i < bc; i++)
            {
                uint ptr =
                    BitConverter.ToUInt32(
                        c8, i * 4);
                var rec = new Chunk8Record();
                if (ptr + 8 > c8.Length)
                {
                    r.Add(rec);
                    continue;
                }
                rec.A =
                    BitConverter.ToUInt16(
                        c8, (int)ptr);
                rec.B =
                    BitConverter.ToUInt16(
                        c8, (int)ptr + 2);
                rec.C =
                    BitConverter.ToUInt16(
                        c8, (int)ptr + 4);
                rec.Tex =
                    BitConverter.ToUInt16(
                        c8, (int)ptr + 6);
                Array.Copy(
                    c8, (int)ptr,
                    rec.Sig, 0, 8);
                r.Add(rec);
            }
            return r;
        }

        // ═════════════════════════════════════════
        // VIF HELPERS
        // ═════════════════════════════════════════
        private bool IsVifHdr(
            byte[] d, int o)
        {
            if (o + 16 > d.Length)
                return false;
            return
                d[o] ==
                    VIFConstants.VIF_B0 &&
                d[o + 1] ==
                    VIFConstants.VIF_B1 &&
                d[o + 3] ==
                    VIFConstants.VIF_B3;
        }

        private List<int> FindAllVif(
            byte[] c)
        {
            var r = new List<int>();
            int i = 0;
            while (i + 16 <= c.Length)
            {
                if (IsVifHdr(c, i))
                {
                    r.Add(i);
                    i += 16;
                }
                else
                    i += 4;
            }
            return r;
        }

        private struct Row
        {
            public uint Flag;
            public float X, Y, Z;
        }

        private List<Row> ParseRows(
            byte[] c, int ds, int de)
        {
            var rows = new List<Row>();
            int o = ds;
            while (o + 16 <= de)
            {
                uint flag =
                    BitConverter.ToUInt32(
                        c, o);

                if (flag ==
                    VIFConstants.FLAG_EOF)
                    break;

                if (flag ==
                    VIFConstants.FLAG_ONE)
                {
                    uint m =
                        BitConverter.ToUInt32(
                            c, o + 4);
                    if (m ==
                        VIFConstants.MARK_END_A
                     || m ==
                        VIFConstants.MARK_END_B)
                        break;
                }

                Row row = new Row();
                row.Flag = flag;
                row.X =
                    BitConverter.ToSingle(
                        c, o + 4);
                row.Y =
                    BitConverter.ToSingle(
                        c, o + 8);
                row.Z =
                    BitConverter.ToSingle(
                        c, o + 12);

                rows.Add(row);
                o += 16;
            }
            return rows;
        }

        private int DetectN(List<Row> rows)
        {
            if (rows.Count == 0) return 0;
            int nz = 0;
            foreach (var r in rows)
            {
                if (r.Flag ==
                    VIFConstants.FLAG_ZERO)
                    nz++;
                else
                    break;
            }
            if (nz >= 2)
            {
                int n = nz - 1;
                if (n * 3 <= rows.Count)
                {
                    bool ok = true;
                    int u =
                        Math.Min(
                            3 * n,
                            rows.Count);
                    for (int i = 2 * n;
                         i < u; i++)
                    {
                        if (rows[i].Flag !=
                            VIFConstants
                                .FLAG_ONE)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (ok) return n;
                }
            }
            if (rows.Count % 3 == 0)
                return rows.Count / 3;
            for (int n = 1;
                 n <= rows.Count / 3; n++)
                if (n * 3 <= rows.Count)
                    return n;
            return 0;
        }

        private List<Tri> MakeStrip(int n)
        {
            var r = new List<Tri>();
            for (int i = 0; i < n - 2; i++)
            {
                if (i % 2 == 0)
                    r.Add(new Tri(
                        i, i + 1, i + 2));
                else
                    r.Add(new Tri(
                        i, i + 2, i + 1));
            }
            return r;
        }

        private List<Tri> FilterDegen(
            List<Tri> faces,
            List<Vec3> verts)
        {
            var g = new List<Tri>();
            foreach (var t in faces)
            {
                if (t.A >= verts.Count ||
                    t.B >= verts.Count ||
                    t.C >= verts.Count)
                    continue;
                var v0 = verts[t.A];
                var v1 = verts[t.B];
                var v2 = verts[t.C];
                float ax = v1.X - v0.X,
                      ay = v1.Y - v0.Y,
                      az = v1.Z - v0.Z;
                float bx = v2.X - v0.X,
                      by = v2.Y - v0.Y,
                      bz = v2.Z - v0.Z;
                float cx = ay * bz - az * by,
                      cy = az * bx - ax * bz,
                      cz = ax * by - ay * bx;
                if (cx * cx + cy * cy +
                    cz * cz > 1e-10f)
                    g.Add(t);
            }
            return g;
        }

        // ═════════════════════════════════════════
        // PARSE BATCHES
        // ═════════════════════════════════════════
        private List<MeshBatch> ParseBatches(
            byte[] c11,
            List<Chunk8Record> mats)
        {
            var batches =
                new List<MeshBatch>();
            if (c11.Length < 32)
                return batches;

            int exp = mats.Count;

            var ptrToBatchIdx =
                new Dictionary<int, int>();
            var orderedPtrs =
                new List<int>();

            for (int i = 0; i < exp; i++)
            {
                int ptrOff = i * 4;
                if (ptrOff + 4 > c11.Length)
                    break;
                int ptr = (int)
                    BitConverter.ToUInt32(
                        c11, ptrOff);
                if (ptr >= 0 &&
                    ptr < c11.Length &&
                    IsVifHdr(c11, ptr) &&
                    !ptrToBatchIdx
                        .ContainsKey(ptr))
                {
                    ptrToBatchIdx[ptr] = i;
                    orderedPtrs.Add(ptr);
                }
            }

            orderedPtrs.Sort();
            var allVif = FindAllVif(c11);

            for (int bi = 0;
                 bi < orderedPtrs.Count;
                 bi++)
            {
                int bs = orderedPtrs[bi];
                int be =
                    (bi + 1 <
                     orderedPtrs.Count)
                    ? orderedPtrs[bi + 1]
                    : c11.Length;

                var lv = allVif
                    .Where(v =>
                        v >= bs && v < be)
                    .ToList();
                if (lv.Count == 0) continue;

                var b = new MeshBatch();
                b.Index = bi;
                b.Offset = bs;

                for (int vi = 0;
                     vi < lv.Count; vi++)
                {
                    int vo = lv[vi];
                    int ve =
                        (vi + 1 < lv.Count)
                        ? lv[vi + 1] : be;

                    var rows = ParseRows(
                        c11, vo + 16, ve);
                    if (rows.Count < 3)
                        continue;

                    int n = DetectN(rows);
                    if (n < 1 ||
                        n * 3 > rows.Count)
                        continue;

                    int bv = b.Verts.Count;

                    var info =
                        new VIFBlockInfo();
                    info.OffsetInChunk = vo;
                    info.VertexCount = n;
                    info.FirstVertexIndex =
                        bv;
                    b.Blocks.Add(info);

                    for (int i = 0;
                         i < n; i++)
                        b.Verts.Add(new Vec3(
                            rows[i].X,
                            rows[i].Y,
                            rows[i].Z));

                    for (int i = n;
                         i < 2 * n; i++)
                        b.Normals.Add(
                            new Vec3(
                                rows[i].X,
                                rows[i].Y,
                                rows[i].Z));

                    for (int i = 2 * n;
                         i < 3 * n; i++)
                        b.UVs.Add(new Vec2(
                            rows[i].X,
                            1.0f -
                            rows[i].Y));

                    foreach (var t in
                        MakeStrip(n))
                        b.Faces.Add(new Tri(
                            bv + t.A,
                            bv + t.B,
                            bv + t.C));
                }

                int matIdx =
                    ptrToBatchIdx
                        .ContainsKey(bs)
                    ? ptrToBatchIdx[bs]
                    : bi;

                if (mats != null &&
                    matIdx < mats.Count)
                {
                    b.TexId =
                        mats[matIdx].Tex;
                    Array.Copy(
                        mats[matIdx].Sig,
                        b.ChunkSig, 8);
                }

                b.Faces = FilterDegen(
                    b.Faces, b.Verts);

                if (b.Verts.Count > 0 &&
                    b.Faces.Count > 0)
                    batches.Add(b);
            }

            return batches;
        }

        // ═════════════════════════════════════════
        // DEDUPE - DISABLED
        // Old version removed too many batches
        // (51 out of 114) which lost arms,
        // legs, torso. Now keeps everything.
        // ═════════════════════════════════════════
        private List<MeshBatch> DedupeByChunk8(
            List<MeshBatch> batches)
        {
            return batches;
        }

        // ═════════════════════════════════════════
        // GROUP BY TEXTURE NUMBER
        // ═════════════════════════════════════════
        private SortedDictionary<
            int, List<MeshBatch>>
            GroupByTextureNumber(
                List<MeshBatch> batches)
        {
            var r =
                new SortedDictionary<
                    int,
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

        // ═════════════════════════════════════════
        // APPLY SPREAD LAYOUT
        // Body parts: NO spread
        // Tools: spread along X axis
        // ═════════════════════════════════════════
        private void ApplySpreadLayout(
            SortedDictionary<
                int,
        List<MeshBatch>> groups)
        {
            if (_useNativeLayout)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [Native: NO spread]");
                Console.ResetColor();
                foreach (var kvp in groups)
                    foreach (var b in kvp.Value)
                        b.SpreadOffset =
                            new Vec3(0, 0, 0);
                return;
            }

            // Find tools group
            // (most batches = tools)
            int toolsTexId = -1;
            int maxBatches = 0;
            foreach (var kvp in groups)
            {
                if (kvp.Value.Count > maxBatches)
                {
                    maxBatches = kvp.Value.Count;
                    toolsTexId = kvp.Key;
                }
            }

            foreach (var kvp in groups)
            {
                int texId = kvp.Key;
                var bs = kvp.Value;

                if (texId != toolsTexId)
                {
                    // Body parts: NO spread
                    foreach (var b in bs)
                        b.SpreadOffset =
                            new Vec3(0, 0, 0);
                    continue;
                }

                // ─────────────────────────────
                // TOOLS: bounds-aware X spread
                // Centered at world origin
                // ─────────────────────────────

                // Pass 1: compute all bounds
                var allBounds =
                    new List<float[]>();

                for (int pi = 0;
                     pi < bs.Count; pi++)
                {
                    var b = bs[pi];
                    if (b.Verts.Count == 0)
                    {
                        allBounds.Add(
                            new float[]
                            { 0,0,0,0,0,0 });
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

                    allBounds.Add(
                        new float[]
                        {
                    mnx, mxx,
                    mny, mxy,
                    mnz, mxz
                        });
                }

                // Pass 2: compute per-item
                // offsets left-to-right
                const float GAP = 5.0f;
                float cursorX = 0.0f;
                var offsets = new Vec3[bs.Count];

                for (int pi = 0;
                     pi < bs.Count; pi++)
                {
                    var b = bs[pi];

                    if (b.Verts.Count == 0)
                    {
                        offsets[pi] =
                            new Vec3(0, 0, 0);
                        continue;
                    }

                    float mnx = allBounds[pi][0];
                    float mxx = allBounds[pi][1];
                    float mny = allBounds[pi][2];
                    float mxy = allBounds[pi][3];
                    float mnz = allBounds[pi][4];
                    float mxz = allBounds[pi][5];

                    float width = mxx - mnx;
                    float cx = (mnx + mxx) * 0.5f;
                    float cy = (mny + mxy) * 0.5f;
                    float cz = (mnz + mxz) * 0.5f;

                    float targetCX =
                        cursorX + width * 0.5f;

                    offsets[pi] = new Vec3(
                        targetCX - cx,
                        -cy,
                        -cz);

                    cursorX += width + GAP;
                }

                // Pass 3: shift whole group
                // so it's centered at X=0
                float totalWidth = cursorX - GAP;
                float groupShift =
                    totalWidth * 0.5f;

                for (int pi = 0;
                     pi < bs.Count; pi++)
                {
                    bs[pi].SpreadOffset =
                        new Vec3(
                            offsets[pi].X -
                                groupShift,
                            offsets[pi].Y,
                            offsets[pi].Z);
                }
            }
        }

        // ═════════════════════════════════════════
        // WRITE PER-TEXTURE OBJ
        // ═════════════════════════════════════════
        private void WritePerTextureObj(
            string outFolder,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            List<string> texPaths)
        {
            foreach (var kvp in groups)
            {
                int texNum = kvp.Key;
                var batchList = kvp.Value;

                string texPath = null;
                if (texNum < texPaths.Count)
                    texPath =
                        texPaths[texNum];

                string modelName =
                    "model_" +
                    texNum.ToString("D2");
                string objPath =
                    Path.Combine(
                        outFolder,
                        modelName + ".obj");
                string mtlPath =
                    Path.Combine(
                        outFolder,
                        modelName + ".mtl");

                CultureInfo ci =
                    CultureInfo
                        .InvariantCulture;

                using (var sw =
                    new StreamWriter(mtlPath))
                {
                    sw.WriteLine(
                        "# " + modelName);
                    sw.WriteLine();
                    sw.WriteLine(
                        "newmtl " +
                        modelName);
                    sw.WriteLine("Ka 1 1 1");
                    sw.WriteLine("Kd 1 1 1");
                    sw.WriteLine("Ks 0 0 0");
                    sw.WriteLine("Ns 10");
                    sw.WriteLine("illum 2");
                    if (texPath != null &&
                        File.Exists(texPath))
                        sw.WriteLine(
                            "map_Kd " +
                            MakeRel(
                                texPath,
                                objPath));
                    sw.WriteLine();
                }

                using (var sw =
                    new StreamWriter(
                        objPath))
                {
                    sw.WriteLine(
                        "# HMSTH model_" +
                        texNum.ToString(
                            "D2"));
                    sw.WriteLine(
                        "mtllib " +
                        Path.GetFileName(
                            mtlPath));
                    sw.WriteLine();

                    int vBase = 1;

                    foreach (var b in
                        batchList)
                    {
                        sw.WriteLine(
                            "# batch " +
                            b.Index);

                        foreach (var v in
                            b.Verts)
                            sw.WriteLine(
                                "v " +
                                (v.X +
                                b.SpreadOffset
                                    .X)
                                .ToString(
                                    "R", ci) +
                                " " +
                                (v.Y +
                                b.SpreadOffset
                                    .Y)
                                .ToString(
                                    "R", ci) +
                                " " +
                                (v.Z +
                                b.SpreadOffset
                                    .Z)
                                .ToString(
                                    "R", ci));
                    }

                    sw.WriteLine();

                    foreach (var b in
                        batchList)
                        foreach (var uv in
                            b.UVs)
                            sw.WriteLine(
                                "vt " +
                                uv.U.ToString(
                                    "R", ci) +
                                " " +
                                uv.V.ToString(
                                    "R", ci));

                    sw.WriteLine();

                    foreach (var b in
                        batchList)
                        foreach (var n in
                            b.Normals)
                            sw.WriteLine(
                                "vn " +
                                n.X.ToString(
                                    "R", ci) +
                                " " +
                                n.Y.ToString(
                                    "R", ci) +
                                " " +
                                n.Z.ToString(
                                    "R", ci));

                    sw.WriteLine();

                    int uvBase = 1;
                    int nrmBase = 1;
                    vBase = 1;

                    foreach (var b in
                        batchList)
                    {
                        sw.WriteLine();
                        sw.WriteLine(
                            "g batch_" +
                            b.Index
                                .ToString(
                                    "D4"));
                        sw.WriteLine(
                            "usemtl " +
                            modelName);

                        foreach (var t in
                            b.Faces)
                        {
                            int a =
                                t.A + vBase;
                            int bb =
                                t.B + vBase;
                            int c =
                                t.C + vBase;
                            int au =
                                t.A + uvBase;
                            int bbu =
                                t.B + uvBase;
                            int cu =
                                t.C + uvBase;
                            int an2 =
                                t.A + nrmBase;
                            int bbn =
                                t.B + nrmBase;
                            int cn =
                                t.C + nrmBase;
                            sw.WriteLine(
                                "f " +
                                a + "/" + au +
                                "/" + an2 +
                                " " +
                                bb + "/" +
                                bbu + "/" +
                                bbn + " " +
                                c + "/" + cu +
                                "/" + cn);
                        }

                        vBase +=
                            b.Verts.Count;
                        uvBase +=
                            b.UVs.Count;
                        nrmBase +=
                            b.Normals.Count;
                    }
                }

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    model_" +
                    texNum.ToString("D2") +
                    ".obj  (" +
                    batchList.Sum(
                        b => b.Verts.Count) +
                    " verts, " +
                    batchList.Count +
                    " batches)");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════════════
        // WRITE PER-TEXTURE DAE
        // ═════════════════════════════════════════
        private void WritePerTextureDae(
            string outFolder,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            List<string> texPaths)
        {
            foreach (var kvp in groups)
            {
                int texNum = kvp.Key;
                var batchList = kvp.Value;

                string texPath = null;
                if (texNum < texPaths.Count)
                    texPath =
                        texPaths[texNum];

                string modelName =
                    "model_" +
                    texNum.ToString("D2");
                string daePath =
                    Path.Combine(
                        outFolder,
                        modelName + ".dae");

                var av = new List<Vec3>();
                var an = new List<Vec3>();
                var au = new List<Vec2>();
                var faces =
                    new List<Tri>();
                int vOff = 0;

                foreach (var b in batchList)
                {
                    foreach (var v in
                        b.Verts)
                        av.Add(new Vec3(
                            v.X +
                            b.SpreadOffset.X,
                            v.Y +
                            b.SpreadOffset.Y,
                            v.Z +
                            b.SpreadOffset.Z));
                    an.AddRange(b.Normals);
                    au.AddRange(b.UVs);
                    foreach (var t in
                        b.Faces)
                        faces.Add(new Tri(
                            t.A + vOff,
                            t.B + vOff,
                            t.C + vOff));
                    vOff += b.Verts.Count;
                }

                WriteDaeFile(
                    daePath, modelName,
                    av, an, au, faces,
                    texPath);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    model_" +
                    texNum.ToString("D2") +
                    ".dae  (" +
                    av.Count + " verts)");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════════════
        // WRITE ALL-IN-ONE OBJ
        // Splits into body and tools files
        // ═════════════════════════════════════════
        private void WriteAllInOneObj(
            string outFolder,
            string baseName,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            List<string> texPaths)
        {
            CultureInfo ci =
                CultureInfo.InvariantCulture;

            int toolsTexId = -1;
            int maxBatches = 0;
            foreach (var kvp in groups)
            {
                if (kvp.Value.Count >
                    maxBatches)
                {
                    maxBatches =
                        kvp.Value.Count;
                    toolsTexId = kvp.Key;
                }
            }

            if (groups.Count <= 1)
                toolsTexId = -1;

            var bodyGroups =
                new SortedDictionary<
                    int,
                    List<MeshBatch>>();
            var toolsGroups =
                new SortedDictionary<
                    int,
                    List<MeshBatch>>();

            foreach (var kvp in groups)
            {
                if (kvp.Key == toolsTexId)
                    toolsGroups[kvp.Key] =
                        kvp.Value;
                else
                    bodyGroups[kvp.Key] =
                        kvp.Value;
            }

            WriteGroupedObj(
                outFolder,
                baseName + "_body",
                bodyGroups,
                texPaths, ci);

            if (toolsGroups.Count > 0)
            {
                WriteGroupedObj(
                    outFolder,
                    baseName + "_tools",
                    toolsGroups,
                    texPaths, ci);
            }
        }

        // ─────────────────────────────────────────
        // WRITE GROUPED OBJ HELPER
        // ─────────────────────────────────────────
        private void WriteGroupedObj(
            string outFolder,
            string name,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            List<string> texPaths,
            CultureInfo ci)
        {
            if (groups.Count == 0) return;

            string objPath =
                Path.Combine(
                    outFolder,
                    name + ".obj");
            string mtlPath =
                Path.Combine(
                    outFolder,
                    name + ".mtl");

            using (var sw =
                new StreamWriter(mtlPath))
            {
                sw.WriteLine(
                    "# " + name + " MTL");
                sw.WriteLine();
                foreach (var kvp in groups)
                {
                    int texNum = kvp.Key;
                    string tp = null;
                    if (texNum <
                        texPaths.Count)
                        tp =
                            texPaths[texNum];
                    sw.WriteLine(
                        "newmtl mat_" +
                        texNum.ToString(
                            "D2"));
                    sw.WriteLine("Ka 1 1 1");
                    sw.WriteLine("Kd 1 1 1");
                    sw.WriteLine("Ks 0 0 0");
                    sw.WriteLine("Ns 10");
                    sw.WriteLine("illum 2");
                    if (tp != null &&
                        File.Exists(tp))
                        sw.WriteLine(
                            "map_Kd " +
                            MakeRel(
                                tp, objPath));
                    sw.WriteLine();
                }
            }

            using (var sw =
                new StreamWriter(objPath))
            {
                sw.WriteLine("# " + name);
                sw.WriteLine(
                    "mtllib " +
                    Path.GetFileName(
                        mtlPath));
                sw.WriteLine();

                int vBase = 1;
                int uvBase = 1;
                int nBase = 1;

                foreach (var kvp in groups)
                {
                    int texNum = kvp.Key;
                    var batchList =
                        kvp.Value;

                    sw.WriteLine();
                    sw.WriteLine(
                        "# texture_" +
                        texNum.ToString(
                            "D2") +
                        ".bmp");

                    foreach (var b in
                        batchList)
                    {
                        foreach (var v in
                            b.Verts)
                            sw.WriteLine(
                                "v " +
                                (v.X +
                                b.SpreadOffset
                                    .X)
                                .ToString(
                                    "R", ci) +
                                " " +
                                (v.Y +
                                b.SpreadOffset
                                    .Y)
                                .ToString(
                                    "R", ci) +
                                " " +
                                (v.Z +
                                b.SpreadOffset
                                    .Z)
                                .ToString(
                                    "R", ci));
                    }

                    sw.WriteLine();

                    foreach (var b in
                        batchList)
                        foreach (var uv in
                            b.UVs)
                            sw.WriteLine(
                                "vt " +
                                uv.U.ToString(
                                    "R", ci) +
                                " " +
                                uv.V.ToString(
                                    "R", ci));

                    sw.WriteLine();

                    foreach (var b in
                        batchList)
                        foreach (var n in
                            b.Normals)
                            sw.WriteLine(
                                "vn " +
                                n.X.ToString(
                                    "R", ci) +
                                " " +
                                n.Y.ToString(
                                    "R", ci) +
                                " " +
                                n.Z.ToString(
                                    "R", ci));

                    sw.WriteLine();

                    foreach (var b in
                        batchList)
                    {
                        sw.WriteLine(
                            "g batch_" +
                            b.Index
                                .ToString(
                                    "D4"));
                        sw.WriteLine(
                            "usemtl mat_" +
                            texNum.ToString(
                                "D2"));

                        foreach (var t in
                            b.Faces)
                        {
                            int a =
                                t.A + vBase;
                            int bb =
                                t.B + vBase;
                            int c =
                                t.C + vBase;
                            int au =
                                t.A + uvBase;
                            int bbu =
                                t.B + uvBase;
                            int cu =
                                t.C + uvBase;
                            int an =
                                t.A + nBase;
                            int bn =
                                t.B + nBase;
                            int cn =
                                t.C + nBase;
                            sw.WriteLine(
                                "f " +
                                a + "/" + au +
                                "/" + an +
                                " " +
                                bb + "/" +
                                bbu + "/" +
                                bn + " " +
                                c + "/" + cu +
                                "/" + cn);
                        }

                        vBase +=
                            b.Verts.Count;
                        uvBase +=
                            b.UVs.Count;
                        nBase +=
                            b.Normals.Count;
                    }
                }
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    " + name +
                ".obj written");
            Console.ResetColor();
        }


        // ═════════════════════════════════════════
        // WRITE ALL-IN-ONE DAE
        // ═════════════════════════════════════════
        private void WriteAllInOneDae(
            string outFolder,
            string baseName,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            List<string> texPaths)
        {
            string daePath =
                Path.Combine(
                    outFolder,
                    baseName + "_all.dae");

            var av = new List<Vec3>();
            var an = new List<Vec3>();
            var au = new List<Vec2>();
            var ft =
                new SortedDictionary<
                    int, List<Tri>>();

            int vOff = 0;
            foreach (var kvp in groups)
            {
                int texNum = kvp.Key;
                var batchList = kvp.Value;
                if (!ft.ContainsKey(texNum))
                    ft[texNum] =
                        new List<Tri>();
                foreach (var b in batchList)
                {
                    foreach (var v in b.Verts)
                        av.Add(new Vec3(
                            v.X +
                            b.SpreadOffset.X,
                            v.Y +
                            b.SpreadOffset.Y,
                            v.Z +
                            b.SpreadOffset.Z));
                    an.AddRange(b.Normals);
                    au.AddRange(b.UVs);
                    foreach (var t in b.Faces)
                        ft[texNum].Add(
                            new Tri(
                                t.A + vOff,
                                t.B + vOff,
                                t.C + vOff));
                    vOff += b.Verts.Count;
                }
            }

            WriteDaeMulti(
                daePath, av, an, au,
                ft, groups, texPaths);
        }

        private void WriteDaeMulti(
            string daePath,
            List<Vec3> av,
            List<Vec3> an,
            List<Vec2> au,
            SortedDictionary<
                int, List<Tri>> ft,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            List<string> texPaths)
        {
            CultureInfo ci =
                CultureInfo.InvariantCulture;

            using (var sw =
                new StreamWriter(
                    daePath, false,
                    new UTF8Encoding(false)))
            {
                sw.WriteLine(
                    "<?xml version=\"1.0\"" +
                    " encoding=\"UTF-8\"?>");
                sw.WriteLine(
                    "<COLLADA xmlns=" +
                    "\"http://www.collada" +
                    ".org/2005/11/" +
                    "COLLADASchema\"" +
                    " version=\"1.4.1\">");
                sw.WriteLine(
                    "<asset><up_axis>" +
                    "Y_UP</up_axis>" +
                    "</asset>");

                sw.WriteLine(
                    "<library_images>");
                foreach (var kvp in groups)
                {
                    int tn = kvp.Key;
                    string tp = null;
                    if (tn < texPaths.Count)
                        tp = texPaths[tn];
                    if (tp != null &&
                        File.Exists(tp))
                        sw.WriteLine(
                            "<image id=" +
                            "\"img" +
                            tn.ToString(
                                "D2") +
                            "\"><init_from>" +
                            MakeRel(
                                tp, daePath) +
                            "</init_from>" +
                            "</image>");
                }
                sw.WriteLine(
                    "</library_images>");

                sw.WriteLine(
                    "<library_effects>");
                foreach (var kvp in groups)
                {
                    int tn = kvp.Key;
                    string tp = null;
                    if (tn < texPaths.Count)
                        tp = texPaths[tn];
                    sw.Write(
                        "<effect id=\"eff" +
                        tn.ToString("D2") +
                        "\"><profile_COMMON>");
                    if (tp != null &&
                        File.Exists(tp))
                    {
                        sw.Write(
                            "<newparam sid=" +
                            "\"surf" +
                            tn.ToString(
                                "D2") +
                            "\"><surface" +
                            " type=\"2D\">" +
                            "<init_from>img" +
                            tn.ToString(
                                "D2") +
                            "</init_from>" +
                            "</surface>" +
                            "</newparam>" +
                            "<newparam sid=" +
                            "\"samp" +
                            tn.ToString(
                                "D2") +
                            "\"><sampler2D>" +
                            "<source>surf" +
                            tn.ToString(
                                "D2") +
                            "</source>" +
                            "</sampler2D>" +
                            "</newparam>");
                    }
                    sw.Write(
                        "<technique sid=" +
                        "\"common\">" +
                        "<lambert>" +
                        "<diffuse>");
                    if (tp != null &&
                        File.Exists(tp))
                        sw.Write(
                            "<texture" +
                            " texture=" +
                            "\"samp" +
                            tn.ToString(
                                "D2") +
                            "\" texcoord=" +
                            "\"UV\"/>");
                    else
                        sw.Write(
                            "<color>0.8 0.8" +
                            " 0.8 1</color>");
                    sw.WriteLine(
                        "</diffuse>" +
                        "</lambert>" +
                        "</technique>" +
                        "</profile_COMMON>" +
                        "</effect>");
                }
                sw.WriteLine(
                    "</library_effects>");

                sw.WriteLine(
                    "<library_materials>");
                foreach (var kvp in groups)
                {
                    int tn = kvp.Key;
                    sw.WriteLine(
                        "<material id=" +
                        "\"mat" +
                        tn.ToString("D2") +
                        "\"><instance_effect" +
                        " url=\"#eff" +
                        tn.ToString("D2") +
                        "\"/></material>");
                }
                sw.WriteLine(
                    "</library_materials>");

                sw.WriteLine(
                    "<library_geometries>" +
                    "<geometry id=\"geom\">" +
                    "<mesh>");

                var sb = new StringBuilder();
                foreach (var v in av)
                    sb.Append(
                        v.X.ToString(
                            "R", ci) +
                        " " +
                        v.Y.ToString(
                            "R", ci) +
                        " " +
                        v.Z.ToString(
                            "R", ci) +
                        " ");
                sw.WriteLine(
                    "<source id=\"pos\">" +
                    "<float_array id=" +
                    "\"pos-arr\" count=\"" +
                    (av.Count * 3) + "\">" +
                    sb.ToString().Trim() +
                    "</float_array>" +
                    "<technique_common>" +
                    "<accessor source=" +
                    "\"#pos-arr\" count=\"" +
                    av.Count +
                    "\" stride=\"3\">" +
                    "<param name=\"X\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Y\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Z\"" +
                    " type=\"float\"/>" +
                    "</accessor>" +
                    "</technique_common>" +
                    "</source>");

                sb.Clear();
                foreach (var n in an)
                    sb.Append(
                        n.X.ToString(
                            "R", ci) +
                        " " +
                        n.Y.ToString(
                            "R", ci) +
                        " " +
                        n.Z.ToString(
                            "R", ci) +
                        " ");
                sw.WriteLine(
                    "<source id=\"nrm\">" +
                    "<float_array id=" +
                    "\"nrm-arr\" count=\"" +
                    (an.Count * 3) + "\">" +
                    sb.ToString().Trim() +
                    "</float_array>" +
                    "<technique_common>" +
                    "<accessor source=" +
                    "\"#nrm-arr\" count=\"" +
                    an.Count +
                    "\" stride=\"3\">" +
                    "<param name=\"X\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Y\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Z\"" +
                    " type=\"float\"/>" +
                    "</accessor>" +
                    "</technique_common>" +
                    "</source>");

                sb.Clear();
                foreach (var uv in au)
                    sb.Append(
                        uv.U.ToString(
                            "R", ci) +
                        " " +
                        uv.V.ToString(
                            "R", ci) +
                        " ");
                sw.WriteLine(
                    "<source id=\"uv\">" +
                    "<float_array id=" +
                    "\"uv-arr\" count=\"" +
                    (au.Count * 2) + "\">" +
                    sb.ToString().Trim() +
                    "</float_array>" +
                    "<technique_common>" +
                    "<accessor source=" +
                    "\"#uv-arr\" count=\"" +
                    au.Count +
                    "\" stride=\"2\">" +
                    "<param name=\"S\"" +
                    " type=\"float\"/>" +
                    "<param name=\"T\"" +
                    " type=\"float\"/>" +
                    "</accessor>" +
                    "</technique_common>" +
                    "</source>");

                sw.WriteLine(
                    "<vertices id=" +
                    "\"verts\">" +
                    "<input semantic=" +
                    "\"POSITION\"" +
                    " source=\"#pos\"/>" +
                    "</vertices>");

                foreach (var kvp in ft)
                {
                    int tn = kvp.Key;
                    var faceList = kvp.Value;
                    if (faceList.Count == 0)
                        continue;
                    sb.Clear();
                    foreach (var t in
                        faceList)
                    {
                        sb.Append(t.A);
                        sb.Append(' ');
                        sb.Append(t.A);
                        sb.Append(' ');
                        sb.Append(t.A);
                        sb.Append(' ');
                        sb.Append(t.B);
                        sb.Append(' ');
                        sb.Append(t.B);
                        sb.Append(' ');
                        sb.Append(t.B);
                        sb.Append(' ');
                        sb.Append(t.C);
                        sb.Append(' ');
                        sb.Append(t.C);
                        sb.Append(' ');
                        sb.Append(t.C);
                        sb.Append(' ');
                    }
                    sw.Write(
                        "<triangles count=" +
                        "\"" + faceList.Count +
                        "\" material=\"mat" +
                        tn.ToString("D2") +
                        "\"><input semantic=" +
                        "\"VERTEX\" source=" +
                        "\"#verts\"" +
                        " offset=\"0\"/>" +
                        "<input semantic=" +
                        "\"NORMAL\" source=" +
                        "\"#nrm\"" +
                        " offset=\"1\"/>" +
                        "<input semantic=" +
                        "\"TEXCOORD\"" +
                        " source=\"#uv\"" +
                        " offset=\"2\"" +
                        " set=\"0\"/><p>");
                    sw.Write(
                        sb.ToString().Trim());
                    sw.WriteLine(
                        "</p></triangles>");
                }

                sw.WriteLine(
                    "</mesh></geometry>" +
                    "</library_geometries>");

                sw.Write(
                    "<library_visual_scenes>" +
                    "<visual_scene id=" +
                    "\"Scene\"><node" +
                    " id=\"node0\">" +
                    "<instance_geometry" +
                    " url=\"#geom\">" +
                    "<bind_material>" +
                    "<technique_common>");
                foreach (var kvp in groups)
                {
                    int tn = kvp.Key;
                    sw.Write(
                        "<instance_material" +
                        " symbol=\"mat" +
                        tn.ToString("D2") +
                        "\" target=\"#mat" +
                        tn.ToString("D2") +
                        "\">" +
                        "<bind_vertex_input" +
                        " semantic=\"UV\"" +
                        " input_semantic=" +
                        "\"TEXCOORD\"" +
                        " input_set=\"0\"/>" +
                        "</instance_material>");
                }
                sw.WriteLine(
                    "</technique_common>" +
                    "</bind_material>" +
                    "</instance_geometry>" +
                    "</node></visual_scene>" +
                    "</library_visual_scenes>");
                sw.WriteLine(
                    "<scene>" +
                    "<instance_visual_scene" +
                    " url=\"#Scene\"/>" +
                    "</scene></COLLADA>");
            }
        }

        // ─────────────────────────────────────────
        // WRITE DAE FILE (single texture)
        // ─────────────────────────────────────────
        private void WriteDaeFile(
            string daePath,
            string modelName,
            List<Vec3> verts,
            List<Vec3> normals,
            List<Vec2> uvs,
            List<Tri> faces,
            string texPath)
        {
            CultureInfo ci =
                CultureInfo.InvariantCulture;

            using (var sw =
                new StreamWriter(
                    daePath, false,
                    new UTF8Encoding(false)))
            {
                sw.WriteLine(
                    "<?xml version=\"1.0\"" +
                    " encoding=\"UTF-8\"?>");
                sw.WriteLine(
                    "<COLLADA xmlns=" +
                    "\"http://www.collada" +
                    ".org/2005/11/" +
                    "COLLADASchema\"" +
                    " version=\"1.4.1\">");
                sw.WriteLine(
                    "<asset><up_axis>" +
                    "Y_UP</up_axis>" +
                    "</asset>");

                if (texPath != null &&
                    File.Exists(texPath))
                    sw.WriteLine(
                        "<library_images>" +
                        "<image id=\"tex0\">" +
                        "<init_from>" +
                        MakeRel(
                            texPath,
                            daePath) +
                        "</init_from>" +
                        "</image>" +
                        "</library_images>");

                sw.WriteLine(
                    "<library_effects>" +
                    "<effect id=\"eff0\">" +
                    "<profile_COMMON>");
                if (texPath != null &&
                    File.Exists(texPath))
                    sw.WriteLine(
                        "<newparam sid=" +
                        "\"surf0\">" +
                        "<surface " +
                        "type=\"2D\">" +
                        "<init_from>tex0" +
                        "</init_from>" +
                        "</surface>" +
                        "</newparam>" +
                        "<newparam sid=" +
                        "\"samp0\">" +
                        "<sampler2D>" +
                        "<source>surf0" +
                        "</source>" +
                        "</sampler2D>" +
                        "</newparam>");
                sw.Write(
                    "<technique sid=" +
                    "\"common\">" +
                    "<lambert><diffuse>");
                if (texPath != null &&
                    File.Exists(texPath))
                    sw.Write(
                        "<texture " +
                        "texture=\"samp0\"" +
                        " texcoord=" +
                        "\"UV\"/>");
                else
                    sw.Write(
                        "<color>0.8 0.8" +
                        " 0.8 1</color>");
                sw.WriteLine(
                    "</diffuse></lambert>" +
                    "</technique>" +
                    "</profile_COMMON>" +
                    "</effect>" +
                    "</library_effects>");

                sw.WriteLine(
                    "<library_materials>" +
                    "<material id=" +
                    "\"mat0\">" +
                    "<instance_effect" +
                    " url=\"#eff0\"/>" +
                    "</material>" +
                    "</library_materials>");

                sw.WriteLine(
                    "<library_geometries>" +
                    "<geometry id=" +
                    "\"geom0\"><mesh>");

                var sb = new StringBuilder();
                foreach (var v in verts)
                    sb.Append(
                        v.X.ToString(
                            "R", ci) +
                        " " +
                        v.Y.ToString(
                            "R", ci) +
                        " " +
                        v.Z.ToString(
                            "R", ci) +
                        " ");
                sw.WriteLine(
                    "<source id=\"pos\">" +
                    "<float_array id=" +
                    "\"pos-arr\" count=\"" +
                    (verts.Count * 3) +
                    "\">" +
                    sb.ToString().Trim() +
                    "</float_array>" +
                    "<technique_common>" +
                    "<accessor source=" +
                    "\"#pos-arr\" count=\"" +
                    verts.Count +
                    "\" stride=\"3\">" +
                    "<param name=\"X\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Y\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Z\"" +
                    " type=\"float\"/>" +
                    "</accessor>" +
                    "</technique_common>" +
                    "</source>");

                sb.Clear();
                foreach (var n in normals)
                    sb.Append(
                        n.X.ToString(
                            "R", ci) +
                        " " +
                        n.Y.ToString(
                            "R", ci) +
                        " " +
                        n.Z.ToString(
                            "R", ci) +
                        " ");
                sw.WriteLine(
                    "<source id=\"nrm\">" +
                    "<float_array id=" +
                    "\"nrm-arr\" count=\"" +
                    (normals.Count * 3) +
                    "\">" +
                    sb.ToString().Trim() +
                    "</float_array>" +
                    "<technique_common>" +
                    "<accessor source=" +
                    "\"#nrm-arr\" count=\"" +
                    normals.Count +
                    "\" stride=\"3\">" +
                    "<param name=\"X\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Y\"" +
                    " type=\"float\"/>" +
                    "<param name=\"Z\"" +
                    " type=\"float\"/>" +
                    "</accessor>" +
                    "</technique_common>" +
                    "</source>");

                sb.Clear();
                foreach (var uv in uvs)
                    sb.Append(
                        uv.U.ToString(
                            "R", ci) +
                        " " +
                        uv.V.ToString(
                            "R", ci) +
                        " ");
                sw.WriteLine(
                    "<source id=\"uv\">" +
                    "<float_array id=" +
                    "\"uv-arr\" count=\"" +
                    (uvs.Count * 2) + "\">" +
                    sb.ToString().Trim() +
                    "</float_array>" +
                    "<technique_common>" +
                    "<accessor source=" +
                    "\"#uv-arr\" count=\"" +
                    uvs.Count +
                    "\" stride=\"2\">" +
                    "<param name=\"S\"" +
                    " type=\"float\"/>" +
                    "<param name=\"T\"" +
                    " type=\"float\"/>" +
                    "</accessor>" +
                    "</technique_common>" +
                    "</source>");

                sw.WriteLine(
                    "<vertices id=" +
                    "\"verts\">" +
                    "<input semantic=" +
                    "\"POSITION\"" +
                    " source=\"#pos\"/>" +
                    "</vertices>");

                sb.Clear();
                foreach (var t in faces)
                {
                    sb.Append(t.A);
                    sb.Append(' ');
                    sb.Append(t.A);
                    sb.Append(' ');
                    sb.Append(t.A);
                    sb.Append(' ');
                    sb.Append(t.B);
                    sb.Append(' ');
                    sb.Append(t.B);
                    sb.Append(' ');
                    sb.Append(t.B);
                    sb.Append(' ');
                    sb.Append(t.C);
                    sb.Append(' ');
                    sb.Append(t.C);
                    sb.Append(' ');
                    sb.Append(t.C);
                    sb.Append(' ');
                }
                sw.Write(
                    "<triangles count=\"" +
                    faces.Count +
                    "\" material=\"mat0\">" +
                    "<input semantic=" +
                    "\"VERTEX\" source=" +
                    "\"#verts\"" +
                    " offset=\"0\"/>" +
                    "<input semantic=" +
                    "\"NORMAL\" source=" +
                    "\"#nrm\"" +
                    " offset=\"1\"/>" +
                    "<input semantic=" +
                    "\"TEXCOORD\"" +
                    " source=\"#uv\"" +
                    " offset=\"2\"" +
                    " set=\"0\"/><p>");
                sw.Write(
                    sb.ToString().Trim());
                sw.WriteLine(
                    "</p></triangles>" +
                    "</mesh></geometry>" +
                    "</library_geometries>");

                sw.WriteLine(
                    "<library_visual_scenes>" +
                    "<visual_scene id=" +
                    "\"Scene\"><node" +
                    " id=\"node0\">" +
                    "<instance_geometry" +
                    " url=\"#geom0\">" +
                    "<bind_material>" +
                    "<technique_common>" +
                    "<instance_material" +
                    " symbol=\"mat0\"" +
                    " target=\"#mat0\">" +
                    "<bind_vertex_input" +
                    " semantic=\"UV\"" +
                    " input_semantic=" +
                    "\"TEXCOORD\"" +
                    " input_set=\"0\"/>" +
                    "</instance_material>" +
                    "</technique_common>" +
                    "</bind_material>" +
                    "</instance_geometry>" +
                    "</node></visual_scene>" +
                    "</library_visual_scenes>");
                sw.WriteLine(
                    "<scene>" +
                    "<instance_visual_scene" +
                    " url=\"#Scene\"/>" +
                    "</scene></COLLADA>");
            }
        }

        // ─────────────────────────────────────────
        // MAKE RELATIVE PATH
        // ─────────────────────────────────────────
        private string MakeRel(
            string target, string from)
        {
            try
            {
                Uri fu = new Uri(
                    Path.GetFullPath(from));
                Uri tu = new Uri(
                    Path.GetFullPath(target));
                return Uri.UnescapeDataString(
                    fu.MakeRelativeUri(tu)
                        .ToString())
                    .Replace('\\', '/');
            }
            catch
            {
                return Path.GetFileName(
                    target);
            }
        }

        // ═════════════════════════════════════════
        // WRITE MANIFEST
        // ═════════════════════════════════════════
        private void WriteManifest(
            string outFolder,
            string baseName,
            string rdtbPath,
            string gdtbPath,
            List<MeshBatch> batches,
            SortedDictionary<
                int,
                List<MeshBatch>> groups,
            int meshChunkIdx)
        {
            string mfp = Path.Combine(
                outFolder,
                "rebuild_manifest.json");

            CultureInfo ci =
                CultureInfo.InvariantCulture;

            string rc = Path.Combine(
                outFolder, "_source.rdtb");
            File.Copy(rdtbPath, rc, true);

            string gc = "";
            if (!string.IsNullOrEmpty(
                    gdtbPath) &&
                File.Exists(gdtbPath))
            {
                gc = Path.Combine(
                    outFolder,
                    "_source.gdtb");
                File.Copy(
                    gdtbPath, gc, true);
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(
                "  \"tool\":" +
                " \"HMSTHModdingTool\",");
            sb.AppendLine(
                "  \"native_layout\": " +
                (_useNativeLayout
                    ? "true" : "false") +
                ",");
            sb.AppendLine(
                "  \"mesh_chunk_idx\": " +
                meshChunkIdx + ",");
            sb.AppendLine(
                "  \"source_rdtb\":" +
                " \"_source.rdtb\",");
            sb.AppendLine(
                "  \"source_gdtb\": \"" +
                (gc.Length > 0
                    ? "_source.gdtb" : "") +
                "\",");
            sb.AppendLine(
                "  \"original_rdtb_name\":" +
                " \"" +
                Path.GetFileName(rdtbPath) +
                "\",");
            sb.AppendLine(
                "  \"original_gdtb_name\":" +
                " \"" +
                (!string.IsNullOrEmpty(
                    gdtbPath)
                    ? Path.GetFileName(
                        gdtbPath)
                    : "") + "\",");
            sb.AppendLine(
                "  \"source_size\": " +
                _data.Length + ",");
            sb.AppendLine(
                "  \"chunk11_offset\": " +
                _chunkOffsets[
                    meshChunkIdx] + ",");
            sb.AppendLine(
                "  \"chunk11_size\": " +
                _chunks[meshChunkIdx]
                    .Length + ",");

            sb.AppendLine(
                "  \"texture_model_map\":" +
                " [");
            var grpKeys =
                groups.Keys.ToList();
            for (int gi = 0;
                 gi < grpKeys.Count; gi++)
            {
                int tn = grpKeys[gi];
                var gb = groups[tn];
                int tv = gb.Sum(
                    b => b.Verts.Count);
                bool last =
                    gi == grpKeys.Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine(
                    "      \"tex_id\": " +
                    tn + ",");
                sb.AppendLine(
                    "      \"model_file\":" +
                    " \"model_" +
                    tn.ToString("D2") +
                    ".obj\",");
                sb.AppendLine(
                    "      \"texture_file\":" +
                    " \"textures/texture_" +
                    tn.ToString("D2") +
                    ".bmp\",");
                sb.AppendLine(
                    "      \"batch_count\":" +
                    " " + gb.Count + ",");
                sb.AppendLine(
                    "      \"total_verts\":" +
                    " " + tv);
                sb.AppendLine(
                    "    }" +
                    (last ? "" : ","));
            }
            sb.AppendLine("  ],");

            sb.AppendLine(
                "  \"batches\": [");

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
                    "      \"model_file\":" +
                    " \"model_" +
                    b.TexId.ToString("D2") +
                    ".obj\",");
                sb.AppendLine(
                    "      \"chunk_offset\":" +
                    " " + b.Offset + ",");
                sb.AppendLine(
                    "      \"vertex_count\":" +
                    " " +
                    b.Verts.Count + ",");
                sb.AppendLine(
                    "      \"face_count\": " +
                    b.Faces.Count + ",");
                sb.AppendLine(
                    "      \"obj_vert_start\":" +
                    " " +
                    b.ObjVertStart + ",");
                sb.AppendLine(
                    "      \"obj_vert_end\":" +
                    " " +
                    b.ObjVertEnd + ",");
                sb.AppendLine(
                    "      \"spread_offset\":" +
                    " [" +
                    b.SpreadOffset.X
                        .ToString("R", ci) +
                    "," +
                    b.SpreadOffset.Y
                        .ToString("R", ci) +
                    "," +
                    b.SpreadOffset.Z
                        .ToString("R", ci) +
                    "],");

                sb.AppendLine(
                    "      \"vif_blocks\":" +
                    " [");
                for (int j = 0;
                     j < b.Blocks.Count;
                     j++)
                {
                    var blk = b.Blocks[j];
                    bool lb =
                        j ==
                        b.Blocks.Count - 1;
                    sb.AppendLine(
                        "        {");
                    sb.AppendLine(
                        "          " +
                        "\"chunk_offset\": " +
                        blk.OffsetInChunk +
                        ",");
                    sb.AppendLine(
                        "          " +
                        "\"vertex_count\": " +
                        blk.VertexCount +
                        ",");
                    sb.AppendLine(
                        "          " +
                        "\"first_vertex\": " +
                        blk.FirstVertexIndex);
                    sb.AppendLine(
                        "        }" +
                        (lb ? "" : ","));
                }
                sb.AppendLine("      ]");
                sb.AppendLine(
                    "    }" +
                    (last ? "" : ","));
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(
                mfp, sb.ToString());

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    rebuild_manifest.json" +
                " -> " + outFolder);
            Console.ResetColor();
        }
    }
}
