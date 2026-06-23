using HMSTHModdingTool.IO;
using HMSTHModdingTool.GDTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// Standalone pure-C# batch
    /// replacer for cbatches command.
    /// No Python dependency.
    /// 
    /// Does everything:
    ///   - Parse OBJ files
    ///   - Compile pure-tri VIF
    ///   - Replace batches in RDTB
    ///   - Hide deleted batches
    ///   - Match original normals
    ///   - Apply slot mirror
    ///   - Build GDTB from textures
    /// </summary>
    public static class RDTBBatchReplacer
    {
        // VIF constants
        const byte VIF_B0 = 0x00;
        const byte VIF_B1 = 0x80;
        const byte VIF_B3 = 0x6C;
        const uint F_ZERO = 0x00000000;
        const uint F_ONE = 0x3F800000;
        const uint EOF_FLAG = 0x70000000;

        static readonly byte[] HDR_TAIL =
        {
            0x00, 0x40, 0x3E, 0x30,
            0x12, 0x04, 0x00, 0x00
        };
        static readonly byte[] GIF_FIRST =
        {
            0x00, 0x00, 0x80, 0x3F,
            0x00, 0x00, 0x00, 0x11,
            0x00, 0x00, 0x00, 0x14,
            0x00, 0x00, 0x00, 0x00
        };
        static readonly byte[] GIF_NEXT =
        {
            0x00, 0x00, 0x80, 0x3F,
            0x00, 0x00, 0x00, 0x17,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        static readonly byte[] EOF_TAG =
        {
            0x00, 0x00, 0x00, 0x70,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        // ═════════════════════════════
        // MAIN ENTRY POINT
        // ═════════════════════════════
        public static void Build(
            string folderPath,
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
                "[+] Build From Batch"
                + " Folder (C#"
                + " Standalone)");
            Console.ResetColor();
            Console.WriteLine(
                "    Folder : " +
                folderPath);
            Console.WriteLine(
                "    Out    : " +
                outDir);
            Console.WriteLine(
                "    Normals: " +
                normalsMode);

            Directory.CreateDirectory(
                outDir);

            string srcRdtb =
                Path.Combine(
                    folderPath,
                    "_source.rdtb");
            string srcGdtb =
                Path.Combine(
                    folderPath,
                    "_source.gdtb");

            if (!File.Exists(srcRdtb))
            {
                TextOut.PrintError(
                    "No _source.rdtb");
                return;
            }

            // Read original names
            string origRdtbName =
                "output.rdtb";
            string origGdtbName =
                "output.gdtb";
            string infoPath =
                Path.Combine(
                    folderPath,
                    "_info.txt");
            if (File.Exists(infoPath))
            {
                foreach (string line in
                    File.ReadAllLines(
                        infoPath))
                {
                    string t =
                        line.Trim();
                    if (t.StartsWith(
                        "Source RDTB:"))
                        origRdtbName =
                            t.Substring(
                                12)
                            .Trim();
                    if (t.StartsWith(
                        "Source GDTB:"))
                        origGdtbName =
                            t.Substring(
                                12)
                            .Trim();
                }
            }

            string outRdtb =
                Path.Combine(outDir,
                    origRdtbName);
            string outGdtb =
                Path.Combine(outDir,
                    origGdtbName);

            Console.WriteLine(
                "    RDTB: " +
                origRdtbName);
            Console.WriteLine(
                "    GDTB: " +
                origGdtbName);

            // Read source RDTB
            byte[] rdtbData =
                File.ReadAllBytes(
                    srcRdtb);

            // Find batch OBJs
            string[] modelDirs =
                Directory
                    .GetDirectories(
                        folderPath,
                        "model_*");

            var batchObjs =
                new SortedDictionary<
                    int, string>();
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
                            fn.Substring(
                                6);
                        int bi;
                        if (int.TryParse(
                                ns,
                                out bi))
                            batchObjs[bi]
                                = f;
                    }
                }
            }

            Console.WriteLine(
                "    Found " +
                batchObjs.Count +
                " batch OBJ files");

            // Missing batches
            int totalBatches =
                GetBatchCount(rdtbData);
            var missingBatches =
                new List<int>();
            for (int i = 0;
                 i < totalBatches; i++)
            {
                if (!batchObjs
                        .ContainsKey(i))
                    missingBatches
                        .Add(i);
            }

            if (missingBatches.Count
                > 0)
            {
                Console.WriteLine(
                    "    Deleted: " +
                    missingBatches.Count
                    + " batches");
            }

            // Chunk layout
            List<int> offs =
                ReadChunkOffsets(
                    rdtbData);
            int meshChunkIdx;
            if (offs.Count >= 14)
                meshChunkIdx = 11;
            else
                meshChunkIdx =
                    offs.Count - 1;

            // Read mesh chunk
            int chunkStart =
                offs[meshChunkIdx];
            int chunkEnd =
                (meshChunkIdx + 1 <
                    offs.Count
                ? offs[meshChunkIdx + 1]
                : rdtbData.Length);
            byte[] meshChunk =
                new byte[
                    chunkEnd -
                    chunkStart];
            Array.Copy(rdtbData,
                chunkStart, meshChunk,
                0, meshChunk.Length);

            uint firstPtr =
                BitConverter.ToUInt32(
                    meshChunk, 0);
            int nPtrs =
                (int)(firstPtr / 4);

            // Read original normals
            var origNormals =
                new Dictionary<int,
                    List<(float[] pos,
                          float[] norm)>>();

            if (normalsMode == "match")
            {
                Console.WriteLine(
                    "    Reading"
                    + " original"
                    + " normals...");
                foreach (int bi in
                    batchObjs.Keys)
                {
                    origNormals[bi] =
                        ReadBatchNormals(
                            meshChunk,
                            bi, nPtrs);
                }
            }

            // ═══════════════════════
            // PROCESS ALL BATCHES
            // (collect data only,
            //  no rebuild yet)
            // ═══════════════════════
            var newBatchData =
                new Dictionary<int,
                    byte[]>();

            int processed = 0;
            int total =
                batchObjs.Count;
            int keptCount = 0;
            int newCount = 0;

            foreach (var kv in batchObjs)
            {
                processed++;
                int bi = kv.Key;
                string objPath = kv.Value;

                // Parse OBJ
                var verts = new List<float[]>();
                var normals = new List<float[]>();
                var uvs = new List<float[]>();
                var tris = new List<int[]>();
                ParseObj(objPath,
                    verts, normals,
                    uvs, tris);


                // Apply normals
                if (normalsMode ==
                    "zero")
                {
                    for (int i = 0;
                         i < normals
                             .Count;
                         i++)
                        normals[i] =
                            new float[]
                            { 0, 0, 0 };
                }
                else if (normalsMode
                    == "up")
                {
                    for (int i = 0;
                         i < normals
                             .Count;
                         i++)
                        normals[i] =
                            new float[]
                            { 0, 1, 0 };
                }
                else if (normalsMode
                    == "custom" &&
                    customNormal !=
                    null)
                {
                    for (int i = 0;
                         i < normals
                             .Count;
                         i++)
                        normals[i] =
                            new float[]
                            {
                                customNormal[0],
                                customNormal[1],
                                customNormal[2]
                            };
                }
                else if (normalsMode
                    == "match" &&
                    origNormals
                        .ContainsKey(
                            bi))
                {
                    var samples =
                        origNormals[bi];
                    if (samples.Count
                        > 0)
                    {
                        for (int i = 0;
                             i < verts
                                 .Count;
                             i++)
                        {
                            float bestD
                                = float
                                    .MaxValue;
                            float[] bestN
                                = new float[]
                                { 0, 1, 0 };
                            float vx =
                                verts[i][0];
                            float vy =
                                verts[i][1];
                            float vz =
                                verts[i][2];
                            foreach (var s
                                in samples)
                            {
                                float dx =
                                    vx -
                                    s.pos[0];
                                float dy =
                                    vy -
                                    s.pos[1];
                                float dz =
                                    vz -
                                    s.pos[2];
                                float d =
                                    dx * dx +
                                    dy * dy +
                                    dz * dz;
                                if (d <
                                    bestD)
                                {
                                    bestD =
                                        d;
                                    bestN =
                                        s.norm;
                                }
                            }
                            if (i < normals
                                    .Count)
                                normals[i]
                                    = bestN;
                        }
                    }
                    else
                    {
                        for (int i = 0;
                             i < normals
                                 .Count;
                             i++)
                            normals[i] =
                                new float[]
                                { 0, 0, 0 };
                    }
                }

                // Check if modified
                int origTriCount =
                    CountOrigBatchTris(
                        meshChunk, bi,
                        nPtrs);

                if (tris.Count ==
                    origTriCount)
                {
                    keptCount++;
                }
                else
                {
                    byte[] vifData =
                        CompilePureTri(
                            verts,
                            normals,
                            uvs, tris);
                    newBatchData[bi] =
                        vifData;
                    newCount++;
                }
            }

            // ═══════════════════════
            // SUMMARY
            // ═══════════════════════
            Console.WriteLine();
            Console.WriteLine(
                "    Batches kept"
                + " original: " +
                keptCount);
            Console.WriteLine(
                "    Batches new"
                + " (pure-tri): " +
                newCount);
            Console.WriteLine(
                "    Batches deleted"
                + " (hidden): " +
                missingBatches.Count);

            // ═══════════════════════
            // REBUILD ONCE
            // ═══════════════════════
            byte[] hiddenVif =
                BuildHiddenBatch();

            Console.WriteLine();
            Console.WriteLine(
                "    Rebuilding"
                + " mesh chunk...");

            byte[] newMeshChunk =
                RebuildMeshChunk(
                    meshChunk, nPtrs,
                    newBatchData,
                    missingBatches,
                    hiddenVif);

            Console.WriteLine(
                "    Mesh: " +
                meshChunk.Length
                    .ToString("N0") +
                " -> " +
                newMeshChunk.Length
                    .ToString("N0") +
                " B");

            Console.WriteLine(
                "    Rebuilding"
                + " RDTB...");

            byte[] newRdtb =
                RebuildRdtbWithChunk(
                    rdtbData, offs,
                    meshChunkIdx,
                    newMeshChunk,
                    nPtrs);

            Console.WriteLine(
                "    Applying slot"
                + " mirror...");

            byte[] finalRdtb =
                ApplySlotMirror(
                    newRdtb);

            File.WriteAllBytes(
                outRdtb, finalRdtb);

            Console.WriteLine(
                "    RDTB: " +
                finalRdtb.Length
                    .ToString("N0") +
                " B");

            // ═══════════════════════
            // BUILD GDTB ONCE
            // ═══════════════════════
            Console.WriteLine(
                "    Building"
                + " GDTB...");
            BuildGdtbFromModels(
                folderPath, outGdtb,
                srcGdtb);

            // ═══════════════════════
            // DONE
            // ═══════════════════════
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Built:");
            Console.WriteLine(
                "     " + outRdtb);
            Console.WriteLine(
                "     " + outGdtb);
            Console.ResetColor();
        }




        static int CountOrigBatchTris(
            byte[] chunk,
            int batchIdx,
            int nPtrs)
        {
            if (batchIdx >= nPtrs)
                return -1;
            uint bp = BitConverter
                .ToUInt32(chunk,
                    batchIdx * 4);
            uint np =
                (batchIdx + 1 < nPtrs
                ? BitConverter
                    .ToUInt32(chunk,
                        (batchIdx + 1)
                        * 4)
                : (uint)chunk.Length);

            int count = 0;
            int pos = (int)bp;
            int end = (int)np;
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
                // Strip produces
                // vc-2 triangles
                if (vc >= 3)
                    count += (vc - 2);

                int bSize = 16 +
                    3 * vc * 16 + 16;
                if (pos + bSize + 16
                    <= end)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                chunk,
                                pos +
                                bSize);
                    if (eof ==
                        EOF_FLAG)
                    {
                        bSize += 16;
                        pos += bSize;
                        break;
                    }
                }
                pos += bSize;
            }
            return count;
        }


        // ═════════════════════════════
        // OBJ PARSER
        // ═════════════════════════════
        static void ParseObj(
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
                new Dictionary<string,
                    int>();

            var ci = System
                .Globalization
                .CultureInfo
                .InvariantCulture;

            foreach (string line in
                File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (string.IsNullOrEmpty(
                        t) ||
                    t[0] == '#')
                    continue;
                string[] p = t.Split(
                    new[] { ' ', '\t' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
                if (p.Length == 0)
                    continue;
                string h = p[0]
                    .ToLower();

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
                            float.Parse(
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
                        int vi =
                            int.Parse(
                                parts[0])
                            - 1;
                        int ti =
                            (parts.Length
                            > 1 &&
                            !string
                                .IsNullOrEmpty(
                                    parts[1])
                            ? int.Parse(
                                parts[1])
                                - 1
                            : vi);
                        int ni =
                            (parts.Length
                            > 2 &&
                            !string
                                .IsNullOrEmpty(
                                    parts[2])
                            ? int.Parse(
                                parts[2])
                                - 1
                            : vi);
                        string key =
                            vi + "/" +
                            ti + "/" +
                            ni;
                        int newIdx;
                        if (!comboMap
                                .TryGetValue(
                                    key,
                                    out newIdx))
                        {
                            newIdx =
                                verts
                                    .Count;
                            comboMap[key]
                                = newIdx;
                            verts.Add(
                                (vi >= 0
                                && vi <
                                rawV.Count)
                                ? rawV[vi]
                                : new float[]
                                  { 0, 0, 0 });
                            uvs.Add(
                                (ti >= 0
                                && ti <
                                rawT.Count)
                                ? rawT[ti]
                                : new float[]
                                  { 0, 0 });
                            normals.Add(
                                (ni >= 0
                                && ni <
                                rawN.Count)
                                ? rawN[ni]
                                : new float[]
                                  { 0, 1, 0 });
                        }
                        idx[fi] =
                            newIdx;
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

            // Flip V for PS2
            for (int i = 0;
                 i < uvs.Count; i++)
                uvs[i] = new float[]
                {
                    uvs[i][0],
                    1f - uvs[i][1]
                };
        }

        // ═════════════════════════════
        // COMPILE PURE-TRI VIF
        // Each triangle = 1 block
        // ═════════════════════════════
        static byte[] CompilePureTri(
            List<float[]> verts,
            List<float[]> normals,
            List<float[]> uvs,
            List<int[]> tris)
        {
            using (var ms =
                new MemoryStream())
            {
                int n = tris.Count;
                for (int bi = 0;
                     bi < n; bi++)
                {
                    bool isFirst =
                        (bi == 0);
                    bool isLast =
                        (bi == n - 1);
                    int[] tri = tris[bi];

                    // VIF header
                    byte[] hdr =
                        new byte[16];
                    hdr[0] = VIF_B0;
                    hdr[1] = VIF_B1;
                    hdr[2] = (byte)(
                        (3 * 3 + 1) &
                        0xFF);
                    hdr[3] = VIF_B3;
                    hdr[4] = 3;
                    hdr[5] = 0x80;
                    Array.Copy(HDR_TAIL,
                        0, hdr, 8, 8);
                    ms.Write(hdr, 0, 16);

                    // 3 verts
                    for (int j = 0;
                         j < 3; j++)
                    {
                        int vi = tri[j];
                        float[] v =
                            (vi < verts
                                .Count)
                            ? verts[vi]
                            : new float[]
                              { 0, 0, 0 };
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    F_ZERO),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    v[0]),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    v[1]),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    v[2]),
                            0, 4);
                    }

                    // 3 normals
                    for (int j = 0;
                         j < 3; j++)
                    {
                        int vi = tri[j];
                        float[] nn =
                            (vi < normals
                                .Count)
                            ? normals[vi]
                            : new float[]
                              { 0, 1, 0 };
                        uint flag =
                            (j == 0)
                            ? F_ZERO
                            : F_ONE;
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    flag),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    nn[0]),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    nn[1]),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    nn[2]),
                            0, 4);
                    }

                    // 3 UVs
                    for (int j = 0;
                         j < 3; j++)
                    {
                        int vi = tri[j];
                        float[] uv =
                            (vi < uvs
                                .Count)
                            ? uvs[vi]
                            : new float[]
                              { 0, 0 };
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    F_ONE),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    uv[0]),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    uv[1]),
                            0, 4);
                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    1f),
                            0, 4);
                    }

                    // GIF tag
                    ms.Write(
                        isFirst
                        ? GIF_FIRST
                        : GIF_NEXT,
                        0, 16);

                    if (isLast)
                        ms.Write(
                            EOF_TAG,
                            0, 16);
                }

                return ms.ToArray();
            }
        }

        // ═════════════════════════════
        // BUILD HIDDEN BATCH
        // ═════════════════════════════
        static byte[] BuildHiddenBatch()
        {
            using (var ms =
                new MemoryStream())
            {
                byte[] hdr =
                    new byte[16];
                hdr[0] = VIF_B0;
                hdr[1] = VIF_B1;
                hdr[2] = (byte)(
                    (3 * 3 + 1) &
                    0xFF);
                hdr[3] = VIF_B3;
                hdr[4] = 3;
                hdr[5] = 0x80;
                Array.Copy(HDR_TAIL,
                    0, hdr, 8, 8);
                ms.Write(hdr, 0, 16);

                // 3 degenerate verts
                // at origin (zero-area
                // triangle = invisible
                // but doesn't fly off
                // to 99999)
                for (int i = 0;
                     i < 3; i++)
                {
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                F_ZERO),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                }

                // 3 normals
                for (int i = 0;
                     i < 3; i++)
                {
                    uint flag =
                        (i == 0)
                        ? F_ZERO
                        : F_ONE;
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                flag),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                1f),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                }

                // 3 UVs
                for (int i = 0;
                     i < 3; i++)
                {
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                F_ONE),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                0f),
                        0, 4);
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                1f),
                        0, 4);
                }

                ms.Write(GIF_FIRST,
                    0, 16);
                ms.Write(EOF_TAG,
                    0, 16);

                return ms.ToArray();
            }
        }

        // ═════════════════════════════
        // READ ORIGINAL BATCH NORMALS
        // ═════════════════════════════
        static List<(float[] pos,
            float[] norm)>
            ReadBatchNormals(
                byte[] chunk,
                int batchIdx,
                int nPtrs)
        {
            var result =
                new List<(float[],
                    float[])>();

            if (batchIdx >= nPtrs)
                return result;

            uint bPtr = BitConverter
                .ToUInt32(chunk,
                    batchIdx * 4);
            uint nPtr =
                (batchIdx + 1 < nPtrs
                ? BitConverter
                    .ToUInt32(chunk,
                        (batchIdx + 1)
                        * 4)
                : (uint)chunk.Length);

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
                int vStart = pos + 16;
                int nStart = vStart +
                    vc * 16;

                for (int i = 0;
                     i < vc; i++)
                {
                    int vOff = vStart +
                        i * 16;
                    int nOff = nStart +
                        i * 16;
                    if (nOff + 16 > end)
                        break;

                    float[] vp =
                        new float[]
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
                    float[] np =
                        new float[]
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
                    result.Add(
                        (vp, np));
                }

                int bSize = 16 +
                    3 * vc * 16 + 16;
                if (pos + bSize + 16
                    <= end)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                chunk,
                                pos +
                                bSize);
                    if (eof ==
                        EOF_FLAG)
                        bSize += 16;
                }
                pos += bSize;
            }

            return result;
        }

        // ═════════════════════════════
        // REBUILD MESH CHUNK
        // ═════════════════════════════
        static byte[] RebuildMeshChunk(
            byte[] origChunk,
            int nPtrs,
            Dictionary<int, byte[]>
                newBatches,
            List<int> hiddenBatches,
            byte[] hiddenVif)
        {
            // Read all original
            // batch data
            var batchData =
                new List<byte[]>();
            for (int i = 0;
                 i < nPtrs; i++)
            {
                uint bp = BitConverter
                    .ToUInt32(
                        origChunk,
                        i * 4);
                uint np =
                    (i + 1 < nPtrs
                    ? BitConverter
                        .ToUInt32(
                            origChunk,
                            (i + 1) * 4)
                    : (uint)origChunk
                        .Length);

                if (bp >= np ||
                    bp >= (uint)
                        origChunk
                            .Length ||
                    np > (uint)
                        origChunk
                            .Length)
                {
                    batchData.Add(
                        BuildHiddenBatch());
                    continue;
                }
                byte[] bd = new byte[
                    np - bp];

                Array.Copy(origChunk,
                    (int)bp, bd, 0,
                    bd.Length);
                batchData.Add(bd);
            }

            // Replace modified batches
            foreach (var kv in
                newBatches)
            {
                if (kv.Key < nPtrs)
                    batchData[kv.Key] =
                        kv.Value;
            }

            // Hide deleted batches
            foreach (int hb in
                hiddenBatches)
            {
                if (hb < nPtrs)
                    batchData[hb] =
                        hiddenVif;
            }

            // Rebuild with new
            // pointer table
            int tableSize = nPtrs * 4;
            using (var ms =
                new MemoryStream())
            {
                // Write pointer table
                int cursor = tableSize;
                for (int i = 0;
                     i < nPtrs; i++)
                {
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                (uint)
                                cursor),
                        0, 4);
                    cursor +=
                        batchData[i]
                            .Length;
                }

                // Write batch data
                for (int i = 0;
                     i < nPtrs; i++)
                {
                    ms.Write(
                        batchData[i],
                        0,
                        batchData[i]
                            .Length);
                }

                return ms.ToArray();
            }
        }

        // ═════════════════════════════
        // REBUILD RDTB WITH NEW CHUNK
        // ═════════════════════════════
        static byte[] RebuildRdtbWithChunk(
            byte[] origData,
            List<int> offs,
            int meshChunkIdx,
            byte[] newMeshChunk,
            int nPtrs)
        {
            // Read all chunks
            var chunks =
                new List<byte[]>();
            for (int i = 0;
                 i < offs.Count; i++)
            {
                int s = offs[i];
                int e = (i + 1 <
                    offs.Count
                    ? offs[i + 1]
                    : origData.Length);
                byte[] c = new byte[
                    e - s];
                Array.Copy(origData,
                    s, c, 0, e - s);
                chunks.Add(c);
            }

            // Replace mesh chunk
            chunks[meshChunkIdx] =
                newMeshChunk;

            // Update lookup chunks
            // (8->11, 9->12, 10->13)
            // For mirrored mode, all
            // lookups point to same
            // mesh chunk, so update
            // all of them with the
            // new QW counts
            int[] lookupIndices =
                { 8, 9, 10 };
            foreach (int li in
                lookupIndices)
            {
                if (li >= chunks.Count)
                    continue;
                byte[] lookupChunk =
                    chunks[li];
                if (lookupChunk.Length
                    < 4)
                    continue;

                uint lFirst =
                    BitConverter
                        .ToUInt32(
                            lookupChunk,
                            0);
                int lookupN =
                    (int)(lFirst / 4);

                // Calculate QW for
                // each batch in new
                // mesh chunk
                uint mFirst =
                    BitConverter
                        .ToUInt32(
                            newMeshChunk,
                            0);
                int mPtrs =
                    (int)(mFirst / 4);

                for (int bi = 0;
                     bi < Math.Min(
                         lookupN,
                         mPtrs);
                     bi++)
                {
                    int lPtrOff =
                        bi * 4;
                    if (lPtrOff + 4 >
                        lookupChunk
                            .Length)
                        break;
                    uint recPtr =
                        BitConverter
                            .ToUInt32(
                                lookupChunk,
                                lPtrOff);
                    int recOff =
                        (int)recPtr;
                    if (recOff + 4 >
                        lookupChunk
                            .Length)
                        continue;

                    // QW = (batch_span
                    // / 16) - 1
                    uint batchPtr =
                        BitConverter
                            .ToUInt32(
                                newMeshChunk,
                                bi * 4);
                    uint nextPtr =
                        (bi + 1 < mPtrs
                        ? BitConverter
                            .ToUInt32(
                                newMeshChunk,
                                (bi + 1)
                                * 4)
                        : (uint)
                            newMeshChunk
                                .Length);
                    int span =
                        (int)(nextPtr -
                              batchPtr);
                    int qw =
                        (span / 16) - 1;
                    byte[] qwBytes =
                        BitConverter
                            .GetBytes(
                                (uint)qw);
                    Array.Copy(
                        qwBytes, 0,
                        lookupChunk,
                        recOff, 4);
                }

                chunks[li] =
                    lookupChunk;
            }

            // Rebuild full RDTB
            byte[] header =
                new byte[0x48];
            Array.Copy(origData, 0,
                header, 0, 0x48);

            int cursor = 0x48;
            int[] newOffs =
                new int[chunks.Count];
            for (int i = 0;
                 i < chunks.Count; i++)
            {
                newOffs[i] = cursor;
                cursor +=
                    chunks[i].Length;
            }

            // Update header offsets
            for (int i = 0;
                 i < newOffs.Length;
                 i++)
            {
                int pos = 0x10 + i * 4;
                byte[] ob =
                    BitConverter
                        .GetBytes(
                            newOffs[i]);
                Array.Copy(ob, 0,
                    header, pos, 4);
            }

            byte[] result =
                new byte[cursor];
            Array.Copy(header, 0,
                result, 0, 0x48);
            for (int i = 0;
                 i < chunks.Count; i++)
            {
                Array.Copy(chunks[i],
                    0, result,
                    newOffs[i],
                    chunks[i].Length);
            }

            return result;
        }

        // ═════════════════════════════
        // APPLY SLOT MIRROR
        // ═════════════════════════════
        static byte[] ApplySlotMirror(
            byte[] data)
        {
            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
            {
                if (0x10 + i * 4 + 4 >
                    data.Length)
                    break;
                rawSlots[i] =
                    BitConverter
                        .ToUInt32(
                            data,
                            0x10 +
                            i * 4);
            }

            int HDR = 0x48;
            uint c0 = rawSlots[0];
            uint c8 = rawSlots[8];
            uint c9 = rawSlots[9];
            uint c11 = rawSlots[11];
            uint c12 = rawSlots[12];

            byte[] chunks07 =
                new byte[c8 - c0];
            Array.Copy(data,
                (int)c0, chunks07,
                0, chunks07.Length);

            byte[] chunk8 =
                new byte[c9 - c8];
            Array.Copy(data,
                (int)c8, chunk8,
                0, chunk8.Length);

            byte[] chunk11 =
                new byte[c12 - c11];
            Array.Copy(data,
                (int)c11, chunk11,
                0, chunk11.Length);

            using (var ms =
                new MemoryStream())
            {
                ms.Write(
                    new byte[HDR],
                    0, HDR);
                ms.Write(chunks07, 0,
                    chunks07.Length);
                uint newC8 =
                    (uint)ms.Length;
                ms.Write(chunk8, 0,
                    chunk8.Length);
                uint newC11 =
                    (uint)ms.Length;
                ms.Write(chunk11, 0,
                    chunk11.Length);

                byte[] result =
                    ms.ToArray();

                // Copy original header
                Array.Copy(data, 0,
                    result, 0, HDR);

                // Patch offsets 0-7
                for (int i = 0;
                     i < 8; i++)
                {
                    byte[] ob =
                        BitConverter
                            .GetBytes(
                                rawSlots[i]);
                    Array.Copy(ob, 0,
                        result,
                        0x10 + i * 4,
                        4);
                }

                // Patch 8,9,10 -> newC8
                byte[] c8b =
                    BitConverter
                        .GetBytes(
                            newC8);
                for (int i = 8;
                     i <= 10; i++)
                    Array.Copy(c8b, 0,
                        result,
                        0x10 + i * 4,
                        4);

                // Patch 11,12,13
                // -> newC11
                byte[] c11b =
                    BitConverter
                        .GetBytes(
                            newC11);
                for (int i = 11;
                     i <= 13; i++)
                    Array.Copy(c11b, 0,
                        result,
                        0x10 + i * 4,
                        4);

                return result;
            }
        }

        // ═════════════════════════════
        // BUILD GDTB FROM MODEL
        // FOLDER TEXTURES
        // ═════════════════════════════
        static void BuildGdtbFromModels(
            string folderPath,
            string outGdtb,
            string srcGdtb)
        {
            string tempTex =
                Path.Combine(
                    folderPath,
                    "_build_tex_tmp");
            try
            {
                if (Directory.Exists(
                        tempTex))
                    Directory.Delete(
                        tempTex, true);
                Directory.CreateDirectory(
                    tempTex);

                string[] modelDirs =
                    Directory
                        .GetDirectories(
                            folderPath,
                            "model_*");
                bool found = false;
                foreach (string md in
                    modelDirs)
                {
                    foreach (string bmp
                        in Directory
                            .GetFiles(
                                md,
                                "texture_"
                                + "*.bmp"))
                    {
                        string fn =
                            Path
                                .GetFileName(
                                    bmp);
                        string dst =
                            Path.Combine(
                                tempTex,
                                fn);
                        try
                        {
                            File.Copy(
                                bmp, dst,
                                true);
                            found = true;
                        }
                        catch { }
                    }
                }

                if (found)
                {
                    try
                    {
                        GDTBArchive
                            .Create(
                                tempTex,
                                outGdtb);
                    }
                    catch (Exception ex)
                    {
                        Console
                            .ForegroundColor
                            = ConsoleColor
                                .Yellow;
                        Console.WriteLine(
                            "    [!] GDTB"
                            + ": " +
                            ex.Message);
                        Console
                            .ResetColor();
                        if (File.Exists(
                                srcGdtb))
                            File.Copy(
                                srcGdtb,
                                outGdtb,
                                true);
                    }
                }
                else if (File.Exists(
                    srcGdtb))
                {
                    File.Copy(srcGdtb,
                        outGdtb, true);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(
                        tempTex, true);
                }
                catch { }
            }
        }

        // ═════════════════════════════
        // HELPERS
        // ═════════════════════════════
        static List<int>
            ReadChunkOffsets(
                byte[] data)
        {
            var offs = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                int v = BitConverter
                    .ToInt32(data,
                        0x10 + i * 4);
                if (v == 0 || v < 0x48 ||
                    v > data.Length)
                    break;
                if (v == -1) continue;
                offs.Add(v);
            }
            return offs;
        }

        static int GetBatchCount(
            byte[] data)
        {
            var offs =
                ReadChunkOffsets(data);
            if (offs.Count < 9)
                return 0;
            int c8 = offs[8];
            int c8e = data.Length;
            for (int ci = 9;
                 ci < offs.Count; ci++)
            {
                if (offs[ci] != c8)
                {
                    c8e = offs[ci];
                    break;
                }
            }
            uint first = BitConverter
                .ToUInt32(data, c8);
            if (first == 0 ||
                first > (uint)(c8e
                                - c8))
                return 0;
            return (int)(first / 4);
        }

        // ═════════════════════════════
        // READ BONE WORLD POSITION
        // Reads the bone chunk (chunk 0)
        // and computes world-space
        // position of the given bone
        // by walking up parent chain.
        // ═════════════════════════════
        static float[] GetBoneWorldPos(
            byte[] rdtbData,
            int boneIdx)
        {
            var offs =
                ReadChunkOffsets(rdtbData);
            if (offs.Count < 1)
                return new float[]
                { 0f, 0f, 0f };

            int c0Off = offs[0];
            int c0End =
                (offs.Count > 1
                ? offs[1]
                : rdtbData.Length);
            int c0Size = c0End - c0Off;

            int boneCount =
                BitConverter.ToUInt16(
                    rdtbData, 0x0E);

            // Bone records start after
            // the pointer array
            int rowsStart =
                c0Off + boneCount * 4;

            // Walk parent chain
            float wx = 0f, wy = 0f,
                  wz = 0f;
            int cur = boneIdx;
            var visited = new HashSet<int>();
            int safety = 0;

            while (cur >= 0 &&
                cur < boneCount &&
                safety < 256)
            {
                if (visited.Contains(cur))
                    break;
                visited.Add(cur);
                safety++;

                int off = rowsStart
                    + cur * 16;
                if (off + 16 >
                    rdtbData.Length)
                    break;

                byte pb = rdtbData[off + 3];

                float lx =
                    BitConverter.ToSingle(
                        rdtbData, off + 4);
                float ly =
                    BitConverter.ToSingle(
                        rdtbData, off + 8);
                float lz =
                    BitConverter.ToSingle(
                        rdtbData, off + 12);
                wx += lx;
                wy += ly;
                wz += lz;

                if (pb == 0xFF ||
                    pb >= boneCount)
                    break;
                cur = pb;
            }

            return new float[]
                { wx, wy, wz };
        }

        // ═════════════════════════════
        // GET BONE INDEX FOR BATCH
        // Reads material table to find
        // which bone a batch is bound to
        // ═════════════════════════════
        static int GetBatchBoneIdx(
            byte[] rdtbData,
            int batchIdx)
        {
            var offs =
                ReadChunkOffsets(rdtbData);
            if (offs.Count < 9)
                return -1;

            int c8 = offs[8];
            int c8e =
                (offs.Count > 9
                ? offs[9]
                : rdtbData.Length);

            uint first =
                BitConverter.ToUInt32(
                    rdtbData, c8);
            if (first == 0 ||
                first > (uint)(c8e - c8))
                return -1;

            int bc = (int)(first / 4);
            if (batchIdx >= bc)
                return -1;

            uint ptr =
                BitConverter.ToUInt32(
                    rdtbData,
                    c8 + batchIdx * 4);
            int rec = c8 + (int)ptr;
            if (rec + 8 >
                rdtbData.Length)
                return -1;

            // bone idx = first u16
            return BitConverter
                .ToUInt16(rdtbData,
                    rec);
        }

    }
}
