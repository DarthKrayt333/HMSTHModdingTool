using HMSTHModdingTool.IO;
using HMSTHModdingTool.GDTB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// FIXED v5: Correctly reads
    /// chunk offsets including ALL
    /// active chunks. Separates
    /// material chunk (8) from mesh
    /// chunk (9 for small RDTBs).
    /// Uses VIF block counting to
    /// identify true mesh chunk.
    /// </summary>
    public static class RDTBBatchFolder
    {
        public static void ExtractBatchFolder(
            string rdtbPath,
            string gdtbPath,
            string baseName)
        {
            if (!File.Exists(rdtbPath))
                return;

            string parent =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        rdtbPath));
            string outDir = Path.Combine(
                parent,
                baseName +
                "_3d_batches_obj");

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Per-Batch Folder"
                + " Extract (FIXED v5)");
            Console.ResetColor();
            Console.WriteLine(
                "    OUT: " + outDir);

            Directory.CreateDirectory(
                outDir);

            string tempTex = Path.Combine(
                outDir, "_tex_tmp");
            Directory.CreateDirectory(
                tempTex);
            if (File.Exists(gdtbPath))
            {
                try
                {
                    GDTBArchive.Extract(
                        gdtbPath,
                        tempTex);
                }
                catch { }
            }

            // DEBUG: show what was extracted
            Console.WriteLine(
                "    Textures in tempTex: " +
                Directory.GetFiles(
                    tempTex, "*.bmp").Length);
            foreach (var dbgBmp in
                Directory.GetFiles(
                    tempTex, "*.bmp"))
                Console.WriteLine(
                    "      " +
                    Path.GetFileName(dbgBmp));

            byte[] data =
                File.ReadAllBytes(
                    rdtbPath);

            // FIX v5: Read raw slot
            // values properly. Each
            // slot may be 0 (unused),
            // 0xFFFFFFFF (skipped),
            // or a valid offset.
            // We need ALL valid
            // offsets in order.
            List<int> offs =
                ReadChunkOffsetsV5(data);

            if (offs.Count < 9)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] Too few"
                    + " chunks (" +
                    offs.Count +
                    "), aborting");
                Console.ResetColor();
                return;
            }

            Console.WriteLine(
                "    Found " +
                offs.Count +
                " active chunks:");
            for (int i = 0;
                 i < offs.Count;
                 i++)
            {
                int end = (i + 1 <
                    offs.Count
                    ? offs[i + 1]
                    : data.Length);
                Console.WriteLine(
                    "      [" +
                    i.ToString("D2") +
                    "] 0x" +
                    offs[i].ToString(
                        "X8") +
                    " size " +
                    (end - offs[i])
                        .ToString("N0"));
            }

            // FIX v5: Material table
            // is at slot index 8 in
            // RAW slot order, but we
            // need to count by ACTIVE
            // chunks. For small RDTBs,
            // active chunk 8 IS the
            // material table (chunk 8
            // in raw slots).
            // For big RDTBs, active
            // chunk 8 is also slot 8.
            // So index 8 in offs[]
            // works for both.
            int matChunkIdx = 8;

            // FIX v5: Find mesh chunk
            // by scanning for VIF
            // data with HIGHEST count
            // (real mesh has many
            // VIF blocks)
            int chunkIdx = FindMeshChunkV5(
                data, offs, matChunkIdx);

            if (chunkIdx < 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] No mesh"
                    + " chunk with VIF"
                    + " data found");
                Console.ResetColor();
                return;
            }

            bool isSmallRdtb =
                offs.Count <= 11;

            Console.WriteLine(
                "    RDTB type: " +
                (isSmallRdtb
                    ? "SMALL"
                    : "BIG") +
                " (" + offs.Count +
                " chunks, mat=" +
                matChunkIdx +
                ", mesh=" +
                chunkIdx + ")");

            // Material table chunk
            int c8Off = offs[matChunkIdx];
            int c8End = (matChunkIdx + 1
                < offs.Count
                ? offs[matChunkIdx + 1]
                : data.Length);

            uint matFirst =
                BitConverter.ToUInt32(
                    data, c8Off);
            if (matFirst == 0 ||
                matFirst > (uint)(c8End
                                   - c8Off))
            {
                File.Copy(rdtbPath,
                    Path.Combine(outDir,
                        "_source.rdtb"),
                    true);
                if (File.Exists(
                        gdtbPath))
                    File.Copy(gdtbPath,
                        Path.Combine(
                            outDir,
                            "_source.gdtb"),
                        true);
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] Empty"
                    + " material table");
                Console.ResetColor();
                return;
            }
            int bc = (int)(matFirst / 4);

            // Map batch -> tex
            Dictionary<int, int> batchTex =
                new Dictionary<int, int>();
            for (int i = 0; i < bc; i++)
            {
                int ptrOff = c8Off + i * 4;
                if (ptrOff + 4 >
                    data.Length)
                    break;
                uint ptr =
                    BitConverter.ToUInt32(
                        data, ptrOff);
                int recOff = c8Off
                    + (int)ptr;
                if (recOff + 8 >
                    data.Length)
                    continue;
                int tex =
                    BitConverter.ToUInt16(
                        data, recOff + 6);
                batchTex[i] = tex;
            }

            SortedDictionary<int,
                List<int>> texGroups =
                new SortedDictionary<int,
                    List<int>>();
            foreach (KeyValuePair<int,
                int> kv in batchTex)
            {
                if (!texGroups
                        .ContainsKey(
                            kv.Value))
                    texGroups[
                        kv.Value] =
                        new List<int>();
                texGroups[kv.Value].Add(
                    kv.Key);
            }

            // Extract MESH chunk
            // (not material chunk!)
            int cs = offs[chunkIdx];
            int ce = (chunkIdx + 1 <
                offs.Count
                ? offs[chunkIdx + 1]
                : data.Length);
            byte[] meshChunk = new byte[
                ce - cs];
            Array.Copy(data, cs,
                meshChunk, 0, ce - cs);

            uint mFirst =
                BitConverter.ToUInt32(
                    meshChunk, 0);

            if (mFirst == 0 ||
                mFirst > (uint)
                    meshChunk.Length ||
                mFirst < 4)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [WARN] Mesh"
                    + " chunk no valid"
                    + " ptr table"
                    + " (mFirst=0x" +
                    mFirst.ToString(
                        "X8") + ")");
                Console.ResetColor();
                File.Copy(rdtbPath,
                    Path.Combine(outDir,
                        "_source.rdtb"),
                    true);
                if (File.Exists(
                        gdtbPath))
                    File.Copy(gdtbPath,
                        Path.Combine(
                            outDir,
                            "_source.gdtb"),
                        true);
                return;
            }

            int nPtrs =
                (int)(mFirst / 4);
            int safeBatchCount =
                Math.Min(bc, nPtrs);

            // Read batch pointers
            // & sort globally
            uint[] batchPtrs =
                new uint[safeBatchCount];
            for (int i = 0;
                 i < safeBatchCount; i++)
            {
                batchPtrs[i] =
                    BitConverter.ToUInt32(
                        meshChunk, i * 4);
            }

            var sortedPtrs =
                new List<uint>();
            for (int i = 0;
                 i < safeBatchCount; i++)
            {
                uint p = batchPtrs[i];
                if (p > 0 &&
                    p < (uint)
                        meshChunk.Length)
                    sortedPtrs.Add(p);
            }
            sortedPtrs.Sort();
            sortedPtrs =
                sortedPtrs
                    .Distinct()
                    .ToList();

            Console.WriteLine(
                "    Mesh chunk: " +
                meshChunk.Length
                    .ToString("N0") +
                " B, " + nPtrs +
                " batch ptrs, " +
                bc + " mats, " +
                sortedPtrs.Count +
                " unique ptrs");

            // ── AUTO-SCALE ──────────────
            float autoScale = 1.0f;
            {
                float mnx = float.MaxValue;
                float mxx = float.MinValue;
                float mny = float.MaxValue;
                float mxy = float.MinValue;
                float mnz = float.MaxValue;
                float mxz = float.MinValue;
                bool any = false;

                for (int i = 0;
                     i < safeBatchCount;
                     i++)
                {
                    uint bPtr = batchPtrs[i];
                    if (bPtr == 0 ||
                        bPtr >= (uint)
                            meshChunk.Length)
                        continue;

                    uint nPtr =
                        (uint)meshChunk.Length;
                    foreach (uint sp in
                        sortedPtrs)
                    {
                        if (sp > bPtr)
                        {
                            nPtr = sp;
                            break;
                        }
                    }

                    int bSz =
                        (int)(nPtr - bPtr);
                    if (bSz <= 16) continue;

                    byte[] bd =
                        new byte[bSz];
                    Array.Copy(meshChunk,
                        (int)bPtr, bd, 0, bSz);

                    int pos = 0;
                    while (pos + 16 <=
                        bd.Length)
                    {
                        if (bd[pos] != 0x00
                            || bd[pos + 1]
                                != 0x80
                            || bd[pos + 3]
                                != 0x6C)
                        {
                            pos += 4;
                            continue;
                        }
                        int vcb = bd[pos + 4];
                        if (vcb < 1 ||
                            vcb > 96)
                        {
                            pos += 4;
                            continue;
                        }
                        int vS = pos + 16;
                        if (vS + vcb * 16 >
                            bd.Length)
                        {
                            pos += 4;
                            continue;
                        }
                        for (int vi = 0;
                             vi < vcb; vi++)
                        {
                            int vo =
                                vS + vi * 16;
                            if (vo + 16 >
                                bd.Length)
                                break;
                            float vx =
                                BitConverter
                                    .ToSingle(
                                        bd,
                                        vo + 4);
                            float vy =
                                BitConverter
                                    .ToSingle(
                                        bd,
                                        vo + 8);
                            float vz =
                                BitConverter
                                    .ToSingle(
                                        bd,
                                        vo + 12);
                            if (float.IsNaN(vx)
                                || float
                                    .IsNaN(vy)
                                || float
                                    .IsNaN(vz)
                                || float
                                    .IsInfinity(
                                        vx)
                                || float
                                    .IsInfinity(
                                        vy)
                                || float
                                    .IsInfinity(
                                        vz))
                                continue;
                            if (vx < mnx)
                                mnx = vx;
                            if (vx > mxx)
                                mxx = vx;
                            if (vy < mny)
                                mny = vy;
                            if (vy > mxy)
                                mxy = vy;
                            if (vz < mnz)
                                mnz = vz;
                            if (vz > mxz)
                                mxz = vz;
                            any = true;
                        }
                        int bSize =
                            16 +
                            3 * vcb * 16 +
                            16;
                        pos += bSize;
                    }
                }

                if (any)
                {
                    float dx = mxx - mnx;
                    float dy = mxy - mny;
                    float dz = mxz - mnz;
                    float maxDim =
                        Math.Max(dx,
                        Math.Max(dy, dz));
                    const float TARGET = 100f;
                    const float THRESHOLD =
                        250f;
                    if (maxDim > THRESHOLD)
                    {
                        autoScale =
                            TARGET / maxDim;
                        Console.WriteLine(
                            "    [auto-scale]"
                            + " " +
                            autoScale
                                .ToString(
                                    "F4") +
                            "x (was " +
                            maxDim
                                .ToString(
                                    "F0") +
                            " units)");
                    }
                }
            }


            int totalWritten = 0;
            int totalSkipped = 0;

            foreach (KeyValuePair<int,
                List<int>> kv in
                texGroups)
            {
                int texId = kv.Key;
                List<int> batches =
                    kv.Value;
                string modelDir =
                    Path.Combine(
                        outDir,
                        "model_" +
                        texId.ToString(
                            "D2"));
                Directory.CreateDirectory(
                    modelDir);

                // Copy matching texture
                // AND all textures as
                // fallback so Blender
                // can find them
                string srcTex =
                    Path.Combine(
                        tempTex,
                        "texture_" +
                        texId.ToString(
                            "D2") +
                        ".bmp");
                string dstTex =
                    Path.Combine(
                        modelDir,
                        "texture_" +
                        texId.ToString(
                            "D2") +
                        ".bmp");
                if (File.Exists(srcTex))
                {
                    try
                    {
                        File.Copy(
                            srcTex,
                            dstTex,
                            true);
                    }
                    catch { }
                }
                else
                {
                    // Texture not found by
                    // exact index - copy ALL
                    // available textures so
                    // at least one matches
                    // (handles embedded RDTBs
                    // from SRDB where tex_ids
                    // may not start at 0)
                    foreach (var anyBmp in
                        Directory.GetFiles(
                            tempTex,
                            "texture_*.bmp"))
                    {
                        string anyDst =
                            Path.Combine(
                                modelDir,
                                Path.GetFileName(
                                    anyBmp));
                        try
                        {
                            File.Copy(
                                anyBmp,
                                anyDst,
                                true);
                        }
                        catch { }
                    }
                }

                Console.WriteLine(
                    "    model_" +
                    texId.ToString("D2")
                    + ": " +
                    batches.Count +
                    " batches");

                foreach (int bi in
                    batches)
                {
                    if (bi >=
                        safeBatchCount)
                    {
                        totalSkipped++;
                        continue;
                    }

                    uint bPtr =
                        batchPtrs[bi];

                    if (bPtr == 0 ||
                        bPtr >= (uint)
                            meshChunk
                                .Length)
                    {
                        Console
                            .ForegroundColor
                            = ConsoleColor
                                .Yellow;
                        Console.WriteLine(
                            "      [SKIP]"
                            + " batch "
                            + bi +
                            " null ptr");
                        Console
                            .ResetColor();
                        totalSkipped++;
                        continue;
                    }

                    uint nPtr = (uint)
                        meshChunk
                            .Length;
                    foreach (uint sp in
                        sortedPtrs)
                    {
                        if (sp > bPtr)
                        {
                            nPtr = sp;
                            break;
                        }
                    }

                    int batchSize =
                        (int)(nPtr -
                              bPtr);
                    if (batchSize
                            <= 16 ||
                        batchSize >
                            meshChunk
                                .Length)
                    {
                        Console
                            .ForegroundColor
                            = ConsoleColor
                                .Yellow;
                        Console.WriteLine(
                            "      [SKIP]"
                            + " batch "
                            + bi +
                            " size "
                            + batchSize
                            + " out of"
                            + " range");
                        Console
                            .ResetColor();
                        totalSkipped++;
                        continue;
                    }

                    byte[] bdata =
                        new byte[
                            batchSize];
                    Array.Copy(
                        meshChunk,
                        (int)bPtr,
                        bdata, 0,
                        bdata.Length);

                    bool hasVif =
                        false;
                    for (int vi = 0;
                         vi + 16 <=
                            bdata.Length;
                         vi += 4)
                    {
                        if (bdata[vi]
                                == 0x00
                            && bdata[
                                vi + 1]
                                == 0x80
                            && bdata[
                                vi + 3]
                                == 0x6C)
                        {
                            hasVif =
                                true;
                            break;
                        }
                    }
                    if (!hasVif)
                    {
                        Console
                            .ForegroundColor
                            = ConsoleColor
                                .Yellow;
                        Console.WriteLine(
                            "      [SKIP]"
                            + " batch "
                            + bi +
                            " no VIF"
                            + " data");
                        Console
                            .ResetColor();
                        totalSkipped++;
                        continue;
                    }

                    string objFile =
                        Path.Combine(
                            modelDir,
                            "batch_" +
                            bi.ToString(
                                "D4")
                            + ".obj");
                    string mtlFile =
                        Path.Combine(
                            modelDir,
                            "batch_" +
                            bi.ToString(
                                "D4")
                            + ".mtl");


                    bool ok =
                        WriteBatchObj(
                            bdata,
                            objFile,
                            mtlFile,
                            bi, texId,
                            autoScale);
                    if (ok)
                        totalWritten++;
                    else
                        totalSkipped++;
                }
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "    Total: " +
                totalWritten +
                " written, " +
                totalSkipped +
                " skipped");
            Console.ResetColor();

            File.Copy(rdtbPath,
                Path.Combine(outDir,
                    "_source.rdtb"),
                true);
            if (File.Exists(gdtbPath))
                File.Copy(gdtbPath,
                    Path.Combine(outDir,
                        "_source.gdtb"),
                    true);

            try
            {
                Directory.Delete(
                    tempTex, true);
            }
            catch { }

            string infoPath =
                Path.Combine(outDir,
                    "_info.txt");
            using (StreamWriter sw =
                new StreamWriter(
                    infoPath))
            {
                sw.WriteLine(
                    "HMSTH Batch Folder");
                sw.WriteLine(
                    "RDTB type: " +
                    (isSmallRdtb
                        ? "SMALL"
                        : "BIG"));
                sw.WriteLine(
                    "Mat chunk idx: "
                    + matChunkIdx);
                sw.WriteLine(
                    "Mesh chunk idx: "
                    + chunkIdx);
                sw.WriteLine(
                    "Source RDTB: " +
                    Path.GetFileName(
                        rdtbPath));
                sw.WriteLine(
                    "Source GDTB: " +
                    Path.GetFileName(
                        gdtbPath));
                sw.WriteLine(
                    "Total batches: "
                    + bc);
                sw.WriteLine(
                    "Written: " +
                    totalWritten);
                sw.WriteLine(
                    "Skipped: " +
                    totalSkipped);
                sw.WriteLine();
                sw.WriteLine(
                    "Models:");
                foreach (KeyValuePair<
                    int, List<int>> kv
                    in texGroups)
                {
                    sw.Write(
                        "  model_" +
                        kv.Key.ToString(
                            "D2") +
                        " (tex=" +
                        kv.Key +
                        "): batches [");
                    for (int i = 0;
                         i < kv.Value
                             .Count;
                         i++)
                    {
                        if (i > 0)
                            sw.Write(
                                ", ");
                        sw.Write(
                            kv.Value[
                                i]);
                    }
                    sw.WriteLine("]");
                }
                sw.WriteLine();
                sw.WriteLine(
                    "TIP: Delete any"
                    + " batch_XXXX.obj"
                    + " file to remove"
                    + " that batch on"
                    + " rebuild!");
                sw.WriteLine(
                    "TIP: Edit the"
                    + " texture_XX.bmp"
                    + " in each model"
                    + " folder to"
                    + " change"
                    + " textures!");

                // Save auto-scale to _info.txt
                if (autoScale != 1.0f)
                {
                    sw.WriteLine(
                        "Auto Scale: " +
                        autoScale.ToString(
                            "F6",
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture));
                }

            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] " + outDir);
            Console.ResetColor();
        }

        // ═════════════════════════════
        // READ CHUNK OFFSETS v5
        // Reads ALL active chunk
        // offsets from the 14-slot
        // table. Skips 0 (unused)
        // and 0xFFFFFFFF (skipped).
        // ═════════════════════════════
        private static List<int>
            ReadChunkOffsetsV5(
                byte[] data)
        {
            List<int> offs =
                new List<int>();
            for (int i = 0; i < 14; i++)
            {
                if (0x10 + i * 4 + 4 >
                    data.Length)
                    break;
                uint v =
                    BitConverter.ToUInt32(
                        data,
                        0x10 + i * 4);
                // Skip unused/null
                if (v == 0)
                    continue;
                // Skip sentinel
                if (v == 0xFFFFFFFF)
                    continue;
                // Sanity check
                if (v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            // FIX v5: Sort ascending
            // because slot order may
            // not match file order
            offs.Sort();
            // Remove duplicates
            // (mirrored slots)
            offs = offs.Distinct().ToList();
            return offs;
        }

        // ═════════════════════════════
        // FIND MESH CHUNK v5
        // Picks the chunk with the
        // MOST VIF blocks AND a valid
        // pointer table. Excludes
        // the material chunk.
        // ═════════════════════════════
        private static int
            FindMeshChunkV5(
                byte[] data,
                List<int> offs,
                int excludeIdx)
        {
            int bestIdx = -1;
            int bestVifCount = 0;

            for (int ci = 0;
                 ci < offs.Count; ci++)
            {
                if (ci == excludeIdx)
                    continue;

                int cs = offs[ci];
                int ce =
                    (ci + 1 < offs.Count
                    ? offs[ci + 1]
                    : data.Length);
                int sz = ce - cs;
                if (sz < 64) continue;

                // Must have valid ptr
                // table
                uint first =
                    BitConverter
                        .ToUInt32(
                            data, cs);
                if (first == 0 ||
                    first > (uint)sz ||
                    first < 4)
                    continue;

                // Count VIF blocks
                int vifCount = 0;
                for (int i = cs;
                     i + 16 <= ce;
                     i += 4)
                {
                    if (data[i] == 0x00
                        && data[i + 1]
                            == 0x80
                        && data[i + 3]
                            == 0x6C)
                    {
                        vifCount++;
                    }
                }

                if (vifCount >
                    bestVifCount)
                {
                    bestVifCount =
                        vifCount;
                    bestIdx = ci;
                }
            }

            return bestIdx;
        }

        public static bool
            IsBatchFolder(
                string folderPath)
        {
            if (!Directory.Exists(
                    folderPath))
                return false;
            string src = Path.Combine(
                folderPath,
                "_source.rdtb");
            if (!File.Exists(src))
                return false;
            string[] modelDirs =
                Directory
                    .GetDirectories(
                        folderPath,
                        "model_*");
            return modelDirs.Length > 0;
        }

        public static void
            BuildFromBatchFolder(
                string folderPath,
                string outDir,
                string normalsMode,
                float[] customNormal,
                bool deleteAll,
                string targetFormat,
                Dictionary<int, int>
                    normalsCopyMap = null)
        {
            RDTBBatchReplacer
                .TargetRdtbFormat fmt;
            string fl = (targetFormat
                ?? "")
                .ToLower().Trim();
            switch (fl)
            {
                case "big":
                    fmt = RDTBBatchReplacer
                        .TargetRdtbFormat.Big;
                    break;
                case "small":
                    fmt = RDTBBatchReplacer
                        .TargetRdtbFormat.Small;
                    break;
                case "mirror":
                case "mirrored":
                    fmt = RDTBBatchReplacer
                        .TargetRdtbFormat.Mirror;
                    break;
                case "match":
                case "auto":
                    fmt = RDTBBatchReplacer
                        .TargetRdtbFormat.Match;
                    break;
                default:
                    fmt = RDTBBatchReplacer
                        .TargetRdtbFormat.Mirror;
                    break;
            }
            RDTBBatchReplacer.Build(
                folderPath,
                outDir,
                normalsMode,
                customNormal,
                deleteAll,
                fmt,
                normalsCopyMap);
        }

        private static bool
            WriteBatchObj(
                byte[] bdata,
                string objPath,
                string mtlPath,
                int batchIdx,
                int texId,
                float autoScale = 1.0f)
        {
            List<float[]> allV =
                new List<float[]>();
            List<float[]> allN =
                new List<float[]>();
            List<float[]> allU =
                new List<float[]>();
            List<List<int>>
                blockLayouts =
                new List<List<int>>();

            int pos = 0;
            while (pos + 16 <=
                bdata.Length)
            {
                if (bdata[pos] != 0x00
                    || bdata[pos + 1]
                       != 0x80
                    || bdata[pos + 3]
                       != 0x6C)
                {
                    pos += 4;
                    continue;
                }
                int vcb = bdata[pos + 4];

                if (vcb < 1 || vcb > 96)
                {
                    pos += 4;
                    continue;
                }

                int vStart = pos + 16;
                int nStart = vStart
                    + vcb * 16;
                int uStart = nStart
                    + vcb * 16;

                if (uStart + vcb * 16
                    > bdata.Length)
                {
                    pos += 4;
                    continue;
                }

                int blockStart =
                    allV.Count;
                for (int i = 0; i < vcb;
                     i++)
                {
                    int vo = vStart
                        + i * 16;
                    int no = nStart
                        + i * 16;
                    int uo = uStart
                        + i * 16;
                    if (uo + 16 >
                        bdata.Length)
                        break;

                    allV.Add(new float[]
                    {
                        BitConverter.ToSingle(
                            bdata, vo + 4)
                            * autoScale,
                        BitConverter.ToSingle(
                            bdata, vo + 8)
                            * autoScale,
                        BitConverter.ToSingle(
                            bdata, vo + 12)
                            * autoScale,
                        BitConverter.ToUInt32(
                            bdata, vo + 0)
                    });

                    allN.Add(new float[]
                    {
                        BitConverter
                            .ToSingle(
                                bdata,
                                no + 4),
                        BitConverter
                            .ToSingle(
                                bdata,
                                no + 8),
                        BitConverter
                            .ToSingle(
                                bdata,
                                no + 12)
                    });
                    allU.Add(new float[]
                    {
                        BitConverter
                            .ToSingle(
                                bdata,
                                uo + 4),
                        BitConverter
                            .ToSingle(
                                bdata,
                                uo + 8)
                    });
                }
                int blockEnd =
                    allV.Count;
                List<int> layout =
                    new List<int>();
                for (int j =
                        blockStart;
                     j < blockEnd; j++)
                    layout.Add(j);
                blockLayouts.Add(
                    layout);
                int blockSize = 16
                    + 3 * vcb * 16
                    + 16;
                if (pos + blockSize
                    + 16 <=
                    bdata.Length)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                bdata,
                                pos +
                                blockSize);
                    if (eof ==
                        0x70000000)
                        blockSize += 16;
                }
                pos += blockSize;
            }

            List<int[]> faces =
                new List<int[]>();
            foreach (List<int> layout
                in blockLayouts)
            {
                int nn = layout.Count;
                for (int i = 0;
                     i < nn - 2; i++)
                {
                    int a, b, c;
                    a = layout[i];
                    if (i % 2 == 0)
                    {
                        b = layout[i + 1];
                        c = layout[i + 2];
                    }
                    else
                    {
                        b = layout[i + 2];
                        c = layout[i + 1];
                    }
                    if (a == b ||
                        b == c ||
                        a == c)
                        continue;
                    float[] v0 =
                        allV[a];
                    float[] v1 =
                        allV[b];
                    float[] v2 =
                        allV[c];
                    float ax = v1[0]
                        - v0[0];
                    float ay = v1[1]
                        - v0[1];
                    float az = v1[2]
                        - v0[2];
                    float bx = v2[0]
                        - v0[0];
                    float by = v2[1]
                        - v0[1];
                    float bz = v2[2]
                        - v0[2];
                    float cx = ay * bz
                        - az * by;
                    float cy = az * bx
                        - ax * bz;
                    float cz = ax * by
                        - ay * bx;
                    if (cx * cx + cy * cy
                        + cz * cz >
                        1e-10f)
                        faces.Add(
                            new int[]
                            { a, b, c });
                }
            }

            if (allV.Count == 0 ||
                faces.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor
                        .DarkYellow;
                Console.WriteLine(
                    "      [EMPTY]"
                    + " batch_" +
                    batchIdx
                        .ToString(
                            "D4") +
                    " no geometry"
                    + " (v=" +
                    allV.Count +
                    " f=" +
                    faces.Count +
                    ")");
                Console.ResetColor();
                return false;
            }

            string mtlName = "batch_"
                + batchIdx.ToString(
                    "D4");
            string texFn = "texture_"
                + texId.ToString("D2")
                + ".bmp";
            using (StreamWriter sw =
                new StreamWriter(
                    mtlPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine("# " +
                    mtlName);
                sw.WriteLine();
                sw.WriteLine("newmtl "
                    + mtlName);
                sw.WriteLine(
                    "Ka 1 1 1");
                sw.WriteLine(
                    "Kd 1 1 1");
                sw.WriteLine(
                    "Ks 0 0 0");
                sw.WriteLine("Ns 10");
                sw.WriteLine("illum 2");
                sw.WriteLine(
                    "map_Kd " + texFn);
            }

            using (StreamWriter sw =
                new StreamWriter(
                    objPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine("# Batch "
                    + batchIdx +
                    " (tex " + texId
                    + ")");
                sw.WriteLine("mtllib "
                    + Path.GetFileName(
                        mtlPath));
                sw.WriteLine();
                for (int i = 0; i < allV.Count; i++)
                {
                    float[] v = allV[i];
                    sw.WriteLine("v "
                        + v[0].ToString("F6") + " "
                        + v[1].ToString("F6") + " "
                        + v[2].ToString("F6"));
                    // Preserve bone weight flag
                    // as metadata comment right
                    // after each vertex line
                    if (v.Length >= 4)
                    {
                        uint flag = (uint)v[3];
                        sw.WriteLine("#vw "
                            + flag.ToString("X8"));
                    }
                }
                sw.WriteLine();
                foreach (float[] uv
                    in allU)
                    sw.WriteLine(
                        "vt " +
                        uv[0].ToString(
                            "F6") + " "
                        + (1f -
                           uv[1])
                        .ToString("F6"));
                sw.WriteLine();
                foreach (float[] n
                    in allN)
                    sw.WriteLine("vn "
                        + n[0].ToString(
                            "F6") + " "
                        + n[1].ToString(
                            "F6") + " "
                        + n[2].ToString(
                            "F6"));
                sw.WriteLine();
                sw.WriteLine("g " +
                    mtlName);
                sw.WriteLine(
                    "usemtl " +
                    mtlName);
                foreach (int[] f in
                    faces)
                {
                    int a = f[0] + 1;
                    int b = f[1] + 1;
                    int c = f[2] + 1;
                    sw.WriteLine("f "
                        + a + "/" + a
                        + "/" + a +
                        " " + b + "/"
                        + b + "/" + b
                        + " " + c +
                        "/" + c + "/"
                        + c);
                }
            }
            return true;
        }

        // ═════════════════════════════
        // GET BONE WORLD POS
        // ═════════════════════════════
        private static float[]
            GetBoneWorldPos(
                byte[] rdtbData,
                int boneIdx)
        {
            var offs =
                ReadChunkOffsets(rdtbData);
            if (offs.Count < 1)
                return new float[]
                { 0f, 0f, 0f };

            int c0Off = offs[0];
            int boneCount =
                BitConverter.ToUInt16(
                    rdtbData, 0x0E);
            int rowsStart =
                c0Off + boneCount * 4;

            float wx = 0f, wy = 0f,
                  wz = 0f;
            int cur = boneIdx;
            var visited =
                new HashSet<int>();
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

                byte pb =
                    rdtbData[off + 3];
                float lx =
                    BitConverter.ToSingle(
                        rdtbData,
                        off + 4);
                float ly =
                    BitConverter.ToSingle(
                        rdtbData,
                        off + 8);
                float lz =
                    BitConverter.ToSingle(
                        rdtbData,
                        off + 12);
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
        // GET BATCH BONE IDX
        // ═════════════════════════════
        private static int
            GetBatchBoneIdx(
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

            return BitConverter
                .ToUInt16(rdtbData,
                    rec);
        }

        private static bool
    WriteBatchObjWithBone(
        byte[] bdata,
        string objPath,
        string mtlPath,
        int batchIdx,
        int texId,
        float[] bonePos)
        {
            List<float[]> allV =
                new List<float[]>();
            List<float[]> allN =
                new List<float[]>();
            List<float[]> allU =
                new List<float[]>();
            List<List<int>>
                blockLayouts =
                new List<List<int>>();

            int pos = 0;
            while (pos + 16 <=
                bdata.Length)
            {
                if (bdata[pos] != 0x00
                    || bdata[pos + 1]
                       != 0x80
                    || bdata[pos + 3]
                       != 0x6C)
                {
                    pos += 4;
                    continue;
                }
                int vcb = bdata[pos + 4];
                if (vcb < 1 || vcb > 96)
                {
                    pos += 4;
                    continue;
                }
                int vStart = pos + 16;
                int nStart = vStart
                    + vcb * 16;
                int uStart = nStart
                    + vcb * 16;
                if (uStart + vcb * 16
                    > bdata.Length)
                {
                    pos += 4;
                    continue;
                }
                int blockStart =
                    allV.Count;
                for (int i = 0;
                     i < vcb; i++)
                {
                    int vo = vStart
                        + i * 16;
                    int no = nStart
                        + i * 16;
                    int uo = uStart
                        + i * 16;
                    if (uo + 16 >
                        bdata.Length)
                        break;

                    // ADD bone world pos
                    // to get world-space
                    // coords for OBJ
                    allV.Add(new float[]
                    {
                BitConverter
                    .ToSingle(
                        bdata,
                        vo + 4)
                    + bonePos[0],
                BitConverter
                    .ToSingle(
                        bdata,
                        vo + 8)
                    + bonePos[1],
                BitConverter
                    .ToSingle(
                        bdata,
                        vo + 12)
                    + bonePos[2]
                    });
                    allN.Add(new float[]
                    {
                BitConverter
                    .ToSingle(
                        bdata,
                        no + 4),
                BitConverter
                    .ToSingle(
                        bdata,
                        no + 8),
                BitConverter
                    .ToSingle(
                        bdata,
                        no + 12)
                    });
                    allU.Add(new float[]
                    {
                BitConverter
                    .ToSingle(
                        bdata,
                        uo + 4),
                BitConverter
                    .ToSingle(
                        bdata,
                        uo + 8)
                    });
                }
                int blockEnd =
                    allV.Count;
                List<int> layout =
                    new List<int>();
                for (int j = blockStart;
                     j < blockEnd; j++)
                    layout.Add(j);
                blockLayouts.Add(
                    layout);
                int blockSize = 16
                    + 3 * vcb * 16
                    + 16;
                if (pos + blockSize
                    + 16 <=
                    bdata.Length)
                {
                    uint eof =
                        BitConverter
                            .ToUInt32(
                                bdata,
                                pos +
                                blockSize);
                    if (eof ==
                        0x70000000)
                        blockSize += 16;
                }
                pos += blockSize;
            }

            List<int[]> faces =
                new List<int[]>();
            foreach (List<int> layout
                in blockLayouts)
            {
                int nn = layout.Count;
                for (int i = 0;
                     i < nn - 2; i++)
                {
                    int a, b, c;
                    a = layout[i];
                    if (i % 2 == 0)
                    {
                        b = layout[i + 1];
                        c = layout[i + 2];
                    }
                    else
                    {
                        b = layout[i + 2];
                        c = layout[i + 1];
                    }
                    if (a == b ||
                        b == c ||
                        a == c)
                        continue;
                    float[] v0 = allV[a];
                    float[] v1 = allV[b];
                    float[] v2 = allV[c];
                    float ax = v1[0] - v0[0];
                    float ay = v1[1] - v0[1];
                    float az = v1[2] - v0[2];
                    float bx = v2[0] - v0[0];
                    float by = v2[1] - v0[1];
                    float bz = v2[2] - v0[2];
                    float cx = ay * bz
                        - az * by;
                    float cy = az * bx
                        - ax * bz;
                    float cz = ax * by
                        - ay * bx;
                    if (cx * cx + cy * cy
                        + cz * cz >
                        1e-10f)
                        faces.Add(
                            new int[]
                            { a, b, c });
                }
            }

            if (allV.Count == 0 ||
                faces.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor
                        .DarkYellow;
                Console.WriteLine(
                    "      [EMPTY] batch_"
                    + batchIdx.ToString(
                        "D4") +
                    " no geometry");
                Console.ResetColor();
                return false;
            }

            string mtlName = "batch_"
                + batchIdx.ToString(
                    "D4");
            string texFn = "texture_"
                + texId.ToString("D2")
                + ".bmp";
            using (StreamWriter sw =
                new StreamWriter(
                    mtlPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine("# " +
                    mtlName);
                sw.WriteLine();
                sw.WriteLine("newmtl "
                    + mtlName);
                sw.WriteLine("Ka 1 1 1");
                sw.WriteLine("Kd 1 1 1");
                sw.WriteLine("Ks 0 0 0");
                sw.WriteLine("Ns 10");
                sw.WriteLine("illum 2");
                sw.WriteLine(
                    "map_Kd " + texFn);
            }

            using (StreamWriter sw =
                new StreamWriter(
                    objPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine("# Batch "
                    + batchIdx +
                    " (tex " + texId
                    + ")");
                // Save bone offset
                // as comment for
                // rebuild reference
                sw.WriteLine(
                    "# bone_offset "
                    + bonePos[0]
                        .ToString("F6")
                    + " "
                    + bonePos[1]
                        .ToString("F6")
                    + " "
                    + bonePos[2]
                        .ToString("F6"));
                sw.WriteLine("mtllib "
                    + Path.GetFileName(
                        mtlPath));
                sw.WriteLine();
                foreach (float[] v
                    in allV)
                    sw.WriteLine("v "
                        + v[0].ToString(
                            "F6") + " "
                        + v[1].ToString(
                            "F6") + " "
                        + v[2].ToString(
                            "F6"));
                sw.WriteLine();
                foreach (float[] uv
                    in allU)
                    sw.WriteLine(
                        "vt " +
                        uv[0].ToString(
                            "F6") + " "
                        + (1f -
                           uv[1])
                        .ToString("F6"));
                sw.WriteLine();
                foreach (float[] n
                    in allN)
                    sw.WriteLine("vn "
                        + n[0].ToString(
                            "F6") + " "
                        + n[1].ToString(
                            "F6") + " "
                        + n[2].ToString(
                            "F6"));
                sw.WriteLine();
                sw.WriteLine("g " +
                    mtlName);
                sw.WriteLine(
                    "usemtl " +
                    mtlName);
                foreach (int[] f in
                    faces)
                {
                    int a = f[0] + 1;
                    int b = f[1] + 1;
                    int c = f[2] + 1;
                    sw.WriteLine("f "
                        + a + "/" + a
                        + "/" + a +
                        " " + b + "/"
                        + b + "/" + b
                        + " " + c +
                        "/" + c + "/"
                        + c);
                }
            }
            return true;
        }

        // ═════════════════════════════
        // READ CHUNK OFFSETS
        // ═════════════════════════════
        private static List<int>
            ReadChunkOffsets(
                byte[] data)
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
                if (v == 0)
                    continue;
                if (v == 0xFFFFFFFF)
                    continue;
                if (v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            offs.Sort();
            var unique = new List<int>();
            foreach (int o in offs)
            {
                if (unique.Count == 0 ||
                    unique[unique.Count
                        - 1] != o)
                    unique.Add(o);
            }
            return unique;
        }
    }
 }
