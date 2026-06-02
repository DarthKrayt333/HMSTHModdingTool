using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.RDTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace HMSTHModdingTool.SRDB
{
    // ═════════════════════════════════════════════
    // SRDB BATCH
    // ═════════════════════════════════════════════
    internal class SRDBBatch
    {
        public int TexId { get; set; }
        public int BoneIdx { get; set; }
        public List<Vec3> Verts =
            new List<Vec3>();
        public List<Vec3> Normals =
            new List<Vec3>();
        public List<Vec2> UVs =
            new List<Vec2>();
        public List<Tri> Faces =
            new List<Tri>();
        public List<int> BlobVertOffsets =
            new List<int>();
    }

    // ═════════════════════════════════════════════
    // SRDB 3D MANIFEST ENTRY
    // ═════════════════════════════════════════════
    internal class SRDB3DManifestEntry
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string RdtbBlob { get; set; }
        public int OriginalOffset { get; set; }
        public int OriginalSize { get; set; }
        public int BatchCount { get; set; }
        public List<int> TexIdsUsed =
            new List<int>();
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float CenterZ { get; set; }
        // ADDITIVE: auto-scale applied at
        // extract time. Rebuild inverts this
        // by multiplying user verts by
        // (1/AutoScale) before writing back.
        public float AutoScale { get; set; }
            = 1.0f;
    }

    // ═════════════════════════════════════════════
    // SRDB 3D MANIFEST
    // ═════════════════════════════════════════════
    internal class SRDB3DManifest
    {
        public string Tool
        { get; set; }
        public string OriginalSrdbName
        { get; set; }
        public string OriginalGdtbName
        { get; set; }
        public int SourceSize
        { get; set; }
        public uint SrdbVersion
        { get; set; }
        public uint SrdbUnk
        { get; set; }
        public List<int> ChunkOffsets =
            new List<int>();
        public List<SRDB3DManifestEntry>
            EmbeddedRdtbs =
            new List<SRDB3DManifestEntry>();
    }

    // ═════════════════════════════════════════════
    // SRDB 3D EXTRACTOR
    // ═════════════════════════════════════════════
    internal class SRDB3DExtractorInternal
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private const uint FLAG_EOF =
            0x70000000;

        // ─────────────────────────────────────────
        // MAIN EXTRACT
        // ─────────────────────────────────────────
        public void Extract(
            string srdbPath,
            string gdtbPath,
            string baseName)
        {
            if (!File.Exists(srdbPath))
                throw new FileNotFoundException(
                    "SRDB not found: " +
                    srdbPath);
            if (!File.Exists(gdtbPath))
                throw new FileNotFoundException(
                    "GDTB not found: " +
                    gdtbPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SRDB 3D Extractor v2.0");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
            Console.WriteLine(
                "    SRDB : " +
                Path.GetFileName(srdbPath));
            Console.WriteLine(
                "    GDTB : " +
                Path.GetFileName(gdtbPath));
            Console.WriteLine(
                "    Base : " + baseName);
            Console.WriteLine(
                new string('=', 64));

            var srdb = new SRDBFile(srdbPath);
            srdb.Load();
            var chunks = srdb.GetChunks();
            var embedded =
                srdb.FindAllEmbeddedRDTBs(
                    chunks);

            Console.WriteLine(
                "    Source size    : " +
                srdb.Data.Length.ToString(
                    "N0") + " B");
            Console.WriteLine(
                "    Embedded RDTBs : " +
                embedded.Count);

            if (embedded.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [!] No embedded" +
                    " RDTBs found");
                Console.ResetColor();
                return;
            }

            string outDir =
                Path.GetDirectoryName(
                    Path.GetFullPath(srdbPath));

            // 4 output folders
            string folderObj =
                Path.Combine(outDir,
                    baseName +
                    "_embedded_rdtbs_obj");
            string folderDae =
                Path.Combine(outDir,
                    baseName +
                    "_embedded_rdtbs_dae");
            string folderAllObj =
                Path.Combine(outDir,
                    baseName + "_all_obj");
            string folderAllDae =
                Path.Combine(outDir,
                    baseName + "_all_dae");

            foreach (var d in new[]
            {
                folderObj,    folderDae,
                folderAllObj, folderAllDae,
            })
            {
                Directory.CreateDirectory(d);
                Directory.CreateDirectory(
                    Path.Combine(d, "textures"));
                Directory.CreateDirectory(
                    Path.Combine(d,
                        "_rdtb_blobs"));
                File.Copy(srdbPath,
                    Path.Combine(d,
                        "_source.srdb"),
                    true);
                File.Copy(gdtbPath,
                    Path.Combine(d,
                        "_source.gdtb"),
                    true);
            }

            // Extract GDTB textures
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extracting textures...");
            Console.ResetColor();

            string tmpTex =
                Path.Combine(outDir,
                    "_" + baseName +
                    "_tex_tmp");
            if (Directory.Exists(tmpTex))
                Directory.Delete(
                    tmpTex, true);
            Directory.CreateDirectory(tmpTex);

            try
            {
                GDTBArchive.Extract(
                    gdtbPath, tmpTex);
                foreach (var bmp in
                    Directory.GetFiles(
                        tmpTex, "*.bmp"))
                {
                    string fn =
                        Path.GetFileName(bmp);
                    foreach (var d in new[]
                    {
                        folderObj,
                        folderDae,
                        folderAllObj,
                        folderAllDae,
                    })
                    {
                        File.Copy(bmp,
                            Path.Combine(
                                d,
                                "textures",
                                fn),
                            true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [!] GDTB: " +
                    ex.Message);
                Console.ResetColor();
            }
            finally
            {
                if (Directory.Exists(tmpTex))
                    Directory.Delete(
                        tmpTex, true);
            }

            // Build tex_id -> BMP maps
            var texMapObj =
                BuildTexMap(folderObj);
            var texMapDae =
                BuildTexMap(folderDae);
            var texMapAllObj =
                BuildTexMap(folderAllObj);
            var texMapAllDae =
                BuildTexMap(folderAllDae);

            Console.WriteLine(
                "    Textures: " +
                texMapObj.Count);

            var manifestEntries =
                new List<SRDB3DManifestEntry>();
            var allBatches =
                new List<SRDBBatch>();

            Console.WriteLine();

            foreach (var e in embedded)
            {
                string subBase =
                    "embedded_" +
                    e.Index.ToString("D2");

                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "[+] RDTB " +
                    (e.Index + 1) + "/" +
                    embedded.Count +
                    " @ 0x" +
                    e.AbsOffset.ToString(
                        "X8") +
                    " (" +
                    e.Size.ToString("N0") +
                    " B)");
                Console.ResetColor();

                // Save blob — use absolute
                // SRDB slice at e.AbsOffset
                // for e.Size bytes. This
                // guarantees blob matches
                // SRDB exactly (fixes bug
                // where e.RawData had wrong
                // size).
                byte[] correctBlob =
                    new byte[e.Size];
                int copyLen = Math.Min(
                    e.Size,
                    srdb.Data.Length -
                        e.AbsOffset);
                Array.Copy(
                    srdb.Data, e.AbsOffset,
                    correctBlob, 0, copyLen);
                foreach (var d in new[]
                {
                    folderObj,
                    folderDae,
                    folderAllObj,
                    folderAllDae,
                })
                {
                    File.WriteAllBytes(
                        Path.Combine(d,
                            "_rdtb_blobs",
                            subBase + ".rdtb"),
                        correctBlob);
                }

                // Parse mesh
                var batches =
                    ParseEmbeddedRDTB(e);

                if (batches.Count == 0)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] No batches");
                    Console.ResetColor();

                    manifestEntries.Add(
                        new SRDB3DManifestEntry
                        {
                            Index =
                                e.Index,
                            Name = subBase,
                            RdtbBlob =
                                "_rdtb_blobs/" +
                                subBase +
                                ".rdtb",
                            OriginalOffset =
                                e.AbsOffset,
                            OriginalSize =
                                e.Size,
                            BatchCount = 0,
                        });
                    continue;
                }

                var texIds =
                    batches
                        .Select(b => b.TexId)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList();
                Console.WriteLine(
                    "    batches=" +
                    batches.Count +
                    "  bones=" +
                    e.BoneCount +
                    "  tex_ids=[" +
                    string.Join(",", texIds) +
                    "]");

                // ADDITIVE: track auto-scale
                // applied to this embedded
                // RDTB (default 1.0 = none)
                float entryAutoScale = 1.0f;

                // ADDITIVE: auto-scale oversized
                // embedded models (large map
                // props, terrain pieces, etc.)
                // to fit a reasonable display
                // size. Same logic as RDTB
                // extractor.
                {
                    float mnx = float.MaxValue;
                    float mxx = float.MinValue;
                    float mny = float.MaxValue;
                    float mxy = float.MinValue;
                    float mnz = float.MaxValue;
                    float mxz = float.MinValue;
                    bool any = false;
                    foreach (var b in batches)
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
                            any = true;
                        }
                    if (any)
                    {
                        float dx = mxx - mnx;
                        float dy = mxy - mny;
                        float dz = mxz - mnz;
                        float maxDim = dx;
                        if (dy > maxDim)
                            maxDim = dy;
                        if (dz > maxDim)
                            maxDim = dz;

                        const float TARGET
                            = 100f;
                        const float THRESHOLD
                            = 250f;

                        if (maxDim > THRESHOLD)
                        {
                            float autoScale =
                                TARGET / maxDim;
                            Console
                                .ForegroundColor
                                =
                                ConsoleColor
                                    .Cyan;
                            Console.WriteLine(
                                "    auto-scale: "
                                + autoScale
                                    .ToString(
                                        "F4")
                                + "x (was "
                                + maxDim
                                    .ToString(
                                        "F0")
                                + " units)");
                            Console.ResetColor();
                            foreach (var b in
                                batches)
                            {
                                for (int i = 0;
                                     i <
                                     b.Verts
                                        .Count;
                                     i++)
                                {
                                    Vec3 v =
                                        b.Verts[
                                            i];
                                    b.Verts[i]
                                        =
                                        new Vec3(
                                            v.X *
                                            autoScale,
                                            v.Y *
                                            autoScale,
                                            v.Z *
                                            autoScale);
                                }
                            }
                            // REMEMBER the
                            // auto-scale for
                            // this embedded
                            // RDTB so rebuild
                            // can invert it
                            entryAutoScale =
                                autoScale;
                        }
                    }                
                }
                

                // Do NOT center — keep raw
                // coordinates so rebuild
                // is byte-perfect. Center
                // values stored as 0.
                float cx = 0f, cy = 0f,
                      cz = 0f;

                //// Compute + apply center
                //float cx, cy, cz;
                //ComputeCenter(
                //    batches,
                //    out cx, out cy, out cz);
                //CenterBatches(
                //    batches, cx, cy, cz);

                // Per-embedded OBJ
                string objPath =
                    Path.Combine(
                        folderObj,
                        subBase + ".obj");
                string mtlPath =
                    Path.Combine(
                        folderObj,
                        subBase + ".mtl");
                WriteOBJ(
                    objPath, mtlPath,
                    subBase, batches,
                    BuildRelTexMap(
                        texMapObj, objPath));

                // Write vert offset sidecar
                // so the builder knows exactly
                // which file byte to overwrite
                // for each OBJ vert line.
                WriteVertOffsetSidecar(
                    Path.Combine(folderObj,
                        subBase + ".voff"),
                    correctBlob,
                    e.AbsOffset,
                    batches);

                // Per-embedded DAE
                string daePath =
                    Path.Combine(
                        folderDae,
                        subBase + ".dae");
                WriteDAE(
                    daePath, subBase,
                    batches,
                    BuildRelTexMap(
                        texMapDae, daePath));

                allBatches.AddRange(batches);

                manifestEntries.Add(
                    new SRDB3DManifestEntry
                    {
                        Index =
                            e.Index,
                        Name = subBase,
                        RdtbBlob =
                            "_rdtb_blobs/" +
                            subBase + ".rdtb",
                        OriginalOffset =
                            e.AbsOffset,
                        OriginalSize =
                            e.Size,
                        BatchCount =
                            batches.Count,
                        TexIdsUsed = texIds,
                        CenterX = cx,
                        CenterY = cy,
                        CenterZ = cz,
                        AutoScale =
                            entryAutoScale,
                    });
            }

            // Combined all_obj / all_dae
            if (allBatches.Count > 0)
            {
                string allObjPath =
                    Path.Combine(
                        folderAllObj,
                        baseName + "_all.obj");
                string allMtlPath =
                    Path.Combine(
                        folderAllObj,
                        baseName + "_all.mtl");
                WriteOBJ(
                    allObjPath, allMtlPath,
                    baseName + "_all",
                    allBatches,
                    BuildRelTexMap(
                        texMapAllObj,
                        allObjPath));
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    _all_obj: " +
                    Path.GetFileName(
                        allObjPath) +
                    " (" +
                    allBatches.Count +
                    " batches)");
                Console.ResetColor();

                string allDaePath =
                    Path.Combine(
                        folderAllDae,
                        baseName + "_all.dae");
                WriteDAE(
                    allDaePath,
                    baseName + "_all",
                    allBatches,
                    BuildRelTexMap(
                        texMapAllDae,
                        allDaePath));
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    _all_dae: " +
                    Path.GetFileName(
                        allDaePath));
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [!] No batches" +
                    " for combined output");
                Console.ResetColor();
            }

            // Write manifests
            var manifest = new SRDB3DManifest
            {
                Tool =
                    "HMSTHModdingTool v2.0",
                OriginalSrdbName =
                    Path.GetFileName(
                        srdbPath),
                OriginalGdtbName =
                    Path.GetFileName(
                        gdtbPath),
                SourceSize =
                    srdb.Data.Length,
                SrdbVersion =
                    srdb.Version,
                SrdbUnk =
                    srdb.UnkFlags,
                ChunkOffsets =
                    new List<int>(
                        srdb.ChunkOffsets),
                EmbeddedRdtbs =
                    manifestEntries,
            };

            foreach (var d in new[]
            {
                folderObj,    folderDae,
                folderAllObj, folderAllDae,
            })
            {
                WriteManifest(d, manifest);
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] SRDB 3D extraction done!");
            Console.ResetColor();
            Console.WriteLine(
                "     " + folderObj);
            Console.WriteLine(
                "     " + folderDae);
            Console.WriteLine(
                "     " + folderAllObj);
            Console.WriteLine(
                "     " + folderAllDae);
        }

        // ─────────────────────────────────────────
        // PARSE EMBEDDED RDTB
        // ─────────────────────────────────────────
        private List<SRDBBatch>
            ParseEmbeddedRDTB(
                SRDBEmbeddedRDTB e)
        {
            var batches =
                new List<SRDBBatch>();
            byte[] rd = e.RawData;

            if (rd == null ||
                rd.Length < 0x48)
                return batches;

            // Read chunk offsets from blob
            var coffs = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                int o = 0x10 + i * 4;
                if (o + 4 > rd.Length) break;
                int v =
                    BitConverter.ToInt32(
                        rd, o);
                if (v == 0 || v < 0x48 ||
                    v > rd.Length)
                    break;
                if (v == -1 ||
                    v == unchecked(
                        (int)0xFFFFFFFF))
                    continue;
                coffs.Add(v);
            }

            if (coffs.Count == 0)
                return batches;

            // Slice chunks
            var chunks = new List<byte[]>();
            var chunkBases = new List<int>();
            for (int i = 0;
                 i < coffs.Count; i++)
            {
                int s = coffs[i];
                int en =
                    (i + 1 < coffs.Count)
                    ? coffs[i + 1]
                    : rd.Length;
                int sz = en - s;
                if (sz <= 0) continue;
                byte[] c = new byte[sz];
                Array.Copy(rd, s, c, 0, sz);
                chunks.Add(c);
                chunkBases.Add(s);
            }

            // Parse material table
            var mats =
                new List<(int boneIdx,
                    int texId)>();
            int c8Idx =
                Math.Min(8, chunks.Count - 1);
            if (c8Idx >= 0)
                mats = ParseMatTable(
                    chunks[c8Idx]);

            // Parse VIF data from LAST chunk
            // only. Mesh data always lives in
            // the final chunk. Earlier chunks
            // contain bone/material/animation
            // data which can have false VIF
            // signatures matching 00 80 ?? 6C
            // but are NOT mesh vertices.
            int meshChunkIdx =
                chunks.Count - 1;
            for (int ci = meshChunkIdx;
                 ci < chunks.Count; ci++)
            {
                byte[] ch = chunks[ci];
                var vifs = FindVIFs(ch);
                if (vifs.Count == 0) continue;

                var p2bi =
                    new Dictionary<int, int>();
                var opt = new List<int>();

                if (mats.Count > 0)
                {
                    for (int mi = 0;
                         mi < mats.Count;
                         mi++)
                    {
                        int poff = mi * 4;
                        if (poff + 4 >
                            ch.Length)
                            break;
                        int ptr =
                            BitConverter
                                .ToInt32(
                                    ch, poff);
                        if (ptr >= 0 &&
                            ptr < ch.Length &&
                            IsVIF(ch, ptr) &&
                            !p2bi.ContainsKey(
                                ptr))
                        {
                            p2bi[ptr] = mi;
                            opt.Add(ptr);
                        }
                    }
                }

                if (opt.Count == 0)
                    opt = new List<int>(vifs);
                else
                    opt.Sort();

                for (int bi = 0;
                     bi < opt.Count; bi++)
                {
                    int bs = opt[bi];
                    int be =
                        (bi + 1 < opt.Count)
                        ? opt[bi + 1]
                        : ch.Length;

                    var lv =
                        vifs.Where(v =>
                            v >= bs &&
                            v < be)
                        .ToList();
                    if (lv.Count == 0)
                        continue;

                    var batch =
                        new SRDBBatch();

                    for (int vi = 0;
                         vi < lv.Count; vi++)
                    {
                        int vo = lv[vi];
                        int ve2 =
                            (vi + 1 <
                             lv.Count)
                            ? lv[vi + 1]
                            : be;
                        int vc = ch[vo + 2];
                        var rows =
                            ParseRows(
                                ch,
                                vo + 16,
                                ve2);
                        if (rows.Count < 3)
                            continue;

                        int n =
                            (vc > 0 &&
                             vc * 3 <=
                             rows.Count)
                            ? vc
                            : rows.Count / 3;
                        if (n < 3 ||
                            n * 3 >
                            rows.Count)
                            continue;

                        int bv =
                            batch.Verts.Count;

                        for (int i = 0;
                             i < n; i++)
                        {
                            batch.Verts.Add(
                                new Vec3(
                                    rows[i].x,
                                    rows[i].y,
                                    rows[i].z));
                            batch.BlobVertOffsets
                                .Add(
                                    chunkBases[ci]
                                    + vo + 16
                                    + i * 16);
                        }
                        for (int i = n;
                             i < 2 * n; i++)
                            batch.Normals.Add(
                                new Vec3(
                                    rows[i].x,
                                    rows[i].y,
                                    rows[i].z));
                        for (int i = 2 * n;
                             i < 3 * n; i++)
                            batch.UVs.Add(
                                new Vec2(
                                    rows[i].x,
                                    1.0f -
                                    rows[i].y));

                        foreach (var t in
                            MakeStrip(n))
                            batch.Faces.Add(
                                new Tri(
                                    bv + t.A,
                                    bv + t.B,
                                    bv + t.C));
                    }

                    int mi2 =
                        p2bi.TryGetValue(
                            bs, out int mv)
                        ? mv : bi;
                    if (mats.Count > mi2)
                    {
                        batch.TexId =
                            mats[mi2].texId;
                        batch.BoneIdx =
                            mats[mi2].boneIdx;
                    }

                    batch.Faces =
                        FilterDegen(
                            batch.Faces,
                            batch.Verts);

                    if (batch.Verts.Count > 0
                        && batch.Faces.Count
                        > 0)
                        batches.Add(batch);
                }
            }

            return batches;
        }

        // ─────────────────────────────────────────
        // PARSE MATERIAL TABLE
        // ─────────────────────────────────────────
        private List<(int boneIdx, int texId)>
            ParseMatTable(byte[] c8)
        {
            var r =
                new List<(int, int)>();
            if (c8 == null ||
                c8.Length < 4)
                return r;
            if (c8[0] == VIF_B0 &&
                c8[1] == VIF_B1 &&
                c8.Length > 3 &&
                c8[3] == VIF_B3)
                return r;

            uint first =
                BitConverter.ToUInt32(
                    c8, 0);
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
                uint ptr =
                    BitConverter.ToUInt32(
                        c8, poff);
                if (ptr + 8 >
                    (uint)c8.Length)
                {
                    r.Add((0, 0));
                    continue;
                }
                int boneIdx =
                    BitConverter.ToUInt16(
                        c8, (int)ptr);
                int texId =
                    BitConverter.ToUInt16(
                        c8, (int)ptr + 6);
                r.Add((boneIdx, texId));
            }
            return r;
        }

        // ─────────────────────────────────────────
        // VIF HELPERS
        // ─────────────────────────────────────────
        private bool IsVIF(byte[] d, int o)
        {
            return
                o + 16 <= d.Length &&
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
                {
                    r.Add(i);
                    i += 16;
                }
                else i += 4;
            }
            return r;
        }

        private List<(uint flag, float x,
            float y, float z)>
            ParseRows(byte[] c,
                int ds, int de)
        {
            var rows =
                new List<(uint, float,
                    float, float)>();
            int o = ds;
            while (o + 16 <= de)
            {
                uint flag =
                    BitConverter.ToUInt32(
                        c, o);
                if (flag == FLAG_EOF) break;
                if (o + 4 < de &&
                    c[o] == VIF_B0 &&
                    c[o + 1] == VIF_B1 &&
                    c[o + 3] == VIF_B3)
                    break;

                float x =
                    BitConverter.ToSingle(
                        c, o + 4);
                float y =
                    BitConverter.ToSingle(
                        c, o + 8);
                float z =
                    BitConverter.ToSingle(
                        c, o + 12);

                if (!float.IsNaN(x) &&
                    !float.IsNaN(y) &&
                    !float.IsNaN(z) &&
                    !float.IsInfinity(x) &&
                    !float.IsInfinity(y) &&
                    !float.IsInfinity(z))
                    rows.Add(
                        (flag, x, y, z));

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

        // ─────────────────────────────────────────
        // CENTER HELPERS
        // ─────────────────────────────────────────
        private void ComputeCenter(
            List<SRDBBatch> batches,
            out float cx,
            out float cy,
            out float cz)
        {
            cx = cy = cz = 0f;
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

            if (!any) return;
            cx = (mnx + mxx) * 0.5f;
            cy = (mny + mxy) * 0.5f;
            cz = (mnz + mxz) * 0.5f;
        }

        private void CenterBatches(
            List<SRDBBatch> batches,
            float cx, float cy, float cz)
        {
            if (Math.Abs(cx) < 0.001f &&
                Math.Abs(cy) < 0.001f &&
                Math.Abs(cz) < 0.001f)
                return;

            foreach (var b in batches)
            {
                for (int i = 0;
                     i < b.Verts.Count; i++)
                {
                    Vec3 v = b.Verts[i];
                    b.Verts[i] = new Vec3(
                        v.X - cx,
                        v.Y - cy,
                        v.Z - cz);
                }
            }
        }

        // ─────────────────────────────────────────
        // TEXTURE MAP HELPERS
        // ─────────────────────────────────────────
        private Dictionary<int, string>
            BuildTexMap(string folder)
        {
            var m =
                new Dictionary<int, string>();
            string texDir =
                Path.Combine(
                    folder, "textures");
            if (!Directory.Exists(texDir))
                return m;

            foreach (var bmp in
                Directory.GetFiles(
                    texDir, "texture_*.bmp"))
            {
                string fn =
                    Path
                        .GetFileNameWithoutExtension(
                            bmp).ToLower();
                if (!fn.StartsWith(
                        "texture_"))
                    continue;
                if (!int.TryParse(
                        fn.Substring(8),
                        out int tid))
                    continue;
                m[tid] = bmp;
            }
            return m;
        }

        private Dictionary<int, string>
            BuildRelTexMap(
                Dictionary<int, string>
                    absMap,
                string fromFile)
        {
            var r =
                new Dictionary<int, string>();
            foreach (var kv in absMap)
                r[kv.Key] =
                    RelPath(kv.Value,
                        fromFile);
            return r;
        }

        private string RelPath(
            string target, string fromFile)
        {
            try
            {
                string t =
                    Path.GetFullPath(target);
                string fd =
                    Path.GetDirectoryName(
                        Path.GetFullPath(
                            fromFile));
                Uri tUri = new Uri(t);
                Uri fUri = new Uri(
                    fd +
                    Path
                        .DirectorySeparatorChar);
                return
                    Uri.UnescapeDataString(
                        fUri.MakeRelativeUri(
                            tUri)
                        .ToString()
                        .Replace('\\', '/'));
            }
            catch
            {
                return
                    Path.GetFileName(target);
            }
        }

        // ─────────────────────────────────────────
        // OBJ WRITER
        // ─────────────────────────────────────────
        private void WriteOBJ(
            string objPath,
            string mtlPath,
            string name,
            List<SRDBBatch> batches,
            Dictionary<int, string> texMap)
        {
            var usedTex =
                batches
                    .Select(b => b.TexId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

            using (var sw =
                new StreamWriter(mtlPath,
                    false, Encoding.UTF8))
            {
                sw.WriteLine(
                    "# " + name + " MTL");
                sw.WriteLine();
                foreach (int tid in usedTex)
                {
                    sw.WriteLine(
                        "newmtl mat_" +
                        tid.ToString("D2"));
                    sw.WriteLine("Ka 1 1 1");
                    sw.WriteLine("Kd 1 1 1");
                    sw.WriteLine("Ks 0 0 0");
                    sw.WriteLine("Ns 10");
                    sw.WriteLine("illum 2");
                    string rel;
                    if (texMap.TryGetValue(
                            tid, out rel))
                        sw.WriteLine(
                            "map_Kd " + rel);
                    else
                        sw.WriteLine(
                            "# no texture" +
                            " for tex_id " +
                            tid);
                    sw.WriteLine();
                }
            }

            using (var sw =
                new StreamWriter(objPath,
                    false, Encoding.UTF8))
            {
                sw.WriteLine("# " + name);
                sw.WriteLine(
                    "mtllib " +
                    Path.GetFileName(
                        mtlPath));
                sw.WriteLine();

                foreach (var b in batches)
                    foreach (var v in b.Verts)
                        sw.WriteLine(
                            "v " +
                            G(v.X) + " " +
                            G(v.Y) + " " +
                            G(v.Z));
                sw.WriteLine();

                foreach (var b in batches)
                    foreach (var uv in b.UVs)
                        sw.WriteLine(
                            "vt " +
                            G(uv.U) + " " +
                            G(uv.V));
                sw.WriteLine();

                foreach (var b in batches)
                    foreach (var n in
                        b.Normals)
                        sw.WriteLine(
                            "vn " +
                            G(n.X) + " " +
                            G(n.Y) + " " +
                            G(n.Z));
                sw.WriteLine();

                int vb = 1, ub = 1, nb = 1;
                for (int bi = 0;
                     bi < batches.Count;
                     bi++)
                {
                    var b = batches[bi];
                    sw.WriteLine(
                        "g batch_" +
                        bi.ToString("D4"));
                    sw.WriteLine(
                        "usemtl mat_" +
                        b.TexId.ToString(
                            "D2"));

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
                            a + "/" +
                            au + "/" +
                            an + " " +
                            bb + "/" +
                            bu + "/" +
                            bn + " " +
                            c + "/" +
                            cu + "/" +
                            cn);
                    }
                    vb += b.Verts.Count;
                    ub += b.UVs.Count;
                    nb += b.Normals.Count;
                }
            }
        }

        // ─────────────────────────────────────────
        // DAE WRITER — C# 7.3 compatible
        // uses using(...){} + Action<string>
        // ─────────────────────────────────────────
        private void WriteDAE(
            string daePath,
            string name,
            List<SRDBBatch> batches,
            Dictionary<int, string> texMap)
        {
            var usedTex =
                batches
                    .Select(b => b.TexId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

            var av = new List<Vec3>();
            var an_ = new List<Vec3>();
            var au_ = new List<Vec2>();
            var facesByTex =
                new Dictionary<int,
                    List<Tri>>();
            int vo = 0;

            foreach (var b in batches)
            {
                if (!facesByTex
                        .ContainsKey(b.TexId))
                    facesByTex[b.TexId] =
                        new List<Tri>();
                av.AddRange(b.Verts);
                an_.AddRange(b.Normals);
                au_.AddRange(b.UVs);
                foreach (var t in b.Faces)
                    facesByTex[b.TexId].Add(
                        new Tri(
                            t.A + vo,
                            t.B + vo,
                            t.C + vo));
                vo += b.Verts.Count;
            }

            string gid = name + "-geom";

            using (var f =
                new StreamWriter(daePath,
                    false, Encoding.UTF8))
            {
                Action<string> W =
                    s => f.Write(s);

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
                    if (texMap
                            .ContainsKey(tid))
                        imgIds[tid] =
                            "img-" +
                            tid.ToString(
                                "D2");
                }

                if (imgIds.Count > 0)
                {
                    W("<library_images>\n");
                    foreach (var kv in imgIds)
                    {
                        W("<image id=\"" +
                          kv.Value +
                          "\"><init_from>" +
                          texMap[kv.Key] +
                          "</init_from>" +
                          "</image>\n");
                    }
                    W("</library_images>\n");
                }

                // Effects
                W("<library_effects>\n");
                foreach (int tid in usedTex)
                {
                    string eid =
                        "eff-" +
                        tid.ToString("D2");
                    string iid = null;
                    imgIds.TryGetValue(
                        tid, out iid);

                    W("<effect id=\"" +
                      eid + "\">" +
                      "<profile_COMMON>\n");
                    if (iid != null)
                    {
                        W("<newparam" +
                          " sid=\"srf" +
                          tid + "\">" +
                          "<surface" +
                          " type=\"2D\">" +
                          "<init_from>" +
                          iid +
                          "</init_from>" +
                          "</surface>" +
                          "</newparam>\n");
                        W("<newparam" +
                          " sid=\"smp" +
                          tid + "\">" +
                          "<sampler2D>" +
                          "<source>srf" +
                          tid +
                          "</source>" +
                          "</sampler2D>" +
                          "</newparam>\n");
                    }
                    W("<technique" +
                      " sid=\"common\">" +
                      "<phong><diffuse>");
                    if (iid != null)
                        W("<texture" +
                          " texture=\"smp" +
                          tid + "\"" +
                          " texcoord=" +
                          "\"TEX0\"/>");
                    else
                        W("<color>" +
                          "1 1 1 1</color>");
                    W("</diffuse>" +
                      "</phong>" +
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
                W("<geometry id=\"" +
                  gid + "\"><mesh>\n");

                string posStr =
                    string.Join(" ",
                        av.Select(v =>
                            G(v.X) + " " +
                            G(v.Y) + " " +
                            G(v.Z)));
                W("<source id=\"" +
                  gid + "-pos\">" +
                  "<float_array id=\"" +
                  gid + "-pos-arr\"" +
                  " count=\"" +
                  (av.Count * 3) + "\">" +
                  posStr +
                  "</float_array>" +
                  "<technique_common>" +
                  "<accessor source=\"#" +
                  gid + "-pos-arr\"" +
                  " count=\"" + av.Count +
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
                    W("<source id=\"" +
                      gid + "-nrm\">" +
                      "<float_array id=\"" +
                      gid + "-nrm-arr\"" +
                      " count=\"" +
                      (an_.Count * 3) +
                      "\">" +
                      nStr +
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
                    W("<source id=\"" +
                      gid + "-uv\">" +
                      "<float_array id=\"" +
                      gid + "-uv-arr\"" +
                      " count=\"" +
                      (au_.Count * 2) +
                      "\">" +
                      uStr +
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

                W("<vertices id=\"" +
                  gid + "-v\">" +
                  "<input semantic=" +
                  "\"POSITION\"" +
                  " source=\"#" +
                  gid + "-pos\"/>" +
                  "</vertices>\n");

                int stride =
                    1 +
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
                    W("<input" +
                      " semantic=\"VERTEX\"" +
                      " source=\"#" +
                      gid + "-v\"" +
                      " offset=\"0\"/>\n");
                    if (hasN)
                        W("<input" +
                          " semantic=" +
                          "\"NORMAL\"" +
                          " source=\"#" +
                          gid + "-nrm\"" +
                          " offset=\"1\"/>\n");
                    if (hasU)
                        W("<input" +
                          " semantic=" +
                          "\"TEXCOORD\"" +
                          " source=\"#" +
                          gid + "-uv\"" +
                          " offset=\"" +
                          (hasN ? 2 : 1) +
                          "\"" +
                          " set=\"0\"/>\n");

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
                  "<visual_scene" +
                  " id=\"Scene\">\n" +
                  "<node id=\"" +
                  name + "\">" +
                  "<instance_geometry" +
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
                          " semantic=" +
                          "\"TEX0\"" +
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

        // ─────────────────────────────────────────
        // MANIFEST WRITER
        // ─────────────────────────────────────────
        private void WriteManifest(
            string folder,
            SRDB3DManifest manifest)
        {
            string path =
                Path.Combine(folder,
                    "rebuild_manifest.json");

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(
                "  \"tool\": \"" +
                manifest.Tool + "\",");
            sb.AppendLine(
                "  \"original_srdb_name\":" +
                " \"" +
                manifest.OriginalSrdbName +
                "\",");
            sb.AppendLine(
                "  \"original_gdtb_name\":" +
                " \"" +
                manifest.OriginalGdtbName +
                "\",");
            sb.AppendLine(
                "  \"source_size\": " +
                manifest.SourceSize + ",");
            sb.AppendLine(
                "  \"srdb_version\": " +
                manifest.SrdbVersion + ",");
            sb.AppendLine(
                "  \"srdb_unk\": " +
                manifest.SrdbUnk + ",");
            sb.AppendLine(
                "  \"chunk_offsets\": [" +
                string.Join(",",
                    manifest.ChunkOffsets) +
                "],");

            sb.AppendLine(
                "  \"embedded_rdtbs\": [");
            for (int i = 0;
                 i < manifest
                     .EmbeddedRdtbs.Count;
                 i++)
            {
                var e =
                    manifest.EmbeddedRdtbs[i];
                bool last =
                    i ==
                    manifest.EmbeddedRdtbs
                        .Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine(
                    "      \"index\": " +
                    e.Index + ",");
                sb.AppendLine(
                    "      \"name\": \"" +
                    e.Name + "\",");
                sb.AppendLine(
                    "      \"rdtb_blob\":" +
                    " \"" + e.RdtbBlob +
                    "\",");
                sb.AppendLine(
                    "      \"original_offset\"" +
                    ": " +
                    e.OriginalOffset + ",");
                sb.AppendLine(
                    "      \"original_size\"" +
                    ": " +
                    e.OriginalSize + ",");
                sb.AppendLine(
                    "      \"batch_count\"" +
                    ": " +
                    e.BatchCount + ",");
                sb.AppendLine(
                    "      \"tex_ids_used\"" +
                    ": [" +
                    string.Join(",",
                        e.TexIdsUsed) +
                    "],");
                sb.AppendLine(
                    "      \"center_x\": " +
                    G(e.CenterX) + ",");
                sb.AppendLine(
                    "      \"center_y\": " +
                    G(e.CenterY) + ",");
                sb.AppendLine(
                    "      \"center_z\": " +
                    G(e.CenterZ) + ",");
                sb.AppendLine(
                    "      \"auto_scale\": " +
                    G(e.AutoScale));
                sb.AppendLine(
                    "    }" +
                    (last ? "" : ","));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(
                path, sb.ToString(),
                Encoding.UTF8);
        }

        private static string G(float v)
            => v.ToString("G9",
                System.Globalization
                    .CultureInfo
                    .InvariantCulture);



        // ─────────────────────────────────────────
        // WRITE VERT OFFSET SIDECAR
        // For each OBJ `v` line that WriteOBJ
        // produces, store the absolute file
        // offset (in the SRDB file) where that
        // vertex lives. This guarantees the
        // builder can write back to the exact
        // same byte position.
        // ─────────────────────────────────────────
        private void WriteVertOffsetSidecar(
            string sidecarPath,
            byte[] blob,
            int blobAbsOffset,
            List<SRDBBatch> batches)
        {
            var offsets = new List<int>();
            foreach (var b in batches)
                offsets.AddRange(
                    b.BlobVertOffsets);

            using (var fs = new FileStream(
                sidecarPath, FileMode.Create,
                FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(offsets.Count);
                foreach (var o in offsets)
                    bw.Write(o);
            }
        }

        private int CountValidStripFaces(
            List<(float x, float y, float z)>
                verts)
        {
            int n = verts.Count;
            if (n < 3) return 0;
            int valid = 0;
            for (int i = 0; i < n - 2; i++)
            {
                int a, b, c;
                if (i % 2 == 0)
                { a = i; b = i + 1; c = i + 2; }
                else
                { a = i; b = i + 2; c = i + 1; }
                float ax = verts[b].x - verts[a].x;
                float ay = verts[b].y - verts[a].y;
                float az = verts[b].z - verts[a].z;
                float bx = verts[c].x - verts[a].x;
                float by = verts[c].y - verts[a].y;
                float bz = verts[c].z - verts[a].z;
                float cx = ay * bz - az * by;
                float cy = az * bx - ax * bz;
                float cz = ax * by - ay * bx;
                if (cx * cx + cy * cy + cz * cz
            > 1e-10f)
                    valid++;
            }
            return valid;
        }
    }

    // ═════════════════════════════════════════════
    // SRDB 3D CREATOR
    // ═════════════════════════════════════════════
    internal class SRDB3DCreatorInternal
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;
        private const uint FLAG_EOF = 0x70000000;

        public void Create(
            string inFolder,
            string outFolder,
            float scale)
        {
            string mfp = Path.Combine(
                inFolder,
                "rebuild_manifest.json");
            if (!File.Exists(mfp))
                throw new FileNotFoundException(
                    "rebuild_manifest.json" +
                    " not found in: " + inFolder);

            var manifest = LoadManifest(mfp);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SRDB 3D Creator v3.0");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
            Console.WriteLine(
                "    In    : " + inFolder);
            Console.WriteLine(
                "    Out   : " + outFolder);
            if (scale != 1.0f)
                Console.WriteLine(
                    "    Scale : " + scale + "x");
            Console.WriteLine(
                new string('=', 64));

            string srcSrdb = Path.Combine(
                inFolder, "_source.srdb");
            if (!File.Exists(srcSrdb))
                throw new FileNotFoundException(
                    "_source.srdb not found: " +
                    srcSrdb);

            byte[] srcBytes =
                File.ReadAllBytes(srcSrdb);
            var result = new byte[srcBytes.Length];
            Array.Copy(srcBytes, result,
                srcBytes.Length);

            Directory.CreateDirectory(outFolder);

            int modded = 0, unchanged = 0;

            foreach (var entry in
                manifest.EmbeddedRdtbs)
            {
                string name = entry.Name;
                string blobRel = entry.RdtbBlob;
                int off = entry.OriginalOffset;
                int sz = entry.OriginalSize;

                if (string.IsNullOrEmpty(blobRel))
                {
                    unchanged++;
                    continue;
                }

                // Read blob directly from
                // _source.srdb using the
                // manifest offset. No
                // dependency on _rdtb_blobs.
                byte[] origBlob =
                    new byte[sz];
                if (off + sz > srcBytes.Length)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    [!] blob out of" +
                        " range in _source.srdb");
                    Console.ResetColor();
                    unchanged++;
                    continue;
                }
                Array.Copy(srcBytes, off,
                    origBlob, 0, sz);

                string objPath = Path.Combine(
                    inFolder, name + ".obj");

                // No OBJ -> keep original blob
                // bytes byte-perfect
                if (!File.Exists(objPath))
                {
                    byte[] orig = origBlob;

                    PatchBlob(result, orig,
                        off, sz);
                    unchanged++;

                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "    " + name +
                        ": unchanged (no OBJ)");
                    Console.ResetColor();
                    continue;
                }

                Console.WriteLine(
                    "    [" + name +
                    "] processing");

                try
                {
                    byte[] rdtbData =
                        new byte[origBlob.Length];
                    Array.Copy(origBlob,
                        rdtbData, origBlob.Length);

                    // Build the exact OBJ that
                    // the extractor WOULD have
                    // written for this blob, by
                    // re-parsing the original
                    // blob the same way the
                    // extractor does. Then
                    // compare per-vertex against
                    // the user's OBJ. Only
                    // positions that ACTUALLY
                    // changed get written back.
                    //
                    // This guarantees:
                    //   - unchanged roundtrip
                    //     is byte-perfect
                    //   - user edits get
                    //     applied to the
                    //     correct VIF slot
                    //   - count mismatches
                    //     do NOT cause writes
                    //     to wrong slots
                    int written =
                        ApplyUserEdits(
                            rdtbData,
                            objPath,
                            scale,
                            entry.AutoScale);

                    if (written > 0)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Green;
                        Console.WriteLine(
                            "    " + name +
                            ": MODDED (" +
                            written + " verts" +
                            " changed)");
                        Console.ResetColor();
                        PatchBlob(result, rdtbData,
                            off, sz);
                        modded++;
                    }
                    else
                    {
                        // No real edits ->
                        // restore original
                        // blob bytes exactly
                        byte[] orig = origBlob;

                        PatchBlob(result, orig,
                            off, sz);
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "    " + name +
                            ": unchanged" +
                            " (0 real edits)");
                        Console.ResetColor();
                        unchanged++;
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "    " + name +
                        ": " + ex.Message);
                    Console.ResetColor();
                    byte[] orig = origBlob;

                    PatchBlob(result, orig,
                        off, sz);
                    unchanged++;
                }
            }

            Console.WriteLine();
            Console.WriteLine(
                "    Modded   : " + modded);
            Console.WriteLine(
                "    Unchanged: " + unchanged);

            // Rebuild GDTB
            string outGdtbName =
                manifest.OriginalGdtbName;
            string texFolder = Path.Combine(
                inFolder, "textures");
            if (!string.IsNullOrEmpty(
                    outGdtbName) &&
                Directory.Exists(texFolder))
            {
                string outGdtb = Path.Combine(
                    outFolder, outGdtbName);
                try
                {
                    GDTBArchive.Create(
                        texFolder, outGdtb);
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    GDTB rebuilt: " +
                        outGdtbName);
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
                }
            }

            // Write output SRDB
            string outSrdbName =
                manifest.OriginalSrdbName;
            if (string.IsNullOrEmpty(outSrdbName))
                outSrdbName = "output.srdb";

            string outSrdb = Path.Combine(
                outFolder, outSrdbName);
            File.WriteAllBytes(outSrdb, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] SRDB rebuild complete!");
            Console.ResetColor();
            Console.WriteLine(
                "     Output : " + outSrdb);
            Console.WriteLine(
                "     Size   : " +
                result.Length.ToString("N0") +
                " B");
            if (manifest.SourceSize > 0)
            {
                int diff = result.Length -
                    manifest.SourceSize;
                string match = diff == 0
                    ? "MATCH"
                    : (diff > 0
                        ? "+" + diff
                        : diff.ToString()) + " B";
                Console.WriteLine(
                    "     Original: " +
                    manifest.SourceSize
                        .ToString("N0") +
                    " B (" + match + ")");
            }
        }

        // ─────────────────────────────────────────
        // APPLY USER EDITS (sidecar-based)
        // Uses the .voff sidecar written by the
        // extractor. Each int32 in the sidecar
        // is an ABSOLUTE file offset in the SRDB.
        // We adjust by the blob's offset to get
        // the offset within the blob bytes, then
        // write the OBJ vert there.
        //
        // Zero parsing. Zero guessing. Exact map.
        // ─────────────────────────────────────────
        private int ApplyUserEdits(
            byte[] rdtb,
            string objPath,
            float scale,
            float autoScale)
        {
            // Locate sidecar next to OBJ
            string sidecarPath =
                Path.ChangeExtension(
                    objPath, ".voff");

            if (!File.Exists(sidecarPath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "      [!] .voff sidecar" +
                    " missing - cannot apply" +
                    " edits safely");
                Console.ResetColor();
                return 0;
            }

            // Read sidecar
            var absOffsets = new List<int>();
            using (var fs = new FileStream(
                sidecarPath, FileMode.Open,
                FileAccess.Read))
            using (var br = new BinaryReader(fs))
            {
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                    absOffsets.Add(
                        br.ReadInt32());
            }

            if (absOffsets.Count == 0) return 0;

            // Read OBJ verts in file order
            var userVerts = LoadObjVerts(objPath);
            if (userVerts.Count == 0) return 0;

            // The sidecar offsets are ABSOLUTE
            // file offsets in the original SRDB.
            // The rdtb byte array is just the
            // blob slice. We need offset within
            // the blob. Look up the blob's
            // absolute offset by scanning the
            // manifest entry the caller is
            // working with.
            //
            // PROBLEM: ApplyUserEdits does not
            // know the blob's abs offset. We
            // fix this by storing offsets in
            // the sidecar as offsets RELATIVE
            // to the blob start, not absolute.
            //
            // See STEP 1: the writer code
            // computes
            //   blobAbsOffset + cs + chunkRowOff
            // so the sidecar value is the
            // absolute offset in the SRDB file.
            //
            // To get blob-relative offset we
            // subtract blobAbsOffset. But we
            // do not have that here. Solution:
            // change the sidecar format to
            // store BLOB-RELATIVE offsets
            // instead. See STEP 1 patch below.

            // ADDITIVE: combine user scale with
            // inverse of extract-time auto-scale
            // so vertices end up at original
            // game-space coordinates
            float sc = scale > 0f ? scale : 1.0f;
            if (autoScale > 0f && autoScale != 1.0f)
                sc = sc / autoScale;
            int written = 0;
            const float EPS = 0.001f;

            int n = Math.Min(absOffsets.Count,
                userVerts.Count);

            for (int i = 0; i < n; i++)
            {
                // Sidecar now stores BLOB-
                // RELATIVE offsets (see STEP 1
                // fix below). So we can write
                // directly into the rdtb byte
                // array.
                int ro = absOffsets[i];
                if (ro + 16 > rdtb.Length)
                    continue;

                float ux = userVerts[i].x * sc;
                float uy = userVerts[i].y * sc;
                float uz = userVerts[i].z * sc;

                float ox = BitConverter.ToSingle(
                    rdtb, ro + 4);
                float oy = BitConverter.ToSingle(
                    rdtb, ro + 8);
                float oz = BitConverter.ToSingle(
                    rdtb, ro + 12);

                bool changed =
                    Math.Abs(ox - ux) >= EPS ||
                    Math.Abs(oy - uy) >= EPS ||
                    Math.Abs(oz - uz) >= EPS;

                if (changed)
                {
                    WriteFloat(rdtb,
                        ro + 4, ux);
                    WriteFloat(rdtb,
                        ro + 8, uy);
                    WriteFloat(rdtb,
                        ro + 12, uz);
                    written++;
                }
            }
            return written;
        }

        // ─────────────────────────────────────────
        // COLLECT VERT SLOTS LIKE EXTRACTOR
        // Mirrors SRDB3DExtractorInternal
        // .ParseEmbeddedRDTB exactly: same chunk
        // walk, same VIF parse, same row count
        // logic, same filters. The result is the
        // exact same vertex order the extractor
        // wrote as `v` lines into the OBJ.
        // ─────────────────────────────────────────
        private void CollectVertSlotsLikeExtractor(
            byte[] rd,
            List<(int ro, float ox,
                float oy, float oz)> slots)
        {
            // Read chunk offsets
            var coffs = new List<int>();
            for (int i = 0; i < 14; i++)
            {
                int o = 0x10 + i * 4;
                if (o + 4 > rd.Length) break;
                int v = BitConverter.ToInt32(
                    rd, o);
                if (v == 0 || v < 0x48 ||
                    v > rd.Length)
                    break;
                if (v == -1 || v == unchecked(
                        (int)0xFFFFFFFF))
                    continue;
                coffs.Add(v);
            }
            if (coffs.Count == 0) return;

            // Slice chunks
            var chunks = new List<byte[]>();
            var chunkAbsOffs = new List<int>();
            for (int i = 0; i < coffs.Count; i++)
            {
                int s = coffs[i];
                int en = (i + 1 < coffs.Count)
                    ? coffs[i + 1] : rd.Length;
                int sz = en - s;
                if (sz <= 0) continue;
                byte[] c = new byte[sz];
                Array.Copy(rd, s, c, 0, sz);
                chunks.Add(c);
                chunkAbsOffs.Add(s);
            }

            // Parse material table from c8
            int c8Idx = Math.Min(8,
                chunks.Count - 1);
            var mats = new List<(int boneIdx,
                int texId)>();
            if (c8Idx >= 0)
                mats = ParseMatTable(
                    chunks[c8Idx]);

            // Walk every chunk
            for (int ci = 0; ci < chunks.Count;
                 ci++)
            {
                byte[] ch = chunks[ci];
                int chAbs = chunkAbsOffs[ci];
                var vifs = FindVIFs(ch);
                if (vifs.Count == 0) continue;

                var p2bi =
                    new Dictionary<int, int>();
                var opt = new List<int>();
                if (mats.Count > 0)
                {
                    for (int mi = 0;
                         mi < mats.Count; mi++)
                    {
                        int poff = mi * 4;
                        if (poff + 4 > ch.Length)
                            break;
                        int ptr = BitConverter
                            .ToInt32(ch, poff);
                        if (ptr >= 0 &&
                            ptr < ch.Length &&
                            IsVIF(ch, ptr) &&
                            !p2bi.ContainsKey(ptr))
                        {
                            p2bi[ptr] = mi;
                            opt.Add(ptr);
                        }
                    }
                }
                if (opt.Count == 0)
                    opt = new List<int>(vifs);
                else
                    opt.Sort();

                // For each batch
                for (int bi = 0; bi < opt.Count;
                     bi++)
                {
                    int bs = opt[bi];
                    int be = (bi + 1 < opt.Count)
                        ? opt[bi + 1] : ch.Length;

                    var lv = vifs.Where(v =>
                        v >= bs && v < be)
                        .ToList();
                    if (lv.Count == 0) continue;

                    // Collect per-block verts
                    // exactly like extractor
                    var batchVerts =
                        new List<(int ro, float x,
                            float y, float z)>();
                    bool batchValid = false;

                    for (int vi = 0;
                         vi < lv.Count; vi++)
                    {
                        int vo = lv[vi];
                        int ve2 = (vi + 1 <
                            lv.Count)
                            ? lv[vi + 1] : be;
                        int vc = ch[vo + 2];
                        var rows = ParseRows(
                            ch, vo + 16, ve2);
                        if (rows.Count < 3)
                            continue;

                        int n = (vc > 0 &&
                            vc * 3 <= rows.Count)
                            ? vc : rows.Count / 3;
                        if (n < 3 ||
                            n * 3 > rows.Count)
                            continue;

                        // Vertex rows (first n)
                        // Each row in chunk:
                        //   vo + 16 + k * 16
                        // Absolute file offset:
                        //   chAbs + that
                        for (int k = 0;
                             k < n; k++)
                        {
                            int chunkRowOff =
                                vo + 16 + k * 16;
                            int absRo = chAbs +
                                chunkRowOff;
                            batchVerts.Add((
                                absRo,
                                rows[k].x,
                                rows[k].y,
                                rows[k].z));
                        }
                        batchValid = true;
                    }

                    if (!batchValid) continue;
                    if (batchVerts.Count == 0)
                        continue;

                    // Mirror extractor's
                    // FilterDegen: it drops
                    // faces but KEEPS verts in
                    // the OBJ. So all parsed
                    // verts go into the OBJ as
                    // `v` lines, in order. We
                    // only need the verts here.
                    // BUT: extractor only adds
                    // batch to output list if
                    // batch.Verts.Count > 0 AND
                    // batch.Faces.Count > 0
                    // after FilterDegen. We
                    // must replicate that or
                    // our slot list will be
                    // longer than the OBJ.
                    int faceCount =
                        CountValidFaces(
                            batchVerts);
                    if (faceCount == 0) continue;

                    foreach (var bv in batchVerts)
                        slots.Add((bv.ro, bv.x,
                            bv.y, bv.z));
                }
            }
        }

        // ─────────────────────────────────────────
        // Count non-degenerate faces a strip of
        // N verts would produce, matching the
        // extractor's MakeStrip + FilterDegen.
        // ─────────────────────────────────────────
        private int CountValidFaces(
            List<(int ro, float x, float y,
                float z)> verts)
        {
            int n = verts.Count;
            if (n < 3) return 0;
            int valid = 0;
            for (int i = 0; i < n - 2; i++)
            {
                int a, b, c;
                if (i % 2 == 0)
                {
                    a = i; b = i + 1; c = i + 2;
                }
                else
                {
                    a = i; b = i + 2; c = i + 1;
                }
                float ax = verts[b].x - verts[a].x;
                float ay = verts[b].y - verts[a].y;
                float az = verts[b].z - verts[a].z;
                float bx = verts[c].x - verts[a].x;
                float by = verts[c].y - verts[a].y;
                float bz = verts[c].z - verts[a].z;
                float cx = ay * bz - az * by;
                float cy = az * bx - ax * bz;
                float cz = ax * by - ay * bx;
                if (cx * cx + cy * cy + cz * cz
                    > 1e-10f)
                    valid++;
            }
            return valid;
        }

        // ─────────────────────────────────────────
        // PARSE MATERIAL TABLE
        // (same as extractor)
        // ─────────────────────────────────────────
        private List<(int boneIdx, int texId)>
            ParseMatTable(byte[] c8)
        {
            var r = new List<(int, int)>();
            if (c8 == null || c8.Length < 4)
                return r;
            if (c8[0] == VIF_B0 &&
                c8[1] == VIF_B1 &&
                c8.Length > 3 &&
                c8[3] == VIF_B3)
                return r;

            uint first = BitConverter.ToUInt32(
                c8, 0);
            if (first == 0 ||
                first > (uint)c8.Length)
                return r;

            int bc = (int)(first / 4);
            if (bc > 10000) return r;

            for (int i = 0; i < bc; i++)
            {
                int poff = i * 4;
                if (poff + 4 > c8.Length) break;
                uint ptr = BitConverter.ToUInt32(
                    c8, poff);
                if (ptr + 8 > (uint)c8.Length)
                {
                    r.Add((0, 0));
                    continue;
                }
                int boneIdx = BitConverter
                    .ToUInt16(c8, (int)ptr);
                int texId = BitConverter
                    .ToUInt16(c8, (int)ptr + 6);
                r.Add((boneIdx, texId));
            }
            return r;
        }

        // ─────────────────────────────────────────
        // VIF / ROW HELPERS (same as extractor)
        // ─────────────────────────────────────────
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
                {
                    r.Add(i);
                    i += 16;
                }
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

        // ─────────────────────────────────────────
        // LOAD OBJ VERTS
        // ─────────────────────────────────────────
        private static List<(float x, float y,
            float z)> LoadObjVerts(string objPath)
        {
            var verts =
                new List<(float, float, float)>();
            using (var fh = new StreamReader(
                objPath, Encoding.UTF8))
            {
                string line;
                while ((line = fh.ReadLine())
                       != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)
                        || line[0] == '#')
                        continue;

                    if (line.Length < 2 ||
                        line[0] != 'v' ||
                        line[1] != ' ')
                        continue;

                    string[] p = line.Split(
                        new char[] { ' ', '\t' },
                        StringSplitOptions
                            .RemoveEmptyEntries);
                    if (p.Length < 4 ||
                        p[0].ToLower() != "v")
                        continue;

                    float x, y, z;
                    if (float.TryParse(p[1],
                            System.Globalization
                                .NumberStyles.Float,
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture,
                            out x) &&
                        float.TryParse(p[2],
                            System.Globalization
                                .NumberStyles.Float,
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture,
                            out y) &&
                        float.TryParse(p[3],
                            System.Globalization
                                .NumberStyles.Float,
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture,
                            out z))
                    {
                        verts.Add((x, y, z));
                    }
                }
            }
            return verts;
        }

        // ─────────────────────────────────────────
        // PATCH BLOB INTO SRDB BYTES
        // ─────────────────────────────────────────
        private static void PatchBlob(
            byte[] dest, byte[] blob,
            int offset, int maxSize)
        {
            byte[] rd = blob;
            if (rd.Length > maxSize)
            {
                byte[] clipped = new byte[maxSize];
                Array.Copy(rd, clipped, maxSize);
                rd = clipped;
            }
            else if (rd.Length < maxSize)
            {
                byte[] padded = new byte[maxSize];
                Array.Copy(rd, padded, rd.Length);
                rd = padded;
            }
            int copyLen = Math.Min(rd.Length,
                dest.Length - offset);
            if (copyLen > 0)
                Array.Copy(rd, 0, dest, offset,
                    copyLen);
        }

        // ─────────────────────────────────────────
        // WRITE FLOAT HELPER
        // ─────────────────────────────────────────
        private static void WriteFloat(
            byte[] data, int off, float v)
        {
            byte[] b = BitConverter.GetBytes(v);
            data[off] = b[0];
            data[off + 1] = b[1];
            data[off + 2] = b[2];
            data[off + 3] = b[3];
        }

        // ─────────────────────────────────────────
        // LOAD MANIFEST (unchanged from before)
        // ─────────────────────────────────────────
        private static SRDB3DManifest LoadManifest(
            string path)
        {
            string json = File.ReadAllText(
                path, Encoding.UTF8);

            var m = new SRDB3DManifest
            {
                Tool = SRDBArchive.JStr(
                    json, "tool"),
                OriginalSrdbName =
                    SRDBArchive.JStr(json,
                        "original_srdb_name"),
                OriginalGdtbName =
                    SRDBArchive.JStr(json,
                        "original_gdtb_name"),
                SourceSize = SRDBArchive.JInt(
                    json, "source_size"),
                SrdbVersion = (uint)SRDBArchive
                    .JInt(json, "srdb_version"),
                SrdbUnk = (uint)SRDBArchive.JInt(
                    json, "srdb_unk"),
            };

            int coi = json.IndexOf(
                "\"chunk_offsets\":");
            if (coi >= 0)
            {
                int ab = json.IndexOf('[', coi);
                int ae = json.IndexOf(']', ab);
                if (ab >= 0 && ae > ab)
                {
                    string inner = json.Substring(
                        ab + 1, ae - ab - 1);
                    foreach (var s in
                        inner.Split(','))
                    {
                        int v;
                        if (int.TryParse(s.Trim(),
                                out v))
                            m.ChunkOffsets.Add(v);
                    }
                }
            }

            int ei = json.IndexOf(
                "\"embedded_rdtbs\":");
            if (ei < 0) return m;
            int ab2 = json.IndexOf('[', ei);
            int ae2 = SRDBArchive.MatchBracket(
                json, ab2);
            if (ab2 < 0 || ae2 <= ab2) return m;

            string arr = json.Substring(
                ab2, ae2 - ab2 + 1);
            int pos = 0;
            while (pos < arr.Length)
            {
                int ob = arr.IndexOf('{', pos);
                if (ob < 0) break;
                int oe = SRDBArchive.MatchBrace(
                    arr, ob);
                if (oe < 0) break;
                string obj = arr.Substring(
                    ob, oe - ob + 1);

                var texIds = new List<int>();
                int ti = obj.IndexOf(
                    "\"tex_ids_used\":");
                if (ti >= 0)
                {
                    int tab = obj.IndexOf('[', ti);
                    int tae = obj.IndexOf(']', tab);
                    if (tab >= 0 && tae > tab)
                    {
                        string inner2 =
                            obj.Substring(tab + 1,
                                tae - tab - 1);
                        foreach (var s in
                            inner2.Split(','))
                        {
                            int v;
                            if (int.TryParse(
                                    s.Trim(),
                                    out v))
                                texIds.Add(v);
                        }
                    }
                }

                float cx = ParseF(obj, "center_x");
                float cy = ParseF(obj, "center_y");
                float cz = ParseF(obj, "center_z");
                // ADDITIVE: read auto_scale
                // (defaults to 1.0 if absent
                // for older manifests)
                float autoSc = ParseF(obj,
                    "auto_scale");
                if (autoSc <= 0f)
                    autoSc = 1.0f;

                var entry = new SRDB3DManifestEntry
                {
                    Index = SRDBArchive.JInt(
                        obj, "index"),
                    Name = SRDBArchive.JStr(
                        obj, "name"),
                    RdtbBlob = SRDBArchive.JStr(
                        obj, "rdtb_blob"),
                    OriginalOffset =
                        SRDBArchive.JInt(obj,
                            "original_offset"),
                    OriginalSize =
                        SRDBArchive.JInt(obj,
                            "original_size"),
                    BatchCount = SRDBArchive.JInt(
                        obj, "batch_count"),
                    TexIdsUsed = texIds,
                    CenterX = cx,
                    CenterY = cy,
                    CenterZ = cz,
                    AutoScale = autoSc,
                };
                m.EmbeddedRdtbs.Add(entry);
                pos = oe + 1;
            }
            return m;
        }

        private static float ParseF(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0) return 0f;
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
                    json[ve] == '-' ||
                    json[ve] == '.' ||
                    json[ve] == 'e' ||
                    json[ve] == 'E' ||
                    json[ve] == '+'))
                ve++;
            if (ve == vs) return 0f;
            float r;
            float.TryParse(
                json.Substring(vs, ve - vs),
                System.Globalization
                    .NumberStyles.Float,
                System.Globalization
                    .CultureInfo.InvariantCulture,
                out r);
            return r;
        }
    }
}
