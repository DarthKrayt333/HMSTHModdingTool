using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    /// <summary>
    /// Handles adding NEW batches and
    /// removing existing batches from
    /// RDTB archives. Called by cbatches
    /// and csrdbbatches when the batch
    /// folder contains batch indices
    /// beyond the original RDTB batch
    /// count, or when original batches
    /// are missing from the folder.
    ///
    /// Does NOT modify RDTBBatchReplacer.
    /// Works alongside it as a separate
    /// processing step.
    ///
    /// Flow:
    /// 1. cbatches scans folder
    /// 2. If new batch indices found
    ///    OR original batches missing:
    ///    → call RDTBBatchAdder.Process()
    ///    → returns modified RDTB bytes
    ///      with expanded/shrunk tables
    /// 3. Then normal cbatches rebuild
    ///    continues on the modified RDTB
    /// </summary>
    public static class RDTBBatchAdder
    {
        // VIF constants
        const byte VIF_B0 = 0x00;
        const byte VIF_B1 = 0x80;
        const byte VIF_B3 = 0x6C;
        const uint F_ZERO = 0x00000000;
        const uint F_ONE = 0x3F800000;

        static readonly byte[]
            HDR_TAIL =
        {
            0x00, 0x40, 0x3E, 0x30,
            0x12, 0x04, 0x00, 0x00
        };
        static readonly byte[]
            GIF_FIRST =
        {
            0x00, 0x00, 0x80, 0x3F,
            0x00, 0x00, 0x00, 0x11,
            0x00, 0x00, 0x00, 0x14,
            0x00, 0x00, 0x00, 0x00
        };
        static readonly byte[]
            GIF_NEXT =
        {
            0x00, 0x00, 0x80, 0x3F,
            0x00, 0x00, 0x00, 0x17,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        static readonly byte[]
            EOF_TAG =
        {
            0x00, 0x00, 0x00, 0x70,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        // ═════════════════════════════
        // SCAN RESULT
        // Describes what the folder
        // contains vs what the
        // original RDTB has
        // ═════════════════════════════
        public class BatchScanResult
        {
            /// <summary>
            /// All batch indices found
            /// in the folder, sorted
            /// </summary>
            public List<int>
                FolderBatchIndices =
                new List<int>();

            /// <summary>
            /// Batch index -> tex_id
            /// from folder model_XX
            /// parent directory
            /// </summary>
            public Dictionary<int, int>
                BatchToTexId =
                new Dictionary<int,
                    int>();

            /// <summary>
            /// Batch index -> OBJ path
            /// </summary>
            public Dictionary<int,
                string> BatchToObjPath =
                new Dictionary<int,
                    string>();

            /// <summary>
            /// Original RDTB batch
            /// count (from material
            /// table)
            /// </summary>
            public int
                OriginalBatchCount;

            /// <summary>
            /// Batch indices that exist
            /// in folder but NOT in
            /// original RDTB (new)
            /// </summary>
            public List<int>
                NewBatchIndices =
                new List<int>();

            /// <summary>
            /// Batch indices that exist
            /// in original RDTB but NOT
            /// in folder (deleted)
            /// </summary>
            public List<int>
                DeletedBatchIndices =
                new List<int>();

            /// <summary>
            /// Batch indices that exist
            /// in both folder and
            /// original (replace)
            /// </summary>
            public List<int>
                ReplaceBatchIndices =
                new List<int>();

            /// <summary>
            /// Highest batch index
            /// across both folder
            /// and original
            /// </summary>
            public int
                MaxBatchIndex;

            /// <summary>
            /// True if any structural
            /// changes needed (add
            /// or remove)
            /// </summary>
            public bool NeedsRestructure
                => NewBatchIndices
                       .Count > 0 ||
                   DeletedBatchIndices
                       .Count > 0;
        }

        // ═════════════════════════════
        // SCAN BATCH FOLDER
        // Determines what needs to
        // be added/removed/replaced
        // ═════════════════════════════
        public static BatchScanResult
            ScanFolder(
                string folderPath,
                byte[] rdtbData)
        {
            var result =
                new BatchScanResult();

            // Get original batch count
            result.OriginalBatchCount =
                GetOriginalBatchCount(
                    rdtbData);

            // Scan all model_XX dirs
            // for batch_XXXX.obj files
            string[] modelDirs =
                Directory
                    .GetDirectories(
                        folderPath,
                        "model_*");

            foreach (string md in
                modelDirs)
            {
                string dirName =
                    Path.GetFileName(md)
                        .ToLower();
                if (!dirName.StartsWith(
                        "model_"))
                    continue;
                string numStr =
                    dirName.Substring(6);
                int texId;
                if (!int.TryParse(
                        numStr,
                        out texId))
                    continue;

                foreach (string f in
                    Directory.GetFiles(
                        md,
                        "batch_*.obj"))
                {
                    string fn =
                        Path
                            .GetFileNameWithoutExtension(
                                f);
                    if (!fn.StartsWith(
                            "batch_"))
                        continue;
                    string bns =
                        fn.Substring(6);
                    int bi;
                    if (!int.TryParse(
                            bns,
                            out bi))
                        continue;

                    result
                        .FolderBatchIndices
                        .Add(bi);
                    result
                        .BatchToTexId[bi]
                        = texId;
                    result
                        .BatchToObjPath[bi]
                        = f;
                }
            }

            result.FolderBatchIndices
                .Sort();

            // Classify each batch
            int origCount =
                result
                    .OriginalBatchCount;

            // Find max index needed
            int maxIdx = origCount - 1;
            if (result
                    .FolderBatchIndices
                    .Count > 0)
            {
                int folderMax =
                    result
                        .FolderBatchIndices
                        .Last();
                if (folderMax > maxIdx)
                    maxIdx = folderMax;
            }
            result.MaxBatchIndex =
                maxIdx;

            // Classify
            var folderSet =
                new HashSet<int>(
                    result
                        .FolderBatchIndices);

            for (int i = 0;
                 i <= maxIdx; i++)
            {
                bool inFolder =
                    folderSet
                        .Contains(i);
                bool inOriginal =
                    i < origCount;

                if (inFolder &&
                    inOriginal)
                    result
                        .ReplaceBatchIndices
                        .Add(i);
                else if (inFolder &&
                    !inOriginal)
                    result
                        .NewBatchIndices
                        .Add(i);
                else if (!inFolder &&
                    inOriginal)
                    result
                        .DeletedBatchIndices
                        .Add(i);
                // else: gap index
                // not in folder and
                // not in original
                // -> will be filled
                // with hidden batch
            }

            return result;
        }

        // ═════════════════════════════
        // PROCESS
        // Main entry point.
        // Takes original RDTB bytes
        // and scan result, returns
        // new RDTB bytes with
        // expanded/modified tables.
        //
        // After this, cbatches can
        // do its normal rebuild on
        // the returned bytes.
        // ═════════════════════════════
        public static byte[] Process(
            byte[] rdtbData,
            BatchScanResult scan)
        {
            if (!scan.NeedsRestructure)
                return rdtbData;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTBBatchAdder"
                + " Processing");
            Console.ResetColor();
            Console.WriteLine(
                "    Original batches:"
                + " " +
                scan.OriginalBatchCount);
            Console.WriteLine(
                "    New batches: " +
                scan.NewBatchIndices
                    .Count +
                " -> [" +
                string.Join(", ",
                    scan
                        .NewBatchIndices) +
                "]");
            Console.WriteLine(
                "    Deleted batches: " +
                scan.DeletedBatchIndices
                    .Count +
                " -> [" +
                string.Join(", ",
                    scan
                        .DeletedBatchIndices)
                + "]");
            Console.WriteLine(
                "    Replace batches: " +
                scan.ReplaceBatchIndices
                    .Count);
            Console.WriteLine(
                "    Max batch index: " +
                scan.MaxBatchIndex);

            // Total batches in output
            // = max index + 1
            int newTotalBatches =
                scan.MaxBatchIndex + 1;

            Console.WriteLine(
                "    New total batches:"
                + " " +
                newTotalBatches);

            // Read raw slot values
            uint[] rawSlots =
                new uint[14];
            for (int i = 0;
                 i < 14; i++)
                rawSlots[i] =
                    BitConverter
                        .ToUInt32(
                            rdtbData,
                            0x10 +
                            i * 4);

            // Build active offsets
            var offs = ReadOffs(
                rdtbData);

            // Slice all chunks
            var chunks =
                SliceChunks(
                    rdtbData, offs);

            // Find material chunk
            // index in offs list
            int matChunkIdx =
                FindChunkIdx(
                    offs, rawSlots[8]);

            // Find mesh chunk(s)
            // in offs list
            var meshChunkIndices =
                FindMeshChunkIndices(
                    offs, rawSlots,
                    chunks);

            if (matChunkIdx < 0)
            {
                Console.ForegroundColor
                    = ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [!] No material"
                    + " chunk found");
                Console.ResetColor();
                return rdtbData;
            }

            Console.WriteLine(
                "    Mat chunk idx: " +
                matChunkIdx);
            Console.WriteLine(
                "    Mesh chunk(s): [" +
                string.Join(", ",
                    meshChunkIndices) +
                "]");

            // ── EXPAND MATERIAL
            //    TABLE (chunk 8) ──
            byte[] oldMat =
                chunks[matChunkIdx];
            byte[] newMat =
                ExpandMaterialTable(
                    oldMat,
                    scan,
                    newTotalBatches);
            chunks[matChunkIdx] =
                newMat;

            Console.WriteLine(
                "    Mat table: " +
                oldMat.Length
                    .ToString("N0") +
                " -> " +
                newMat.Length
                    .ToString("N0") +
                " B");

            // ── EXPAND MESH CHUNKS ──
            // For each mesh chunk
            // (11, 12, 13 or just
            // last for small):
            // expand pointer table
            // to fit newTotalBatches
            foreach (int mci in
                meshChunkIndices)
            {
                if (mci < 0 ||
                    mci >=
                        chunks.Count)
                    continue;

                byte[] oldMesh =
                    chunks[mci];
                byte[] newMesh =
                    ExpandMeshChunk(
                        oldMesh,
                        scan,
                        newTotalBatches);
                chunks[mci] = newMesh;

                Console.WriteLine(
                    "    Mesh[" +
                    mci + "]: " +
                    oldMesh.Length
                        .ToString(
                            "N0") +
                    " -> " +
                    newMesh.Length
                        .ToString(
                            "N0") +
                    " B");
            }

            // ── EXPAND LOOKUP
            //    CHUNKS (8/9/10) ──
            // These mirror material
            // table in structure.
            // Slots 9/10 may be
            // aliases or sentinels.
            int[] lookupSlots =
                { 8, 9, 10 };
            foreach (int ls in
                lookupSlots)
            {
                // Skip the one we
                // already expanded
                // as material table
                uint sv = rawSlots[ls];
                if (sv == 0 ||
                    sv == 0xFFFFFFFF)
                    continue;

                int li = FindChunkIdx(
                    offs, sv);
                if (li < 0 ||
                    li >= chunks.Count)
                    continue;

                // Skip if same chunk
                // as material (already
                // expanded)
                if (li == matChunkIdx)
                    continue;

                // Only expand if this
                // is a distinct lookup
                // chunk (big RDTB has
                // separate 9/10)
                byte[] oldLookup =
                    chunks[li];
                byte[] newLookup =
                    ExpandLookupChunk(
                        oldLookup,
                        scan,
                        newTotalBatches);
                chunks[li] = newLookup;

                Console.WriteLine(
                    "    Lookup[" +
                    li + "]: " +
                    oldLookup.Length
                        .ToString(
                            "N0") +
                    " -> " +
                    newLookup.Length
                        .ToString(
                            "N0") +
                    " B");
            }

            // ── REASSEMBLE RDTB ──
            byte[] result =
                Reassemble(
                    rdtbData,
                    rawSlots,
                    offs,
                    chunks);

            Console.WriteLine(
                "    Final: " +
                rdtbData.Length
                    .ToString("N0") +
                " -> " +
                result.Length
                    .ToString("N0") +
                " B");

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    [OK] Tables"
                + " expanded for " +
                newTotalBatches +
                " batches");
            Console.ResetColor();

            return result;
        }

        // ═════════════════════════════
        // EXPAND MATERIAL TABLE
        // Adds new 8-byte records
        // for new batch indices.
        // Fills gaps with placeholder
        // records.
        // ═════════════════════════════
        private static byte[]
            ExpandMaterialTable(
                byte[] oldC8,
                BatchScanResult scan,
                int newTotal)
        {
            int origCount =
                GetBatchCount(oldC8);

            if (newTotal <= origCount)
                return oldC8;

            // Read all existing records
            var records =
                new List<byte[]>();
            for (int i = 0;
                 i < origCount; i++)
            {
                byte[] rec =
                    ReadMatRecord(
                        oldC8, i);
                records.Add(rec);
            }

            // Extend to newTotal
            // For new indices: build
            // record based on tex_id
            // from folder scan
            for (int i = origCount;
                 i < newTotal; i++)
            {
                byte[] rec =
                    new byte[8];

                if (scan.BatchToTexId
                        .ContainsKey(i))
                {
                    int texId =
                        scan.BatchToTexId
                            [i];

                    // Find bone idx
                    // from existing
                    // batch with same
                    // tex_id
                    int boneIdx =
                        FindBoneForTex(
                            oldC8,
                            texId,
                            origCount);

                    // Find field_b and
                    // field_c from
                    // existing same-tex
                    ushort fieldB = 0;
                    ushort fieldC =
                        0x00BB;
                    FindFieldsForTex(
                        oldC8, texId,
                        origCount,
                        out fieldB,
                        out fieldC);

                    WriteU16(rec, 0,
                        (ushort)boneIdx);
                    WriteU16(rec, 2,
                        fieldB);
                    WriteU16(rec, 4,
                        fieldC);
                    WriteU16(rec, 6,
                        (ushort)texId);
                }
                else
                {
                    // Gap/placeholder
                    // Use bone 0, tex 0
                    WriteU16(rec, 0, 0);
                    WriteU16(rec, 2, 0);
                    WriteU16(rec, 4,
                        0x00BB);
                    WriteU16(rec, 6, 0);
                }

                records.Add(rec);
            }

            // Rebuild chunk:
            // pointer table + records
            int ptrTableSize =
                newTotal * 4;

            using (var ms =
                new MemoryStream())
            {
                // Placeholder for
                // pointer table
                ms.Write(
                    new byte[
                        ptrTableSize],
                    0, ptrTableSize);

                // Write records and
                // track their offsets
                var recOffsets =
                    new List<int>();
                for (int i = 0;
                     i < newTotal; i++)
                {
                    recOffsets.Add(
                        (int)ms.Length);
                    ms.Write(
                        records[i],
                        0, 8);
                }

                byte[] result =
                    ms.ToArray();

                // Fill pointer table
                for (int i = 0;
                     i < newTotal; i++)
                {
                    WriteU32(result,
                        i * 4,
                        (uint)
                        recOffsets[i]);
                }

                return result;
            }
        }

        // ═════════════════════════════
        // EXPAND MESH CHUNK
        // Adds placeholder VIF blocks
        // for new batch indices.
        // Fills gaps with hidden
        // batches.
        // ═════════════════════════════
        private static byte[]
            ExpandMeshChunk(
                byte[] oldMesh,
                BatchScanResult scan,
                int newTotal)
        {
            uint firstPtr =
                BitConverter.ToUInt32(
                    oldMesh, 0);
            int origPtrCount =
                (int)(firstPtr / 4);

            // Read existing pointers
            uint[] origPtrs =
                new uint[origPtrCount];
            for (int i = 0;
                 i < origPtrCount; i++)
                origPtrs[i] =
                    BitConverter.ToUInt32(
                        oldMesh, i * 4);

            // Check if we need to do
            // anything: if all new batch
            // indices already have valid
            // (non-null) pointers, skip
            bool needsWork = false;
            foreach (int ni in
                scan.NewBatchIndices)
            {
                if (ni >= origPtrCount)
                {
                    // Need to expand table
                    needsWork = true;
                    break;
                }
                if (origPtrs[ni] == 0)
                {
                    // Slot exists but is
                    // NULL - need to fill
                    needsWork = true;
                    break;
                }
            }

            if (!needsWork)
                return oldMesh;

            // Build hidden VIF block
            byte[] hiddenVif =
                BuildHiddenVif();

            // Determine actual pointer
            // count needed
            int finalPtrCount =
                Math.Max(
                    origPtrCount,
                    newTotal);

            int newPtrTableSize =
                finalPtrCount * 4;
            int oldPtrTableSize =
                origPtrCount * 4;
            int ptrTableGrowth =
                newPtrTableSize -
                oldPtrTableSize;

            using (var ms =
                new MemoryStream())
            {
                // Write expanded pointer
                // table placeholder
                ms.Write(
                    new byte[
                        newPtrTableSize],
                    0, newPtrTableSize);

                // Copy existing batch data
                // (everything after old
                // pointer table)
                if (oldPtrTableSize <
                    oldMesh.Length)
                {
                    int oldDataLen =
                        oldMesh.Length -
                        oldPtrTableSize;
                    ms.Write(
                        oldMesh,
                        oldPtrTableSize,
                        oldDataLen);
                }

                // For each new batch index
                // that needs a VIF slot,
                // append hidden VIF data
                var newSlotOffsets =
                    new Dictionary<int, int>();

                foreach (int ni in
                    scan.NewBatchIndices)
                {
                    // Check if this slot
                    // already has VIF data
                    // from the original
                    if (ni < origPtrCount &&
                        origPtrs[ni] != 0)
                        continue;

                    // Align to 16
                    while (ms.Length % 16
                           != 0)
                        ms.WriteByte(0);

                    newSlotOffsets[ni] =
                        (int)ms.Length;
                    ms.Write(
                        hiddenVif, 0,
                        hiddenVif.Length);
                }

                byte[] result =
                    ms.ToArray();

                // Fix pointer table:
                // existing entries shifted
                // by table growth
                for (int i = 0;
                     i < origPtrCount; i++)
                {
                    uint oldOff = origPtrs[i];
                    if (oldOff == 0)
                    {
                        // Keep null unless
                        // this is a new batch
                        if (newSlotOffsets
                                .ContainsKey(i))
                        {
                            WriteU32(result,
                                i * 4,
                                (uint)
                                newSlotOffsets[i]);
                        }
                        else
                        {
                            WriteU32(result,
                                i * 4, 0);
                        }
                    }
                    else
                    {
                        uint newOff =
                            oldOff +
                            (uint)
                            ptrTableGrowth;
                        WriteU32(result,
                            i * 4, newOff);
                    }
                }

                // Fill new pointer entries
                // (beyond original count)
                for (int i = origPtrCount;
                     i < finalPtrCount; i++)
                {
                    if (newSlotOffsets
                            .ContainsKey(i))
                    {
                        WriteU32(result,
                            i * 4,
                            (uint)
                            newSlotOffsets[i]);
                    }
                    else
                    {
                        WriteU32(result,
                            i * 4, 0);
                    }
                }

                return result;
            }
        }

        // ═════════════════════════════
        // EXPAND LOOKUP CHUNK
        // Same structure as material
        // table. Adds QW=0 records
        // for new batch indices.
        // ═════════════════════════════
        private static byte[]
            ExpandLookupChunk(
                byte[] oldLookup,
                BatchScanResult scan,
                int newTotal)
        {
            if (oldLookup == null ||
                oldLookup.Length < 4)
                return oldLookup;

            uint firstPtr =
                BitConverter.ToUInt32(
                    oldLookup, 0);
            if (firstPtr == 0 ||
                firstPtr >
                (uint)oldLookup.Length)
                return oldLookup;

            int origCount =
                (int)(firstPtr / 4);

            if (newTotal <= origCount)
                return oldLookup;

            // Read existing records
            var records =
                new List<byte[]>();
            for (int i = 0;
                 i < origCount; i++)
            {
                int poff = i * 4;
                if (poff + 4 >
                    oldLookup.Length)
                    break;
                uint ptr =
                    BitConverter
                        .ToUInt32(
                            oldLookup,
                            poff);
                if (ptr + 8 >
                    (uint)
                    oldLookup.Length)
                {
                    records.Add(
                        new byte[8]);
                    continue;
                }
                byte[] rec =
                    new byte[8];
                Array.Copy(
                    oldLookup,
                    (int)ptr,
                    rec, 0, 8);
                records.Add(rec);
            }

            // Add placeholder
            // records for new slots
            // QW = 0, flags = 0
            for (int i = origCount;
                 i < newTotal; i++)
            {
                byte[] rec =
                    new byte[8];
                // QW = 0
                WriteU32(rec, 0, 0);
                // Flags = 0
                WriteU32(rec, 4, 0);
                records.Add(rec);
            }

            // Rebuild
            int ptrTableSize =
                newTotal * 4;

            using (var ms =
                new MemoryStream())
            {
                ms.Write(
                    new byte[
                        ptrTableSize],
                    0, ptrTableSize);

                var recOffsets =
                    new List<int>();
                for (int i = 0;
                     i < newTotal; i++)
                {
                    recOffsets.Add(
                        (int)ms.Length);
                    ms.Write(
                        records[i],
                        0,
                        records[i]
                            .Length);
                }

                byte[] result =
                    ms.ToArray();

                for (int i = 0;
                     i < newTotal; i++)
                {
                    WriteU32(result,
                        i * 4,
                        (uint)
                        recOffsets[i]);
                }

                return result;
            }
        }

        // ═════════════════════════════
        // BUILD HIDDEN VIF BLOCK
        // A single tiny batch that
        // renders nothing visible.
        // 3 degenerate verts at origin.
        // ═════════════════════════════
        private static byte[]
            BuildHiddenVif()
        {
            using (var ms =
                new MemoryStream())
            {
                // VIF header
                byte[] hdr =
                    new byte[16];
                hdr[0] = VIF_B0;
                hdr[1] = VIF_B1;
                hdr[2] = (byte)(
                    (3 * 3 + 1)
                    & 0xFF);
                hdr[3] = VIF_B3;
                hdr[4] = 3;
                hdr[5] = 0x80;
                Array.Copy(
                    HDR_TAIL, 0,
                    hdr, 8, 8);
                ms.Write(hdr, 0, 16);

                // 3 verts at origin
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
                        i == 0
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

                // GIF tag
                ms.Write(GIF_FIRST,
                    0, 16);
                // EOF
                ms.Write(EOF_TAG,
                    0, 16);

                return ms.ToArray();
            }
        }

        // ═════════════════════════════
        // REASSEMBLE RDTB
        // Preserves slot layout
        // (mirrored, small, big)
        // ═════════════════════════════
        private static byte[]
            Reassemble(
                byte[] original,
                uint[] rawSlots,
                List<int> origOffs,
                List<byte[]> chunks)
        {
            int[] newOffs =
                new int[chunks.Count];
            int cursor = 0x48;
            for (int i = 0;
                 i < chunks.Count;
                 i++)
            {
                newOffs[i] = cursor;
                cursor +=
                    chunks[i].Length;
            }

            var oldToNew =
                new Dictionary<int,
                    int>();
            for (int i = 0;
                 i < origOffs.Count
                 && i < chunks.Count;
                 i++)
                oldToNew[origOffs[i]] =
                    newOffs[i];

            byte[] header =
                new byte[0x48];
            Array.Copy(
                original, 0,
                header, 0, 0x48);

            for (int i = 0;
                 i < 14; i++)
            {
                uint sv = rawSlots[i];
                uint newVal;
                if (sv == 0)
                    newVal = 0;
                else if (sv ==
                    0xFFFFFFFF)
                    newVal = 0xFFFFFFFF;
                else if (
                    oldToNew
                        .TryGetValue(
                            (int)sv,
                            out int nv))
                    newVal = (uint)nv;
                else
                    newVal = sv;

                byte[] b =
                    BitConverter
                        .GetBytes(
                            newVal);
                Array.Copy(b, 0,
                    header,
                    0x10 + i * 4, 4);
            }

            byte[] result =
                new byte[cursor];
            Array.Copy(header, 0,
                result, 0, 0x48);
            for (int i = 0;
                 i < chunks.Count;
                 i++)
                Array.Copy(
                    chunks[i], 0,
                    result,
                    newOffs[i],
                    chunks[i].Length);

            return result;
        }

        // ═════════════════════════════
        // HELPERS
        // ═════════════════════════════
        private static List<int>
            ReadOffs(byte[] data)
        {
            var offs = new List<int>();
            for (int i = 0;
                 i < 14; i++)
            {
                uint v =
                    BitConverter
                        .ToUInt32(
                            data,
                            0x10 +
                            i * 4);
                if (v == 0 ||
                    v == 0xFFFFFFFF ||
                    v < 0x48 ||
                    v > (uint)
                        data.Length)
                    continue;
                offs.Add((int)v);
            }
            offs = offs.Distinct()
                .OrderBy(x => x)
                .ToList();
            return offs;
        }

        private static List<byte[]>
            SliceChunks(
                byte[] data,
                List<int> offs)
        {
            var chunks =
                new List<byte[]>();
            for (int i = 0;
                 i < offs.Count; i++)
            {
                int s = offs[i];
                int e =
                    (i + 1 <
                        offs.Count)
                    ? offs[i + 1]
                    : data.Length;
                byte[] c =
                    new byte[e - s];
                Array.Copy(data, s,
                    c, 0, e - s);
                chunks.Add(c);
            }
            return chunks;
        }

        private static int
            FindChunkIdx(
                List<int> offs,
                uint rawSlot)
        {
            if (rawSlot == 0 ||
                rawSlot == 0xFFFFFFFF)
                return -1;
            return offs.IndexOf(
                (int)rawSlot);
        }

        private static List<int>
            FindMeshChunkIndices(
                List<int> offs,
                uint[] rawSlots,
                List<byte[]> chunks)
        {
            var result =
                new List<int>();

            // Try slots 11, 12, 13
            int[] meshSlots =
                { 11, 12, 13 };
            foreach (int ms in
                meshSlots)
            {
                uint sv = rawSlots[ms];
                if (sv == 0 ||
                    sv == 0xFFFFFFFF)
                    continue;
                int idx =
                    FindChunkIdx(
                        offs, sv);
                if (idx >= 0 &&
                    !result.Contains(
                        idx))
                    result.Add(idx);
            }

            // If none found, use
            // last chunk
            if (result.Count == 0 &&
                chunks.Count > 0)
                result.Add(
                    chunks.Count - 1);

            return result;
        }

        private static int
            GetOriginalBatchCount(
                byte[] rdtbData)
        {
            var offs =
                ReadOffs(rdtbData);
            if (offs.Count < 9)
                return 0;

            // Material chunk is at
            // index 8 in active offs
            int c8Off = offs[8];
            int c8End =
                (offs.Count > 9)
                ? offs[9]
                : rdtbData.Length;

            // But also check raw
            // slot 8 directly
            uint rawC8 =
                BitConverter.ToUInt32(
                    rdtbData,
                    0x10 + 8 * 4);
            if (rawC8 != 0 &&
                rawC8 != 0xFFFFFFFF)
            {
                c8Off = (int)rawC8;
                // Find end
                c8End =
                    rdtbData.Length;
                for (int i = 0;
                     i < offs.Count;
                     i++)
                {
                    if (offs[i] >
                        c8Off &&
                        offs[i] <
                        c8End)
                        c8End =
                            offs[i];
                }
            }

            if (c8Off + 4 >
                rdtbData.Length)
                return 0;

            uint first =
                BitConverter.ToUInt32(
                    rdtbData, c8Off);
            if (first == 0 ||
                first > (uint)
                    (c8End - c8Off))
                return 0;
            return (int)(first / 4);
        }

        private static int
            GetBatchCount(byte[] c8)
        {
            if (c8 == null ||
                c8.Length < 4)
                return 0;
            uint first =
                BitConverter.ToUInt32(
                    c8, 0);
            if (first == 0 ||
                first > (uint)
                    c8.Length)
                return 0;
            return (int)(first / 4);
        }

        private static byte[]
            ReadMatRecord(
                byte[] c8, int idx)
        {
            byte[] rec = new byte[8];
            int poff = idx * 4;
            if (poff + 4 > c8.Length)
                return rec;
            uint ptr =
                BitConverter.ToUInt32(
                    c8, poff);
            if (ptr + 8 >
                (uint)c8.Length)
                return rec;
            Array.Copy(c8, (int)ptr,
                rec, 0, 8);
            return rec;
        }

        private static int
            FindBoneForTex(
                byte[] c8,
                int texId,
                int batchCount)
        {
            for (int i = 0;
                 i < batchCount; i++)
            {
                int poff = i * 4;
                if (poff + 4 >
                    c8.Length)
                    break;
                uint ptr =
                    BitConverter
                        .ToUInt32(
                            c8, poff);
                if (ptr + 8 >
                    (uint)c8.Length)
                    continue;
                int t =
                    BitConverter
                        .ToUInt16(
                            c8,
                            (int)ptr
                            + 6);
                if (t == texId)
                    return
                        BitConverter
                            .ToUInt16(
                                c8,
                                (int)
                                ptr);
            }
            return 0;
        }

        private static void
            FindFieldsForTex(
                byte[] c8,
                int texId,
                int batchCount,
                out ushort fieldB,
                out ushort fieldC)
        {
            fieldB = 0;
            fieldC = 0x00BB;

            for (int i = 0;
                 i < batchCount; i++)
            {
                int poff = i * 4;
                if (poff + 4 >
                    c8.Length)
                    break;
                uint ptr =
                    BitConverter
                        .ToUInt32(
                            c8, poff);
                if (ptr + 8 >
                    (uint)c8.Length)
                    continue;
                int t =
                    BitConverter
                        .ToUInt16(
                            c8,
                            (int)ptr
                            + 6);
                if (t == texId)
                {
                    fieldB =
                        BitConverter
                            .ToUInt16(
                                c8,
                                (int)
                                ptr + 2);
                    fieldC =
                        BitConverter
                            .ToUInt16(
                                c8,
                                (int)
                                ptr + 4);
                    return;
                }
            }
        }

        private static void WriteU32(
            byte[] data, int off,
            uint v)
        {
            byte[] b =
                BitConverter
                    .GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }

        private static void WriteU16(
            byte[] data, int off,
            ushort v)
        {
            byte[] b =
                BitConverter
                    .GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
        }
    }
}
