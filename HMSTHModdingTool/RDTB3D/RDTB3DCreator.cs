using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB3D
{
    // ═════════════════════════════════════════════
    // OBJ PARSER
    // ═════════════════════════════════════════════
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
                new Dictionary<
                    string, List<Tri>>();
    }

    internal static class ObjParser
    {
        public static ParsedObj Parse(
            string path)
        {
            var o = new ParsedObj();
            o.FacesByGroup["default"] =
                new List<Tri>();
            string curGroup = "default";
            CultureInfo ci =
                CultureInfo.InvariantCulture;

            foreach (var lineRaw in
                File.ReadAllLines(path))
            {
                string line = lineRaw.Trim();
                if (line.Length == 0 ||
                    line[0] == '#')
                    continue;

                string[] p = line.Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
                if (p.Length == 0) continue;
                string h = p[0].ToLower();

                if (h == "v" && p.Length >= 4)
                    o.Verts.Add(new Vec3(
                        float.Parse(p[1], ci),
                        float.Parse(p[2], ci),
                        float.Parse(p[3], ci)));

                else if (h == "vn" &&
                    p.Length >= 4)
                    o.Normals.Add(new Vec3(
                        float.Parse(p[1], ci),
                        float.Parse(p[2], ci),
                        float.Parse(p[3], ci)));

                else if (h == "vt" &&
                    p.Length >= 3)
                    o.UVs.Add(new Vec2(
                        float.Parse(p[1], ci),
                        float.Parse(p[2], ci)));

                else if (h == "g" &&
                    p.Length >= 2)
                {
                    curGroup = p[1];
                    if (!o.FacesByGroup
                            .ContainsKey(
                                curGroup))
                        o.FacesByGroup[curGroup]
                            = new List<Tri>();
                }

                else if (h == "f" &&
                    p.Length >= 4)
                {
                    int[] idx = new int[3];
                    for (int i = 0; i < 3; i++)
                    {
                        string[] sub =
                            p[i + 1].Split('/');
                        idx[i] =
                            int.Parse(sub[0])
                            - 1;
                    }
                    var t = new Tri(
                        idx[0], idx[1], idx[2]);
                    o.FacesByGroup[curGroup]
                        .Add(t);
                    o.AllFaces.Add(t);
                }
            }

            return o;
        }
    }

    // ═════════════════════════════════════════════
    // DAE PARSER
    // ═════════════════════════════════════════════
    internal static class DaeParser
    {
        public static ParsedObj Parse(
            string path)
        {
            var o = new ParsedObj();
            o.FacesByGroup["batch_0000"] =
                new List<Tri>();
            CultureInfo ci =
                CultureInfo.InvariantCulture;
            string text =
                File.ReadAllText(path);

            var floatArrays =
                new Dictionary<
                    string, float[]>();
            int pos = 0;

            while (true)
            {
                int s = text.IndexOf(
                    "<float_array", pos);
                if (s < 0) break;
                int ia = text.IndexOf(
                    "id=\"", s);
                if (ia < 0) break;
                int ie = text.IndexOf(
                    "\"", ia + 4);
                if (ie < 0) break;
                string id = text.Substring(
                    ia + 4, ie - ia - 4);
                int cs = text.IndexOf(">", ie);
                int ce = text.IndexOf(
                    "</float_array>", cs);
                if (cs < 0 || ce < 0) break;
                string content =
                    text.Substring(
                        cs + 1,
                        ce - cs - 1).Trim();
                string[] parts =
                    content.Split(
                        new char[]
                        { ' ','\t','\n','\r' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                float[] vals =
                    new float[parts.Length];
                for (int i = 0;
                     i < parts.Length; i++)
                    float.TryParse(
                        parts[i],
                        NumberStyles.Float,
                        ci, out vals[i]);
                floatArrays[id] = vals;
                pos = ce + 14;
            }

            float[] posD = null;
            float[] normD = null;
            float[] uvD = null;

            foreach (var kv in floatArrays)
            {
                string lid = kv.Key.ToLower();
                if (lid.Contains("pos") &&
                    posD == null)
                    posD = kv.Value;
                else if (
                    (lid.Contains("nrm") ||
                     lid.Contains("norm")) &&
                    normD == null)
                    normD = kv.Value;
                else if (
                    (lid.Contains("uv") ||
                     lid.Contains("tex") ||
                     lid.Contains("map")) &&
                    uvD == null)
                    uvD = kv.Value;
            }

            if (posD == null)
                foreach (var kv in floatArrays)
                    if (kv.Value.Length % 3 == 0
                        && kv.Value.Length >= 9)
                    {
                        posD = kv.Value;
                        break;
                    }

            if (posD == null)
                throw new InvalidDataException(
                    "DAE no positions: " + path);

            for (int i = 0;
                 i + 2 < posD.Length; i += 3)
                o.Verts.Add(new Vec3(
                    posD[i], posD[i + 1],
                    posD[i + 2]));

            if (normD != null)
                for (int i = 0;
                     i + 2 < normD.Length;
                     i += 3)
                    o.Normals.Add(new Vec3(
                        normD[i],
                        normD[i + 1],
                        normD[i + 2]));

            while (o.Normals.Count <
                   o.Verts.Count)
                o.Normals.Add(
                    new Vec3(0, 1, 0));

            if (uvD != null)
                for (int i = 0;
                     i + 1 < uvD.Length;
                     i += 2)
                    o.UVs.Add(new Vec2(
                        uvD[i], uvD[i + 1]));

            while (o.UVs.Count <
                   o.Verts.Count)
                o.UVs.Add(new Vec2(0, 0));

            int bIdx = 0;
            pos = 0;

            while (true)
            {
                int ts = text.IndexOf(
                    "<triangles", pos);
                if (ts < 0) break;
                int te = text.IndexOf(
                    "</triangles>", ts);
                if (te < 0) break;
                string tb = text.Substring(
                    ts, te - ts);

                int posOff = 0;
                int maxOff = 0;
                int ip = 0;

                while (true)
                {
                    int is2 = tb.IndexOf(
                        "<input", ip);
                    if (is2 < 0) break;
                    int ie2 = tb.IndexOf(
                        "/>", is2);
                    if (ie2 < 0) break;
                    string inp =
                        tb.Substring(
                            is2, ie2 - is2);
                    int si = inp.IndexOf(
                        "semantic=\"");
                    int oi = inp.IndexOf(
                        "offset=\"");
                    if (si >= 0 && oi >= 0)
                    {
                        int se = inp.IndexOf(
                            "\"", si + 10);
                        string sem =
                            inp.Substring(
                                si + 10,
                                se - si - 10);
                        int oe = inp.IndexOf(
                            "\"", oi + 8);
                        int off;
                        int.TryParse(
                            inp.Substring(
                                oi + 8,
                                oe - oi - 8),
                            out off);
                        if (off > maxOff)
                            maxOff = off;
                        if (sem == "VERTEX" ||
                            sem == "POSITION")
                            posOff = off;
                    }
                    ip = ie2 + 2;
                }

                int stride = maxOff + 1;
                int ps = tb.IndexOf("<p>");
                int pe = tb.IndexOf("</p>");
                if (ps < 0 || pe < 0)
                {
                    pos = te + 12;
                    continue;
                }

                string pc = tb.Substring(
                    ps + 3,
                    pe - ps - 3).Trim();
                string[] pp = pc.Split(
                    new char[]
                    { ' ','\t','\n','\r' },
                    StringSplitOptions
                        .RemoveEmptyEntries);

                int[] ids = new int[pp.Length];
                for (int i = 0;
                     i < pp.Length; i++)
                    int.TryParse(
                        pp[i], out ids[i]);

                string gn =
                    "batch_" +
                    bIdx.ToString("D4");
                bIdx++;
                if (!o.FacesByGroup
                        .ContainsKey(gn))
                    o.FacesByGroup[gn] =
                        new List<Tri>();

                int tc =
                    ids.Length / (stride * 3);
                for (int t = 0; t < tc; t++)
                {
                    int bi = t * stride * 3;
                    if (bi + stride * 2 +
                        posOff >= ids.Length)
                        break;
                    var tri = new Tri(
                        ids[bi + posOff],
                        ids[bi + stride +
                            posOff],
                        ids[bi + stride * 2 +
                            posOff]);
                    o.FacesByGroup[gn]
                        .Add(tri);
                    o.AllFaces.Add(tri);
                }

                pos = te + 12;
            }

            if (o.FacesByGroup.ContainsKey(
                    "batch_0000") &&
                o.FacesByGroup["batch_0000"]
                    .Count == 0)
                o.FacesByGroup.Remove(
                    "batch_0000");

            return o;
        }
    }

    // ═════════════════════════════════════════════
    // MESH TRANSFER
    // ═════════════════════════════════════════════
    internal static class MeshTransfer
    {
        public static List<Vec3> CenterAlign(
            List<Vec3> source,
            List<Vec3> target)
        {
            if (source.Count == 0)
                return new List<Vec3>(source);

            Vec3 sMin, sMax, tMin, tMax;
            GetBounds(source,
                out sMin, out sMax);
            GetBounds(target,
                out tMin, out tMax);

            float dx =
                ((tMin.X + tMax.X) -
                 (sMin.X + sMax.X)) * 0.5f;
            float dy =
                ((tMin.Y + tMax.Y) -
                 (sMin.Y + sMax.Y)) * 0.5f;
            float dz =
                ((tMin.Z + tMax.Z) -
                 (sMin.Z + sMax.Z)) * 0.5f;

            var r = new List<Vec3>(
                source.Count);
            foreach (var v in source)
                r.Add(new Vec3(
                    v.X + dx,
                    v.Y + dy,
                    v.Z + dz));
            return r;
        }

        public static List<Vec3> SampleToCount(
            List<Vec3> src,
            List<Tri> faces,
            int n)
        {
            if (src.Count == 0 || n == 0)
                return new List<Vec3>();
            if (faces != null &&
                faces.Count > 0)
                return SampleByFaces(
                    src, faces, n);
            return SampleLinear(src, n);
        }

        private static List<Vec3> SampleByFaces(
            List<Vec3> verts,
            List<Tri> faces,
            int n)
        {
            var r = new List<Vec3>(n);
            int fc = faces.Count;
            for (int i = 0; i < n; i++)
            {
                float t =
                    (float)i /
                    Math.Max(1, n - 1);
                int fi = Math.Min(
                    (int)(t * (fc - 1)),
                    fc - 1);
                Tri f = faces[fi];
                if (f.A >= verts.Count ||
                    f.B >= verts.Count ||
                    f.C >= verts.Count)
                {
                    r.Add(verts[0]);
                    continue;
                }
                Vec3 a = verts[f.A];
                Vec3 b = verts[f.B];
                Vec3 c = verts[f.C];
                float st =
                    (t * (fc - 1)) - fi;
                float w1 =
                    (1f - st) * 0.5f + 0.25f;
                float w2 =
                    st * 0.5f + 0.25f;
                float w3 = 0.25f;
                float tot = w1 + w2 + w3;
                w1 /= tot;
                w2 /= tot;
                w3 /= tot;
                r.Add(new Vec3(
                    a.X * w1 + b.X * w2 +
                        c.X * w3,
                    a.Y * w1 + b.Y * w2 +
                        c.Y * w3,
                    a.Z * w1 + b.Z * w2 +
                        c.Z * w3));
            }
            return r;
        }

        private static List<Vec3> SampleLinear(
            List<Vec3> src, int n)
        {
            var r = new List<Vec3>(n);
            int sc = src.Count;
            for (int i = 0; i < n; i++)
            {
                float t =
                    (float)i /
                    Math.Max(1, n - 1);
                int si = Math.Max(0,
                    Math.Min(
                        (int)(t * (sc - 1)),
                        sc - 1));
                r.Add(src[si]);
            }
            return r;
        }

        public static void GetBounds(
            List<Vec3> v,
            out Vec3 mn, out Vec3 mx)
        {
            if (v.Count == 0)
            {
                mn = new Vec3(0, 0, 0);
                mx = new Vec3(0, 0, 0);
                return;
            }
            float x0 = v[0].X, x1 = v[0].X;
            float y0 = v[0].Y, y1 = v[0].Y;
            float z0 = v[0].Z, z1 = v[0].Z;
            foreach (var p in v)
            {
                if (p.X < x0) x0 = p.X;
                if (p.X > x1) x1 = p.X;
                if (p.Y < y0) y0 = p.Y;
                if (p.Y > y1) y1 = p.Y;
                if (p.Z < z0) z0 = p.Z;
                if (p.Z > z1) z1 = p.Z;
            }
            mn = new Vec3(x0, y0, z0);
            mx = new Vec3(x1, y1, z1);
        }
    }

    // ═════════════════════════════════════════════
    // RDTB 3D CREATOR
    // ═════════════════════════════════════════════
    public class RDTB3DCreator
    {
        public static void Create(
            string folderPath,
            string outFolder)
        {
            new RDTB3DCreator()
                .DoCreate(
                    folderPath, outFolder);
        }

        private string _folder;
        private ManifestData _manifest;

        private class ManifestBatch
        {
            public int Index;
            public int TexId;
            public int ChunkOffset;
            public int VertexCount;
            public int FaceCount;
            public int ObjVertStart;
            public int ObjVertEnd;
            public Vec3 SpreadOffset;
            public List<ManifestBlock>
                Blocks =
                    new List<ManifestBlock>();
        }

        private class ManifestBlock
        {
            public int ChunkOffset;
            public int VertexCount;
            public int FirstVertex;
        }

        private class ManifestData
        {
            public string SourceRdtb = "";
            public string SourceGdtb = "";
            public string OriginalRdtbName = "";
            public string OriginalGdtbName = "";
            public int SourceSize;
            public int Chunk11Offset;
            public int Chunk11Size;
            public List<ManifestBatch>
                Batches =
                    new List<ManifestBatch>();
        }

        // ═════════════════════════════════════════
        // DO CREATE
        // ═════════════════════════════════════════
        private void DoCreate(
            string folder, string outFolder)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] HMSTH 3D Creator v1.4.5");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
            Console.WriteLine(
                "    Folder : " + folder);
            Console.WriteLine(
                "    Output : " + outFolder);
            Console.WriteLine(
                new string('=', 64));

            _folder = folder;

            string mfp = Path.Combine(
                folder,
                "rebuild_manifest.json");
            if (!File.Exists(mfp))
                throw new
                    FileNotFoundException(
                    "rebuild_manifest.json" +
                    " not found in: " +
                    folder +
                    "\nRun -x3d first.");

            Console.WriteLine();
            Console.WriteLine(
                "[+] Loading manifest...");
            _manifest =
                LoadManifest(mfp);
            Console.WriteLine(
                "    RDTB: " +
                _manifest.OriginalRdtbName);
            Console.WriteLine(
                "    Batches: " +
                _manifest.Batches.Count);

            string srcRdtb = Path.Combine(
                folder,
                _manifest.SourceRdtb);
            if (!File.Exists(srcRdtb))
                throw new
                    FileNotFoundException(
                    "Source RDTB not found: " +
                    _manifest.SourceRdtb);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Loading source RDTB...");
            byte[] rdtbData =
                File.ReadAllBytes(srcRdtb);

            // Show expected model files
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Loading model files...");
            Console.WriteLine(
                "    Expects:");
            Console.ResetColor();

            var texNums =
                _manifest.Batches
                .Select(b => b.TexId)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            foreach (int tn in texNums)
                Console.WriteLine(
                    "      model_" +
                    tn.ToString("D2") +
                    ".obj  ←→  texture_" +
                    tn.ToString("D2") +
                    ".bmp");
            Console.WriteLine();

            // Load model files
            var texObjs =
                new Dictionary<
                    int, ParsedObj>();

            var allFiles = new List<string>();
            allFiles.AddRange(
                Directory.GetFiles(
                    folder, "*.obj"));
            foreach (var dae in
                Directory.GetFiles(
                    folder, "*.dae"))
            {
                string bn =
                    Path
                    .GetFileNameWithoutExtension(
                        dae);
                if (!File.Exists(
                    Path.Combine(
                        folder,
                        bn + ".obj")))
                    allFiles.Add(dae);
            }

            foreach (string fp in allFiles)
            {
                string fname =
                    Path
                    .GetFileNameWithoutExtension(
                        fp).ToLower();
                string ext =
                    Path.GetExtension(fp)
                        .ToLower();

                if (!fname.StartsWith(
                    "model_"))
                    continue;

                string rest =
                    fname.Substring(6);
                if (rest.EndsWith("_all"))
                    rest = rest.Substring(
                        0, rest.Length - 4);
                int us = rest.IndexOf('_');
                if (us > 0)
                    rest = rest.Substring(
                        0, us);

                int texId;
                if (!int.TryParse(
                    rest, out texId))
                    continue;

                if (texObjs.ContainsKey(texId))
                    continue;

                ParsedObj obj = null;
                try
                {
                    obj = (ext == ".obj")
                        ? ObjParser.Parse(fp)
                        : DaeParser.Parse(fp);
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

                if (obj == null) continue;

                texObjs[texId] = obj;
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    model_" +
                    texId.ToString("D2") +
                    ".obj  loaded  (" +
                    obj.Verts.Count +
                    " verts, " +
                    obj.AllFaces.Count +
                    " faces)");
                Console.ResetColor();
            }

            if (texObjs.Count == 0)
                throw new InvalidDataException(
                    "No model files found!" +
                    "\nExpected model_00.obj" +
                    " through model_" +
                    texNums.Last()
                        .ToString("D2") +
                    ".obj in:\n" + folder);

            // Apply to RDTB
            Console.WriteLine();
            Console.WriteLine(
                "[+] Applying mesh mods...");

            byte[] modified =
                (byte[])rdtbData.Clone();

            int cExact = 0;
            int cFix = 0;
            int cPad = 0;
            int cSkip = 0;
            int cNoMdl = 0;

            foreach (var mb in
                _manifest.Batches
                    .OrderBy(b => b.TexId)
                    .ThenBy(b => b.Index))
            {
                if (!texObjs.ContainsKey(
                    mb.TexId))
                {
                    cNoMdl++;
                    continue;
                }

                ParsedObj obj =
                    texObjs[mb.TexId];

                int vStart = mb.ObjVertStart;
                int vEnd = Math.Min(
                    mb.ObjVertEnd,
                    obj.Verts.Count);

                var rawV = new List<Vec3>();
                var rawN = new List<Vec3>();
                var rawUV = new List<Vec2>();

                for (int i = vStart;
                     i < vEnd; i++)
                {
                    Vec3 v = obj.Verts[i];

                    // Remove spread offset
                    rawV.Add(new Vec3(
                        v.X - mb.SpreadOffset.X,
                        v.Y - mb.SpreadOffset.Y,
                        v.Z -
                            mb.SpreadOffset.Z));

                    if (i < obj.Normals.Count)
                        rawN.Add(
                            obj.Normals[i]);
                    else
                        rawN.Add(
                            new Vec3(0, 1, 0));

                    if (i < obj.UVs.Count)
                        rawUV.Add(new Vec2(
                            obj.UVs[i].U,
                            1.0f -
                            obj.UVs[i].V));
                    else
                        rawUV.Add(
                            new Vec2(0, 0));
                }

                int need = mb.VertexCount;
                bool bFix = false;
                bool bPad = false;

                if (rawV.Count != need)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [fix] tex_" +
                        mb.TexId
                            .ToString("D2") +
                        " batch " + mb.Index +
                        ": slice " +
                        rawV.Count +
                        " → need " + need);
                    Console.ResetColor();

                    List<Vec3> template =
                        ReadOriginalVerts(
                            rdtbData, mb);

                    rawV = MeshTransfer
                        .CenterAlign(
                            rawV, template);

                    if (rawV.Count != need)
                    {
                        var sampled =
                            MeshTransfer
                                .SampleToCount(
                                    rawV,
                                    obj.AllFaces,
                                    need);
                        if (sampled != null &&
                            sampled.Count ==
                            need)
                        {
                            rawV = sampled;
                            rawN = Resample(
                                rawN, need);
                            rawUV = Resample(
                                rawUV, need);
                        }
                    }
                    bFix = true;
                }

                // Final pad / trim
                while (rawV.Count < need)
                {
                    rawV.Add(
                        rawV.Count > 0
                        ? rawV[rawV.Count - 1]
                        : new Vec3(0, 0, 0));
                    rawN.Add(
                        rawN.Count > 0
                        ? rawN[rawN.Count - 1]
                        : new Vec3(0, 1, 0));
                    rawUV.Add(
                        rawUV.Count > 0
                        ? rawUV[rawUV.Count - 1]
                        : new Vec2(0, 0));
                    bPad = true;
                }

                if (rawV.Count > need)
                {
                    rawV = rawV
                        .Take(need).ToList();
                    rawN = rawN
                        .Take(need).ToList();
                    rawUV = rawUV
                        .Take(need).ToList();
                }

                if (rawV.Count == 0)
                {
                    cSkip++;
                    continue;
                }

                // SURGICAL WRITE
                WriteBatchToData(
                    modified, mb,
                    rawV, rawN, rawUV);

                string tag =
                    "tex_" +
                    mb.TexId.ToString("D2") +
                    " batch " + mb.Index +
                    " (" + need + "v)";

                if (bFix)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [fixed]  " + tag);
                    Console.ResetColor();
                    cFix++;
                }
                else if (bPad)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [padded] " + tag);
                    Console.ResetColor();
                    cPad++;
                }
                else
                {
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    [exact]  " + tag);
                    Console.ResetColor();
                    cExact++;
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Summary:");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    Exact   : " + cExact +
                " (perfect roundtrip)");
            Console.ResetColor();
            if (cFix > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    Fixed   : " + cFix +
                    " (vert count changed," +
                    " auto-fixed)");
                Console.ResetColor();
            }
            if (cPad > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    Padded  : " + cPad);
                Console.ResetColor();
            }
            if (cSkip > 0)
                Console.WriteLine(
                    "    Skipped : " + cSkip);
            if (cNoMdl > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No model: " + cNoMdl +
                    " batches (no model_NN" +
                    ".obj for their tex_id)");
                Console.ResetColor();
            }

            // Write RDTB
            Directory.CreateDirectory(
                outFolder);
            string outRdtb = Path.Combine(
                outFolder,
                _manifest.OriginalRdtbName);
            Console.WriteLine();
            Console.WriteLine(
                "[+] Writing RDTB: " +
                Path.GetFileName(outRdtb));
            File.WriteAllBytes(
                outRdtb, modified);

            // Write GDTB
            string outGdtb = "";
            string texFolder = Path.Combine(
                folder, "textures");

            if (Directory.Exists(texFolder) &&
                _manifest.OriginalGdtbName
                    .Length > 0)
            {
                outGdtb = Path.Combine(
                    outFolder,
                    _manifest.OriginalGdtbName);
                Console.WriteLine(
                    "[+] Writing GDTB: " +
                    Path.GetFileName(outGdtb));
                try
                {
                    GDTBArchive.Create(
                        texFolder, outGdtb);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] GDTB failed:" +
                        " " + ex.Message);
                    Console.ResetColor();
                    string srcG =
                        Path.Combine(
                            folder,
                            _manifest.SourceGdtb);
                    if (File.Exists(srcG))
                        File.Copy(
                            srcG, outGdtb,
                            true);
                }
            }
            else if (
                _manifest.SourceGdtb
                    .Length > 0)
            {
                outGdtb = Path.Combine(
                    outFolder,
                    _manifest.OriginalGdtbName);
                string srcG = Path.Combine(
                    folder,
                    _manifest.SourceGdtb);
                if (File.Exists(srcG))
                    File.Copy(
                        srcG, outGdtb, true);
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Rebuild complete!");
            Console.ResetColor();
            Console.WriteLine(
                "     Output : " + outFolder);
            Console.WriteLine(
                "     RDTB   : " +
                Path.GetFileName(outRdtb));
            if (outGdtb.Length > 0)
                Console.WriteLine(
                    "     GDTB   : " +
                    Path.GetFileName(
                        outGdtb));
            Console.WriteLine();
        }

        // ═════════════════════════════════════════
        // READ ORIGINAL VERTS FROM RDTB
        // ORIGINAL VERSION - DO NOT CHANGE
        // ═════════════════════════════════════════
        private List<Vec3> ReadOriginalVerts(
            byte[] rdtb, ManifestBatch mb)
        {
            var r = new List<Vec3>();
            int c11 = _manifest.Chunk11Offset;
            foreach (var blk in mb.Blocks)
            {
                int bs = c11 + blk.ChunkOffset;
                int n = blk.VertexCount;
                int ds = bs + 16;
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + i * 16;
                    if (ro + 16 > rdtb.Length)
                        break;
                    // Original: reads from +4, +8, +12
                    r.Add(new Vec3(
                        BitConverter.ToSingle(
                            rdtb, ro + 4),
                        BitConverter.ToSingle(
                            rdtb, ro + 8),
                        BitConverter.ToSingle(
                            rdtb, ro + 12)));
                }
            }
            return r;
        }

        // ═════════════════════════════════════════
        // WRITE BATCH TO DATA
        // ORIGINAL VERSION - RESTORED
        // ═════════════════════════════════════════
        private void WriteBatchToData(
            byte[] data,
            ManifestBatch mb,
            List<Vec3> verts,
            List<Vec3> normals,
            List<Vec2> uvs)
        {
            int c11 = _manifest.Chunk11Offset;
            int vIdx = 0;

            foreach (var blk in mb.Blocks)
            {
                int bs = c11 + blk.ChunkOffset;
                int n = blk.VertexCount;
                int ds = bs + 16;

                // Positions
                for (int i = 0; i < n; i++)
                {
                    int ro = ds + i * 16;
                    if (vIdx + i >=
                            verts.Count ||
                        ro + 16 > data.Length)
                        break;
                    Vec3 v = verts[vIdx + i];
                    // +0 = bone weight flag → skip
                    // +4 = X
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(v.X),
                        0, data, ro + 4, 4);
                    // +8 = Y
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(v.Y),
                        0, data, ro + 8, 4);
                    // +12 = Z
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(v.Z),
                        0, data, ro + 12, 4);
                }

                // Normals
                for (int i = 0; i < n; i++)
                {
                    int ro =
                        ds + (n + i) * 16;
                    if (vIdx + i >=
                            normals.Count ||
                        ro + 16 > data.Length)
                        break;
                    Vec3 nr = normals[vIdx + i];
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(nr.X),
                        0, data, ro + 4, 4);
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(nr.Y),
                        0, data, ro + 8, 4);
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(nr.Z),
                        0, data, ro + 12, 4);
                }

                // UVs
                for (int i = 0; i < n; i++)
                {
                    int ro =
                        ds + (2 * n + i) * 16;
                    if (vIdx + i >= uvs.Count
                        || ro + 12 > data.Length)
                        break;
                    Vec2 uv = uvs[vIdx + i];
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(uv.U),
                        0, data, ro + 4, 4);
                    Buffer.BlockCopy(
                        BitConverter.GetBytes(uv.V),
                        0, data, ro + 8, 4);
                    // +12 = tex ID → skip
                }

                vIdx += n;
            }
        }

        // ═════════════════════════════════════════
        // RESAMPLE HELPERS
        // ═════════════════════════════════════════
        private static List<Vec3> Resample(
            List<Vec3> src, int n)
        {
            var r = new List<Vec3>(n);
            int sc = src.Count;
            if (sc == 0)
            {
                for (int i = 0; i < n; i++)
                    r.Add(new Vec3(0, 1, 0));
                return r;
            }
            for (int i = 0; i < n; i++)
            {
                float t =
                    (float)i /
                    Math.Max(1, n - 1);
                int si = Math.Max(0,
                    Math.Min(
                        (int)(t * (sc - 1)),
                        sc - 1));
                r.Add(src[si]);
            }
            return r;
        }

        private static List<Vec2> Resample(
            List<Vec2> src, int n)
        {
            var r = new List<Vec2>(n);
            int sc = src.Count;
            if (sc == 0)
            {
                for (int i = 0; i < n; i++)
                    r.Add(new Vec2(0, 0));
                return r;
            }
            for (int i = 0; i < n; i++)
            {
                float t =
                    (float)i /
                    Math.Max(1, n - 1);
                int si = Math.Max(0,
                    Math.Min(
                        (int)(t * (sc - 1)),
                        sc - 1));
                r.Add(src[si]);
            }
            return r;
        }

        // ═════════════════════════════════════════
        // LOAD MANIFEST
        // ═════════════════════════════════════════
        private ManifestData LoadManifest(
            string path)
        {
            string json =
                File.ReadAllText(path);
            var m = new ManifestData();

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

            int bi =
                json.IndexOf("\"batches\":");
            if (bi < 0) return m;
            int as2 =
                json.IndexOf('[', bi);
            int ae =
                json.LastIndexOf(']');
            if (as2 < 0 || ae < 0) return m;

            string arr = json.Substring(
                as2 + 1, ae - as2 - 1);

            int pos = 0;
            while (pos < arr.Length)
            {
                int os =
                    arr.IndexOf('{', pos);
                if (os < 0) break;
                int oe =
                    MatchBrace(arr, os);
                if (oe < 0) break;
                string obj =
                    arr.Substring(
                        os,
                        oe - os + 1);

                var mb =
                    new ManifestBatch();
                mb.Index =
                    JInt(obj, "index");
                mb.TexId =
                    JInt(obj, "tex_id");
                mb.ChunkOffset =
                    JInt(obj, "chunk_offset");
                mb.VertexCount =
                    JInt(obj, "vertex_count");
                mb.FaceCount =
                    JInt(obj, "face_count");
                mb.ObjVertStart =
                    JInt(obj,
                        "obj_vert_start");
                mb.ObjVertEnd =
                    JInt(obj,
                        "obj_vert_end");

                float[] sp = JFloatArr(
                    obj, "spread_offset");
                if (sp.Length >= 3)
                    mb.SpreadOffset =
                        new Vec3(
                            sp[0],
                            sp[1],
                            sp[2]);

                int vbi = obj.IndexOf(
                    "\"vif_blocks\":");
                if (vbi >= 0)
                {
                    int vbs =
                        obj.IndexOf(
                            '[', vbi);
                    int vbe =
                        MatchBracket(
                            obj, vbs);
                    if (vbs >= 0 &&
                        vbe >= 0)
                    {
                        string vbt =
                            obj.Substring(
                                vbs + 1,
                                vbe - vbs - 1);
                        int p2 = 0;
                        while (
                            p2 < vbt.Length)
                        {
                            int bs2 =
                                vbt.IndexOf(
                                    '{', p2);
                            if (bs2 < 0) break;
                            int be2 =
                                MatchBrace(
                                    vbt, bs2);
                            if (be2 < 0) break;
                            string bt =
                                vbt.Substring(
                                    bs2,
                                    be2 -
                                    bs2 + 1);
                            var blk =
                                new ManifestBlock();
                            blk.ChunkOffset =
                                JInt(bt,
                                "chunk_offset");
                            blk.VertexCount =
                                JInt(bt,
                                "vertex_count");
                            blk.FirstVertex =
                                JInt(bt,
                                "first_vertex");
                            mb.Blocks.Add(blk);
                            p2 = be2 + 1;
                        }
                    }
                }

                m.Batches.Add(mb);
                pos = oe + 1;
            }

            m.Batches = m.Batches
                .OrderBy(b => b.TexId)
                .ThenBy(b => b.Index)
                .ToList();

            return m;
        }

        private int MatchBrace(
            string s, int start)
        {
            int d = 0;
            for (int i = start;
                 i < s.Length; i++)
            {
                if (s[i] == '{') d++;
                else if (s[i] == '}')
                {
                    d--;
                    if (d == 0) return i;
                }
            }
            return -1;
        }

        private int MatchBracket(
            string s, int start)
        {
            int d = 0;
            for (int i = start;
                 i < s.Length; i++)
            {
                if (s[i] == '[') d++;
                else if (s[i] == ']')
                {
                    d--;
                    if (d == 0) return i;
                }
            }
            return -1;
        }

        private string JStr(
            string json, string key)
        {
            string s = "\"" + key + "\"";
            int ki = json.IndexOf(s);
            if (ki < 0) return "";
            int co =
                json.IndexOf(':', ki);
            int q1 =
                json.IndexOf('"', co + 1);
            int q2 =
                json.IndexOf('"', q1 + 1);
            if (q1 < 0 || q2 < 0) return "";
            return json.Substring(
                q1 + 1, q2 - q1 - 1);
        }

        private int JInt(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0) return 0;
            int vs = ki + s.Length;
            while (vs < json.Length &&
                   (json[vs] == ' ' ||
                    json[vs] == '\t' ||
                    json[vs] == '\r' ||
                    json[vs] == '\n'))
                vs++;
            int ve = vs;
            while (ve < json.Length &&
                   (char.IsDigit(json[ve]) ||
                    json[ve] == '-'))
                ve++;
            int r;
            int.TryParse(
                json.Substring(vs, ve - vs),
                out r);
            return r;
        }

        private float[] JFloatArr(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0)
                return new float[0];
            int as2 = json.IndexOf(
                '[', ki + s.Length);
            int ae =
                json.IndexOf(']', as2);
            if (as2 < 0 || ae < 0)
                return new float[0];
            string at = json.Substring(
                as2 + 1, ae - as2 - 1);
            string[] parts =
                at.Split(',');
            float[] r =
                new float[parts.Length];
            CultureInfo ci =
                CultureInfo.InvariantCulture;
            for (int i = 0;
                 i < parts.Length; i++)
                float.TryParse(
                    parts[i].Trim(),
                    NumberStyles.Float,
                    ci, out r[i]);
            return r;
        }
    }
}
