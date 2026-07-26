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
        public enum TargetRdtbFormat
        {
            Mirror,    // default for cbatches
            Big,
            Small,
            Match      // read from source
        }

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
        // MOUTH BATCH FLAG PATTERN
        // Learned from ORIG BOY batch 19
        // (bone 300, tex 4). The PS2
        // VU1 microcode reads these
        // per-vertex flag bits during
        // animation. Missing them makes
        // the mouth jump.
        //
        // Key = (block_idx, vertex_idx)
        // Value = (vertex_flag,
        //          normal_flag)
        // Blocks with size matching the
        // reference get their exact
        // flag pattern applied. Blocks
        // with different size get a
        // safe fallback (all zeros
        // except normal[0]=0).
        // ═════════════════════════════
        static readonly
            Dictionary<(int, int),
                (uint vf, uint nf)>
            MOUTH_FLAGS = new Dictionary<
                (int, int),
                (uint vf, uint nf)>
        {
            {(0,0), (0x00000000u, 0x00000001u)},
            {(0,1), (0x00000000u, 0x3F800000u)},
            {(0,2), (0x00000000u, 0x3F800000u)},
            {(0,3), (0x00000001u, 0x3F800000u)},
            {(0,4), (0x00000000u, 0x3F800000u)},
            {(0,5), (0x00000001u, 0x3F800000u)},
            {(0,6), (0x00000000u, 0x3F800000u)},
            {(0,7), (0x00000001u, 0x3F800000u)},
            {(0,8), (0x00000000u, 0x3F800000u)},
            {(0,9), (0x00000001u, 0x3F800000u)},
            {(0,10),(0x00000000u, 0x3F800000u)},
            {(0,11),(0x00000001u, 0x3F800000u)},
            {(1,0), (0x00000000u, 0x00000000u)},
            {(1,1), (0x00000000u, 0x3F800000u)},
            {(1,2), (0x00000000u, 0x3F800000u)},
            {(1,3), (0x00000000u, 0x3F800000u)},
            {(1,4), (0x00000000u, 0x3F800000u)},
            {(1,5), (0x00000000u, 0x3F800000u)},
            {(1,6), (0x00000000u, 0x3F800000u)},
            {(1,7), (0x00000000u, 0x3F800000u)},
            {(1,8), (0x00000000u, 0x3F800000u)},
            {(1,9), (0x00000000u, 0x3F800000u)},
            {(1,10),(0x00000000u, 0x3F800000u)},
            {(1,11),(0x00000000u, 0x3F800000u)},
            {(2,0), (0x00000000u, 0x00000000u)},
            {(2,1), (0x00000000u, 0x3F800000u)},
            {(2,2), (0x00000000u, 0x3F800000u)},
            {(2,3), (0x00000000u, 0x3F800000u)},
            {(2,4), (0x00000000u, 0x3F800000u)},
            {(2,5), (0x00000000u, 0x3F800000u)},
            {(2,6), (0x00000000u, 0x3F800000u)},
            {(2,7), (0x00000000u, 0x3F800000u)},
            {(2,8), (0x00000000u, 0x3F800000u)},
            {(2,9), (0x00000000u, 0x3F800000u)},
            {(2,10),(0x00000001u, 0x3F800000u)},
            {(3,0), (0x00000000u, 0x00000001u)},
            {(3,1), (0x00000001u, 0x3F800000u)},
            {(3,2), (0x00000000u, 0x3F800000u)},
            {(3,3), (0x00000001u, 0x3F800000u)},
            {(3,4), (0x00000000u, 0x3F800000u)},
            {(3,5), (0x00000001u, 0x3F800000u)},
            {(3,6), (0x00000000u, 0x3F800000u)},
            {(3,7), (0x00000001u, 0x3F800000u)},
            {(3,8), (0x00000000u, 0x3F800000u)},
            {(4,0), (0x00000000u, 0x00000000u)},
            {(4,1), (0x00000000u, 0x3F800000u)},
            {(4,2), (0x00000000u, 0x3F800000u)},
            {(4,3), (0x00000000u, 0x3F800000u)},
            {(4,4), (0x00000000u, 0x3F800000u)},
            {(4,5), (0x00000000u, 0x3F800000u)},
            {(4,6), (0x00000000u, 0x3F800000u)},
            {(4,7), (0x00000000u, 0x3F800000u)},
            {(4,8), (0x00000000u, 0x3F800000u)},
            {(5,0), (0x00000000u, 0x00000000u)},
            {(5,1), (0x00000000u, 0x3F800000u)},
            {(5,2), (0x00000000u, 0x3F800000u)},
            {(5,3), (0x00000000u, 0x3F800000u)},
            {(5,4), (0x00000000u, 0x3F800000u)},
            {(5,5), (0x00000000u, 0x3F800000u)},
            {(5,6), (0x00000000u, 0x3F800000u)},
            {(6,0), (0x00000000u, 0x00000000u)},
            {(6,1), (0x00000001u, 0x3F800000u)},
            {(6,2), (0x00000001u, 0x3F800000u)},
            {(6,3), (0x00000000u, 0x3F800000u)},
            {(6,4), (0x00000000u, 0x3F800000u)},
            {(6,5), (0x00000000u, 0x3F800000u)},
            {(6,6), (0x00000000u, 0x3F800000u)},
            {(7,0), (0x00000000u, 0x00000000u)},
            {(7,1), (0x00000000u, 0x3F800000u)},
            {(7,2), (0x00000000u, 0x3F800000u)},
            {(7,3), (0x00000000u, 0x3F800000u)},
            {(7,4), (0x00000000u, 0x3F800000u)},
            {(7,5), (0x00000000u, 0x3F800000u)},
            {(7,6), (0x00000001u, 0x3F800000u)},
            {(8,0), (0x00000000u, 0x00000001u)},
            {(8,1), (0x00000000u, 0x3F800000u)},
            {(8,2), (0x00000000u, 0x3F800000u)},
            {(8,3), (0x00000000u, 0x3F800000u)},
            {(8,4), (0x00000000u, 0x3F800000u)},
            {(8,5), (0x00000000u, 0x3F800000u)},
            {(8,6), (0x00000001u, 0x3F800000u)},
            {(9,0), (0x00000000u, 0x00000000u)},
            {(9,1), (0x00000000u, 0x3F800000u)},
            {(9,2), (0x00000000u, 0x3F800000u)},
            {(9,3), (0x00000000u, 0x3F800000u)},
            {(9,4), (0x00000000u, 0x3F800000u)},
            {(10,0),(0x00000000u, 0x00000000u)},
            {(10,1),(0x00000001u, 0x3F800000u)},
            {(10,2),(0x00000001u, 0x3F800000u)},
            {(11,0),(0x00000000u, 0x00000000u)},
            {(11,1),(0x00000000u, 0x3F800000u)},
            {(11,2),(0x00000000u, 0x3F800000u)},
        };

        // Expected block sizes that
        // trigger the mouth flag
        // pattern (matches BOY's
        // 12-block/92-vert mouth
        // shape)
        static readonly int[]
            MOUTH_BLOCK_SIZES =
            new int[] {
                12, 12, 11, 9, 9,
                7, 7, 7, 7, 5, 3, 3
            };

        static bool IsVerbose =>
            Environment.GetEnvironmentVariable(
                "HMSTH_VERBOSE") == "1";

        // ═════════════════════════════
        // MAIN ENTRY POINT
        // ═════════════════════════════
        public static void Build(
            string folderPath,
            string outDir,
            string normalsMode,
            float[] customNormal,
            bool deleteAll,
            TargetRdtbFormat targetFormat
                = TargetRdtbFormat.Mirror,
            Dictionary<int, int> normalsCopyMap
                = null)
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

            if (IsVerbose)
                Console.WriteLine(
                    "    Normals: " +
                    normalsMode);

            // ADDITIVE D: Show normals-copy
            // operations queued for this build
            if (normalsCopyMap != null
                && normalsCopyMap.Count > 0)
            {
                Console.WriteLine(
                    "    Normal copies: "
                    + normalsCopyMap.Count);
                foreach (var kv in
                    normalsCopyMap)
                {
                    Console.WriteLine(
                        "      batch " + kv.Key
                        + " <- batch "
                        + kv.Value);
                }
            }

            Directory.CreateDirectory(
                outDir);

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

            float autoScaleInvert = 1.0f;

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
                    if (t.StartsWith(
                        "Auto Scale:"))
                    {
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
                        {
                            autoScaleInvert =
                                1.0f / sc;
                            if (IsVerbose)
                                Console.WriteLine(
                                    "    [auto-scale"
                                    + " invert] x" +
                                    autoScaleInvert
                                        .ToString("F4"));
                        }
                    }
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

            // ── HOOK: EXPAND TABLES IF
            // NEW BATCHES EXIST IN FOLDER ──
            {
                var adderScan =
                    RDTBBatchAdder.ScanFolder(
                        folderPath, rdtbData);

                if (adderScan.NeedsRestructure)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "    [RESTRUCTURE]"
                        + " New batches: [" +
                        string.Join(", ",
                            adderScan
                                .NewBatchIndices)
                        + "]  Deleted: [" +
                        string.Join(", ",
                            adderScan
                                .DeletedBatchIndices)
                        + "]");
                    Console.ResetColor();

                    // Process expands the
                    // material table, mesh
                    // chunk pointer table,
                    // and lookup chunks to
                    // fit the new batch count
                    rdtbData =
                        RDTBBatchAdder.Process(
                            rdtbData, adderScan);

                    // Overwrite _source.rdtb
                    // so ALL downstream code
                    // reads the expanded layout
                    File.WriteAllBytes(
                        srcRdtb, rdtbData);

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    [OK] Tables"
                        + " expanded to " +
                        (adderScan.MaxBatchIndex
                         + 1) +
                        " batches");
                    Console.ResetColor();
                }
            }

            // NOW read batch count from
            // the (possibly expanded) data
            int totalBatches =
                GetBatchCount(rdtbData);

            // ── CHECK FOR NEW/REMOVED BATCHES ──
            // If the folder has batch indices
            // beyond the original RDTB count,
            // or original batches are missing,
            // pre-process the RDTB to expand/
            // shrink its tables BEFORE the
            // normal cbatches rebuild runs.
            {
                var adderScan =
                    RDTBBatchAdder.ScanFolder(
                        folderPath, rdtbData);

                if (adderScan.NeedsRestructure)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "    [RESTRUCTURE]"
                        + " Expanding tables"
                        + " for batch"
                        + " add/remove...");
                    Console.ResetColor();

                    rdtbData =
                        RDTBBatchAdder.Process(
                            rdtbData, adderScan);

                    // Overwrite _source.rdtb
                    // with expanded version
                    // so ALL downstream code
                    // (mesh chunk reading,
                    // material table parsing,
                    // lookup chunk updates)
                    // works on the correct
                    // expanded byte layout.
                    File.WriteAllBytes(
                        srcRdtb, rdtbData);

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    [OK] Tables"
                        + " expanded ("
                        + adderScan
                            .NewBatchIndices
                            .Count +
                        " new, " +
                        adderScan
                            .DeletedBatchIndices
                            .Count +
                        " deleted)");
                    Console.ResetColor();

                    Environment.SetEnvironmentVariable(
                        "HMSTH_VERBOSE", null);
                }
            }

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
                ReadChunkOffsets(rdtbData);

            // Read RAW slot values to
            // detect source format
            // regardless of offs count
            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
            {
                if (0x10 + i * 4 + 4 >
                    rdtbData.Length)
                    break;
                rawSlots[i] =
                    BitConverter.ToUInt32(
                        rdtbData,
                        0x10 + i * 4);
            }

            // Detect source format by
            // looking at raw slot 9
            bool sourceIsSmall =
                rawSlots[9] == 0xFFFFFFFF;
            bool sourceIsBig =
                !sourceIsSmall
                && rawSlots[9] != 0
                && rawSlots[9] != rawSlots[8];

            // Find mesh chunk: it's at
            // raw slot 11. Locate its
            // position in the offs list.
            uint meshRawOffset =
                rawSlots[11];
            int meshChunkIdx = -1;
            if (meshRawOffset != 0
                && meshRawOffset != 0xFFFFFFFF)
            {
                meshChunkIdx =
                    offs.IndexOf(
                        (int)meshRawOffset);
            }
            if (meshChunkIdx < 0)
            {
                // Fallback to last active
                // chunk in offs list
                meshChunkIdx =
                    offs.Count - 1;
            }

            Console.WriteLine(
                "    Source format: " +
                (sourceIsBig ? "BIG" :
                 sourceIsSmall ? "SMALL" :
                 "MIRROR"));
            Console.WriteLine(
                "    Mesh chunk idx: " +
                meshChunkIdx +
                " (raw offset 0x" +
                meshRawOffset.ToString("X") +
                ")");

            // Resolve target format.
            // If user did not explicitly
            // set a format flag, auto-
            // detect from source RDTB.
            // SMALL sources default to
            // SMALL output. BIG and
            // MIRROR sources default
            // to MIRROR output.
            TargetRdtbFormat outFmt =
                targetFormat;
            if (outFmt ==
                TargetRdtbFormat.Match)
            {
                // Match mode: SMALL stays
                // SMALL, everything else
                // becomes MIRROR
                outFmt = sourceIsSmall
                    ? TargetRdtbFormat.Small
                    : TargetRdtbFormat.Mirror;
            }
            else if (outFmt ==
                TargetRdtbFormat.Mirror
                && sourceIsSmall)
            {
                // Default mirror mode but
                // source is SMALL: auto-
                // switch to SMALL output
                outFmt =
                    TargetRdtbFormat.Small;
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "    [auto] Source"
                    + " is SMALL RDTB,"
                    + " output set to"
                    + " SMALL");
                Console.ResetColor();
            }
            Console.WriteLine(
                "    Output format: " +
                outFmt);

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

            // Always read original normals
            // regardless of mode.
            // This is needed for "match" mode
            // which is now the default.
            if (IsVerbose)
                Console.WriteLine(
                    "    Reading original"
                    + " normals for all"
                    + " batches...");
            foreach (int bi in batchObjs.Keys)
            {
                origNormals[bi] =
                    ReadBatchNormals(
                        meshChunk,
                        bi, nPtrs);
                if (IsVerbose)
                    Console.WriteLine(
                        "      batch " + bi
                        + ": "
                        + origNormals[bi].Count
                        + " samples");
            }

            var dupForcedBatches =
                new HashSet<int>();

            // ═══════════════════════
            // DUP-FORCE (safe ver)
            // For duplicate groups
            // (mouth, eyes, blink),
            // when an OBJ has a
            // DIFFERENT vertex count
            // from original, force
            // all group members to
            // use the first member's
            // normals. This prevents
            // the mouth-jump bug on
            // modded-vertex batches.
            // ONLY triggers when
            // vertex count changed.
            // Unchanged batches
            // (moved/resized) are
            // left alone.
            // ═══════════════════════
            {
                var offsF =
                    ReadChunkOffsets(
                        rdtbData);
                if (offsF.Count >= 9)
                {
                    int c8f = offsF[8];
                    uint firstF =
                        BitConverter
                            .ToUInt32(
                                rdtbData,
                                c8f);
                    int matCntF =
                        (int)(
                            firstF / 4);

                    // Group by bone+tex
                    var groups =
                        new Dictionary<
                            string,
                            List<int>>();
                    for (int bix = 0;
                         bix <
                         Math.Min(
                             matCntF,
                             nPtrs);
                         bix++)
                    {
                        uint ptr =
                            BitConverter
                                .ToUInt32(
                                    rdtbData,
                                    c8f +
                                    bix * 4);
                        int rec =
                            c8f +
                            (int)ptr;
                        if (rec + 8 >
                            rdtbData
                                .Length)
                            continue;
                        int bone =
                            BitConverter
                                .ToUInt16(
                                    rdtbData,
                                    rec);
                        int tex =
                            BitConverter
                                .ToUInt16(
                                    rdtbData,
                                    rec + 6);
                        string k =
                            bone + "|"
                            + tex;

                        if (!groups
                                .ContainsKey(
                                    k))
                            groups[k] =
                                new
                                List<int>();
                        groups[k]
                            .Add(bix);
                    }

                    foreach (var kv
                        in groups)
                    {
                        if (kv.Value
                                .Count
                            < 2)
                            continue;

                        // Check if ANY
                        // member has a
                        // DIFFERENT
                        // vertex count
                        // in its OBJ
                        // vs original
                        bool anyVcChanged
                            = false;
                        foreach (int
                            mem in
                            kv.Value)
                        {
                            if (!batchObjs
                                    .ContainsKey(
                                        mem))
                                continue;
                            int origVc =
                                origNormals
                                    .ContainsKey(
                                        mem)
                                ? origNormals[
                                    mem].Count
                                : 0;
                            int objVc =
                                CountObjVerts(
                                    batchObjs[
                                        mem]);
                            if (objVc !=
                                origVc &&
                                origVc > 0)
                            {
                                anyVcChanged
                                    = true;
                                break;
                            }
                        }

                        if (!anyVcChanged)
                            continue;

                        // Expand group to
                        // include ALL batches
                        // with same tex, even
                        // if they have
                        // different bones
                        // (like mouth batches
                        // 26 and 29 which have
                        // bone 334/432 instead
                        // of 300)
                        var expandedGroup =
                            new List<int>(
                                kv.Value);
                        foreach (var kv2
                            in groups)
                        {
                            if (kv2.Key ==
                                kv.Key)
                                continue;
                            // Check if same
                            // tex
                            string[] parts1 =
                                kv.Key.Split(
                                    '|');
                            string[] parts2 =
                                kv2.Key.Split(
                                    '|');
                            if (parts1.Length
                                < 2 ||
                                parts2.Length
                                < 2)
                                continue;
                            if (parts1[1] ==
                                parts2[1])
                            {
                                foreach (int
                                    extra in
                                    kv2.Value)
                                {
                                    if (!expandedGroup
                                            .Contains(
                                                extra))
                                        expandedGroup
                                            .Add(
                                                extra);
                                }
                            }
                        }

                        // Force all
                        // members to
                        // use first
                        // member's
                        // normals
                        int leader =
                            expandedGroup[0];

                        if (!origNormals
                                .ContainsKey(
                                    leader))
                            continue;
                        var leaderData
                            = origNormals[
                                leader];

                        Console
                            .ForegroundColor
                            = ConsoleColor
                                .Cyan;

                        Console.WriteLine(
                            "    [DUP-FORCE]"
                            + " group ["
                            + string.Join(
                                ",",
                                expandedGroup)

                            + "]: leader"
                            + " = batch "
                            + leader
                            + " (vc changed)");
                        Console
                            .ResetColor();

                        foreach (int
                            mem in
                            expandedGroup)
                        {
                            if (mem ==
                                leader)
                                continue;
                            origNormals[
                                mem] =
                                leaderData;
                            dupForcedBatches
                                .Add(mem);
                        }
                        dupForcedBatches
                            .Add(leader);
                    }
                }
            }

            // ADDITIVE: If normals-copy map is
            // provided, ensure we have original
            // normals loaded for every SOURCE
            // batch referenced in the map, even
            // when normalsMode != "match".
            if (normalsCopyMap != null
                && normalsCopyMap.Count > 0)
            {
                var srcBatches =
                    new HashSet<int>(
                        normalsCopyMap.Values);
                foreach (int srcBi in srcBatches)
                {
                    if (!origNormals.ContainsKey(
                            srcBi))
                    {
                        origNormals[srcBi] =
                            ReadBatchNormals(
                                meshChunk,
                                srcBi, nPtrs);
                    }
                }
                Console.WriteLine(
                    "    Loaded " +
                    srcBatches.Count +
                    " source batch normals"
                    + " for copy operations");
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
                var vertFlags = new List<uint>();
                ParseObj(objPath,
                    verts, normals,
                    uvs, tris,
                    vertFlags);

                // Invert auto-scale so
                // game gets exact original
                // coordinates back
                if (autoScaleInvert != 1.0f)
                {
                    for (int si = 0;
                         si < verts.Count;
                         si++)
                    {
                        verts[si] =
                            new float[]
                            {
                verts[si][0] *
                    autoScaleInvert,
                verts[si][1] *
                    autoScaleInvert,
                verts[si][2] *
                    autoScaleInvert,
                            };
                    }
                }

                // Apply normals
                // ─────────────────────────────
                // NORMALS PROCESSING
                // Priority:
                //   1. forcenew  → keep OBJ
                //   2. zero      → all zeros
                //   3. up        → all (0,1,0)
                //   4. custom    → user vector
                //   5. default   → nearest
                //                  neighbor
                //                  from original
                // ─────────────────────────────

                // forcenew only applies to
                // batches that were actually
                // modified by the user.
                // Unmodified batches always
                // use original RDTB normals
                // regardless of the flag.
                bool batchWasModified =
                    newBatchData.ContainsKey(bi)
                    || tris.Count !=
                        CountOrigBatchTris(
                            meshChunk, bi, nPtrs);

                if (normalsMode == "forcenew"
                    && batchWasModified)
                {
                    // User explicitly wants their
                    // Blender normals kept as-is
                    // for this specific batch.
                    Console.ForegroundColor =
                        ConsoleColor.Cyan;
                    Console.WriteLine(
                        "      [normals] batch "
                        + bi
                        + ": forcenew"
                        + " (OBJ normals kept,"
                        + " batch was modified)");
                    Console.ResetColor();
                }
                else if (normalsMode == "forcenew"
                    && !batchWasModified)
                {
                    // Batch is unchanged roundtrip.
                    // Even with forcenew active,
                    // restore original normals so
                    // unmodded body parts look
                    // correct in game.
                    if (origNormals.ContainsKey(bi)
                        && origNormals[bi].Count > 0)
                    {
                        var samples =
                            origNormals[bi];

                        for (int i = 0;
                             i < verts.Count;
                             i++)
                        {
                            float vx = verts[i][0];
                            float vy = verts[i][1];
                            float vz = verts[i][2];

                            float bestD =
                                float.MaxValue;
                            float[] bestN =
                                new float[]
                                { 0f, 1f, 0f };

                            foreach (var s in samples)
                            {
                                float dx =
                                    vx - s.pos[0];
                                float dy =
                                    vy - s.pos[1];
                                float dz =
                                    vz - s.pos[2];

                                float d =
                                    dx * dx
                                    + dy * dy
                                    + dz * dz;

                                if (d < bestD)
                                {
                                    bestD = d;
                                    bestN = s.norm;
                                }
                            }

                            if (i < normals.Count)
                                normals[i] = bestN;
                            else
                                normals.Add(bestN);
                        }

                        Console.ForegroundColor =
                            ConsoleColor.DarkGray;
                        Console.WriteLine(
                            "      [normals] batch "
                            + bi
                            + ": roundtrip,"
                            + " original normals"
                            + " restored");
                        Console.ResetColor();
                    }
                }

                else if (normalsMode == "zero")
                {
                    for (int i = 0;
                         i < normals.Count;
                         i++)
                        normals[i] =
                            new float[]
                            { 0f, 0f, 0f };
                }
                else if (normalsMode == "up")
                {
                    for (int i = 0;
                         i < normals.Count;
                         i++)
                        normals[i] =
                            new float[]
                            { 0f, 1f, 0f };
                }
                else if (normalsMode == "custom"
                    && customNormal != null)
                {
                    for (int i = 0;
                         i < normals.Count;
                         i++)
                        normals[i] =
                            new float[]
                            {
                                customNormal[0],
                                customNormal[1],
                                customNormal[2]
                            };
                }
                else
                {
                    // DEFAULT: Nearest-neighbor
                    // transfer from original
                    // RDTB batch normals.
                    if (origNormals
                            .ContainsKey(bi)
                        && origNormals[bi]
                            .Count > 0)
                    {
                        var samples =
                            origNormals[bi];

                        for (int i = 0;
                             i < verts.Count;
                             i++)
                        {
                            float vx =
                                verts[i][0];
                            float vy =
                                verts[i][1];
                            float vz =
                                verts[i][2];

                            float bestD =
                                float.MaxValue;
                            float[] bestN =
                                new float[]
                                { 0f, 1f, 0f };

                            foreach (var s
                                in samples)
                            {
                                float dx =
                                    vx - s.pos[0];
                                float dy =
                                    vy - s.pos[1];
                                float dz =
                                    vz - s.pos[2];

                                float d =
                                    dx * dx
                                    + dy * dy
                                    + dz * dz;

                                if (d < bestD)
                                {
                                    bestD = d;
                                    bestN =
                                        s.norm;
                                }
                            }

                            if (i <
                                normals.Count)
                                normals[i] =
                                    bestN;
                            else
                                normals.Add(
                                    bestN);
                        }

                        Console.ForegroundColor =
                            ConsoleColor.Green;
                        Console.WriteLine(
                            "      [normals]"
                            + " batch " + bi
                            + ": matched "
                            + verts.Count
                            + " verts from "
                            + samples.Count
                            + " original"
                            + " samples");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "      [normals]"
                            + " batch " + bi
                            + ": no original"
                            + " samples,"
                            + " keeping"
                            + " OBJ normals");
                        Console.ResetColor();
                    }
                }

                // ─────────────────────────────
                // NORMALS-COPY MAP OVERRIDE
                // Only runs if forcenew is NOT
                // active. Prevents the copy map
                // from overwriting OBJ normals
                // when forcenew is requested.
                // ─────────────────────────────
                if (normalsMode != "forcenew"
                    && normalsCopyMap != null
                    && normalsCopyMap
                        .TryGetValue(
                            bi,
                            out int srcBatchIdx)
                    && origNormals
                        .ContainsKey(
                            srcBatchIdx))
                {
                    var srcSamples =
                        origNormals[srcBatchIdx];

                    if (srcSamples.Count > 0)
                    {
                        Console.WriteLine(
                            "      [copy]"
                            + " batch " + bi
                            + " normals <-"
                            + " batch "
                            + srcBatchIdx
                            + " ("
                            + srcSamples.Count
                            + " src samples)");

                        for (int i = 0;
                             i < verts.Count;
                             i++)
                        {
                            float vx =
                                verts[i][0];
                            float vy =
                                verts[i][1];
                            float vz =
                                verts[i][2];

                            float bestD =
                                float.MaxValue;
                            float[] bestN =
                                new float[]
                                { 0f, 1f, 0f };

                            foreach (var s
                                in srcSamples)
                            {
                                float dx =
                                    vx - s.pos[0];
                                float dy =
                                    vy - s.pos[1];
                                float dz =
                                    vz - s.pos[2];
                                float d =
                                    dx * dx
                                    + dy * dy
                                    + dz * dz;

                                if (d < bestD)
                                {
                                    bestD = d;
                                    bestN =
                                        s.norm;
                                }
                            }

                            if (i <
                                normals.Count)
                                normals[i] =
                                    bestN;
                        }
                    }
                    else
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "      [!] batch "
                            + bi
                            + " normals-copy"
                            + " src batch "
                            + srcBatchIdx
                            + " has no normals"
                            + " - skipping");
                        Console.ResetColor();
                    }
                }

                // Check if modified
                int origTriCount =
                    CountOrigBatchTris(
                        meshChunk, bi,
                        nPtrs);

                if (tris.Count ==
                    origTriCount &&
                    !dupForcedBatches
                        .Contains(bi))
                {
                    // Check if any vertex
                    // actually moved vs
                    // original. If yes,
                    // recompile instead
                    // of keeping original
                    // bytes.
                    bool anyMoved = false;
                    if (origNormals
                            .ContainsKey(bi))
                    {
                        var samples =
                            origNormals[bi];
                        const float EPS =
                            0.01f;
                        for (int vi2 = 0;
                             vi2 <
                             Math.Min(
                                 verts.Count,
                                 samples
                                     .Count);
                             vi2++)
                        {
                            float dx =
                                verts[vi2][0]
                                - samples[vi2]
                                    .pos[0];
                            float dy =
                                verts[vi2][1]
                                - samples[vi2]
                                    .pos[1];
                            float dz =
                                verts[vi2][2]
                                - samples[vi2]
                                    .pos[2];
                            if (Math.Abs(dx)
                                    > EPS ||
                                Math.Abs(dy)
                                    > EPS ||
                                Math.Abs(dz)
                                    > EPS)
                            {
                                anyMoved =
                                    true;
                                break;
                            }
                        }
                    }

                    if (anyMoved)
                    {
                        byte[] vifData =
                            CompilePureTri(
                                verts,
                                normals,
                                uvs, tris,
                                vertFlags,
                                false);
                        newBatchData[bi] =
                            vifData;
                        newCount++;
                    }
                    else
                    {
                        keptCount++;
                    }
                }
                else
                {
                    byte[] vifData =
                        CompilePureTri(
                            verts,
                            normals,
                            uvs, tris,
                            vertFlags,
                            dupForcedBatches
                                .Contains(bi));
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

            byte[] finalRdtb;
            switch (outFmt)
            {
                case TargetRdtbFormat.Big:
                    Console.WriteLine(
                        "    Building BIG"
                        + " RDTB (3 LOD"
                        + " chunks)...");
                    finalRdtb =
                        ApplyBigLayout(
                            newRdtb);
                    break;
                case TargetRdtbFormat.Small:
                    Console.WriteLine(
                        "    Building SMALL"
                        + " RDTB (single"
                        + " mesh chunk)...");
                    finalRdtb =
                        ApplySmallLayout(
                            newRdtb);
                    break;
                case TargetRdtbFormat.Mirror:
                default:
                    Console.WriteLine(
                        "    Applying mirror"
                        + " layout...");
                    finalRdtb =
                        ApplySlotMirror(
                            newRdtb);
                    break;
            }

            File.WriteAllBytes(
                outRdtb, finalRdtb);

            Console.WriteLine(
                "    RDTB: " +
                finalRdtb.Length
                    .ToString("N0") +
                " B");

            // ═══════════════════════════════
            // IN-PLACE SCALE/MOVE
            // Only active when NO auto-scale
            // was applied during extraction.
            // When autoScaleInvert != 1.0 the
            // scale was already corrected by
            // the autoScaleInvert path above.
            // Calling the scaler on top of
            // that would re-apply the display-
            // scale coordinates and shrink
            // every batch back to Blender
            // display size in game.
            // ═══════════════════════════════
            if (autoScaleInvert == 1.0f)
            {
                RDTBInPlaceScaler.Apply(
                    folderPath, outRdtb);
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.DarkGray;
                Console.WriteLine(
                    "    [scale] Skipping"
                    + " in-place scaler"
                    + " (auto-scale active,"
                    + " coords already"
                    + " corrected by"
                    + " autoScaleInvert x"
                    + autoScaleInvert
                        .ToString("F4")
                    + ")");
                Console.ResetColor();
            }


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

        // ═════════════════════════════
        // COUNT OBJ VERTS
        // Quick count of vertex lines
        // in an OBJ file.
        // ═════════════════════════════
        static int CountObjVerts(
            string path)
        {
            int count = 0;
            foreach (string line in
                File.ReadAllLines(path))
            {
                string t =
                    line.Trim();
                if (t.Length >= 2 &&
                    t[0] == 'v' &&
                    t[1] == ' ')
                    count++;
            }
            return count;
        }

        // ═════════════════════════════
        // COUNT OBJ TRIS
        // Quick count of face lines
        // in an OBJ file.
        // ═════════════════════════════
        static int CountObjTris(
            string path)
        {
            int count = 0;
            foreach (string line in
                File.ReadAllLines(path))
            {
                string t =
                    line.Trim();
                if (t.Length >= 2 &&
                    t[0] == 'f' &&
                    t[1] == ' ')
                    count++;
            }
            return count;
        }

        // ═════════════════════════════
        // COUNT ORIG BATCH TRIS (FIXED)
        // Counts only NON-DEGENERATE
        // triangles in the original
        // strip — matching the OBJ
        // extractor's behavior. This
        // ensures roundtrips report
        // equal tri counts and skip
        // the CompilePureTri path.
        // ═════════════════════════════
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
            // Find next NON-NULL pointer
            // (skip null sentinel slots
            // at end of pointer table).
            // If no real next pointer
            // exists, use chunk end.
            uint np = (uint)chunk.Length;
            for (int nj = batchIdx + 1;
                 nj < nPtrs; nj++)
            {
                uint candidate =
                    BitConverter.ToUInt32(
                        chunk, nj * 4);
                if (candidate != 0 &&
                    candidate > bp)
                {
                    np = candidate;
                    break;
                }
            }

            int count = 0;
            int pos = (int)bp;
            int end = (int)np;

            while (pos + 16 <= end)
            {
                if (chunk[pos] != VIF_B0
                    || chunk[pos + 1]
                        != VIF_B1
                    || chunk[pos + 3]
                        != VIF_B3)
                {
                    pos += 4;
                    continue;
                }
                int vc = chunk[pos + 4];

                // Read vertex positions
                // for this block so we
                // can filter degenerates
                int vStart = pos + 16;
                var verts =
                    new List<float[]>();
                for (int i = 0;
                     i < vc; i++)
                {
                    int vo = vStart
                        + i * 16;
                    if (vo + 16 > end)
                        break;
                    verts.Add(
                        new float[]
                        {
                    BitConverter
                        .ToSingle(
                            chunk,
                            vo + 4),
                    BitConverter
                        .ToSingle(
                            chunk,
                            vo + 8),
                    BitConverter
                        .ToSingle(
                            chunk,
                            vo + 12)
                        });
                }

                // Walk the strip and
                // count only non-
                // degenerate triangles
                for (int i = 0;
                     i < verts.Count - 2;
                     i++)
                {
                    int a, b, c;
                    a = i;
                    if (i % 2 == 0)
                    {
                        b = i + 1;
                        c = i + 2;
                    }
                    else
                    {
                        b = i + 2;
                        c = i + 1;
                    }
                    float[] v0 = verts[a];
                    float[] v1 = verts[b];
                    float[] v2 = verts[c];
                    float ax = v1[0] - v0[0];
                    float ay = v1[1] - v0[1];
                    float az = v1[2] - v0[2];
                    float bx = v2[0] - v0[0];
                    float by = v2[1] - v0[1];
                    float bz = v2[2] - v0[2];
                    float cx =
                        ay * bz - az * by;
                    float cy =
                        az * bx - ax * bz;
                    float cz =
                        ax * by - ay * bx;
                    if (cx * cx + cy * cy
                        + cz * cz > 1e-10f)
                        count++;
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
                    if (eof == EOF_FLAG)
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
            List<int[]> tris,
            List<uint> vertFlags)
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

                // NEW: capture #vw flag comment
                if (t.StartsWith("#vw "))
                {
                    string hex = t.Substring(4).Trim();
                    uint flag;
                    if (uint.TryParse(hex,
                        System.Globalization
                            .NumberStyles.HexNumber,
                        System.Globalization
                            .CultureInfo
                            .InvariantCulture,
                        out flag))
                        vertFlags.Add(flag);
                    continue;
                }

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

            // Pad vertFlags to match verts count
            while (vertFlags.Count < verts.Count)
                vertFlags.Add(0);
        }

        // ═════════════════════════════
        // COMPILE STRIP VIF
        // Builds triangle strips like
        // the original PS2 format.
        // Each block holds many verts
        // (3 + strip extensions) so
        // the VU1 microcode can DMA
        // the whole batch in just a
        // few blocks instead of one
        // per triangle.
        // ═════════════════════════════
        static byte[] CompilePureTri(
            List<float[]> verts,
            List<float[]> normals,
            List<float[]> uvs,
            List<int[]> tris,
            List<uint> vertFlags,
            bool skipMouthFlags
                = false)
        {
            // Build vertex strips from
            // the triangle list
            List<List<int>> strips =
                BuildStrips(tris);

            // ═════════════════════
            // MOUTH-PATTERN DETECT
            // If block-size pattern
            // matches the mouth's
            // known shape, we apply
            // the hardcoded flag
            // lookup below. This
            // preserves the vertex
            // and normal flag bits
            // that the VU1 microcode
            // reads during animation.
            // ═════════════════════
            bool useMouthFlags = false;

            // Split strips into blocks
            // not larger than MAX_VC
            const int MAX_VC = 29;
            const int MIN_VC = 3;
            List<List<int>> blocks =
                new List<List<int>>();
            foreach (var strip in strips)
            {
                if (strip.Count < MIN_VC)
                    continue;
                int i = 0;
                while (i < strip.Count)
                {
                    int end =
                        Math.Min(
                            i + MAX_VC,
                            strip.Count);
                    int len = end - i;
                    if (len >= MIN_VC)
                    {
                        var chunk =
                            strip.GetRange(
                                i, len);
                        blocks.Add(chunk);
                    }
                    if (end < strip.Count)
                        i = end - 2;
                    else
                        i = end;
                }
            }

            if (blocks.Count == 0)
                return new byte[0];

            // Check if block sizes match
            // the known mouth pattern
            if (blocks.Count ==
                MOUTH_BLOCK_SIZES.Length)
            {
                bool match = true;
                for (int k = 0;
                     k < blocks.Count;
                     k++)
                {
                    if (blocks[k].Count !=
                        MOUTH_BLOCK_SIZES[k])
                    {
                        match = false;
                        break;
                    }
                }
                if (match &&
                    !skipMouthFlags)
                {
                    useMouthFlags = true;
                }
            }

            // Emit VIF blocks
            using (var ms =
                new MemoryStream())
            {
                int nBlocks = blocks.Count;
                for (int bi = 0;
                     bi < nBlocks; bi++)
                {
                    List<int> blockVerts =
                        blocks[bi];
                    bool isFirst =
                        (bi == 0);
                    bool isLast =
                        (bi == nBlocks - 1);
                    int vc =
                        blockVerts.Count;

                    // VIF header
                    byte[] hdr =
                        new byte[16];
                    hdr[0] = VIF_B0;
                    hdr[1] = VIF_B1;
                    hdr[2] = (byte)(
                        (3 * vc + 1)
                        & 0xFF);
                    hdr[3] = VIF_B3;
                    hdr[4] = (byte)
                        (vc & 0xFF);
                    hdr[5] = 0x80;
                    Array.Copy(HDR_TAIL,
                        0, hdr, 8, 8);
                    ms.Write(hdr, 0, 16);

                    // Vertex rows
                    for (int j = 0;
                         j < vc; j++)
                    {
                        int vi =
                            blockVerts[j];
                        float[] v =
                            (vi < verts
                                .Count)
                            ? verts[vi]
                            : new float[]
                              { 0, 0, 0 };

                        uint vflag;
                        if (useMouthFlags &&
                            MOUTH_FLAGS
                                .TryGetValue(
                                    (bi, j),
                                    out var mf))
                        {
                            vflag = mf.vf;
                        }
                        else if (vi <
                            vertFlags.Count)
                        {
                            vflag =
                                vertFlags[vi];
                        }
                        else
                        {
                            vflag = F_ZERO;
                        }

                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    vflag),
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

                    // Normal rows
                    for (int j = 0;
                         j < vc; j++)
                    {
                        int vi =
                            blockVerts[j];
                        float[] nn =
                            (vi < normals
                                .Count)
                            ? normals[vi]
                            : new float[]
                              { 0, 1, 0 };

                        uint nflag;
                        if (useMouthFlags &&
                            MOUTH_FLAGS
                                .TryGetValue(
                                    (bi, j),
                                    out var mfN))
                        {
                            nflag = mfN.nf;
                        }
                        else
                        {
                            nflag = (j == 0)
                                ? F_ZERO
                                : F_ONE;
                        }

                        ms.Write(
                            BitConverter
                                .GetBytes(
                                    nflag),
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

                    // UV rows
                    for (int j = 0;
                         j < vc; j++)
                    {
                        int vi =
                            blockVerts[j];
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
        // BUILD STRIPS
        // Greedy triangle strip builder.
        // Walks triangles and chains
        // them via shared edges to
        // build long vertex strips.
        // ═════════════════════════════
        static List<List<int>> BuildStrips(
            List<int[]> tris)
        {
            var result =
                new List<List<int>>();
            if (tris == null
                || tris.Count == 0)
                return result;

            int n = tris.Count;

            // Build edge -> tri list
            var edgeMap =
                new Dictionary<
                    long,
                    List<(int triIdx,
                          int oppVert)>>();
            for (int ti = 0;
                 ti < n; ti++)
            {
                int a = tris[ti][0];
                int b = tris[ti][1];
                int c = tris[ti][2];
                AddEdge(edgeMap,
                    a, b, ti, c);
                AddEdge(edgeMap,
                    b, c, ti, a);
                AddEdge(edgeMap,
                    c, a, ti, b);
            }

            bool[] used =
                new bool[n];

            for (int seed = 0;
                 seed < n; seed++)
            {
                if (used[seed])
                    continue;
                used[seed] = true;
                int a = tris[seed][0];
                int b = tris[seed][1];
                int c = tris[seed][2];

                var strip =
                    new List<int>
                    { a, b, c };

                // Extend strip forward
                int last2 = b;
                int last1 = c;
                bool flip = false;
                while (true)
                {
                    int next =
                        FindNextStripVert(
                            edgeMap, used,
                            tris,
                            last2, last1,
                            flip);
                    if (next < 0)
                        break;
                    strip.Add(next);
                    last2 = last1;
                    last1 = next;
                    flip = !flip;
                }

                result.Add(strip);
            }
            return result;
        }

        static void AddEdge(
            Dictionary<long,
                List<(int, int)>>
                map,
            int v0, int v1,
            int triIdx,
            int oppVert)
        {
            long key =
                EdgeKey(v0, v1);
            if (!map.ContainsKey(key))
                map[key] =
                    new List<(int, int)>();
            map[key].Add(
                (triIdx, oppVert));
        }

        static long EdgeKey(
            int a, int b)
        {
            int lo =
                Math.Min(a, b);
            int hi =
                Math.Max(a, b);
            return ((long)lo << 32)
                | (uint)hi;
        }

        static int FindNextStripVert(
            Dictionary<long,
                List<(int triIdx,
                      int oppVert)>>
                edgeMap,
            bool[] used,
            List<int[]> tris,
            int v0, int v1,
            bool flip)
        {
            long key =
                EdgeKey(v0, v1);
            if (!edgeMap.ContainsKey(
                    key))
                return -1;
            foreach (var entry in
                edgeMap[key])
            {
                if (used[entry.triIdx])
                    continue;
                // Verify orientation
                int[] t =
                    tris[entry.triIdx];
                // Find which edge this
                // is in t and check
                // winding matches
                bool winding =
                    CheckWinding(
                        t, v0, v1,
                        flip);
                if (!winding)
                    continue;
                used[entry.triIdx] =
                    true;
                return entry.oppVert;
            }
            return -1;
        }

        static bool CheckWinding(
            int[] tri,
            int v0, int v1,
            bool flip)
        {
            // Strip winding alternates.
            // For even position the
            // shared edge should be in
            // (v0, v1) order in the tri;
            // for odd in (v1, v0) order.
            int a = tri[0];
            int b = tri[1];
            int c = tri[2];
            bool fwd =
                (a == v0 && b == v1) ||
                (b == v0 && c == v1) ||
                (c == v0 && a == v1);
            bool rev =
                (a == v1 && b == v0) ||
                (b == v1 && c == v0) ||
                (c == v1 && a == v0);
            if (flip)
                return rev || fwd;
            return fwd || rev;
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

            // If the batch pointer is
            // null, this slot is empty.
            if (bPtr == 0)
                return result;

            // Find the next VALID pointer
            // that is greater than bPtr.
            // Skip null pointers and
            // pointers equal to bPtr.
            uint nPtr = (uint)chunk.Length;
            for (int j = batchIdx + 1;
                 j < nPtrs; j++)
            {
                uint candidate =
                    BitConverter.ToUInt32(
                        chunk, j * 4);
                if (candidate > bPtr &&
                    candidate <
                        (uint)chunk.Length)
                {
                    nPtr = candidate;
                    break;
                }
            }

            // Safety: if nPtr is invalid
            // or same as bPtr, use full
            // chunk end.
            if (nPtr <= bPtr ||
                nPtr > (uint)chunk.Length)
                nPtr = (uint)chunk.Length;

            int pos = (int)bPtr;
            int end = (int)nPtr;

            while (pos + 16 <= end)
            {
                if (chunk[pos] != VIF_B0 ||
                    chunk[pos + 1] != VIF_B1
                    || chunk[pos + 3]
                        != VIF_B3)
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
                int nStart = vStart + vc * 16;

                // Safety check
                if (nStart + vc * 16 > end)
                {
                    pos += 4;
                    continue;
                }

                for (int i = 0; i < vc; i++)
                {
                    int vOff = vStart + i * 16;
                    int nOff = nStart + i * 16;
                    if (nOff + 16 > end) break;

                    float[] vp = new float[]
                    {
                BitConverter.ToSingle(
                    chunk, vOff + 4),
                BitConverter.ToSingle(
                    chunk, vOff + 8),
                BitConverter.ToSingle(
                    chunk, vOff + 12)
                    };
                    float[] np = new float[]
                    {
                BitConverter.ToSingle(
                    chunk, nOff + 4),
                BitConverter.ToSingle(
                    chunk, nOff + 8),
                BitConverter.ToSingle(
                    chunk, nOff + 12)
                    };
                    result.Add((vp, np));
                }

                int bSize = 16 + 3 * vc * 16 + 16;
                if (pos + bSize + 16 <= end)
                {
                    uint eof =
                        BitConverter.ToUInt32(
                            chunk, pos + bSize);
                    if (eof == EOF_FLAG)
                        bSize += 16;
                }
                pos += bSize;
            }

            return result;
        }

        // ═════════════════════════════
        // REBUILD MESH CHUNK (FIXED)
        // Preserves NULL pointer slots
        // at end of pointer table.
        // Original RDTBs have N material
        // batches but pad the pointer
        // table with null entries
        // (0x00000000) for alignment.
        // These must remain null in the
        // rebuild.
        // ═════════════════════════════
        static byte[] RebuildMeshChunk(
            byte[] origChunk,
            int nPtrs,
            Dictionary<int, byte[]>
                newBatches,
            List<int> hiddenBatches,
            byte[] hiddenVif)
        {
            // Read original pointer
            // table to identify null
            // slots
            uint[] origPtrs =
                new uint[nPtrs];
            for (int i = 0; i < nPtrs;
                 i++)
            {
                origPtrs[i] =
                    BitConverter
                        .ToUInt32(
                            origChunk,
                            i * 4);
            }

            // Classify each slot:
            // NULL = pointer is 0
            //        (preserve as null)
            // REAL = pointer is valid
            bool[] isNull =
                new bool[nPtrs];
            for (int i = 0; i < nPtrs;
                 i++)
            {
                if (origPtrs[i] == 0)
                    isNull[i] = true;
            }

            // For computing batch sizes,
            // we walk through real
            // pointers in order. Each
            // real batch ends at the
            // NEXT real pointer (or end
            // of chunk if it's the last
            // real one).
            var batchData =
                new List<byte[]>();
            for (int i = 0; i < nPtrs;
                 i++)
            {
                if (isNull[i])
                {
                    batchData.Add(
                        new byte[0]);
                    continue;
                }

                uint bp = origPtrs[i];

                // Find next non-null
                // pointer to determine
                // this batch's end
                uint np = (uint)
                    origChunk.Length;
                for (int j = i + 1;
                     j < nPtrs; j++)
                {
                    if (!isNull[j])
                    {
                        np = origPtrs[j];
                        break;
                    }
                }

                if (bp >= np ||
                    bp >= (uint)
                        origChunk.Length
                    ||
                    np > (uint)
                        origChunk.Length)
                {
                    // Treat as null if
                    // computed span is
                    // invalid
                    batchData.Add(
                        new byte[0]);
                    isNull[i] = true;
                    continue;
                }

                byte[] bd =
                    new byte[np - bp];
                Array.Copy(origChunk,
                    (int)bp, bd, 0,
                    bd.Length);
                batchData.Add(bd);
            }

            // Replace modified batches
            // If the batch has new data,
            // write it even if the original
            // pointer was NULL (this handles
            // newly added batches where
            // the pointer table was expanded
            // but the pointer slot was zero)
            foreach (var kv in
                newBatches)
            {
                if (kv.Key < nPtrs)
                {
                    batchData[kv.Key] =
                        kv.Value;
                    // Mark as non-null so
                    // it gets a real pointer
                    // in the rebuilt table
                    isNull[kv.Key] = false;
                }
            }

            // Hide deleted batches
            // (also skip null slots)
            foreach (int hb in
                hiddenBatches)
            {
                if (hb < nPtrs &&
                    !isNull[hb])
                {
                    batchData[hb] =
                        hiddenVif;
                }
            }

            // Rebuild with new pointer
            // table. NULL slots stay
            // as 0x00000000.
            int tableSize = nPtrs * 4;
            using (var ms =
                new MemoryStream())
            {
                // Compute new offsets
                // (only for real slots)
                var newOffsets =
                    new uint[nPtrs];
                int cursor = tableSize;

                for (int i = 0; i < nPtrs;
                     i++)
                {
                    if (isNull[i])
                    {
                        newOffsets[i] = 0;
                    }
                    else
                    {
                        newOffsets[i] =
                            (uint)cursor;
                        cursor +=
                            batchData[i]
                                .Length;
                    }
                }

                // Write pointer table
                for (int i = 0; i < nPtrs;
                     i++)
                {
                    ms.Write(
                        BitConverter
                            .GetBytes(
                                newOffsets[i]),
                        0, 4);
                }

                // Write batch data
                // (null slots write
                // nothing)
                for (int i = 0; i < nPtrs;
                     i++)
                {
                    if (!isNull[i] &&
                        batchData[i]
                            .Length > 0)
                    {
                        ms.Write(
                            batchData[i],
                            0,
                            batchData[i]
                                .Length);
                    }
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
            // Read original raw slots so
            // we know which were sentinels
            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
            {
                rawSlots[i] =
                    BitConverter.ToUInt32(
                        origData,
                        0x10 + i * 4);
            }

            // Read all unique chunks in
            // order from offs list
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

            // Map original raw slot ->
            // which chunk index it
            // pointed at (for sentinels
            // map to -1)
            int[] slotToChunkIdx =
                new int[14];
            for (int i = 0; i < 14; i++)
            {
                if (rawSlots[i] == 0
                    || rawSlots[i] ==
                        0xFFFFFFFF)
                {
                    slotToChunkIdx[i] = -1;
                }
                else
                {
                    slotToChunkIdx[i] =
                        offs.IndexOf(
                            (int)rawSlots[i]);
                }
            }

            // Update lookup chunks if
            // they exist (skip for SMALL
            // sources where slots 9/10
            // are sentinels)
            int[] lookupRawSlots =
                { 8, 9, 10 };
            foreach (int rs in
                lookupRawSlots)
            {
                if (rawSlots[rs] == 0
                    || rawSlots[rs] ==
                        0xFFFFFFFF)
                    continue;
                int ci =
                    slotToChunkIdx[rs];
                if (ci < 0 ||
                    ci >= chunks.Count)
                    continue;

                byte[] lookupChunk =
                    chunks[ci];
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

                    uint batchPtr =
                        BitConverter
                            .ToUInt32(
                                newMeshChunk,
                                bi * 4);
                    if (batchPtr == 0)
                        continue;

                    uint nextPtr =
                        (uint)newMeshChunk
                            .Length;
                    for (int nj = bi + 1;
                         nj < mPtrs;
                         nj++)
                    {
                        uint np =
                            BitConverter
                                .ToUInt32(
                                    newMeshChunk,
                                    nj * 4);
                        if (np != 0)
                        {
                            nextPtr = np;
                            break;
                        }
                    }

                    int span =
                        (int)(nextPtr -
                               batchPtr);
                    if (span <= 0)
                        continue;

                    int qw =
                        (span / 16) - 1;
                    if (qw < 0)
                        continue;

                    byte[] qwBytes =
                        BitConverter
                            .GetBytes(
                                (uint)qw);
                    Array.Copy(
                        qwBytes, 0,
                        lookupChunk,
                        recOff, 4);
                }
                chunks[ci] =
                    lookupChunk;
            }


            // Compute new positions for
            // each unique chunk in file
            int[] newChunkOffs =
                new int[chunks.Count];
            int cursor = 0x48;
            for (int i = 0;
                 i < chunks.Count; i++)
            {
                newChunkOffs[i] = cursor;
                cursor +=
                    chunks[i].Length;
            }

            // Build header preserving
            // sentinels for slots that
            // were sentinels in source
            byte[] header =
                new byte[0x48];
            Array.Copy(origData, 0,
                header, 0, 0x48);

            for (int i = 0; i < 14; i++)
            {
                int pos = 0x10 + i * 4;
                uint newVal;
                if (rawSlots[i] == 0)
                {
                    newVal = 0;
                }
                else if (rawSlots[i] ==
                    0xFFFFFFFF)
                {
                    newVal = 0xFFFFFFFF;
                }
                else
                {
                    int ci =
                        slotToChunkIdx[i];
                    if (ci < 0 ||
                        ci >= newChunkOffs
                            .Length)
                        newVal = 0xFFFFFFFF;
                    else
                        newVal = (uint)
                            newChunkOffs[ci];
                }
                byte[] ob =
                    BitConverter.GetBytes(
                        newVal);
                Array.Copy(ob, 0,
                    header, pos, 4);
            }

            // Assemble final file
            byte[] result =
                new byte[cursor];
            Array.Copy(header, 0,
                result, 0, 0x48);
            for (int i = 0;
                 i < chunks.Count; i++)
            {
                Array.Copy(chunks[i], 0,
                    result,
                    newChunkOffs[i],
                    chunks[i].Length);
            }

            return result;
        }

        // ═════════════════════════════
        // APPLY SLOT MIRROR (FIXED)
        // Handles BIG, SMALL, and MIRROR
        // sources correctly. Reads mesh
        // from the right raw slot for
        // each format.
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
            uint c11 = rawSlots[11];

            // Bail if essential slots
            // are sentinels
            if (c8 == 0xFFFFFFFF
                || c8 == 0
                || c11 == 0xFFFFFFFF
                || c11 == 0)
            {
                return data;
            }

            // Compute chunk 8 end:
            // next valid offset after
            // c8 in the slot table
            uint c8End = (uint)data.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c8
                    && v < c8End
                    && v != 0xFFFFFFFF
                    && v != 0)
                    c8End = v;
            }

            // Compute chunk 11 end:
            // next valid offset after
            // c11, OR file end
            uint c11End = (uint)data.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c11
                    && v < c11End
                    && v != 0xFFFFFFFF
                    && v != 0)
                    c11End = v;
            }

            // Extract chunks
            byte[] chunks07 =
                new byte[c8 - c0];
            Array.Copy(data,
                (int)c0, chunks07,
                0, chunks07.Length);

            byte[] chunk8 = new byte[
                c8End - c8];
            Array.Copy(data,
                (int)c8, chunk8,
                0, chunk8.Length);

            byte[] chunk11 = new byte[
                c11End - c11];
            Array.Copy(data,
                (int)c11, chunk11,
                0, chunk11.Length);

            // Build new file
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

                Array.Copy(data, 0,
                    result, 0, HDR);

                // Patch slots 0-7
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

                // Slots 8, 9, 10 ->
                // new chunk 8 offset
                byte[] c8b =
                    BitConverter
                        .GetBytes(newC8);
                for (int i = 8;
                     i <= 10; i++)
                    Array.Copy(c8b, 0,
                        result,
                        0x10 + i * 4,
                        4);

                // Slots 11, 12, 13 ->
                // new chunk 11 offset
                byte[] c11b =
                    BitConverter
                        .GetBytes(newC11);
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
        static List<int> ReadChunkOffsets(
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
                // Skip null and sentinel
                // slots, but DO NOT BREAK
                // — keep scanning the rest
                if (v == 0)
                    continue;
                if (v == 0xFFFFFFFF)
                    continue;
                if (v < 0x48 ||
                    v > (uint)data.Length)
                    continue;
                offs.Add((int)v);
            }
            // Sort because slots may
            // store offsets out of order
            offs.Sort();
            // Remove duplicates
            var unique =
                new List<int>();
            foreach (int o in offs)
            {
                if (unique.Count == 0 ||
                    unique[unique.Count - 1]
                        != o)
                    unique.Add(o);
            }
            return unique;
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

        // ═════════════════════════════
        // APPLY BIG LAYOUT
        // Writes 3 copies of mesh chunk
        // at slots 11, 12, 13 with their
        // own offsets (not mirrored).
        // Matches original game format.
        // ═════════════════════════════
        static byte[] ApplyBigLayout(
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
            uint c11 = rawSlots[11];

            // Validate
            if (c8 == 0xFFFFFFFF
                || c11 == 0xFFFFFFFF
                || c11 <= c8)
            {
                // Source malformed —
                // fall back to mirror
                return ApplySlotMirror(
                    data);
            }

            // Find chunk 8 end and
            // chunk 11 end via next
            // distinct offset
            uint c8End = data.Length
                == 0
                ? 0
                : (uint)data.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c8 &&
                    v < c8End &&
                    v != 0xFFFFFFFF)
                    c8End = v;
            }

            uint c11End = (uint)
                data.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c11 &&
                    v < c11End &&
                    v != 0xFFFFFFFF)
                    c11End = v;
            }

            byte[] chunks07 =
                new byte[c8 - c0];
            Array.Copy(data,
                (int)c0, chunks07,
                0, chunks07.Length);

            byte[] chunk8 = new byte[
                c8End - c8];
            Array.Copy(data,
                (int)c8, chunk8,
                0, chunk8.Length);

            byte[] chunk11 = new byte[
                c11End - c11];
            Array.Copy(data,
                (int)c11, chunk11,
                0, chunk11.Length);

            // Preserve lookup chunks
            // 9/10 if they exist as
            // distinct chunks in source
            byte[] chunk9 = null;
            byte[] chunk10 = null;
            uint c9 = rawSlots[9];
            uint c10 = rawSlots[10];
            if (c9 != 0xFFFFFFFF
                && c9 != c8
                && c9 > c8)
            {
                uint c9End = c10 ==
                    0xFFFFFFFF
                    ? c11
                    : c10;
                chunk9 = new byte[
                    c9End - c9];
                Array.Copy(data,
                    (int)c9, chunk9,
                    0, chunk9.Length);
            }
            if (c10 != 0xFFFFFFFF
                && c10 != c8
                && c10 > c8)
            {
                chunk10 = new byte[
                    c11 - c10];
                Array.Copy(data,
                    (int)c10, chunk10,
                    0,
                    chunk10.Length);
            }

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

                uint newC9;
                if (chunk9 != null)
                {
                    newC9 =
                        (uint)ms.Length;
                    ms.Write(chunk9, 0,
                        chunk9.Length);
                }
                else
                {
                    // Duplicate chunk 8
                    // for slot 9
                    newC9 =
                        (uint)ms.Length;
                    ms.Write(chunk8, 0,
                        chunk8.Length);
                }

                uint newC10;
                if (chunk10 != null)
                {
                    newC10 =
                        (uint)ms.Length;
                    ms.Write(chunk10, 0,
                        chunk10.Length);
                }
                else
                {
                    newC10 =
                        (uint)ms.Length;
                    ms.Write(chunk8, 0,
                        chunk8.Length);
                }

                uint newC11 =
                    (uint)ms.Length;
                ms.Write(chunk11, 0,
                    chunk11.Length);

                uint newC12 =
                    (uint)ms.Length;
                ms.Write(chunk11, 0,
                    chunk11.Length);

                uint newC13 =
                    (uint)ms.Length;
                ms.Write(chunk11, 0,
                    chunk11.Length);

                byte[] result =
                    ms.ToArray();

                Array.Copy(data, 0,
                    result, 0, HDR);

                for (int i = 0; i < 8;
                     i++)
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

                WriteU32At(result,
                    0x10 + 8 * 4, newC8);
                WriteU32At(result,
                    0x10 + 9 * 4, newC9);
                WriteU32At(result,
                    0x10 + 10 * 4,
                    newC10);
                WriteU32At(result,
                    0x10 + 11 * 4,
                    newC11);
                WriteU32At(result,
                    0x10 + 12 * 4,
                    newC12);
                WriteU32At(result,
                    0x10 + 13 * 4,
                    newC13);

                return result;
            }
        }

        // ═════════════════════════════
        // APPLY SMALL LAYOUT
        // Single mesh chunk, slots
        // 9/10/12/13 = 0xFFFFFFFF.
        // ═════════════════════════════
        static byte[] ApplySmallLayout(
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
            uint c11 = rawSlots[11];

            if (c8 == 0xFFFFFFFF
                || c11 == 0xFFFFFFFF
                || c11 <= c8)
            {
                return ApplySlotMirror(
                    data);
            }

            uint c8End = (uint)
                data.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c8 &&
                    v < c8End &&
                    v != 0xFFFFFFFF)
                    c8End = v;
            }

            uint c11End = (uint)
                data.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c11 &&
                    v < c11End &&
                    v != 0xFFFFFFFF)
                    c11End = v;
            }

            byte[] chunks07 =
                new byte[c8 - c0];
            Array.Copy(data,
                (int)c0, chunks07,
                0, chunks07.Length);

            byte[] chunk8 = new byte[
                c8End - c8];
            Array.Copy(data,
                (int)c8, chunk8,
                0, chunk8.Length);

            byte[] chunk11 = new byte[
                c11End - c11];
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

                Array.Copy(data, 0,
                    result, 0, HDR);

                for (int i = 0; i < 8;
                     i++)
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

                WriteU32At(result,
                    0x10 + 8 * 4,
                    newC8);
                WriteU32At(result,
                    0x10 + 9 * 4,
                    0xFFFFFFFF);
                WriteU32At(result,
                    0x10 + 10 * 4,
                    0xFFFFFFFF);
                WriteU32At(result,
                    0x10 + 11 * 4,
                    newC11);
                WriteU32At(result,
                    0x10 + 12 * 4,
                    0xFFFFFFFF);
                WriteU32At(result,
                    0x10 + 13 * 4,
                    0xFFFFFFFF);

                return result;
            }
        }

        // Helper used by all 3 layouts
        static void WriteU32At(
            byte[] data,
            int offset,
            uint value)
        {
            byte[] b =
                BitConverter.GetBytes(
                    value);
            Array.Copy(b, 0, data,
                offset, 4);
        }
    }
}
