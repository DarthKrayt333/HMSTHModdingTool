using HMSTHModdingTool.IO;
using HMSTHModdingTool.GDTB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HMSTHModdingTool.SRDB
{
    /// <summary>
    /// Post-processor for x3d that fixes
    /// wrong per-batch texture assignments
    /// in the combined _obj folder output.
    ///
    /// Works in two modes:
    ///
    /// MODE A - SRDB source:
    ///   Called after SRDBArchive.Extract3D
    ///   on a .srdb file. Uses master
    ///   table from _source.srdb to find
    ///   each embedded RDTB blob and read
    ///   correct tex_ids.
    ///
    /// MODE B - RDTB source (standalone):
    ///   Called after Model3D.Extract on a
    ///   single .rdtb file (which may be
    ///   an extracted embedded_NN.rdtb).
    ///   Reads correct tex_ids directly
    ///   from that RDTB file.
    ///
    /// Only rewrites OBJ usemtl lines and
    /// the .mtl file. Does NOT touch
    /// vertices, UVs, normals, or faces.
    /// </summary>
    public static class SRDBEmbedObjTextureFixer
    {
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;

        // ═════════════════════════════════
        // MODE A: SRDB source
        // Called after SRDBArchive.Extract3D
        // ═════════════════════════════════
        public static void ApplyForSRDB(
            string objFolderPath)
        {
            if (!Directory.Exists(
                    objFolderPath))
                return;

            var objFiles =
                Directory.GetFiles(
                    objFolderPath,
                    "embedded_*.obj");
            if (objFiles.Length == 0)
                return;

            string srcSrdb = Path.Combine(
                objFolderPath,
                "_source.srdb");
            if (!File.Exists(srcSrdb))
                return;

            byte[] srdbData =
                File.ReadAllBytes(
                    srcSrdb);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Fixing embedded OBJ"
                + " texture assignments"
                + " (SRDB mode)");
            Console.ResetColor();

            var embeds =
                ParseSRDBEmbeds(srdbData);
            if (embeds.Count == 0)
                return;

            int totalFixed = 0;

            foreach (string objPath in
                objFiles)
            {
                string fn = Path
                    .GetFileNameWithoutExtension(
                        objPath);
                if (!fn.StartsWith(
                        "embedded_"))
                    continue;
                string ns =
                    fn.Substring(9);
                int embedIdx;
                if (!int.TryParse(
                        ns, out embedIdx))
                    continue;
                if (embedIdx < 0 ||
                    embedIdx >=
                        embeds.Count)
                    continue;

                var emb =
                    embeds[embedIdx];

                var batchTexIds =
                    GetTexIdsFromRDTB(
                        srdbData,
                        emb.AbsOffset,
                        emb.Size);

                if (batchTexIds.Count
                    == 0)
                    continue;

                bool ok =
                    RewriteObjAndMtl(
                        objPath,
                        batchTexIds);

                if (ok)
                {
                    totalFixed++;
                    Console
                        .ForegroundColor
                        = ConsoleColor
                            .Green;
                    Console.WriteLine(
                        "    [OK] "
                        + Path.GetFileName(
                            objPath) +
                        " ("
                        + batchTexIds
                            .Count
                        + " batches)");
                    Console.ResetColor();
                }
            }

            if (totalFixed > 0)
            {
                Console.ForegroundColor
                    = ConsoleColor.Green;
                Console.WriteLine(
                    "    Fixed "
                    + totalFixed +
                    " embedded OBJ"
                    + " file(s)");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════
        // MODE B: Standalone RDTB source
        // Called after Model3D.Extract on
        // an RDTB file. Reads the RDTB
        // directly and fixes the combined
        // OBJ named baseName.obj.
        // ═════════════════════════════════
        public static void ApplyForRDTB(
            string rdtbPath,
            string objFolderPath,
            string baseName)
        {
            if (!File.Exists(rdtbPath))
                return;
            if (!Directory.Exists(
                    objFolderPath))
                return;

            // The OBJ is named
            // baseName.obj inside the
            // _obj folder
            string objPath =
                Path.Combine(
                    objFolderPath,
                    baseName + ".obj");
            if (!File.Exists(objPath))
                return;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Fixing embedded OBJ"
                + " texture assignments"
                + " (RDTB mode)");
            Console.ResetColor();
            Console.WriteLine(
                "    OBJ : " + objPath);

            byte[] rdtbBytes =
                File.ReadAllBytes(
                    rdtbPath);

            // For standalone RDTB the
            // whole file IS the embed
            // (offset 0, full size)
            var batchTexIds =
                GetTexIdsFromRDTB(
                    rdtbBytes,
                    0,
                    rdtbBytes.Length);

            if (batchTexIds.Count == 0)
            {
                Console
                    .ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [SKIP] Could"
                    + " not read"
                    + " material table");
                Console.ResetColor();
                return;
            }

            bool ok =
                RewriteObjAndMtl(
                    objPath,
                    batchTexIds);

            if (ok)
            {
                Console
                    .ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    [OK] Fixed ("
                    + batchTexIds.Count
                    + " batches)");
                Console.ResetColor();
            }
            else
            {
                Console
                    .ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [SKIP] No"
                    + " changes needed");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════
        // BACKWARD COMPAT ALIAS
        // Old signature that only knew
        // about SRDB folders
        // ═════════════════════════════════
        public static void Apply(
            string objFolderPath)
        {
            ApplyForSRDB(objFolderPath);
        }

        // ═════════════════════════════════
        // Small embed descriptor
        // ═════════════════════════════════
        private class EmbedInfo
        {
            public int AbsOffset;
            public int Size;
        }

        // ═════════════════════════════════
        // Parse SRDB master table
        // ═════════════════════════════════
        private static List<EmbedInfo>
            ParseSRDBEmbeds(byte[] data)
        {
            var result =
                new List<EmbedInfo>();

            if (data.Length < 16)
                return result;
            if (data[0] != 0x53 ||
                data[1] != 0x52 ||
                data[2] != 0x44 ||
                data[3] != 0x42)
                return result;

            uint firstOff =
                BitConverter.ToUInt32(
                    data, 0x0C);
            var chunkOffs =
                new List<uint>();
            int pos = 0x0C;
            while (pos + 4 <=
                (int)firstOff)
            {
                uint v =
                    BitConverter.ToUInt32(
                        data, pos);
                if (v == 0) break;
                if (v > (uint)
                    data.Length) break;
                chunkOffs.Add(v);
                pos += 4;
            }
            if (chunkOffs.Count < 3)
                return result;

            uint c2Start = chunkOffs[2];
            uint masterSize =
                BitConverter.ToUInt32(
                    data, (int)c2Start);

            var masterPtrs =
                new List<uint>();
            pos = (int)c2Start;
            while (pos < (int)(c2Start +
                masterSize))
            {
                uint v =
                    BitConverter.ToUInt32(
                        data, pos);
                if (v == 0) break;
                masterPtrs.Add(v);
                pos += 4;
            }

            for (int i = 0;
                 i < masterPtrs.Count;
                 i++)
            {
                uint s = c2Start
                    + masterPtrs[i];
                uint e;
                if (i + 1 <
                    masterPtrs.Count)
                    e = c2Start
                        + masterPtrs[i + 1];
                else
                    e = (uint)
                        data.Length;
                int sz = (int)(e - s);
                if (sz <= 0) continue;
                result.Add(
                    new EmbedInfo
                    {
                        AbsOffset = (int)s,
                        Size = sz,
                    });
            }
            return result;
        }

        // ═════════════════════════════════
        // Parse RDTB material table and
        // return tex_id for each batch
        // in batch-index order.
        // ═════════════════════════════════
        private static
            SortedDictionary<int, int>
            GetTexIdsFromRDTB(
                byte[] container,
                int rdtbOff,
                int rdtbSize)
        {
            var result =
                new SortedDictionary<int,
                    int>();

            if (rdtbOff + rdtbSize
                > container.Length)
                return result;

            // Copy blob for local
            // parsing (offsets in
            // slot table are relative
            // to blob start, not
            // container start)
            byte[] rd =
                new byte[rdtbSize];
            Array.Copy(container,
                rdtbOff, rd, 0,
                rdtbSize);

            // Verify RDTB magic
            if (rd.Length < 0x48)
                return result;
            if (rd[0] != 0x52 ||
                rd[1] != 0x44 ||
                rd[2] != 0x54 ||
                rd[3] != 0x42)
                return result;

            uint[] rawSlots =
                new uint[14];
            for (int i = 0; i < 14; i++)
            {
                if (0x10 + i * 4 + 4
                    > rd.Length)
                    break;
                rawSlots[i] =
                    BitConverter.ToUInt32(
                        rd, 0x10 + i * 4);
            }

            uint c8Off = rawSlots[8];
            if (c8Off == 0 ||
                c8Off == 0xFFFFFFFF ||
                c8Off >= (uint)rd.Length)
                return result;

            uint c8End = (uint)rd.Length;
            for (int i = 0; i < 14; i++)
            {
                uint v = rawSlots[i];
                if (v > c8Off &&
                    v < c8End &&
                    v != 0xFFFFFFFF &&
                    v != 0)
                    c8End = v;
            }

            int c8Len =
                (int)(c8End - c8Off);
            if (c8Len < 4) return result;

            // Skip if chunk 8 starts
            // with VIF (no mat table)
            if (rd[c8Off] == VIF_B0 &&
                rd[c8Off + 1] == VIF_B1
                && c8Len > 3 &&
                rd[c8Off + 3] == VIF_B3)
                return result;

            uint matFirst =
                BitConverter.ToUInt32(
                    rd, (int)c8Off);
            if (matFirst == 0 ||
                matFirst > (uint)c8Len)
                return result;

            int bc = (int)(matFirst / 4);
            if (bc > 10000)
                return result;

            for (int i = 0; i < bc; i++)
            {
                int ptrOff = (int)c8Off
                    + i * 4;
                if (ptrOff + 4 >
                    rd.Length) break;
                uint ptr =
                    BitConverter.ToUInt32(
                        rd, ptrOff);
                int recOff =
                    (int)c8Off + (int)ptr;
                if (recOff + 8 >
                    rd.Length) continue;

                int texId =
                    BitConverter.ToUInt16(
                        rd, recOff + 6);
                result[i] = texId;
            }

            return result;
        }

        // ═════════════════════════════════
        // Rewrite OBJ + MTL
        // ═════════════════════════════════
        private static bool
            RewriteObjAndMtl(
                string objPath,
                SortedDictionary<int, int>
                    batchTexIds)
        {
            if (!File.Exists(objPath))
                return false;

            string[] lines =
                File.ReadAllLines(
                    objPath);

            var newLines =
                new List<string>();
            int curBatch = -1;
            var texIdsUsed =
                new HashSet<int>();
            bool changed = false;

            for (int i = 0;
                 i < lines.Length; i++)
            {
                string line = lines[i];
                string t = line.Trim();

                if (t.StartsWith("g "))
                {
                    string[] parts =
                        t.Split(
                            new[]
                            { ' ', '\t' },
                            StringSplitOptions
                                .RemoveEmptyEntries);
                    if (parts.Length >= 2
                        && parts[1]
                            .StartsWith(
                                "batch_"))
                    {
                        string bs =
                            parts[1]
                                .Substring(6);
                        int bi;
                        if (int.TryParse(
                                bs, out bi))
                            curBatch = bi;
                        else
                            curBatch = -1;
                    }
                    else
                    {
                        curBatch = -1;
                    }
                    newLines.Add(line);
                }
                else if (t.StartsWith(
                        "usemtl ") &&
                    curBatch >= 0)
                {
                    // Look up correct
                    // tex_id for this
                    // batch. If not
                    // found, this OBJ
                    // group is really
                    // an extra VIF
                    // block of a real
                    // batch — inherit
                    // tex_id from the
                    // highest-indexed
                    // real batch that
                    // is <= curBatch.
                    int correctTexId
                        = -1;
                    int direct;
                    if (batchTexIds
                            .TryGetValue(
                                curBatch,
                                out
                                direct))
                    {
                        correctTexId
                            = direct;
                    }
                    else
                    {
                        int bestKey
                            = -1;
                        foreach (int k
                            in
                            batchTexIds
                                .Keys)
                        {
                            if (k <=
                                curBatch
                                && k >
                                bestKey)
                                bestKey
                                    = k;
                        }
                        if (bestKey
                            >= 0)
                        {
                            correctTexId
                                =
                                batchTexIds[
                                    bestKey];
                        }
                    }

                    if (correctTexId
                        >= 0)
                    {
                        string newMtl
                            =
                            "usemtl mat_"
                            +
                            correctTexId
                                .ToString(
                                    "D2");
                        if (t != newMtl)
                            changed
                                = true;
                        newLines.Add(
                            newMtl);
                        texIdsUsed.Add(
                            correctTexId);
                    }
                    else
                    {
                        newLines.Add(
                            line);
                    }
                }
                else
                {
                    newLines.Add(line);
                }
            }

            foreach (var kv in
                batchTexIds)
                texIdsUsed.Add(
                    kv.Value);

            if (!changed &&
                texIdsUsed.Count == 0)
                return false;

            File.WriteAllLines(objPath,
                newLines);

            string mtlPath =
                Path.ChangeExtension(
                    objPath, ".mtl");
            string baseName = Path
                .GetFileNameWithoutExtension(
                    objPath);

            WriteMtl(mtlPath, baseName,
                texIdsUsed);

            return true;
        }

        // ═════════════════════════════════
        // Write MTL file
        // ═════════════════════════════════
        private static void WriteMtl(
            string mtlPath,
            string baseName,
            HashSet<int> texIds)
        {
            var sorted = texIds.OrderBy(
                x => x).ToList();

            using (var sw =
                new StreamWriter(
                    mtlPath, false,
                    Encoding.UTF8))
            {
                sw.WriteLine("# " +
                    baseName + " MTL");
                sw.WriteLine();

                foreach (int tid in
                    sorted)
                {
                    sw.WriteLine(
                        "newmtl mat_" +
                        tid.ToString(
                            "D2"));
                    sw.WriteLine(
                        "Ka 1 1 1");
                    sw.WriteLine(
                        "Kd 1 1 1");
                    sw.WriteLine(
                        "Ks 0 0 0");
                    sw.WriteLine("Ns 10");
                    sw.WriteLine(
                        "illum 2");
                    sw.WriteLine(
                        "map_Kd textures/"
                        + "texture_" +
                        tid.ToString(
                            "D2") +
                        ".bmp");
                    sw.WriteLine();
                }
            }
        }
    }
}
