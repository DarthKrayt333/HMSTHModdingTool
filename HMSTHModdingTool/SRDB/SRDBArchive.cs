using HMSTHModdingTool.IO;
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
    // SRDB CHUNK INFO
    // ═════════════════════════════════════════════
    internal class SRDBChunkInfo
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public byte[] Data { get; set; }

        public string Filename =>
            $"{Index:D2}_srdb_chunk.bin";

        public bool HasVIFData
        {
            get
            {
                if (Data == null ||
                    Data.Length < 16)
                    return false;
                for (int i = 0;
                     i + 16 <= Data.Length;
                     i += 4)
                {
                    if (Data[i] == 0x00 &&
                        Data[i + 1] == 0x80 &&
                        Data[i + 3] == 0x6C)
                        return true;
                }
                return false;
            }
        }

        public int VIFBlockCount
        {
            get
            {
                if (Data == null) return 0;
                int count = 0;
                for (int i = 0;
                     i + 16 <= Data.Length;
                     i += 4)
                {
                    if (Data[i] == 0x00 &&
                        Data[i + 1] == 0x80 &&
                        Data[i + 3] == 0x6C)
                    {
                        count++;
                        i += 12;
                    }
                }
                return count;
            }
        }

        public int EmbeddedRDTBCount
        {
            get
            {
                if (Data == null) return 0;
                int count = 0;
                int pos = 0;
                while (pos < Data.Length - 4)
                {
                    int idx =
                        IndexOfRDTB(Data, pos);
                    if (idx < 0) break;
                    count++;
                    pos = idx + 4;
                }
                return count;
            }
        }

        private static int IndexOfRDTB(
            byte[] d, int start)
        {
            for (int i = start;
                 i <= d.Length - 4; i++)
            {
                if (d[i] == 0x52 &&
                    d[i + 1] == 0x44 &&
                    d[i + 2] == 0x54 &&
                    d[i + 3] == 0x42)
                    return i;
            }
            return -1;
        }

        public string HexPreview
        {
            get
            {
                if (Data == null ||
                    Data.Length == 0)
                    return "";
                int len =
                    Math.Min(8, Data.Length);
                var sb = new StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(
                        Data[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }

    // ═════════════════════════════════════════════
    // SRDB EMBEDDED RDTB INFO
    // ═════════════════════════════════════════════
    internal class SRDBEmbeddedRDTB
    {
        public int Index { get; set; }
        public int AbsOffset { get; set; }
        public int Size { get; set; }
        public int PtrCount { get; set; }
        public int BoneCount { get; set; }
        public int ChunkCount { get; set; }
        public byte[] RawData { get; set; }
        public List<int> ChunkOffsets
        { get; set; } =
            new List<int>();
        public List<int> TexIdsUsed
        { get; set; } =
            new List<int>();
        public int BatchCount { get; set; }

        public string Filename =>
            $"embedded_{Index:D2}.rdtb";
    }

    // ═════════════════════════════════════════════
    // SRDB MANIFEST
    // ═════════════════════════════════════════════
    internal class SRDBManifest
    {
        public string Tool { get; set; }
        public string SourceFile { get; set; }
        public int SourceSize { get; set; }
        public uint Version { get; set; }
        public uint UnkFlags { get; set; }
        public List<int> ChunkOffsets
        { get; set; } =
            new List<int>();
        public List<SRDBManifestChunk>
            Chunks
        { get; set; } =
            new List<SRDBManifestChunk>();
        public List<SRDBManifestEmbedded>
            EmbeddedRdtbs
        { get; set; } =
            new List<SRDBManifestEmbedded>();
        public string OriginalGdtbName
        { get; set; }
    }

    internal class SRDBManifestChunk
    {
        public int Index { get; set; }
        public string Filename { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public bool HasVIF { get; set; }
        public int VIFCount { get; set; }
        public int EmbeddedRdtbCount
        { get; set; }
    }

    internal class SRDBManifestEmbedded
    {
        public int Index { get; set; }
        public int AbsOffset { get; set; }
        public int Size { get; set; }
        public int PtrCount { get; set; }
        public int BoneCount { get; set; }
        public int ChunkCount { get; set; }
        public string Filename { get; set; }
        public List<int> TexIdsUsed
        { get; set; } =
            new List<int>();
        public int BatchCount { get; set; }
    }

    // ═════════════════════════════════════════════
    // SRDB FILE PARSER
    // ═════════════════════════════════════════════
    internal class SRDBFile
    {
        private static readonly byte[]
            SRDB_MAGIC =
            { 0x53, 0x52, 0x44, 0x42 };
        private static readonly byte[]
            RDTB_MAGIC =
            { 0x52, 0x44, 0x54, 0x42 };
        private static readonly byte[]
            EOF_TERM =
            {
                0x00, 0x00, 0x00, 0x70,
                0x00, 0x00, 0x00, 0x00,
            };

        public string Path { get; }
        public byte[] Data { get; private set; }
        public uint Version { get; private set; }
        public uint UnkFlags { get; private set; }
        public List<int> ChunkOffsets
        { get; private set; } =
            new List<int>();

        public SRDBFile(string path)
        {
            Path = path;
        }

        public void Load()
        {
            Data = File.ReadAllBytes(Path);
            if (Data.Length < 4 ||
                Data[0] != 0x53 ||
                Data[1] != 0x52 ||
                Data[2] != 0x44 ||
                Data[3] != 0x42)
                throw new InvalidDataException(
                    "Not SRDB: " + Path);

            Version =
                BitConverter.ToUInt32(
                    Data, 0x04);
            UnkFlags =
                BitConverter.ToUInt32(
                    Data, 0x08);

            // Read chunk offset table
            // First offset tells us where
            // data begins
            int pos = 0x0C;
            int firstOff = -1;
            ChunkOffsets.Clear();

            while (pos + 4 <= Data.Length)
            {
                if (firstOff >= 0 &&
                    pos >= firstOff)
                    break;
                uint v =
                    BitConverter.ToUInt32(
                        Data, pos);
                if (v == 0 ||
                    v > (uint)Data.Length)
                    break;
                if (v < 0x0C) break;
                if (firstOff < 0)
                    firstOff = (int)v;
                ChunkOffsets.Add((int)v);
                pos += 4;
            }
        }

        public List<SRDBChunkInfo> GetChunks()
        {
            var result =
                new List<SRDBChunkInfo>();
            var offs = ChunkOffsets;
            for (int i = 0;
                 i < offs.Count; i++)
            {
                int s = offs[i];
                int e =
                    (i + 1 < offs.Count)
                    ? offs[i + 1]
                    : Data.Length;
                int sz = e - s;
                if (sz <= 0) continue;
                byte[] c = new byte[sz];
                Array.Copy(Data, s, c, 0, sz);
                result.Add(new SRDBChunkInfo
                {
                    Index = i,
                    Offset = s,
                    Size = sz,
                    Data = c,
                });
            }
            return result;
        }

        // ── RDTB detection ───────────────────
        // Method 1: pointer table scan
        // (fast, accurate for map SRDBs where
        //  chunk 2 pointer table entries point
        //  directly to RDTB magic bytes)
        public List<SRDBEmbeddedRDTB>
            FindEmbeddedRDTBsViaPointers(
                List<SRDBChunkInfo> chunks)
        {
            var result =
                new List<SRDBEmbeddedRDTB>();
            var seen =
                new HashSet<int>();

            // The mesh chunk (usually chunk 2)
            // has a pointer table at its start.
            // Each pointer that resolves to
            // RDTB magic is an embedded RDTB.
            SRDBChunkInfo meshChunk = null;
            foreach (var c in chunks)
            {
                if (c.HasVIFData ||
                    c.EmbeddedRDTBCount > 0)
                {
                    meshChunk = c;
                    break;
                }
                // Heuristic: largest chunk
                // or last chunk
            }
            if (meshChunk == null &&
                chunks.Count > 0)
                meshChunk =
                    chunks[chunks.Count - 1];
            if (meshChunk == null) return result;

            byte[] cd = meshChunk.Data;
            if (cd == null || cd.Length < 8)
                return result;

            // Try reading pointer table
            uint first =
                BitConverter.ToUInt32(cd, 0);
            if (first > 0 &&
                first < (uint)cd.Length)
            {
                int pCount = (int)(first / 4);
                for (int pi = 0;
                     pi < pCount; pi++)
                {
                    int poff = pi * 4;
                    if (poff + 4 > cd.Length)
                        break;
                    uint ptr =
                        BitConverter.ToUInt32(
                            cd, poff);
                    if (ptr == 0) continue;

                    // Convert to absolute
                    // offset in SRDB file
                    int absOff =
                        meshChunk.Offset +
                        (int)ptr;
                    if (absOff < 0 ||
                        absOff + 8 >
                        Data.Length)
                        continue;

                    // Check for RDTB magic
                    if (Data[absOff] != 0x52 ||
                        Data[absOff + 1] != 0x44 ||
                        Data[absOff + 2] != 0x54 ||
                        Data[absOff + 3] != 0x42)
                        continue;

                    if (seen.Contains(absOff))
                        continue;
                    seen.Add(absOff);

                    var emb =
                        ParseEmbeddedRDTB(
                            absOff,
                            result.Count);
                    if (emb != null)
                        result.Add(emb);
                }
            }

            return result;
        }

        // Method 2: raw binary scan
        // (thorough, works for all file types)
        public List<SRDBEmbeddedRDTB>
            FindEmbeddedRDTBsRawScan()
        {
            var result =
                new List<SRDBEmbeddedRDTB>();
            var seen = new HashSet<int>();
            int pos = 0;

            while (pos < Data.Length - 4)
            {
                // Search for RDTB magic
                int idx = -1;
                for (int i = pos;
                     i <= Data.Length - 4;
                     i++)
                {
                    if (Data[i] == 0x52 &&
                        Data[i + 1] == 0x44 &&
                        Data[i + 2] == 0x54 &&
                        Data[i + 3] == 0x42)
                    {
                        idx = i;
                        break;
                    }
                }
                if (idx < 0) break;

                // Skip file's own header
                // if this is SRDB itself
                // (SRDB starts with SRDB not RDTB
                // so this is always a real embed)

                // Must be 4-byte aligned
                if (idx % 4 != 0)
                {
                    pos = idx + 1;
                    continue;
                }

                if (idx + 0x48 > Data.Length)
                {
                    pos = idx + 4;
                    continue;
                }

                if (seen.Contains(idx))
                {
                    pos = idx + 4;
                    continue;
                }

                // Validate RDTB header
                int pc =
                    BitConverter.ToUInt16(
                        Data, idx + 0x0C);
                int bc =
                    BitConverter.ToUInt16(
                        Data, idx + 0x0E);

                if (pc == 0 || pc > 10000 ||
                    bc > 1000)
                {
                    pos = idx + 4;
                    continue;
                }

                seen.Add(idx);
                var emb =
                    ParseEmbeddedRDTB(
                        idx, result.Count);
                if (emb != null)
                {
                    result.Add(emb);
                    pos = idx + emb.Size;
                }
                else
                {
                    pos = idx + 4;
                }
            }

            return result;
        }

        // Combined: pointer table first,
        // then raw scan for any missed
        public List<SRDBEmbeddedRDTB>
            FindAllEmbeddedRDTBs(
                List<SRDBChunkInfo> chunks)
        {
            var seen = new HashSet<int>();
            var result =
                new List<SRDBEmbeddedRDTB>();

            // Method 1: pointer table
            var fromPtrs =
                FindEmbeddedRDTBsViaPointers(
                    chunks);
            foreach (var e in fromPtrs)
            {
                if (!seen.Contains(e.AbsOffset))
                {
                    seen.Add(e.AbsOffset);
                    result.Add(e);
                }
            }

            // Method 2: raw scan
            var fromScan =
                FindEmbeddedRDTBsRawScan();
            foreach (var e in fromScan)
            {
                if (!seen.Contains(e.AbsOffset))
                {
                    seen.Add(e.AbsOffset);
                    // Re-index
                    e.Index = result.Count;
                    result.Add(e);
                }
            }

            // Re-index all
            for (int i = 0;
                 i < result.Count; i++)
                result[i].Index = i;

            return result;
        }

        private SRDBEmbeddedRDTB
            ParseEmbeddedRDTB(
                int absOff, int idx)
        {
            if (absOff + 0x48 > Data.Length)
                return null;

            int pc =
                BitConverter.ToUInt16(
                    Data, absOff + 0x0C);
            int bc =
                BitConverter.ToUInt16(
                    Data, absOff + 0x0E);

            if (pc == 0 || pc > 10000 ||
                bc > 1000)
                return null;

            // Read chunk offsets
            var coffs = new List<int>();
            for (int s = 0; s < 14; s++)
            {
                int o = absOff + 0x10 + s * 4;
                if (o + 4 > Data.Length) break;
                int v =
                    BitConverter.ToInt32(
                        Data, o);
                if (v == 0 || v < 0x48)
                    break;
                if (v == -1 ||
                    v == unchecked(
                        (int)0xFFFFFFFF))
                    continue;
                if (v > Data.Length - absOff)
                    break;
                coffs.Add(v);
            }

            // Find EOF terminator to get size
            int eofIdx = -1;
            for (int i =
                     absOff + 0x48;
                 i <= Data.Length - 8;
                 i++)
            {
                if (Data[i] == 0x00 &&
                    Data[i + 1] == 0x00 &&
                    Data[i + 2] == 0x00 &&
                    Data[i + 3] == 0x70 &&
                    Data[i + 4] == 0x00 &&
                    Data[i + 5] == 0x00 &&
                    Data[i + 6] == 0x00 &&
                    Data[i + 7] == 0x00)
                {
                    eofIdx = i;
                    break;
                }
            }

            int sz;
            if (eofIdx >= 0)
            {
                int end =
                    ((eofIdx + 16 + 15) / 16)
                    * 16;
                sz = end - absOff;
            }
            else
            {
                // No EOF: use next offset or end
                sz = Data.Length - absOff;
            }

            if (sz < 64 ||
                absOff + sz > Data.Length)
                sz = Data.Length - absOff;

            byte[] raw = new byte[sz];
            Array.Copy(
                Data, absOff, raw, 0, sz);

            // Parse material table to get
            // tex_ids used
            var texIds = new List<int>();
            int batchCount = 0;
            if (coffs.Count > 8)
            {
                int c8off =
                    absOff + coffs[8];
                int c8end =
                    coffs.Count > 9
                    ? absOff + coffs[9]
                    : absOff + sz;
                int c8sz = c8end - c8off;
                if (c8off + 4 <= Data.Length
                    && c8sz > 4)
                {
                    var texSet =
                        ParseMatTexIds(
                            Data, c8off, c8sz);
                    texIds = texSet.Keys
                        .OrderBy(x => x)
                        .ToList();
                    batchCount = texSet.Count > 0
                        ? texSet.Values.Sum()
                        : 0;
                }
            }

            return new SRDBEmbeddedRDTB
            {
                Index = idx,
                AbsOffset = absOff,
                Size = sz,
                PtrCount = pc,
                BoneCount = bc,
                ChunkCount = coffs.Count,
                RawData = raw,
                ChunkOffsets = coffs,
                TexIdsUsed = texIds,
                BatchCount = batchCount,
            };
        }

        private static Dictionary<int, int>
            ParseMatTexIds(
                byte[] data,
                int c8off, int c8sz)
        {
            var result =
                new Dictionary<int, int>();
            if (c8sz < 4) return result;

            // Guard: check if starts with VIF
            if (data[c8off] == 0x00 &&
                data[c8off + 1] == 0x80 &&
                c8off + 3 < data.Length &&
                data[c8off + 3] == 0x6C)
                return result;

            uint first =
                BitConverter.ToUInt32(
                    data, c8off);
            if (first == 0 ||
                first > (uint)c8sz)
                return result;

            int bc = (int)(first / 4);
            if (bc > 10000) return result;

            for (int i = 0; i < bc; i++)
            {
                int poff = c8off + i * 4;
                if (poff + 4 > data.Length)
                    break;
                uint ptr =
                    BitConverter.ToUInt32(
                        data, poff);
                if (ptr + 8 > (uint)c8sz)
                    continue;
                int recOff =
                    c8off + (int)ptr;
                if (recOff + 8 > data.Length)
                    continue;
                int texId =
                    BitConverter.ToUInt16(
                        data, recOff + 6);
                if (!result.ContainsKey(texId))
                    result[texId] = 0;
                result[texId]++;
            }
            return result;
        }
    }

    // ═════════════════════════════════════════════
    // SRDB ARCHIVE  — public entry point
    // Called from Program.cs
    // ═════════════════════════════════════════════
    public static class SRDBArchive
    {

        // ════════════════════════════════════════
        // XSRDB - Extract embedded RDTBs from SRDB
        // Each blob saved as embedded_NN.rdtb plus
        // _layout.txt for byte-exact patch back.
        // ════════════════════════════════════════
        public static void Extract(
            string srdbPath,
            string outFolder)
        {
            if (!File.Exists(srdbPath))
                throw new FileNotFoundException(
                    "SRDB not found: " +
                    srdbPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extract Embedded RDTBs");
            Console.ResetColor();
            Console.WriteLine(
                "    SRDB : " +
                System.IO.Path.GetFileName(
                    srdbPath));
            Console.WriteLine(
                "    Out  : " + outFolder);

            Directory.CreateDirectory(
                outFolder);

            var srdb = new SRDBFile(srdbPath);
            srdb.Load();
            var chunks = srdb.GetChunks();
            var embedded =
                srdb.FindAllEmbeddedRDTBs(
                    chunks);

            Console.WriteLine(
                "    Found: " +
                embedded.Count +
                " embedded RDTBs");
            Console.WriteLine();

            File.Copy(srdbPath,
                System.IO.Path.Combine(
                    outFolder,
                    "_source.srdb"),
                true);

            var layout = new StringBuilder();
            layout.AppendLine(
                "# SRDB embedded RDTB layout");
            layout.AppendLine(
                "# format: index offset" +
                " size filename");
            layout.AppendLine(
                "source=" +
                System.IO.Path.GetFileName(
                    srdbPath));
            layout.AppendLine(
                "source_size=" +
                srdb.Data.Length);

            foreach (var e in embedded)
            {
                string fn = "embedded_" +
                    e.Index.ToString("D2") +
                    ".rdtb";
                string outFile =
                    System.IO.Path.Combine(
                        outFolder, fn);

                byte[] blob =
                    new byte[e.Size];
                int copyLen = Math.Min(
                    e.Size,
                    srdb.Data.Length -
                        e.AbsOffset);
                Array.Copy(srdb.Data,
                    e.AbsOffset,
                    blob, 0, copyLen);

                File.WriteAllBytes(
                    outFile, blob);

                layout.AppendLine(
                    e.Index + " " +
                    e.AbsOffset + " " +
                    e.Size + " " + fn);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    [" +
                    e.Index.ToString("D2") +
                    "] @0x" +
                    e.AbsOffset.ToString("X8") +
                    " (" +
                    e.Size.ToString("N0") +
                    " B) -> " + fn);
                Console.ResetColor();
            }

            File.WriteAllText(
                System.IO.Path.Combine(
                    outFolder,
                    "_layout.txt"),
                layout.ToString(),
                Encoding.UTF8);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Extracted " +
                embedded.Count +
                " RDTBs to " +
                outFolder);
            Console.ResetColor();
        }

        // ════════════════════════════════════════
        // CSRDB - Repack embedded RDTBs into SRDB
        // Preserves original SRDB structure
        // including gaps between RDTBs. Only
        // shifts blobs if they grow beyond their
        // original slot. Patches header offset
        // table for any shifted blobs.
        // ════════════════════════════════════════
        public static void Create(
            string inFolder,
            string outSrdb)
        {
            string layoutPath =
                System.IO.Path.Combine(
                    inFolder, "_layout.txt");
            if (!File.Exists(layoutPath))
                throw new FileNotFoundException(
                    "_layout.txt not found" +
                    " in: " + inFolder);

            string srcPath =
                System.IO.Path.Combine(
                    inFolder, "_source.srdb");
            if (!File.Exists(srcPath))
                throw new FileNotFoundException(
                    "_source.srdb not found" +
                    " in: " + inFolder);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Rebuild SRDB" +
                " (preserve-gaps mode)");
            Console.ResetColor();
            Console.WriteLine(
                "    In  : " + inFolder);
            Console.WriteLine(
                "    Out : " + outSrdb);

            byte[] srcBytes =
                File.ReadAllBytes(srcPath);

            // Parse layout
            var entries = new List<(int idx,
                int origOff, int origSz,
                string fn)>();
            string[] lines =
                File.ReadAllLines(layoutPath);
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line)
                    || line.StartsWith("#") ||
                    line.StartsWith("source"))
                    continue;
                string[] parts = line.Split(
                    new char[] { ' ', '\t' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                int idx, off, sz;
                if (!int.TryParse(parts[0],
                        out idx)) continue;
                if (!int.TryParse(parts[1],
                        out off)) continue;
                if (!int.TryParse(parts[2],
                        out sz)) continue;
                entries.Add((idx, off, sz,
                    parts[3]));
            }

            entries.Sort((a, b) =>
                a.origOff.CompareTo(b.origOff));

            // Load all blobs first
            var blobs = new List<byte[]>();
            foreach (var e in entries)
            {
                string blobPath =
                    System.IO.Path.Combine(
                        inFolder, e.fn);
                if (File.Exists(blobPath))
                    blobs.Add(
                        File.ReadAllBytes(
                            blobPath));
                else
                {
                    byte[] orig =
                        new byte[e.origSz];
                    int cl = Math.Min(
                        e.origSz,
                        srcBytes.Length -
                            e.origOff);
                    Array.Copy(srcBytes,
                        e.origOff,
                        orig, 0, cl);
                    blobs.Add(orig);
                }
            }

            // Compute new offsets:
            // - Try to keep original offset
            // - If blob grew, shift this and
            //   subsequent ones forward by the
            //   minimum amount needed
            var newOffsets = new int[
                entries.Count];
            int shiftAccum = 0;
            for (int i = 0; i < entries.Count;
                 i++)
            {
                int desiredOff =
                    entries[i].origOff +
                    shiftAccum;
                newOffsets[i] = desiredOff;

                int newSz = blobs[i].Length;
                int origSlotEnd = i + 1 <
                    entries.Count
                    ? entries[i + 1].origOff
                    : srcBytes.Length;
                int origSlotSize =
                    origSlotEnd -
                    entries[i].origOff;

                // If blob exceeds its
                // original slot, increase
                // shift for everything after
                if (newSz > origSlotSize)
                {
                    int extra =
                        newSz - origSlotSize;
                    // Align extra to 16
                    if (extra % 16 != 0)
                        extra += 16 -
                            (extra % 16);
                    shiftAccum += extra;
                }
            }

            // Final file size
            int origFileSize =
                srcBytes.Length;
            int finalSize =
                origFileSize + shiftAccum;

            // Build result starting as
            // a copy of source (preserves
            // all gap data)
            byte[] result =
                new byte[finalSize];
            Array.Copy(srcBytes, 0,
                result, 0,
                Math.Min(origFileSize,
                    finalSize));

            // If file grew, the tail beyond
            // origFileSize stays as zeros.
            // If shifts occurred, we need to
            // slide all original data after
            // each shifted blob forward.
            //
            // Strategy: walk entries in
            // reverse and copy original gap
            // data into new positions. Then
            // overwrite each blob slot with
            // the new blob.

            // First, copy all gap data from
            // source to result at shifted
            // positions. Walking reverse so
            // we don't overwrite data we
            // still need to read.
            if (shiftAccum > 0)
            {
                // Build a list of "segments"
                // from the original file:
                // [header] [blob0] [gap0]
                // [blob1] [gap1] ... [blobN]
                // [tail]
                // Then re-place each one in
                // result at the new position.
                //
                // For simplicity: copy
                // entire tail of original
                // file to its new shifted
                // position. Walk entries
                // reverse.

                for (int i = entries.Count - 1;
                     i >= 0; i--)
                {
                    int origStart =
                        entries[i].origOff;
                    int origEnd = i + 1 <
                        entries.Count
                        ? entries[i + 1].origOff
                        : origFileSize;
                    int origLen =
                        origEnd - origStart;

                    int newStart =
                        newOffsets[i];

                    // Copy the entire
                    // original slot (blob
                    // + trailing gap) to
                    // the new position
                    if (newStart != origStart
                        && origLen > 0)
                    {
                        // Clamp to bounds
                        int copyLen = Math.Min(
                            origLen,
                            result.Length -
                                newStart);
                        if (copyLen > 0 &&
                            origStart +
                                copyLen <=
                                srcBytes.Length)
                        {
                            Array.Copy(srcBytes,
                                origStart,
                                result, newStart,
                                copyLen);
                        }
                    }
                }
            }

            // Now overwrite each blob slot
            // with the new blob bytes.
            // (Tail gap data was already
            // copied in the previous step.)
            for (int i = 0; i < entries.Count;
                 i++)
            {
                int newStart = newOffsets[i];
                byte[] b = blobs[i];
                int copyLen = Math.Min(
                    b.Length,
                    result.Length - newStart);
                if (copyLen > 0)
                    Array.Copy(b, 0,
                        result, newStart,
                        copyLen);

                // If the new blob is smaller
                // than the original slot,
                // we need to zero out the
                // leftover bytes from the
                // old blob (the gap stays).
                int origEnd = i + 1 <
                    entries.Count
                    ? entries[i + 1].origOff
                    : origFileSize;
                int origSlotSize =
                    origEnd -
                    entries[i].origOff;
                int newSlotSize = b.Length;

                if (newSlotSize < origSlotSize
                    && newStart + newSlotSize
                       + (origSlotSize -
                          newSlotSize)
                       <= result.Length)
                {
                    // Don't zero - keep
                    // any data that was
                    // there (it might be
                    // padding the game
                    // expects). Just leave
                    // it as copied from
                    // source.
                }
            }

            // Patch header offset table for
            // any blob that moved
            int headerEnd = entries.Count > 0
                ? entries[0].origOff
                : srcBytes.Length;

            var offsetMap =
                new Dictionary<int, int>();
            for (int i = 0;
                 i < entries.Count; i++)
            {
                if (entries[i].origOff !=
                    newOffsets[i])
                    offsetMap[
                        entries[i].origOff]
                        = newOffsets[i];
            }

            int patches = 0;
            for (int p = 0;
                 p + 4 <= headerEnd; p += 4)
            {
                int v = BitConverter.ToInt32(
                    result, p);
                if (v < 0x80) continue;
                if (offsetMap.TryGetValue(v,
                        out int newV) &&
                    newV != v)
                {
                    byte[] nb =
                        BitConverter.GetBytes(
                            newV);
                    result[p] = nb[0];
                    result[p + 1] = nb[1];
                    result[p + 2] = nb[2];
                    result[p + 3] = nb[3];
                    patches++;
                }
            }

            // Write output
            string outDir =
                System.IO.Path.GetDirectoryName(
                    System.IO.Path.GetFullPath(
                        outSrdb));
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(
                    outDir);

            File.WriteAllBytes(
                outSrdb, result);

            // Report
            Console.WriteLine();
            int shifted = 0;
            for (int i = 0; i < entries.Count;
                 i++)
            {
                int oldOff =
                    entries[i].origOff;
                int newOff = newOffsets[i];
                int blobSz = blobs[i].Length;
                int origSz = entries[i].origSz;

                string change;
                if (blobSz == origSz)
                    change = "same size";
                else if (blobSz > origSz)
                    change = "+" +
                        (blobSz - origSz) +
                        " B";
                else
                    change =
                        (blobSz - origSz)
                            .ToString() + " B";

                string moved = oldOff != newOff
                    ? " SHIFTED"
                    : "";
                if (oldOff != newOff)
                    shifted++;

                Console.ForegroundColor =
                    oldOff != newOff
                    ? ConsoleColor.Yellow
                    : ConsoleColor.Green;
                Console.WriteLine(
                    "    [" +
                    entries[i].idx
                        .ToString("D2") +
                    "] " + entries[i].fn +
                    "  0x" +
                    oldOff.ToString("X8") +
                    " -> 0x" +
                    newOff.ToString("X8") +
                    "  (" + change + ")" +
                    moved);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] SRDB rebuilt: " +
                outSrdb);
            Console.ResetColor();
            Console.WriteLine(
                "     Loaded blobs : " +
                entries.Count);
            Console.WriteLine(
                "     Shifted blobs: " +
                shifted);
            Console.WriteLine(
                "     Header patches: " +
                patches);
            Console.WriteLine(
                "     New size      : " +
                result.Length.ToString("N0") +
                " B");
            Console.WriteLine(
                "     Original size : " +
                srcBytes.Length
                    .ToString("N0") +
                " B");
            int diff =
                result.Length -
                srcBytes.Length;
            string ds = diff == 0
                ? "no change"
                : (diff > 0
                    ? "+" + diff
                    : diff.ToString()) +
                  " B";
            Console.WriteLine(
                "     Size delta    : " + ds);
        }


        // ── Extract raw chunks ───────────────────
        public static void Extract2(
            string srdbPath,
            string outFolder)
        {
            var srdb = new SRDBFile(srdbPath);
            srdb.Load();
            Directory.CreateDirectory(outFolder);

            var chunks = srdb.GetChunks();

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SRDB Extract: " +
                System.IO.Path.GetFileName(
                    srdbPath));
            Console.ResetColor();
            Console.WriteLine(
                $"    Chunks : {chunks.Count}");
            Console.WriteLine(
                $"    Size   : " +
                $"{srdb.Data.Length:N0} B");
            Console.WriteLine();

            foreach (var c in chunks)
            {
                string dest =
                    System.IO.Path.Combine(
                        outFolder,
                        c.Filename);
                File.WriteAllBytes(dest, c.Data);

                string vif =
                    c.HasVIFData
                    ? $" [VIF×{c.VIFBlockCount}]"
                    : "";
                string emb =
                    c.EmbeddedRDTBCount > 0
                    ? $" [RDTB×{c.EmbeddedRDTBCount}]"
                    : "";
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    [{c.Index:D2}] " +
                    $"{c.Filename,-28} " +
                    $"({c.Size,10:N0} B) " +
                    $"@ 0x{c.Offset:X8}" +
                    $"{vif}{emb}");
                Console.ResetColor();
            }

            // Write manifest
            var manifest = new SRDBManifest
            {
                Tool =
                    "HMSTHModdingTool v2.0",
                SourceFile =
                    System.IO.Path.GetFileName(
                        srdbPath),
                SourceSize =
                    srdb.Data.Length,
                Version = srdb.Version,
                UnkFlags = srdb.UnkFlags,
                ChunkOffsets =
                    new List<int>(
                        srdb.ChunkOffsets),
            };
            foreach (var c in chunks)
            {
                manifest.Chunks.Add(
                    new SRDBManifestChunk
                    {
                        Index = c.Index,
                        Filename = c.Filename,
                        Offset = c.Offset,
                        Size = c.Size,
                        HasVIF = c.HasVIFData,
                        VIFCount =
                            c.VIFBlockCount,
                        EmbeddedRdtbCount =
                            c.EmbeddedRDTBCount,
                    });
            }

            WriteManifest(
                outFolder, manifest,
                srdbPath);

            // Copy source
            File.Copy(srdbPath,
                System.IO.Path.Combine(
                    outFolder,
                    "_source.srdb"),
                true);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] SRDB extraction done!");
            Console.ResetColor();
            Console.WriteLine(
                "     Output: " + outFolder);
        }

        // ── Create (repack) SRDB ─────────────────
        public static void Create2(
            string inFolder,
            string srdbPath)
        {
            string mfp =
                System.IO.Path.Combine(
                    inFolder,
                    "srdb_manifest.json");
            if (!File.Exists(mfp))
                throw new FileNotFoundException(
                    "srdb_manifest.json not" +
                    " found in: " + inFolder);

            var manifest =
                ReadManifest(mfp);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SRDB Create: " +
                System.IO.Path.GetFileName(
                    srdbPath));
            Console.ResetColor();
            Console.WriteLine(
                $"    Chunks : " +
                $"{manifest.Chunks.Count}");

            // Load source SRDB header
            string srcSrdb =
                System.IO.Path.Combine(
                    inFolder, "_source.srdb");
            if (!File.Exists(srcSrdb))
                throw new FileNotFoundException(
                    "_source.srdb not found: " +
                    srcSrdb);

            byte[] srcData =
                File.ReadAllBytes(srcSrdb);

            // Load chunk files
            var chunkData =
                new List<byte[]>();
            foreach (var entry in
                manifest.Chunks.OrderBy(
                    c => c.Index))
            {
                string cp =
                    System.IO.Path.Combine(
                        inFolder,
                        entry.Filename);
                if (!File.Exists(cp))
                    throw new FileNotFoundException(
                        "Missing chunk: " +
                        entry.Filename);
                byte[] raw =
                    File.ReadAllBytes(cp);
                chunkData.Add(raw);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    [{entry.Index:D2}] " +
                    $"{entry.Filename,-28} " +
                    $"({raw.Length:N0} B)");
                Console.ResetColor();
            }

            // Reassemble SRDB
            byte[] result =
                AssembleSRDB(
                    srcData, chunkData,
                    manifest);

            File.WriteAllBytes(srdbPath, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] SRDB created: " +
                srdbPath);
            Console.ResetColor();
            Console.WriteLine(
                $"     Size    : " +
                $"{result.Length:N0} B");
            Console.WriteLine(
                $"     Original: " +
                $"{manifest.SourceSize:N0} B");
        }


        // ── Extract 3D models ────────────────────
        public static void Extract3D(
            string srdbPath,
            string gdtbPath,
            string baseName)
        {
            new SRDB3DExtractorInternal()
                .Extract(
                    srdbPath,
                    gdtbPath,
                    baseName);
        }

        // ── Create 3D (rebuild) ──────────────────
        public static void Create3D(
            string inFolder,
            string outFolder,
            float scale = 1.0f)
        {
            new SRDB3DCreatorInternal()
                .Create(
                    inFolder,
                    outFolder,
                    scale);
        }

        // ── Byte-for-byte verify ─────────────
        public static void Verify(
            string originalPath,
            string rebuiltPath)
        {
            byte[] orig =
                File.ReadAllBytes(
                    originalPath);
            byte[] reb =
                File.ReadAllBytes(rebuiltPath);
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Verify SRDB v2.0");
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
            Console.WriteLine(
                "    Original: " +
                System.IO.Path.GetFileName(
                    originalPath) +
                "  (" +
                orig.Length.ToString("N0") +
                " B)");
            Console.WriteLine(
                "    Rebuilt : " +
                System.IO.Path.GetFileName(
                    rebuiltPath) +
                "  (" +
                reb.Length.ToString("N0") +
                " B)");
            int minLen = Math.Min(
                orig.Length, reb.Length);
            int diffOff = -1;
            for (int i = 0; i < minLen; i++)
            {
                if (orig[i] != reb[i])
                {
                    diffOff = i;
                    break;
                }
            }
            if (diffOff == -1 &&
                orig.Length != reb.Length)
                diffOff = minLen;
            if (diffOff == -1)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "    IDENTICAL √");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "    First diff @ 0x" +
                    diffOff.ToString("X8"));
                Console.ResetColor();
                int total = 0;
                for (int i = 0;
                     i < minLen; i++)
                    if (orig[i] != reb[i])
                        total++;
                total += Math.Abs(
                    orig.Length - reb.Length);
                Console.WriteLine(
                    "    Total diff: " +
                    total.ToString("N0") +
                    " bytes");
            }
            Console.WriteLine(
                new string('=', 64));
        }

        // ── Info + embedded RDTB detect ──────────
        public static void Info(
            string srdbPath)
        {
            var srdb = new SRDBFile(srdbPath);
            srdb.Load();
            var chunks = srdb.GetChunks();
            var embedded =
                srdb.FindAllEmbeddedRDTBs(
                    chunks);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] SRDB Info v2.0: " +
                System.IO.Path.GetFileName(
                    srdbPath));
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 64));
            Console.WriteLine(
                $"    File    : " +
                System.IO.Path.GetFileName(
                    srdbPath));
            Console.WriteLine(
                $"    Size    : " +
                $"{srdb.Data.Length:N0} B");
            Console.WriteLine(
                $"    Version : " +
                $"0x{srdb.Version:X8}");
            Console.WriteLine(
                $"    Flags   : " +
                $"0x{srdb.UnkFlags:X8}");
            Console.WriteLine(
                $"    Chunks  : " +
                $"{chunks.Count}");
            Console.WriteLine(
                $"    Embedded" +
                $" RDTBs: " +
                $"{embedded.Count}");
            Console.WriteLine();

            // Chunk table
            Console.WriteLine(
                "    " +
                new string('-', 60));
            Console.WriteLine(
                $"    {"#",3}  " +
                $"{"OFFSET",10}  " +
                $"{"SIZE",10}  " +
                $"{"VIF",4}  " +
                $"{"RDTB",4}  " +
                $"PREVIEW");
            Console.WriteLine(
                "    " +
                new string('-', 60));

            foreach (var c in chunks)
            {
                string vif =
                    c.HasVIFData
                    ? c.VIFBlockCount
                        .ToString()
                        .PadLeft(4)
                    : "   -";
                string emb =
                    c.EmbeddedRDTBCount > 0
                    ? c.EmbeddedRDTBCount
                        .ToString()
                        .PadLeft(4)
                    : "   -";
                Console.WriteLine(
                    $"    [{c.Index,2}]  " +
                    $"0x{c.Offset:X8}  " +
                    $"0x{c.Size:X8}  " +
                    $"{vif}  " +
                    $"{emb}  " +
                    $"{c.HexPreview}");
            }
            Console.WriteLine(
                "    " +
                new string('-', 60));

            // Embedded RDTBs
            if (embedded.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Magenta;
                Console.WriteLine(
                    $"    Embedded RDTBs " +
                    $"({embedded.Count} found):");
                Console.ResetColor();
                Console.WriteLine(
                    "    " +
                    new string('-', 60));

                foreach (var e in embedded)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        $"    [{e.Index:D2}]" +
                        $" @ 0x{e.AbsOffset:X8}" +
                        $"  {e.Size,8:N0} B" +
                        $"  ptr={e.PtrCount}" +
                        $"  bones={e.BoneCount}" +
                        $"  chunks={e.ChunkCount}" +
                        $"  batches={e.BatchCount}");
                    Console.ResetColor();
                    if (e.TexIdsUsed.Count > 0)
                        Console.WriteLine(
                            $"         tex_ids: [" +
                            string.Join(", ",
                                e.TexIdsUsed) +
                            "]");
                }
                Console.WriteLine(
                    "    " +
                    new string('-', 60));
            }

            Console.WriteLine(
                new string('=', 64));
        }

        // ── Extract embedded RDTBs ───────────────
        public static void ExtractEmbeddedRdtbs(
            string srdbPath,
            string outFolder)
        {
            var srdb = new SRDBFile(srdbPath);
            srdb.Load();
            var chunks = srdb.GetChunks();
            var embedded =
                srdb.FindAllEmbeddedRDTBs(
                    chunks);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extract Embedded RDTBs:");
            Console.ResetColor();
            Console.WriteLine(
                "    Source: " +
                System.IO.Path.GetFileName(
                    srdbPath));
            Console.WriteLine(
                $"    Found : " +
                $"{embedded.Count}");
            Console.WriteLine(
                "    Output: " + outFolder);
            Console.WriteLine();

            if (embedded.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No embedded RDTBs.");
                Console.ResetColor();
                return;
            }

            Directory.CreateDirectory(outFolder);

            // Manifest for rebuild
            var manifest = new SRDBManifest
            {
                Tool =
                    "HMSTHModdingTool v2.0",
                SourceFile =
                    System.IO.Path.GetFileName(
                        srdbPath),
                SourceSize =
                    srdb.Data.Length,
                Version = srdb.Version,
                UnkFlags = srdb.UnkFlags,
            };

            foreach (var e in embedded)
            {
                string dest =
                    System.IO.Path.Combine(
                        outFolder,
                        e.Filename);
                File.WriteAllBytes(
                    dest, e.RawData);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    [{e.Index:D2}] " +
                    $"{e.Filename}" +
                    $"  ({e.Size:N0} B)" +
                    $"  @ 0x{e.AbsOffset:X8}");
                Console.ResetColor();

                if (e.TexIdsUsed.Count > 0)
                    Console.WriteLine(
                        $"         tex_ids: [" +
                        string.Join(", ",
                            e.TexIdsUsed) +
                        "]");

                manifest.EmbeddedRdtbs.Add(
                    new SRDBManifestEmbedded
                    {
                        Index = e.Index,
                        AbsOffset = e.AbsOffset,
                        Size = e.Size,
                        PtrCount = e.PtrCount,
                        BoneCount = e.BoneCount,
                        ChunkCount = e.ChunkCount,
                        Filename = e.Filename,
                        TexIdsUsed = e.TexIdsUsed,
                        BatchCount = e.BatchCount,
                    });
            }

            // Copy source SRDB for rebuild
            File.Copy(srdbPath,
                System.IO.Path.Combine(
                    outFolder, "_source.srdb"),
                true);

            // Write manifest
            WriteEmbeddedManifest(
                outFolder, manifest);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Embedded RDTBs extracted!");
            Console.ResetColor();
            Console.WriteLine(
                "     Output: " + outFolder);
        }

        // ════════════════════════════════════════
        // SRDB ASSEMBLER
        // ════════════════════════════════════════
        private static byte[] AssembleSRDB(
            byte[] originalData,
            List<byte[]> chunkData,
            SRDBManifest manifest)
        {
            // Determine header size from
            // original chunk offsets
            int headerSize =
                manifest.ChunkOffsets.Count > 0
                ? manifest.ChunkOffsets[0]
                : 0x0C + manifest.Chunks.Count
                  * 4;

            // NO ALIGNMENT. SRDB chunks start
            // immediately after the header
            // with no gaps and no 16-byte
            // alignment requirement.
            var offsets =
                new int[chunkData.Count];
            int cursor = headerSize;
            for (int i = 0;
                 i < chunkData.Count; i++)
            {
                offsets[i] = cursor;
                cursor += chunkData[i].Length;
            }

            // Build header from original
            byte[] header =
                new byte[headerSize];
            if (originalData.Length >=
                headerSize)
                Array.Copy(
                    originalData, header,
                    headerSize);

            // Update chunk offsets in header
            // Offsets start at 0x0C
            for (int i = 0;
                 i < offsets.Length; i++)
            {
                int pos = 0x0C + i * 4;
                if (pos + 4 > headerSize) break;
                byte[] ob =
                    BitConverter.GetBytes(
                        (uint)offsets[i]);
                Array.Copy(ob, 0, header, pos, 4);
            }

            // Assemble (gaps stay zero)
            byte[] result =
                new byte[cursor];
            Array.Copy(
                header, 0, result, 0,
                headerSize);
            for (int i = 0;
                 i < chunkData.Count; i++)
            {
                Array.Copy(
                    chunkData[i], 0,
                    result, offsets[i],
                    chunkData[i].Length);
            }

            return result;
        }

        // ════════════════════════════════════════
        // MANIFEST WRITERS / READERS
        // ════════════════════════════════════════
        private static void WriteManifest(
            string outFolder,
            SRDBManifest manifest,
            string srdbPath)
        {
            string path =
                System.IO.Path.Combine(
                    outFolder,
                    "srdb_manifest.json");

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(
                "  \"tool\": " +
                $"\"{manifest.Tool}\",");
            sb.AppendLine(
                "  \"source_file\": \"" +
                manifest.SourceFile + "\",");
            sb.AppendLine(
                "  \"source_size\": " +
                manifest.SourceSize + ",");
            sb.AppendLine(
                "  \"srdb_version\": " +
                manifest.Version + ",");
            sb.AppendLine(
                "  \"srdb_unk\": " +
                manifest.UnkFlags + ",");

            // Chunk offsets
            sb.AppendLine(
                "  \"chunk_offsets\": [" +
                string.Join(",",
                    manifest.ChunkOffsets) +
                "],");

            // Chunks array
            sb.AppendLine("  \"chunks\": [");
            for (int i = 0;
                 i < manifest.Chunks.Count; i++)
            {
                var c = manifest.Chunks[i];
                bool last =
                    i == manifest.Chunks.Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine(
                    $"      \"index\": {c.Index},");
                sb.AppendLine(
                    $"      \"filename\":" +
                    $" \"{c.Filename}\",");
                sb.AppendLine(
                    $"      \"offset\": {c.Offset},");
                sb.AppendLine(
                    $"      \"size\": {c.Size},");
                sb.AppendLine(
                    $"      \"has_vif\": " +
                    (c.HasVIF ? "true" : "false")
                    + ",");
                sb.AppendLine(
                    $"      \"vif_count\":" +
                    $" {c.VIFCount},");
                sb.AppendLine(
                    $"      \"embedded_rdtb_count\":" +
                    $" {c.EmbeddedRdtbCount}");
                sb.AppendLine(
                    "    }" +
                    (last ? "" : ","));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(
                path, sb.ToString(),
                Encoding.UTF8);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    srdb_manifest.json");
            Console.ResetColor();
        }

        private static void
            WriteEmbeddedManifest(
                string outFolder,
                SRDBManifest manifest)
        {
            string path =
                System.IO.Path.Combine(
                    outFolder,
                    "embedded_manifest.json");

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(
                "  \"tool\": " +
                $"\"{manifest.Tool}\",");
            sb.AppendLine(
                "  \"source_file\": \"" +
                manifest.SourceFile + "\",");
            sb.AppendLine(
                "  \"source_size\": " +
                manifest.SourceSize + ",");

            sb.AppendLine(
                "  \"embedded_rdtbs\": [");
            for (int i = 0;
                 i < manifest.EmbeddedRdtbs
                     .Count; i++)
            {
                var e =
                    manifest.EmbeddedRdtbs[i];
                bool last =
                    i ==
                    manifest.EmbeddedRdtbs
                        .Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine(
                    $"      \"index\": " +
                    $"{e.Index},");
                sb.AppendLine(
                    $"      \"abs_offset\": " +
                    $"{e.AbsOffset},");
                sb.AppendLine(
                    $"      \"abs_offset_hex\":" +
                    $" \"0x{e.AbsOffset:X8}\",");
                sb.AppendLine(
                    $"      \"size\": " +
                    $"{e.Size},");
                sb.AppendLine(
                    $"      \"ptr_count\": " +
                    $"{e.PtrCount},");
                sb.AppendLine(
                    $"      \"bone_count\": " +
                    $"{e.BoneCount},");
                sb.AppendLine(
                    $"      \"chunk_count\": " +
                    $"{e.ChunkCount},");
                sb.AppendLine(
                    $"      \"batch_count\": " +
                    $"{e.BatchCount},");
                sb.AppendLine(
                    $"      \"filename\": " +
                    $"\"{e.Filename}\",");
                sb.AppendLine(
                    $"      \"tex_ids_used\": [" +
                    string.Join(",",
                        e.TexIdsUsed) +
                    "]");
                sb.AppendLine(
                    "    }" +
                    (last ? "" : ","));
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(
                path, sb.ToString(),
                Encoding.UTF8);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "    embedded_manifest.json");
            Console.ResetColor();
        }

        internal static SRDBManifest
            ReadManifest(string path)
        {
            string json =
                File.ReadAllText(
                    path, Encoding.UTF8);
            var m = new SRDBManifest
            {
                Tool =
                    JStr(json, "tool"),
                SourceFile =
                    JStr(json, "source_file"),
                SourceSize =
                    JInt(json, "source_size"),
                Version =
                    (uint)JInt(
                        json, "srdb_version"),
                UnkFlags =
                    (uint)JInt(
                        json, "srdb_unk"),
                OriginalGdtbName =
                    JStr(json,
                        "original_gdtb_name"),
            };

            // chunk_offsets
            int coi =
                json.IndexOf(
                    "\"chunk_offsets\":");
            if (coi >= 0)
            {
                int ab =
                    json.IndexOf('[', coi);
                int ae =
                    json.IndexOf(']', ab);
                if (ab >= 0 && ae > ab)
                {
                    string inner =
                        json.Substring(
                            ab + 1, ae - ab - 1);
                    foreach (var s in
                        inner.Split(','))
                    {
                        if (int.TryParse(
                                s.Trim(),
                                out int v))
                            m.ChunkOffsets.Add(v);
                    }
                }
            }

            // chunks array
            int ci =
                json.IndexOf("\"chunks\":");
            if (ci >= 0)
            {
                int ab =
                    json.IndexOf('[', ci);
                int ae =
                    MatchBracket(json, ab);
                if (ab >= 0 && ae > ab)
                {
                    string arr =
                        json.Substring(
                            ab, ae - ab + 1);
                    int pos = 0;
                    while (pos < arr.Length)
                    {
                        int ob =
                            arr.IndexOf('{', pos);
                        if (ob < 0) break;
                        int oe =
                            MatchBrace(arr, ob);
                        if (oe < 0) break;
                        string obj =
                            arr.Substring(
                                ob, oe - ob + 1);
                        m.Chunks.Add(
                            new SRDBManifestChunk
                            {
                                Index =
                                    JInt(obj,
                                        "index"),
                                Filename =
                                    JStr(obj,
                                        "filename"),
                                Offset =
                                    JInt(obj,
                                        "offset"),
                                Size =
                                    JInt(obj,
                                        "size"),
                                HasVIF =
                                    JBool(obj,
                                        "has_vif"),
                                VIFCount =
                                    JInt(obj,
                                        "vif_count"),
                                EmbeddedRdtbCount =
                                    JInt(obj,
                                        "embedded_rdtb_count"),
                            });
                        pos = oe + 1;
                    }
                }
            }

            m.Chunks.Sort(
                (a, b) =>
                a.Index.CompareTo(b.Index));

            return m;
        }

        // ── JSON helpers ─────────────────────────
        internal static string JStr(
            string json, string key)
        {
            string s = "\"" + key + "\"";
            int ki = json.IndexOf(s);
            if (ki < 0) return "";
            int c = json.IndexOf(':', ki);
            if (c < 0) return "";
            int q1 =
                json.IndexOf('"', c + 1);
            if (q1 < 0) return "";
            int q2 =
                json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(
                q1 + 1, q2 - q1 - 1);
        }

        internal static int JInt(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0) return 0;
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
                    json[ve] == '-'))
                ve++;
            if (ve == vs) return 0;
            int.TryParse(
                json.Substring(vs, ve - vs),
                out int r);
            return r;
        }

        internal static bool JBool(
            string json, string key)
        {
            string s = "\"" + key + "\":";
            int ki = json.IndexOf(s);
            if (ki < 0) return false;
            int vs = ki + s.Length;
            while (vs < json.Length &&
                   json[vs] == ' ')
                vs++;
            return vs < json.Length &&
                   json[vs] == 't';
        }

        internal static int MatchBracket(
            string s, int start)
        {
            if (start < 0 ||
                start >= s.Length)
                return -1;
            int depth = 0;
            for (int i = start;
                 i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                if (s[i] == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        internal static int MatchBrace(
            string s, int start)
        {
            if (start < 0 ||
                start >= s.Length)
                return -1;
            int depth = 0;
            for (int i = start;
                 i < s.Length; i++)
            {
                if (s[i] == '{') depth++;
                if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }
    }
}
