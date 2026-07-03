using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace HMSTHModdingTool.RDTB
{
    // ═════════════════════════════════════
    // RDTB / OBJ / DAE INSPECTOR
    // Comprehensive diagnostics for 3D
    // mesh data. Tells you exactly what
    // you're working with — no guessing.
    // ═════════════════════════════════════
    public static class RDTBInspector
    {
        const byte VIF_B0 = 0x00;
        const byte VIF_B1 = 0x80;
        const byte VIF_B3 = 0x6C;
        const uint EOF_FLAG = 0x70000000;

        // ─────────────────────────────────
        // INSPECT RDTB MODEL
        // ─────────────────────────────────
        public static void InspectModel(
            string rdtbPath)
        {
            if (!File.Exists(rdtbPath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "[ERROR] File not"
                    + " found: " + rdtbPath);
                Console.ResetColor();
                return;
            }

            byte[] data =
                File.ReadAllBytes(rdtbPath);

            if (data.Length < 0x48
                || data[0] != 'R'
                || data[1] != 'D'
                || data[2] != 'T'
                || data[3] != 'B')
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "[ERROR] Not a valid"
                    + " RDTB file");
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Model"
                + " Inspector");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 70));
            Console.WriteLine(
                "    File : "
                + Path.GetFileName(
                    rdtbPath));
            Console.WriteLine(
                "    Size : "
                + data.Length.ToString(
                    "N0") + " B ("
                + (data.Length / 1024.0)
                    .ToString("F1")
                + " KB)");

            // Read raw slots
            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14;
                 i++)
            {
                rawSlots[i] =
                    BitConverter
                        .ToUInt32(
                            data,
                            0x10 +
                            i * 4);
            }

            // Detect format
            string format = "UNKNOWN";
            if (rawSlots[9] == 0xFFFFFFFF
                && rawSlots[12] ==
                    0xFFFFFFFF)
                format = "SMALL";
            else if (rawSlots[9] ==
                rawSlots[8]
                && rawSlots[12] ==
                    rawSlots[11])
                format = "MIRROR";
            else if (rawSlots[9] !=
                rawSlots[8]
                && rawSlots[9] !=
                    0xFFFFFFFF)
                format = "BIG";

            Console.WriteLine(
                "    Format: " + format);
            Console.WriteLine();

            // Find mesh chunk
            uint meshOff =
                rawSlots[11];
            if (meshOff == 0
                || meshOff ==
                    0xFFFFFFFF)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "[!] No mesh chunk"
                    + " at slot 11");
                Console.ResetColor();
                return;
            }

            uint meshEnd =
                (uint)data.Length;
            for (int i = 0; i < 14;
                 i++)
            {
                uint v = rawSlots[i];
                if (v > meshOff
                    && v < meshEnd
                    && v != 0xFFFFFFFF)
                    meshEnd = v;
            }

            byte[] mesh = new byte[
                meshEnd - meshOff];
            Array.Copy(data,
                (int)meshOff, mesh,
                0, mesh.Length);

            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "    ── MESH CHUNK ──");
            Console.ResetColor();
            Console.WriteLine(
                "    Offset : 0x"
                + meshOff.ToString("X8"));
            Console.WriteLine(
                "    Size   : "
                + mesh.Length.ToString(
                    "N0") + " B ("
                + (mesh.Length / 1024.0)
                    .ToString("F1")
                + " KB)");

            // Read material table
            // for tex IDs and bones
            var matInfo =
                ReadMaterialTable(
                    data, rawSlots);
            int matCount =
                matInfo.Count;

            // Read mesh pointers
            uint firstPtr =
                BitConverter.ToUInt32(
                    mesh, 0);
            int nPtrs =
                (int)(firstPtr / 4);

            Console.WriteLine(
                "    Pointers: "
                + nPtrs);
            Console.WriteLine(
                "    Materials: "
                + matCount);
            Console.WriteLine();

            uint[] ptrs =
                new uint[nPtrs];
            for (int i = 0; i < nPtrs;
                 i++)
                ptrs[i] =
                    BitConverter
                        .ToUInt32(
                            mesh,
                            i * 4);

            // Per-batch analysis
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "    ── PER-BATCH"
                + " ANALYSIS ──");
            Console.ResetColor();
            Console.WriteLine(
                "    "
                + "idx | tex | bone |"
                + "  size  | blocks |"
                + " avg vc | tris |"
                + " status");
            Console.WriteLine(
                "    "
                + "----+-----+------+"
                + "--------+--------+"
                + "--------+------+"
                + "--------");

            int totalTris = 0;
            int totalBlocks = 0;
            int totalVerts = 0;
            int validBatches = 0;
            int nullBatches = 0;

            for (int bi = 0;
                 bi < nPtrs; bi++)
            {
                uint bp = ptrs[bi];
                if (bp == 0)
                {
                    Console.WriteLine(
                        "    "
                        + bi.ToString()
                            .PadLeft(3)
                        + " |"
                        + "  -  |  -   |"
                        + "      - |"
                        + "      - |"
                        + "      - |"
                        + "    - | NULL");
                    nullBatches++;
                    continue;
                }

                uint be =
                    (uint)mesh.Length;
                for (int j = bi + 1;
                     j < nPtrs; j++)
                {
                    if (ptrs[j] != 0)
                    {
                        be = ptrs[j];
                        break;
                    }
                }

                var batchStats =
                    AnalyzeBatch(
                        mesh,
                        (int)bp,
                        (int)be);

                int tex = -1;
                int bone = -1;
                if (bi < matInfo.Count)
                {
                    tex =
                        matInfo[bi].tex;
                    bone =
                        matInfo[bi].bone;
                }

                string status =
                    batchStats.blocks
                        > 0
                    ? "OK"
                    : "EMPTY";

                Console.WriteLine(
                    "    "
                    + bi.ToString()
                        .PadLeft(3)
                    + " | "
                    + (tex >= 0
                        ? tex.ToString()
                            .PadLeft(3)
                        : "  -")
                    + " | "
                    + (bone >= 0
                        ? bone.ToString()
                            .PadLeft(4)
                        : "  -")
                    + " | "
                    + (be - bp)
                        .ToString("N0")
                        .PadLeft(6)
                    + " | "
                    + batchStats.blocks
                        .ToString()
                        .PadLeft(6)
                    + " | "
                    + batchStats.avgVc
                        .ToString("F1")
                        .PadLeft(6)
                    + " | "
                    + batchStats.tris
                        .ToString()
                        .PadLeft(4)
                    + " | " + status);

                totalTris +=
                    batchStats.tris;
                totalBlocks +=
                    batchStats.blocks;
                totalVerts +=
                    batchStats.totalVerts;
                if (batchStats.blocks
                    > 0)
                    validBatches++;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    ── TOTALS ──");
            Console.ResetColor();
            Console.WriteLine(
                "    Valid batches : "
                + validBatches);
            Console.WriteLine(
                "    Null batches  : "
                + nullBatches);
            Console.WriteLine(
                "    Total triangles: "
                + totalTris.ToString(
                    "N0"));
            Console.WriteLine(
                "    Total vertices: "
                + totalVerts.ToString(
                    "N0"));
            Console.WriteLine(
                "    Total VIF blocks: "
                + totalBlocks.ToString(
                    "N0"));
            if (totalBlocks > 0)
            {
                double avgVcOverall =
                    (double)totalVerts
                    / totalBlocks;
                Console.WriteLine(
                    "    Avg vc/block  : "
                    + avgVcOverall
                        .ToString("F2")
                    + " (higher = better"
                    + " strip packing)");
            }
            if (totalTris > 0)
            {
                double bytesPerTri =
                    (double)mesh.Length
                    / totalTris;
                Console.WriteLine(
                    "    Bytes/triangle: "
                    + bytesPerTri
                        .ToString("F1")
                    + " (lower = better"
                    + " density)");
            }

            // Texture/bone summary
            if (matInfo.Count > 0)
            {
                var uniqueTex =
                    matInfo
                        .Select(
                            m => m.tex)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();
                var uniqueBone =
                    matInfo
                        .Select(
                            m => m.bone)
                        .Distinct()
                        .Count();
                Console.WriteLine();
                Console.WriteLine(
                    "    Unique textures: "
                    + uniqueTex.Count
                    + " [tex_ids: "
                    + string.Join(", ",
                        uniqueTex)
                    + "]");
                Console.WriteLine(
                    "    Unique bones  : "
                    + uniqueBone);
            }

            Console.WriteLine(
                new string('=', 70));
        }

        // ─────────────────────────────────
        // INSPECT OBJ FILE
        // ─────────────────────────────────
        public static void InspectObj(
            string objPath)
        {
            if (!File.Exists(objPath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "[ERROR] File not"
                    + " found: " + objPath);
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] OBJ File Inspector");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 70));
            Console.WriteLine(
                "    File: "
                + Path.GetFileName(
                    objPath));

            var info = new FileInfo(
                objPath);
            Console.WriteLine(
                "    Size: "
                + info.Length.ToString(
                    "N0") + " B");

            int verts = 0;
            int normals = 0;
            int uvs = 0;
            int faces = 0;
            int triFaces = 0;
            int quadFaces = 0;
            int ngonFaces = 0;
            int groupCount = 0;
            int matCount = 0;
            int vwComments = 0;
            var groups =
                new List<string>();
            var materials =
                new List<string>();
            var groupFaceCounts =
                new Dictionary<
                    string, int>();
            string currentGroup =
                "default";

            // Vertex range tracking
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            using (var sr =
                new StreamReader(objPath))
            {
                string line;
                while ((line =
                    sr.ReadLine()) != null)
                {
                    string t = line.Trim();

                    if (t.StartsWith(
                            "#vw "))
                    {
                        vwComments++;
                        continue;
                    }
                    if (string.IsNullOrEmpty(
                            t)
                        || t[0] == '#')
                        continue;

                    string[] p = t.Split(
                        new[] { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                    if (p.Length == 0)
                        continue;

                    string h =
                        p[0].ToLower();

                    if (h == "v"
                        && p.Length >= 4)
                    {
                        verts++;
                        try
                        {
                            float x =
                                float.Parse(
                                    p[1],
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture);
                            float y =
                                float.Parse(
                                    p[2],
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture);
                            float z =
                                float.Parse(
                                    p[3],
                                    System.Globalization
                                        .CultureInfo
                                        .InvariantCulture);
                            if (x < minX)
                                minX = x;
                            if (y < minY)
                                minY = y;
                            if (z < minZ)
                                minZ = z;
                            if (x > maxX)
                                maxX = x;
                            if (y > maxY)
                                maxY = y;
                            if (z > maxZ)
                                maxZ = z;
                        }
                        catch { }
                    }
                    else if (h == "vn")
                        normals++;
                    else if (h == "vt")
                        uvs++;
                    else if (h == "f")
                    {
                        int n = p.Length - 1;
                        faces++;
                        if (n == 3)
                            triFaces++;
                        else if (n == 4)
                            quadFaces++;
                        else if (n > 4)
                            ngonFaces++;
                        if (!groupFaceCounts
                            .ContainsKey(
                                currentGroup))
                            groupFaceCounts[
                                currentGroup]
                                = 0;
                        groupFaceCounts[
                            currentGroup]++;
                    }
                    else if (h == "g"
                        && p.Length >= 2)
                    {
                        groupCount++;
                        currentGroup =
                            p[1];
                        if (!groups
                            .Contains(p[1]))
                            groups.Add(
                                p[1]);
                    }
                    else if (h == "usemtl"
                        && p.Length >= 2)
                    {
                        matCount++;
                        if (!materials
                            .Contains(p[1]))
                            materials.Add(
                                p[1]);
                    }
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "    ── GEOMETRY ──");
            Console.ResetColor();
            Console.WriteLine(
                "    Vertices    : "
                + verts.ToString("N0"));
            Console.WriteLine(
                "    Normals     : "
                + normals.ToString(
                    "N0"));
            Console.WriteLine(
                "    UVs         : "
                + uvs.ToString("N0"));
            Console.WriteLine(
                "    Faces total : "
                + faces.ToString("N0"));
            Console.WriteLine(
                "      Triangles : "
                + triFaces.ToString(
                    "N0"));
            if (quadFaces > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "      Quads     : "
                    + quadFaces.ToString(
                        "N0")
                    + " [!] Triangulate"
                    + " before RDTB"
                    + " rebuild");
                Console.ResetColor();
            }
            if (ngonFaces > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "      N-gons    : "
                    + ngonFaces.ToString(
                        "N0")
                    + " [!] Must"
                    + " triangulate");
                Console.ResetColor();
            }

            if (verts > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.WriteLine(
                    "    ── BOUNDING"
                    + " BOX ──");
                Console.ResetColor();
                Console.WriteLine(
                    "    X: "
                    + minX.ToString(
                        "F3")
                    + " to "
                    + maxX.ToString(
                        "F3")
                    + " (width: "
                    + (maxX - minX)
                        .ToString("F3")
                    + ")");
                Console.WriteLine(
                    "    Y: "
                    + minY.ToString(
                        "F3")
                    + " to "
                    + maxY.ToString(
                        "F3")
                    + " (height: "
                    + (maxY - minY)
                        .ToString("F3")
                    + ")");
                Console.WriteLine(
                    "    Z: "
                    + minZ.ToString(
                        "F3")
                    + " to "
                    + maxZ.ToString(
                        "F3")
                    + " (depth: "
                    + (maxZ - minZ)
                        .ToString("F3")
                    + ")");
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "    ── GROUPS ──");
            Console.ResetColor();
            Console.WriteLine(
                "    Group changes : "
                + groupCount);
            Console.WriteLine(
                "    Unique groups : "
                + groups.Count);
            if (groups.Count > 0
                && groups.Count <= 20)
            {
                foreach (var g in
                    groups)
                {
                    int fc =
                        groupFaceCounts
                            .ContainsKey(g)
                        ? groupFaceCounts[g]
                        : 0;
                    Console.WriteLine(
                        "      \"" + g
                        + "\" - " + fc
                        + " faces");
                }
            }
            else if (groups.Count > 20)
            {
                Console.WriteLine(
                    "    (showing first"
                    + " 20)");
                for (int i = 0;
                     i < 20; i++)
                {
                    int fc =
                        groupFaceCounts
                            .ContainsKey(
                                groups[i])
                        ? groupFaceCounts[
                            groups[i]]
                        : 0;
                    Console.WriteLine(
                        "      \""
                        + groups[i]
                        + "\" - " + fc
                        + " faces");
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "    ── MATERIALS ──");
            Console.ResetColor();
            Console.WriteLine(
                "    Material switches: "
                + matCount);
            Console.WriteLine(
                "    Unique materials: "
                + materials.Count);
            if (materials.Count > 0
                && materials.Count <= 20)
            {
                foreach (var m in
                    materials)
                    Console.WriteLine(
                        "      \"" + m
                        + "\"");
            }

            if (vwComments > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "    ── HMSTH"
                    + " METADATA ──");
                Console.ResetColor();
                Console.WriteLine(
                    "    #vw bone weight"
                    + " comments: "
                    + vwComments);
                Console.WriteLine(
                    "    [+] Bone weights"
                    + " preserved for"
                    + " roundtrip");
            }

            Console.WriteLine(
                new string('=', 70));
        }

        // ─────────────────────────────────
        // INSPECT DAE FILE
        // ─────────────────────────────────
        public static void InspectDae(
            string daePath)
        {
            if (!File.Exists(daePath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "[ERROR] File not"
                    + " found: " + daePath);
                Console.ResetColor();
                return;
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] DAE File Inspector");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 70));
            Console.WriteLine(
                "    File: "
                + Path.GetFileName(
                    daePath));

            var info = new FileInfo(
                daePath);
            Console.WriteLine(
                "    Size: "
                + info.Length.ToString(
                    "N0") + " B");

            try
            {
                var doc = XDocument.Load(
                    daePath);
                XNamespace ns =
                    doc.Root.Name
                        .Namespace;

                // Count geometry / mesh
                var geoms =
                    doc.Descendants(
                        ns + "geometry")
                    .ToList();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.WriteLine(
                    "    ── GEOMETRY ──");
                Console.ResetColor();
                Console.WriteLine(
                    "    Geometries: "
                    + geoms.Count);

                int gi = 0;
                int totalVerts = 0;
                int totalTris = 0;
                int totalNorms = 0;
                int totalUvs = 0;

                foreach (var g in geoms)
                {
                    gi++;
                    string gid =
                        g.Attribute(
                            "id")?.Value
                        ?? "?";
                    var mesh =
                        g.Element(
                            ns + "mesh");
                    if (mesh == null)
                        continue;

                    // Count source data
                    int verts = 0;
                    int norms = 0;
                    int uvs = 0;
                    foreach (var src
                        in mesh.Elements(
                            ns + "source"))
                    {
                        var fa =
                            src.Element(
                                ns +
                                "float_array");
                        if (fa == null)
                            continue;
                        int cnt =
                            int.Parse(
                                fa.Attribute(
                                    "count")
                                ?.Value
                                ?? "0");
                        string sid =
                            src.Attribute(
                                "id")
                            ?.Value
                            ?? "";
                        if (sid.Contains(
                                "pos"))
                            verts = cnt / 3;
                        else if (sid
                            .Contains("nrm")
                            || sid.Contains(
                                "norm"))
                            norms = cnt / 3;
                        else if (sid
                            .Contains("uv")
                            || sid.Contains(
                                "tex"))
                            uvs = cnt / 2;
                    }

                    int tris = 0;
                    foreach (var triEl
                        in mesh.Elements(
                            ns + "triangles"))
                    {
                        int tc =
                            int.Parse(
                                triEl
                                .Attribute(
                                    "count")
                                ?.Value
                                ?? "0");
                        tris += tc;
                    }
                    foreach (var polyEl
                        in mesh.Elements(
                            ns + "polylist"))
                    {
                        int pc =
                            int.Parse(
                                polyEl
                                .Attribute(
                                    "count")
                                ?.Value
                                ?? "0");
                        tris += pc;
                    }

                    Console.WriteLine(
                        "      [" + gi
                        + "] \"" + gid
                        + "\"");
                    Console.WriteLine(
                        "          Verts: "
                        + verts
                        + ", Norms: "
                        + norms
                        + ", UVs: " + uvs
                        + ", Tris: " + tris);

                    totalVerts += verts;
                    totalTris += tris;
                    totalNorms += norms;
                    totalUvs += uvs;
                }

                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    ── TOTALS ──");
                Console.ResetColor();
                Console.WriteLine(
                    "    Total vertices: "
                    + totalVerts);
                Console.WriteLine(
                    "    Total normals : "
                    + totalNorms);
                Console.WriteLine(
                    "    Total UVs     : "
                    + totalUvs);
                Console.WriteLine(
                    "    Total tris    : "
                    + totalTris);

                // Materials
                var mats =
                    doc.Descendants(
                        ns + "material")
                    .ToList();
                var imgs =
                    doc.Descendants(
                        ns + "image")
                    .ToList();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.WriteLine(
                    "    ── MATERIALS ──");
                Console.ResetColor();
                Console.WriteLine(
                    "    Materials: "
                    + mats.Count);
                Console.WriteLine(
                    "    Textures : "
                    + imgs.Count);

                if (imgs.Count > 0
                    && imgs.Count <= 20)
                {
                    foreach (var img
                        in imgs)
                    {
                        var initFrom =
                            img.Element(
                                ns +
                                "init_from")
                            ?.Value
                            ?? "(none)";
                        Console.WriteLine(
                            "      "
                            + initFrom);
                    }
                }

                // Scene nodes
                var nodes =
                    doc.Descendants(
                        ns + "node")
                    .ToList();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.White;
                Console.WriteLine(
                    "    ── SCENE ──");
                Console.ResetColor();
                Console.WriteLine(
                    "    Scene nodes: "
                    + nodes.Count);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "[ERROR] Failed to"
                    + " parse DAE: "
                    + ex.Message);
                Console.ResetColor();
            }

            Console.WriteLine(
                new string('=', 70));
        }

        // ═════════════════════════════
        // INTERNAL HELPERS
        // ═════════════════════════════
        struct BatchStats
        {
            public int blocks;
            public int totalVerts;
            public int tris;
            public double avgVc;
        }

        static BatchStats AnalyzeBatch(
            byte[] mesh,
            int start, int end)
        {
            var s = new BatchStats();
            int pos = start;
            var vcList = new List<int>();

            while (pos + 16 <= end)
            {
                if (mesh[pos] != VIF_B0
                    || mesh[pos + 1]
                        != VIF_B1
                    || mesh[pos + 3]
                        != VIF_B3)
                {
                    pos += 4;
                    continue;
                }
                int vc = mesh[pos + 4];
                if (vc < 1 || vc > 96)
                {
                    pos += 4;
                    continue;
                }
                s.blocks++;
                s.totalVerts += vc;
                vcList.Add(vc);
                if (vc >= 3)
                    s.tris += (vc - 2);

                int bsize =
                    16 + 3 * vc * 16 + 16;
                if (pos + bsize + 16
                    <= end)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                mesh,
                                pos +
                                bsize);
                    if (eof == EOF_FLAG)
                        bsize += 16;
                }
                pos += bsize;
            }

            s.avgVc = s.blocks > 0
                ? (double)s.totalVerts
                    / s.blocks
                : 0;
            return s;
        }

        struct MatInfo
        {
            public int tex;
            public int bone;
        }

        static List<MatInfo>
            ReadMaterialTable(
                byte[] data,
                uint[] rawSlots)
        {
            var result =
                new List<MatInfo>();
            uint c8 = rawSlots[8];
            if (c8 == 0
                || c8 == 0xFFFFFFFF)
                return result;
            uint c8End =
                (uint)data.Length;
            for (int i = 0; i < 14;
                 i++)
            {
                uint v = rawSlots[i];
                if (v > c8
                    && v < c8End
                    && v != 0xFFFFFFFF)
                    c8End = v;
            }
            int c8size =
                (int)(c8End - c8);
            if (c8size < 4)
                return result;
            uint first =
                BitConverter.ToUInt32(
                    data, (int)c8);
            if (first == 0
                || first > (uint)c8size)
                return result;
            int bc = (int)(first / 4);
            for (int i = 0; i < bc;
                 i++)
            {
                int poff =
                    (int)c8 + i * 4;
                if (poff + 4
                    > data.Length)
                    break;
                uint ptr =
                    BitConverter
                        .ToUInt32(data,
                            poff);
                int rec =
                    (int)c8 + (int)ptr;
                if (rec + 8
                    > data.Length)
                {
                    result.Add(
                        new MatInfo
                        {
                            tex = -1,
                            bone = -1
                        });
                    continue;
                }
                int bone =
                    BitConverter
                        .ToUInt16(data,
                            rec);
                int tex =
                    BitConverter
                        .ToUInt16(data,
                            rec + 6);
                result.Add(
                    new MatInfo
                    {
                        tex = tex,
                        bone = bone
                    });
            }
            return result;
        }
    }
}
