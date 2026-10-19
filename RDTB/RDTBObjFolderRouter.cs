using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// Routes _obj folders (containing
    /// model_XX.obj files with grouped
    /// batches) through the same engine
    /// as _3d_batches_obj folders by
    /// converting on the fly to a
    /// temporary batch folder structure.
    /// </summary>
    public static class RDTBObjFolderRouter
    {
        // ─────────────────────────────────
        // DETECT IF FOLDER IS AN
        // _OBJ STYLE FOLDER (from x3d)
        // ─────────────────────────────────
        public static bool IsObjFolder(
            string folderPath)
        {
            if (!Directory.Exists(
                    folderPath))
                return false;

            // Must contain at least one
            // model_XX.obj file at root
            string[] objs =
                Directory.GetFiles(
                    folderPath,
                    "model_*.obj");
            if (objs.Length == 0)
                return false;

            // Must have textures
            // subfolder
            string texDir =
                Path.Combine(
                    folderPath,
                    "textures");
            if (!Directory.Exists(
                    texDir))
                return false;

            // Must NOT be a batch
            // folder (those have
            // _source.rdtb)
            string srcRdtb =
                Path.Combine(
                    folderPath,
                    "_source.rdtb");
            if (File.Exists(srcRdtb))
                return false;

            return true;
        }

        // ─────────────────────────────────
        // FIND SOURCE RDTB FOR _OBJ
        // FOLDER. Looks at sibling
        // file with same base name
        // (e.g. BOY_obj -> BOY_00000.rdtb)
        // ─────────────────────────────────
        public static string
            FindSourceRdtb(
                string folderPath)
        {
            string folderName =
                Path.GetFileName(
                    folderPath);
            string parent =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        folderPath));

            // Strip "_obj", "_dae",
            // "_all_obj", "_all_dae"
            // suffix to get base name
            string baseName =
                folderName;
            string[] suffixes =
            {
                "_all_obj", "_all_dae",
                "_obj", "_dae"
            };
            foreach (string s in
                suffixes)
            {
                if (baseName.EndsWith(
                        s,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    baseName =
                        baseName
                            .Substring(0,
                                baseName.Length
                                - s.Length);
                    break;
                }
            }

            // Try various filename
            // patterns
            string[] tryNames =
            {
                baseName +
                    "_00000.rdtb",
                baseName + ".rdtb",
                baseName + "_00000.RDTB",
                baseName + ".RDTB"
            };
            foreach (string n in
                tryNames)
            {
                string p =
                    Path.Combine(
                        parent, n);
                if (File.Exists(p))
                    return p;
            }
            return null;
        }

        // ─────────────────────────────────
        // FIND SOURCE GDTB (sibling
        // to RDTB)
        // ─────────────────────────────────
        public static string
            FindSourceGdtb(
                string rdtbPath)
        {
            if (rdtbPath == null)
                return null;
            string dir =
                Path.GetDirectoryName(
                    rdtbPath);
            string baseName =
                Path
                    .GetFileNameWithoutExtension(
                        rdtbPath);

            // Strip _00000 suffix
            if (baseName.EndsWith(
                    "_00000"))
                baseName =
                    baseName.Substring(
                        0,
                        baseName.Length
                        - 6);

            string[] tryNames =
            {
                baseName +
                    "_00001.gdtb",
                baseName + ".gdtb",
                baseName +
                    "_00001.GDTB",
                baseName + ".GDTB"
            };
            foreach (string n in
                tryNames)
            {
                string p =
                    Path.Combine(
                        dir, n);
                if (File.Exists(p))
                    return p;
            }
            return null;
        }

        // ─────────────────────────────────
        // CONVERT _OBJ FOLDER TO
        // BATCH FOLDER STRUCTURE
        // ─────────────────────────────────
        public static string
            ConvertToBatchFolder(
                string objFolderPath,
                string sourceRdtbPath,
                string sourceGdtbPath)
        {
            string folderName =
                Path.GetFileName(
                    objFolderPath);
            string parent =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        objFolderPath));
            string tempFolder =
                Path.Combine(
                    parent,
                    "_c3d_temp_" +
                    folderName +
                    "_batches");

            // Clean up any old temp
            if (Directory.Exists(
                    tempFolder))
            {
                try
                {
                    Directory.Delete(
                        tempFolder,
                        true);
                }
                catch { }
            }
            Directory.CreateDirectory(
                tempFolder);

            // Copy source RDTB and
            // GDTB
            string destRdtb =
                Path.Combine(
                    tempFolder,
                    "_source.rdtb");
            File.Copy(
                sourceRdtbPath,
                destRdtb, true);
            if (sourceGdtbPath !=
                null &&
                File.Exists(
                    sourceGdtbPath))
            {
                string destGdtb =
                    Path.Combine(
                        tempFolder,
                        "_source.gdtb");
                File.Copy(
                    sourceGdtbPath,
                    destGdtb, true);
            }

            // Write _info.txt with
            // original names so the
            // rebuild produces files
            // with correct names
            string origRdtbName =
                Path.GetFileName(
                    sourceRdtbPath);
            string origGdtbName =
                sourceGdtbPath != null
                    ? Path.GetFileName(
                        sourceGdtbPath)
                    : "output.gdtb";
            File.WriteAllText(
                Path.Combine(
                    tempFolder,
                    "_info.txt"),
                "Source RDTB: " +
                origRdtbName + "\n" +
                "Source GDTB: " +
                origGdtbName + "\n");

            // Process each model_XX.obj
            // file at root of objFolder
            string[] modelObjs =
                Directory.GetFiles(
                    objFolderPath,
                    "model_*.obj");

            Console.WriteLine(
                "    Splitting " +
                modelObjs.Length +
                " model OBJ files" +
                " into batch files...");

            int totalBatches = 0;
            var modelGroups =
                new Dictionary<
                    int, List<int>>();

            foreach (string modelObj
                in modelObjs)
            {
                string modelName =
                    Path
                        .GetFileNameWithoutExtension(
                            modelObj);
                // Extract texture id
                // from model_NN
                int texId = -1;
                var match =
                    Regex.Match(
                        modelName,
                        @"model_(\d+)");
                if (match.Success)
                    int.TryParse(
                        match.Groups[1]
                            .Value,
                        out texId);
                if (texId < 0)
                    continue;

                // Create model
                // subfolder
                string modelSubdir =
                    Path.Combine(
                        tempFolder,
                        "model_" +
                        texId.ToString(
                            "D2"));
                Directory
                    .CreateDirectory(
                        modelSubdir);

                // Copy texture for
                // this model
                string srcTex =
                    Path.Combine(
                        objFolderPath,
                        "textures",
                        "texture_" +
                        texId.ToString(
                            "D2") +
                        ".bmp");
                if (File.Exists(srcTex))
                {
                    string dstTex =
                        Path.Combine(
                            modelSubdir,
                            "texture_" +
                            texId
                                .ToString(
                                    "D2") +
                            ".bmp");
                    File.Copy(
                        srcTex, dstTex,
                        true);
                }

                // Split obj into
                // per-batch files
                var batchesWritten =
                    SplitObjByBatch(
                        modelObj,
                        modelSubdir,
                        texId);
                modelGroups[texId] =
                    batchesWritten;
                totalBatches +=
                    batchesWritten.Count;
            }

            Console.WriteLine(
                "    Created " +
                totalBatches +
                " batch OBJ files" +
                " in temp folder");

            return tempFolder;
        }

        // ─────────────────────────────────
        // SPLIT ONE MODEL OBJ INTO
        // PER-BATCH OBJ FILES
        // ─────────────────────────────────
        static List<int>
            SplitObjByBatch(
                string modelObj,
                string outDir,
                int texId)
        {
            var written =
                new List<int>();

            // Read all lines once
            string[] lines =
                File.ReadAllLines(
                    modelObj);

            // Pass 1: collect all
            // global v/vn/vt lines
            // and identify batch
            // groups
            var verts =
                new List<string>();
            var norms =
                new List<string>();
            var uvs =
                new List<string>();
            var vwComments =
                new List<string>();

            // Map: batchIdx ->
            // list of face lines
            var batchFaces =
                new SortedDictionary<
                    int,
                    List<string>>();
            int curBatch = -1;

            foreach (string line in
                lines)
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(
                        t))
                    continue;

                if (t.StartsWith(
                        "#vw "))
                {
                    vwComments.Add(t);
                    continue;
                }
                if (t[0] == '#')
                    continue;

                string[] parts =
                    t.Split(
                        new[]
                        { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                if (parts.Length == 0)
                    continue;

                string h =
                    parts[0]
                        .ToLower();
                if (h == "v")
                {
                    verts.Add(line);
                }
                else if (h == "vn")
                {
                    norms.Add(line);
                }
                else if (h == "vt")
                {
                    uvs.Add(line);
                }
                else if (h == "g" &&
                    parts.Length >= 2)
                {
                    // Parse batch
                    // index from
                    // group name
                    string gname =
                        parts[1];
                    var bm =
                        Regex.Match(
                            gname,
                            @"batch_(\d+)");
                    if (bm.Success)
                    {
                        int.TryParse(
                            bm.Groups[1]
                                .Value,
                            out curBatch);
                        if (curBatch
                            >= 0 &&
                            !batchFaces
                                .ContainsKey(
                                    curBatch))
                            batchFaces[
                                curBatch] =
                                new List<
                                    string>();
                    }
                    else
                    {
                        curBatch = -1;
                    }
                }
                else if (h == "f" &&
                    curBatch >= 0)
                {
                    batchFaces[
                        curBatch]
                        .Add(line);
                }
            }

            // Pass 2: for each
            // batch, write a
            // standalone OBJ
            // containing only the
            // vertices referenced
            // by its faces
            foreach (var kv in
                batchFaces)
            {
                int batchIdx =
                    kv.Key;
                List<string> faces =
                    kv.Value;
                if (faces.Count == 0)
                    continue;

                string outPath =
                    Path.Combine(
                        outDir,
                        "batch_" +
                        batchIdx
                            .ToString(
                                "D4") +
                        ".obj");

                WriteStandaloneBatchObj(
                    outPath,
                    verts, norms,
                    uvs, vwComments,
                    faces,
                    batchIdx, texId,
                    Path.GetFileName(
                        Path.Combine(
                            outDir,
                            "texture_" +
                            texId
                                .ToString(
                                    "D2") +
                            ".bmp")));

                // Also write a
                // matching .mtl
                string mtlPath =
                    Path.Combine(
                        outDir,
                        "batch_" +
                        batchIdx
                            .ToString(
                                "D4") +
                        ".mtl");
                WriteBatchMtl(
                    mtlPath,
                    batchIdx,
                    texId);

                written.Add(
                    batchIdx);
            }

            return written;
        }

        // ─────────────────────────────────
        // WRITE STANDALONE OBJ
        // CONTAINING ONLY REFERENCED
        // VERTICES (with face remap)
        // ─────────────────────────────────
        static void
            WriteStandaloneBatchObj(
                string outPath,
                List<string> verts,
                List<string> norms,
                List<string> uvs,
                List<string>
                    vwComments,
                List<string> faces,
                int batchIdx,
                int texId,
                string texFn)
        {
            // Find which v/vt/vn
            // indices the faces
            // reference
            var usedV =
                new SortedSet<int>();
            var usedT =
                new SortedSet<int>();
            var usedN =
                new SortedSet<int>();

            foreach (string fline
                in faces)
            {
                string[] parts =
                    fline.Trim().Split(
                        new[]
                        { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                for (int i = 1;
                     i < parts.Length;
                     i++)
                {
                    string[] sp =
                        (parts[i] +
                         "//")
                        .Split('/');
                    int vi, ti, ni;
                    int.TryParse(
                        sp[0], out vi);
                    if (sp.Length >
                        1 &&
                        !string
                            .IsNullOrEmpty(
                                sp[1]))
                        int.TryParse(
                            sp[1],
                            out ti);
                    else
                        ti = vi;
                    if (sp.Length >
                        2 &&
                        !string
                            .IsNullOrEmpty(
                                sp[2]))
                        int.TryParse(
                            sp[2],
                            out ni);
                    else
                        ni = vi;
                    if (vi > 0)
                        usedV.Add(vi);
                    if (ti > 0)
                        usedT.Add(ti);
                    if (ni > 0)
                        usedN.Add(ni);
                }
            }

            // Build remap tables
            // (1-based)
            var vRemap =
                new Dictionary<int,
                    int>();
            var tRemap =
                new Dictionary<int,
                    int>();
            var nRemap =
                new Dictionary<int,
                    int>();
            int newV = 1;
            foreach (int v in usedV)
                vRemap[v] = newV++;
            int newT = 1;
            foreach (int t in usedT)
                tRemap[t] = newT++;
            int newN = 1;
            foreach (int n in usedN)
                nRemap[n] = newN++;

            using (var sw =
                new StreamWriter(
                    outPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine(
                    "# Batch " +
                    batchIdx +
                    " (tex " + texId
                    + ")");
                sw.WriteLine(
                    "mtllib batch_" +
                    batchIdx
                        .ToString(
                            "D4") +
                    ".mtl");
                sw.WriteLine();

                // Write vertices
                // and matching #vw
                // comments
                foreach (int v in
                    usedV)
                {
                    if (v <= verts
                        .Count &&
                        v > 0)
                    {
                        sw.WriteLine(
                            verts[
                                v - 1]);
                        // Write
                        // matching
                        // #vw if
                        // available
                        if (v <=
                            vwComments
                                .Count)
                            sw.WriteLine(
                                vwComments[
                                    v -
                                    1]);
                    }
                }
                sw.WriteLine();

                // UVs
                foreach (int t in
                    usedT)
                {
                    if (t <= uvs.Count
                        && t > 0)
                        sw.WriteLine(
                            uvs[
                                t - 1]);
                }
                sw.WriteLine();

                // Normals
                foreach (int n in
                    usedN)
                {
                    if (n <= norms
                        .Count &&
                        n > 0)
                        sw.WriteLine(
                            norms[
                                n - 1]);
                }
                sw.WriteLine();

                sw.WriteLine(
                    "g batch_" +
                    batchIdx
                        .ToString(
                            "D4"));
                sw.WriteLine(
                    "usemtl batch_" +
                    batchIdx
                        .ToString(
                            "D4"));

                // Rewrite faces
                // with remapped
                // indices
                foreach (string
                    fline in faces)
                {
                    string[] parts =
                        fline.Trim()
                            .Split(
                            new[]
                            {
                                ' ',
                                '\t'
                            },
                            StringSplitOptions
                                .RemoveEmptyEntries);
                    var newFace =
                        new
                        StringBuilder();
                    newFace
                        .Append("f");
                    for (int i = 1;
                         i <
                            parts.Length;
                         i++)
                    {
                        string[] sp =
                            (parts[i]
                             + "//")
                            .Split(
                                '/');
                        int vi, ti,
                            ni;
                        int.TryParse(
                            sp[0],
                            out vi);
                        if (sp.Length
                            > 1 &&
                            !string
                                .IsNullOrEmpty(
                                    sp[1]))
                            int
                                .TryParse(
                                sp[1],
                                out ti);
                        else
                            ti = vi;
                        if (sp.Length
                            > 2 &&
                            !string
                                .IsNullOrEmpty(
                                    sp[2]))
                            int
                                .TryParse(
                                sp[2],
                                out ni);
                        else
                            ni = vi;

                        int nv =
                            vRemap
                                .ContainsKey(
                                    vi)
                            ? vRemap[vi]
                            : 0;
                        int nt =
                            tRemap
                                .ContainsKey(
                                    ti)
                            ? tRemap[ti]
                            : 0;
                        int nn =
                            nRemap
                                .ContainsKey(
                                    ni)
                            ? nRemap[ni]
                            : 0;

                        newFace
                            .Append(
                            " " + nv +
                            "/" + nt +
                            "/" + nn);
                    }
                    sw.WriteLine(
                        newFace
                            .ToString());
                }
            }
        }

        // ─────────────────────────────────
        // WRITE MATCHING MTL
        // ─────────────────────────────────
        static void WriteBatchMtl(
            string mtlPath,
            int batchIdx,
            int texId)
        {
            using (var sw =
                new StreamWriter(
                    mtlPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine(
                    "# batch_" +
                    batchIdx
                        .ToString(
                            "D4"));
                sw.WriteLine();
                sw.WriteLine(
                    "newmtl batch_" +
                    batchIdx
                        .ToString(
                            "D4"));
                sw.WriteLine(
                    "Ka 1 1 1");
                sw.WriteLine(
                    "Kd 1 1 1");
                sw.WriteLine(
                    "Ks 0 0 0");
                sw.WriteLine(
                    "Ns 10");
                sw.WriteLine(
                    "illum 2");
                sw.WriteLine(
                    "map_Kd texture_"
                    + texId
                        .ToString(
                            "D2")
                    + ".bmp");
            }
        }

        // ─────────────────────────────────
        // MAIN: BUILD FROM _OBJ FOLDER
        // Converts to batch folder, then
        // calls cbatches engine, then
        // cleans up
        // ─────────────────────────────────
        public static void
            BuildFromObjFolder(
                string objFolderPath,
                string outDir,
                string normalsMode,
                float[] customNormal,
                bool deleteAll,
                string targetFormat)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] c3d on _obj"
                + " folder → routing"
                + " through cbatches"
                + " engine");
            Console.ResetColor();
            Console.WriteLine(
                "    Folder: " +
                objFolderPath);

            // Find sibling source
            // RDTB and GDTB
            string sourceRdtb =
                FindSourceRdtb(
                    objFolderPath);
            if (sourceRdtb == null)
            {
                Console.ForegroundColor
                    = ConsoleColor
                        .Yellow;
                Console.WriteLine(
                    "    [WARN] Could"
                    + " not find sibling"
                    + " source RDTB"
                    + " (e.g. BOY_obj"
                    + " expects"
                    + " BOY_00000.rdtb"
                    + " in parent"
                    + " folder)");
                Console.ResetColor();
                return;
            }
            string sourceGdtb =
                FindSourceGdtb(
                    sourceRdtb);

            Console.WriteLine(
                "    Source RDTB: " +
                Path.GetFileName(
                    sourceRdtb));
            if (sourceGdtb != null)
                Console.WriteLine(
                    "    Source GDTB: " +
                    Path.GetFileName(
                        sourceGdtb));

            // Convert to batch
            // folder structure
            string tempFolder =
                ConvertToBatchFolder(
                    objFolderPath,
                    sourceRdtb,
                    sourceGdtb);

            try
            {
                // Now call the
                // proven cbatches
                // engine
                RDTBBatchFolder
                    .BuildFromBatchFolder(
                        tempFolder,
                        outDir,
                        normalsMode,
                        customNormal,
                        deleteAll,
                        targetFormat);
            }
            finally
            {
                // Clean up temp
                // folder
                try
                {
                    Directory.Delete(
                        tempFolder,
                        true);
                }
                catch
                {
                    Console
                        .ForegroundColor
                        = ConsoleColor
                            .DarkGray;
                    Console.WriteLine(
                        "    (Temp"
                        + " folder not"
                        + " cleaned: "
                        + tempFolder
                        + ")");
                    Console
                        .ResetColor();
                }
            }
        }
    }
}
