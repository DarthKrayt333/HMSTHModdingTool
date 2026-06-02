using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HMSTHModdingTool.RDTB
{
    // ═════════════════════════════════════════════
    // BONE RECORD  (16 bytes per bone)
    // ═════════════════════════════════════════════
    // CORRECTED LAYOUT v2.0 (verified against
    // working Python _compute_bone_world_t and
    // successful boyscale modifications)
    //
    // Byte layout per 16-byte bone record:
    //  0x00  uint8    self_index
    //  0x01  uint8    flags_byte1
    //  0x02  uint8    child_index  (0xFF = none)
    //  0x03  uint8    parent_index (0xFF = root)
    //  0x04  float32  bind_x  (local translation)
    //  0x08  float32  bind_y  (local translation)
    //  0x0C  float32  bind_z  (local translation)
    //
    // NOTE: The old RDTBArchive.cs had this REVERSED
    // (floats at 0-11, indices at 12-15).
    // The Python extractor which successfully scales
    // bones reads parent at byte+3, floats at +4/+8/+12.
    // This corrected layout matches the Python.
    // ═════════════════════════════════════════════
    internal class RDTBBone
    {
        public byte SelfIndex { get; set; }
        public byte FlagsByte1 { get; set; }
        public byte ChildIndex { get; set; }
        public byte ParentIndex { get; set; }
        public float BindX { get; set; }
        public float BindY { get; set; }
        public float BindZ { get; set; }

        public bool IsRoot =>
            ParentIndex == 0xFF;
        public bool HasChild =>
            ChildIndex != 0xFF;

        public byte[] RawBytes { get; set; }

        // ─────────────────────────────────────
        // Parse from raw bytes (CORRECTED)
        // ─────────────────────────────────────
        public static RDTBBone FromBytes(
            byte[] data, int offset)
        {
            byte[] raw = new byte[16];
            Array.Copy(data, offset, raw, 0, 16);

            return new RDTBBone
            {
                SelfIndex =
                    data[offset + 0],
                FlagsByte1 =
                    data[offset + 1],
                ChildIndex =
                    data[offset + 2],
                ParentIndex =
                    data[offset + 3],
                BindX =
                    BitConverter.ToSingle(
                        data, offset + 4),
                BindY =
                    BitConverter.ToSingle(
                        data, offset + 8),
                BindZ =
                    BitConverter.ToSingle(
                        data, offset + 12),
                RawBytes = raw,
            };
        }

        // ─────────────────────────────────────
        // Serialize back to 16 bytes (CORRECTED)
        // ─────────────────────────────────────
        public byte[] ToBytes()
        {
            byte[] buf = new byte[16];
            buf[0] = SelfIndex;
            buf[1] = FlagsByte1;
            buf[2] = ChildIndex;
            buf[3] = ParentIndex;
            Array.Copy(
                BitConverter.GetBytes(BindX),
                0, buf, 4, 4);
            Array.Copy(
                BitConverter.GetBytes(BindY),
                0, buf, 8, 4);
            Array.Copy(
                BitConverter.GetBytes(BindZ),
                0, buf, 12, 4);
            return buf;
        }

        public override string ToString()
        {
            string par = IsRoot
                ? "ROOT"
                : ParentIndex.ToString("D3");
            string chl = HasChild
                ? ChildIndex.ToString("D3")
                : "none";
            return
                $"[{SelfIndex:D3}] " +
                $"parent={par} " +
                $"child={chl} " +
                $"X={BindX:F4} " +
                $"Y={BindY:F4} " +
                $"Z={BindZ:F4} " +
                $"f1=0x{FlagsByte1:X2}";
        }
    }

    // ═════════════════════════════════════════════
    // RDTB CHUNK
    // ═════════════════════════════════════════════
    internal class RDTBChunk
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public byte[] Data { get; set; }

        public string Label =>
            RDTBArchive.GetChunkLabel(Index);
        public string Description =>
            RDTBArchive.GetChunkDesc(Index);
        public string Filename =>
            $"{Index:D2}_{Label}.bin";

        public bool HasEofTerminator
        {
            get
            {
                if (Data == null ||
                    Data.Length < 16)
                    return false;
                int o = Data.Length - 16;
                return
                    Data[o + 0] == 0x00 &&
                    Data[o + 1] == 0x00 &&
                    Data[o + 2] == 0x00 &&
                    Data[o + 3] == 0x70 &&
                    Data[o + 4] == 0x00 &&
                    Data[o + 5] == 0x00 &&
                    Data[o + 6] == 0x00 &&
                    Data[o + 7] == 0x00;
            }
        }

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
    // RDTB SKELETON
    // ═════════════════════════════════════════════
    internal class RDTBSkeleton
    {
        public int BoneCount { get; set; }
        public List<uint> BonePtrs { get; set; }
        public List<RDTBBone> Bones { get; set; }

        public List<int> GetRoots()
        {
            var roots = new List<int>();
            for (int i = 0;
                 i < Bones.Count; i++)
            {
                if (Bones[i].IsRoot)
                    roots.Add(i);
            }
            return roots;
        }

        public List<int> GetChildrenOf(int idx)
        {
            var ch = new List<int>();
            for (int i = 0;
                 i < Bones.Count; i++)
            {
                if (Bones[i].ParentIndex == idx)
                    ch.Add(i);
            }
            return ch;
        }

        // ─────────────────────────────────────
        // Compute world-space translations
        // (same algorithm as Python
        //  _compute_bone_world_t)
        // ─────────────────────────────────────
        public float[] ComputeWorldX()
        {
            return ComputeWorldAxis(b => b.BindX);
        }
        public float[] ComputeWorldY()
        {
            return ComputeWorldAxis(b => b.BindY);
        }
        public float[] ComputeWorldZ()
        {
            return ComputeWorldAxis(b => b.BindZ);
        }

        private float[] ComputeWorldAxis(
            Func<RDTBBone, float> getLocal)
        {
            int n = Bones.Count;
            float[] world = new float[n];
            for (int i = 0; i < n; i++)
            {
                world[i] = getLocal(Bones[i]);
                var visited = new HashSet<int>
                    { i };
                int p = Bones[i].IsRoot
                    ? -1
                    : Bones[i].ParentIndex;
                while (p >= 0 && p < n)
                {
                    if (visited.Contains(p))
                        break;
                    visited.Add(p);
                    world[i] +=
                        getLocal(Bones[p]);
                    p = Bones[p].IsRoot
                        ? -1
                        : Bones[p].ParentIndex;
                }
            }
            return world;
        }
    }

    // ═════════════════════════════════════════════
    // MATERIAL RECORD (from chunk 8)
    // 8 bytes per record in material table
    // ═════════════════════════════════════════════
    internal class RDTBMaterialRecord
    {
        public int Index { get; set; }
        public ushort BoneIndex { get; set; }
        public ushort FieldB { get; set; }
        public ushort FieldC { get; set; }
        public ushort TextureId { get; set; }
        public byte[] Signature { get; set; }

        public override string ToString()
        {
            return
                $"[{Index:D2}] bone={BoneIndex}" +
                $" b={FieldB} c={FieldC}" +
                $" tex={TextureId}" +
                $" sig={BitConverter.ToString(Signature ?? new byte[8]).Replace('-', ' ')}";
        }
    }

    // ═════════════════════════════════════════════
    // EMBEDDED RDTB INFO (for SRDB detection)
    // ═════════════════════════════════════════════
    internal class EmbeddedRDTBInfo
    {
        public int Offset { get; set; }
        public int Size { get; set; }
        public int PtrCount { get; set; }
        public int BoneCount { get; set; }
        public List<int> ChunkOffsets
        { get; set; }
        public byte[] RawData { get; set; }
    }

    // ═════════════════════════════════════════════
    // MANIFEST DATA CLASSES
    // ═════════════════════════════════════════════
    internal class RDTBManifest
    {
        public string Tool { get; set; }
        public string Credits { get; set; }
        public string Game { get; set; }
        public string SourceFile { get; set; }
        public int SourceSize { get; set; }
        public string Unk08Hex { get; set; }
        public int PtrCount { get; set; }
        public int BoneCount { get; set; }
        public int ChunkCount =>
            Chunks?.Count ?? 0;
        public List<RDTBManifestChunk>
                      Chunks
        { get; set; }
        public List<EmbeddedRDTBManifest>
                      EmbeddedRdtbs
        { get; set; }
    }

    internal class RDTBManifestChunk
    {
        public int Index { get; set; }
        public string Filename { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public int Offset { get; set; }
        public string OffsetHex { get; set; }
        public int Size { get; set; }
        public string SizeHex { get; set; }
        public bool HasEof { get; set; }
        public bool HasVIF { get; set; }
        public int VIFCount { get; set; }
    }

    internal class EmbeddedRDTBManifest
    {
        public int Index { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public int PtrCount { get; set; }
        public int BoneCount { get; set; }
        public string Filename { get; set; }
    }

    // ═════════════════════════════════════════════
    // RDTB ARCHIVE  (main class) v2.0
    // CORRECTED bone layout + new features
    // ═════════════════════════════════════════════
    public class RDTBArchive
    {
        // ─────────────────────────────────────
        // CONSTANTS
        // ─────────────────────────────────────
        private const int HEADER_SIZE = 0x48;
        private const int OFFSET_TBL_START = 0x10;
        private const int OFFSET_TBL_SLOTS = 14;
        private const int BONE_PTR_SIZE = 4;
        private const int BONE_REC_SIZE = 16;
        private const int MAT_REC_SIZE = 8;

        // VIF signature bytes
        private const byte VIF_B0 = 0x00;
        private const byte VIF_B1 = 0x80;
        private const byte VIF_B3 = 0x6C;

        // EOF terminator pattern
        private const uint FLAG_EOF = 0x70000000;

        private const string TOOL_VERSION =
            "HMSTHModdingTool v2.0.0";
        private const string TOOL_CREDITS =
            "gdkchan (original), " +
            "DarthKrayt333 (upgrade v2.0)";
        private const string TOOL_GAME =
            "Harvest Moon Save The Homeland" +
            " (PS2)";

        private static readonly byte[] MAGIC =
        {
            0x52, 0x44, 0x54, 0x42  // "RDTB"
        };
        private static readonly byte[] SRDB_MAGIC =
        {
            0x53, 0x52, 0x44, 0x42  // "SRDB"
        };
        private static readonly byte[] EOF_TERM =
        {
            0x00, 0x00, 0x00, 0x70,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        // ─────────────────────────────────────
        // PRIVATE FIELDS
        // ─────────────────────────────────────
        private string _filepath;
        private byte[] _data;
        private byte[] _unk08;
        private int _ptrCount;
        private int _boneCount;
        private List<int> _offsets;
        private List<RDTBChunk> _chunks;
        private RDTBSkeleton _skeleton;
        private List<RDTBMaterialRecord> _materials;
        private List<EmbeddedRDTBInfo>
            _embeddedRdtbs;

        // ─────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────
        public RDTBArchive(string filepath)
        {
            _filepath = filepath;
            _offsets = new List<int>();
            _chunks = new List<RDTBChunk>();
            _unk08 = new byte[4];
            _materials =
                new List<RDTBMaterialRecord>();
            _embeddedRdtbs =
                new List<EmbeddedRDTBInfo>();
        }

        // ─────────────────────────────────────
        // READ HELPERS
        // ─────────────────────────────────────
        private int ReadInt32(int offset)
            => BitConverter.ToInt32(
                   _data, offset);

        private uint ReadUInt32(int offset)
            => BitConverter.ToUInt32(
                   _data, offset);

        private ushort ReadUInt16(int offset)
            => BitConverter.ToUInt16(
                   _data, offset);

        private float ReadFloat(int offset)
            => BitConverter.ToSingle(
                   _data, offset);

        private byte[] GetBytes(
            int offset, int len)
        {
            byte[] buf = new byte[len];
            Array.Copy(
                _data, offset,
                buf, 0, len);
            return buf;
        }

        // ═════════════════════════════════════
        // STATIC ENTRY POINTS
        // ═════════════════════════════════════
        public static void Info(
            string rdtbPath)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.Load();
            arc.ShowInfo(showBones: true);
        }

        public static void InfoNoBones(
            string rdtbPath)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.Load();
            arc.ShowInfo(showBones: false);
        }

        public static void Extract(
            string rdtbPath,
            string outputFolder)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.Load();
            arc.ExtractAll(outputFolder);
        }

        public static void Create(
            string inputFolder,
            string rdtbPath)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.CreateFromFolder(
                inputFolder, rdtbPath);
        }

        public static void Skeleton(
            string rdtbPath)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.Load();
            arc.ShowSkeletonTree();
        }

        public static void Compare(
            string rdtbPathA,
            string rdtbPathB)
        {
            var a = new RDTBArchive(rdtbPathA);
            var b = new RDTBArchive(rdtbPathB);
            a.Load();
            b.Load();
            a.CompareWith(b);
        }

        public static void Verify(
            string originalPath,
            string rebuiltPath)
        {
            var arc =
                new RDTBArchive(originalPath);
            arc.VerifyAgainst(rebuiltPath);
        }

        public static void ReplaceChunk(
            string rdtbPath,
            int chunkIndex,
            string chunkFile)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.Load();
            arc.DoReplaceChunk(
                chunkIndex, chunkFile);
        }

        public static void ScanFolder(
            string folderPath)
        {
            DoScanFolder(folderPath);
        }

        // ─── NEW: Detect embedded RDTBs ─────
        public static void DetectEmbedded(
            string filePath)
        {
            var arc = new RDTBArchive(filePath);
            arc._data =
                File.ReadAllBytes(filePath);
            arc._filepath = filePath;
            arc.FindEmbeddedRDTBs();
            arc.ShowEmbeddedInfo();
        }

        // ─── NEW: Material table info ────────
        public static void Materials(
            string rdtbPath)
        {
            var arc = new RDTBArchive(rdtbPath);
            arc.Load();
            arc.ShowMaterialTable();
        }

        // ═════════════════════════════════════
        // LOAD
        // ═════════════════════════════════════
        public void Load()
        {
            _data =
                File.ReadAllBytes(_filepath);

            // ── Validate magic ───────────────
            if (_data.Length < 4 ||
                _data[0] != 'R' ||
                _data[1] != 'D' ||
                _data[2] != 'T' ||
                _data[3] != 'B')
            {
                throw new InvalidDataException(
                    "Not a valid RDTB file: " +
                    _filepath);
            }

            if (_data.Length < HEADER_SIZE)
            {
                throw new InvalidDataException(
                    $"File too small " +
                    $"({_data.Length} B < " +
                    $"{HEADER_SIZE} B)");
            }

            // ── Header fields ────────────────
            _unk08 = GetBytes(8, 4);
            _ptrCount = ReadUInt16(0x0C);
            _boneCount = ReadUInt16(0x0E);

            // ── Offset table ─────────────────
            LoadOffsets();

            // ── Slice chunks ─────────────────
            _chunks.Clear();
            for (int i = 0;
                 i < _offsets.Count; i++)
            {
                int start = _offsets[i];
                int end =
                    (i + 1 < _offsets.Count)
                    ? _offsets[i + 1]
                    : _data.Length;
                int sz = end - start;
                if (sz <= 0) continue;

                _chunks.Add(new RDTBChunk
                {
                    Index = i,
                    Offset = start,
                    Size = sz,
                    Data = GetBytes(start, sz),
                });
            }

            // ── Parse skeleton (chunk 0) ─────
            ParseSkeleton();

            // ── Parse materials (chunk 8) ────
            ParseMaterialTable();

            // ── Detect embedded RDTBs ────────
            FindEmbeddedRDTBs();
        }

        // ═════════════════════════════════════
        // LOAD OFFSETS  (safe)
        // Handles both normal offsets and
        // 0xFFFFFFFF sentinel values seen in
        // some RDTB files
        // ═════════════════════════════════════
        private void LoadOffsets()
        {
            _offsets.Clear();

            for (int slot = 0;
                 slot < OFFSET_TBL_SLOTS;
                 slot++)
            {
                int pos =
                    OFFSET_TBL_START + slot * 4;
                if (pos + 4 > _data.Length)
                    break;

                int val = ReadInt32(pos);

                // Zero = unused slot = end
                if (val == 0) break;

                // 0xFFFFFFFF sentinel = skip
                if (val == -1 ||
                    val == unchecked(
                        (int)0xFFFFFFFF))
                    continue;

                // Sanity check
                if (val < HEADER_SIZE ||
                    val > _data.Length)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"[!] Offset[{slot}] " +
                        $"= 0x{val:X8} " +
                        $"suspicious - skipping");
                    Console.ResetColor();
                    continue;
                }

                _offsets.Add(val);
            }
        }

        // ═════════════════════════════════════
        // PARSE SKELETON (CORRECTED v2.0)
        // Matches Python _compute_bone_world_t
        // ═════════════════════════════════════
        private void ParseSkeleton()
        {
            if (_chunks.Count == 0 ||
                _boneCount == 0)
                return;

            var c0 = _chunks[0];
            var dat = c0.Data;

            int ptrEnd =
                _boneCount * BONE_PTR_SIZE;

            if (ptrEnd > dat.Length)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"[!] Bone ptr array " +
                    $"({ptrEnd} B) > " +
                    $"chunk0 ({dat.Length} B)");
                Console.ResetColor();
                return;
            }

            // ── Read bone pointers ───────────
            var ptrs = new List<uint>();
            for (int i = 0; i < _boneCount; i++)
            {
                ptrs.Add(
                    BitConverter.ToUInt32(
                        dat,
                        i * BONE_PTR_SIZE));
            }

            // ── Read bone records ────────────
            // CORRECTED: indices at bytes 0-3,
            //            floats at bytes 4-15
            int recStart = ptrEnd;
            int recEnd =
                recStart +
                _boneCount * BONE_REC_SIZE;

            if (recEnd > dat.Length)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "[!] Bone records " +
                    "exceed chunk 0");
                Console.ResetColor();
                return;
            }

            var bones = new List<RDTBBone>();
            for (int i = 0; i < _boneCount; i++)
            {
                int off =
                    recStart + i * BONE_REC_SIZE;
                bones.Add(
                    RDTBBone.FromBytes(
                        dat, off));
            }

            _skeleton = new RDTBSkeleton
            {
                BoneCount = _boneCount,
                BonePtrs = ptrs,
                Bones = bones,
            };
        }

        // ═════════════════════════════════════
        // PARSE MATERIAL TABLE (chunk 8)
        // NEW in v2.0
        // ═════════════════════════════════════
        private void ParseMaterialTable()
        {
            _materials.Clear();

            // Find chunk 8 (or equivalent)
            int c8Idx = Math.Min(
                8, _chunks.Count - 1);
            if (c8Idx < 0) return;

            var c8 = _chunks[c8Idx];
            var dat = c8.Data;
            if (dat == null || dat.Length < 4)
                return;

            // Check if chunk 8 starts with VIF
            // (small RDTB - no material table)
            if (dat.Length >= 4 &&
                dat[0] == VIF_B0 &&
                dat[1] == VIF_B1 &&
                dat[3] == VIF_B3)
                return;

            uint first =
                BitConverter.ToUInt32(dat, 0);
            if (first == 0 ||
                first > (uint)dat.Length)
                return;

            int batchCount = (int)(first / 4);
            if (batchCount <= 0 ||
                batchCount > 10000)
                return;

            for (int i = 0; i < batchCount; i++)
            {
                int ptrOff = i * 4;
                if (ptrOff + 4 > dat.Length)
                    break;

                uint ptr =
                    BitConverter.ToUInt32(
                        dat, ptrOff);
                if (ptr + MAT_REC_SIZE >
                    (uint)dat.Length)
                {
                    _materials.Add(
                        new RDTBMaterialRecord
                        {
                            Index = i,
                            Signature =
                                new byte[8],
                        });
                    continue;
                }

                var rec = new RDTBMaterialRecord
                {
                    Index = i,
                    BoneIndex =
                        BitConverter.ToUInt16(
                            dat, (int)ptr),
                    FieldB =
                        BitConverter.ToUInt16(
                            dat, (int)ptr + 2),
                    FieldC =
                        BitConverter.ToUInt16(
                            dat, (int)ptr + 4),
                    TextureId =
                        BitConverter.ToUInt16(
                            dat, (int)ptr + 6),
                };
                byte[] sig = new byte[8];
                Array.Copy(
                    dat, (int)ptr,
                    sig, 0, 8);
                rec.Signature = sig;
                _materials.Add(rec);
            }
        }

        // ═════════════════════════════════════
        // FIND EMBEDDED RDTBs (NEW in v2.0)
        // Scans binary data for RDTB magic
        // bytes, validates each candidate
        // ═════════════════════════════════════
        private void FindEmbeddedRDTBs()
        {
            _embeddedRdtbs.Clear();
            if (_data == null ||
                _data.Length < HEADER_SIZE)
                return;

            int pos = 0;
            while (pos < _data.Length - 4)
            {
                int idx = IndexOfMagic(
                    _data, MAGIC, pos);
                if (idx < 0) break;

                // Skip the file's own header
                if (idx == 0)
                {
                    pos = idx + 4;
                    continue;
                }

                // Must be 4-byte aligned
                if (idx % 4 != 0)
                {
                    pos = idx + 1;
                    continue;
                }

                // Validate RDTB header
                if (idx + HEADER_SIZE >
                    _data.Length)
                {
                    pos = idx + 4;
                    continue;
                }

                int pc =
                    BitConverter.ToUInt16(
                        _data, idx + 0x0C);
                int bc =
                    BitConverter.ToUInt16(
                        _data, idx + 0x0E);

                if (pc == 0 || pc > 10000 ||
                    bc > 1000)
                {
                    pos = idx + 4;
                    continue;
                }

                // Find EOF terminator
                int eofIdx = IndexOfPattern(
                    _data, EOF_TERM, idx + 0x48);
                if (eofIdx < 0)
                {
                    pos = idx + 4;
                    continue;
                }

                int endAligned =
                    ((eofIdx + 16 + 15) / 16)
                    * 16;
                int sz = endAligned - idx;

                if (sz < 64 ||
                    sz > _data.Length - idx)
                {
                    pos = idx + 4;
                    continue;
                }

                // Read chunk offsets
                var coffs = new List<int>();
                for (int s = 0; s < 14; s++)
                {
                    int off = idx + 0x10 + s * 4;
                    if (off + 4 > _data.Length)
                        break;
                    int v =
                        BitConverter.ToInt32(
                            _data, off);
                    if (v == 0 || v < 0x48 ||
                        v > sz)
                        break;
                    if (v == -1 ||
                        v == unchecked(
                            (int)0xFFFFFFFF))
                        continue;
                    coffs.Add(v);
                }

                byte[] raw = new byte[sz];
                Array.Copy(
                    _data, idx, raw, 0, sz);

                _embeddedRdtbs.Add(
                    new EmbeddedRDTBInfo
                    {
                        Offset = idx,
                        Size = sz,
                        PtrCount = pc,
                        BoneCount = bc,
                        ChunkOffsets = coffs,
                        RawData = raw,
                    });

                pos = idx + sz;
            }
        }

        private static int IndexOfMagic(
            byte[] data, byte[] magic,
            int start)
        {
            for (int i = start;
                 i <= data.Length - magic.Length;
                 i++)
            {
                bool match = true;
                for (int j = 0;
                     j < magic.Length; j++)
                {
                    if (data[i + j] != magic[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private static int IndexOfPattern(
            byte[] data, byte[] pattern,
            int start)
        {
            // Only match first 8 bytes of
            // EOF pattern for detection
            int pLen = Math.Min(
                8, pattern.Length);
            for (int i = start;
                 i <= data.Length - pLen;
                 i++)
            {
                bool match = true;
                for (int j = 0; j < pLen; j++)
                {
                    if (data[i + j] !=
                        pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        // ═════════════════════════════════════
        // CHUNK LABELS / DESCRIPTIONS
        // (KEPT from original, these are
        //  confirmed correct for large RDTB)
        // ═════════════════════════════════════
        public static string GetChunkLabel(
            int idx)
        {
            switch (idx)
            {
                case 0:
                    return "skeleton";
                case 1:
                    return "anim_ptr_table";
                case 2:
                    return "anim_data_0";
                case 3:
                    return "anim_data_1";
                case 4:
                    return "anim_data_2";
                case 5:
                    return "anim_data_3";
                case 6:
                    return "anim_data_4";
                case 7:
                    return "lookup_table_0";
                case 8:
                    return "material_mesh";
                case 9:
                    return "lookup_table_1";
                case 10:
                    return "lookup_table_2";
                case 11:
                    return "vif_mesh_lod0";
                case 12:
                    return "vif_mesh_lod1";
                case 13:
                    return "vif_mesh_lod2";
                default:
                    return $"chunk_{idx:D2}";
            }
        }

        public static string GetChunkDesc(
            int idx)
        {
            switch (idx)
            {
                case 0:
                    return
                        "Bone ptr array + " +
                        "bone records (16B each)";
                case 1:
                    return
                        "Animation pointer table";
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                    return
                        $"Animation/transform " +
                        $"data {idx - 2}";
                case 7:
                    return
                        "Small lookup/index table";
                case 8:
                    return
                        "Material table + mesh " +
                        "VIF data (small RDTB) " +
                        "or material records " +
                        "(large RDTB)";
                case 9:
                case 10:
                    return
                        "Small lookup/index table";
                case 11:
                    return
                        "VIF mesh LOD0 (high " +
                        "quality, close range)";
                case 12:
                    return
                        "VIF mesh LOD1 (medium " +
                        "quality, mid range)";
                case 13:
                    return
                        "VIF mesh LOD2 (low " +
                        "quality, far range)";
                default:
                    return "Unknown data";
            }
        }

        // ═════════════════════════════════════
        // SHOW INFO (ENHANCED v2.0)
        // ═════════════════════════════════════
        private void ShowInfo(bool showBones)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Info v2.0: " +
                Path.GetFileName(_filepath));
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));

            Console.WriteLine(
                $"    File          : " +
                Path.GetFileName(_filepath));
            Console.WriteLine(
                $"    Size          : " +
                _data.Length.ToString("N0") +
                $" bytes " +
                $"(0x{_data.Length:X8})");
            Console.WriteLine(
                $"    Magic         : RDTB");
            Console.WriteLine(
                $"    Version       : " +
                "00 01 00 00");
            Console.WriteLine(
                $"    Metadata 0x08 : " +
                BitConverter
                    .ToString(_unk08)
                    .Replace('-', ' '));
            Console.WriteLine(
                $"    Ptr count     : " +
                _ptrCount +
                $" (0x{_ptrCount:X4})");
            Console.WriteLine(
                $"    Bone count    : " +
                _boneCount +
                $" (0x{_boneCount:X4})");
            Console.WriteLine(
                $"    Chunks        : " +
                _chunks.Count);
            Console.WriteLine(
                $"    Materials     : " +
                _materials.Count);
            Console.WriteLine(
                $"    Embedded RDTBs: " +
                _embeddedRdtbs.Count);

            // ── EOF check ────────────────────
            bool hasEof = HasEofTerminator();
            Console.ForegroundColor = hasEof
                ? ConsoleColor.Green
                : ConsoleColor.Yellow;
            Console.WriteLine(
                $"    EOF term      : " +
                (hasEof
                    ? "00 00 00 70 ... ✓"
                    : "unexpected!"));
            Console.ResetColor();

            // ── Chunk table ──────────────────
            Console.WriteLine();
            Console.WriteLine(
                "    " +
                new string('─', 72));
            Console.WriteLine(
                $"    {"#",3}  " +
                $"{"OFFSET",10}  " +
                $"{"SIZE",10}  " +
                $"{"SIZE_B",12}  " +
                $"{"VIF",4}  LABEL");
            Console.WriteLine(
                "    " +
                new string('─', 72));

            foreach (var c in _chunks)
            {
                string eof =
                    c.HasEofTerminator
                    ? " [EOF]" : "";
                string vif =
                    c.HasVIFData
                    ? $"{c.VIFBlockCount,4}"
                    : "   -";
                Console.WriteLine(
                    $"    [{c.Index,2}]  " +
                    $"0x{c.Offset:X8}  " +
                    $"0x{c.Size:X8}  " +
                    $"{c.Size,12:N0} B  " +
                    $"{vif}  " +
                    $"{c.Label}{eof}");
            }

            Console.WriteLine(
                "    " +
                new string('─', 72));

            // ── Material records ─────────────
            if (_materials.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"    Material Table " +
                    $"({_materials.Count}" +
                    $" records)");
                Console.ResetColor();

                var texIds =
                    new HashSet<int>();
                foreach (var m in _materials)
                    texIds.Add(m.TextureId);
                Console.WriteLine(
                    $"    Unique tex_ids: " +
                    $"[{string.Join(", ", texIds)}]");

                Console.WriteLine(
                    "    " +
                    new string('─', 60));
                int showMax = Math.Min(
                    _materials.Count, 20);
                for (int i = 0;
                     i < showMax; i++)
                {
                    Console.WriteLine(
                        $"    {_materials[i]}");
                }
                if (_materials.Count > showMax)
                    Console.WriteLine(
                        $"    ... and " +
                        $"{_materials.Count - showMax}" +
                        $" more");
            }

            // ── Embedded RDTBs ──────────────
            if (_embeddedRdtbs.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Magenta;
                Console.WriteLine(
                    $"    Embedded RDTBs " +
                    $"({_embeddedRdtbs.Count}" +
                    $" found)");
                Console.ResetColor();
                Console.WriteLine(
                    "    " +
                    new string('─', 60));
                for (int i = 0;
                     i < _embeddedRdtbs.Count;
                     i++)
                {
                    var e = _embeddedRdtbs[i];
                    Console.WriteLine(
                        $"    [{i:D2}] " +
                        $"@ 0x{e.Offset:X8}  " +
                        $"{e.Size,8:N0} B  " +
                        $"ptr={e.PtrCount}  " +
                        $"bones={e.BoneCount}  " +
                        $"chunks=" +
                        $"{e.ChunkOffsets.Count}");
                }
            }

            // ── Skeleton summary ─────────────
            if (_skeleton != null)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"    Skeleton " +
                    $"({_skeleton.BoneCount}" +
                    $" bones)  " +
                    $"[CORRECTED v2.0 layout]");
                Console.ResetColor();

                if (showBones)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        $"    {"#",3}  " +
                        $"{"SELF",4}  " +
                        $"{"PAR",4}  " +
                        $"{"CHLD",4}  " +
                        $"{"BIND_X",10}  " +
                        $"{"BIND_Y",10}  " +
                        $"{"BIND_Z",10}  " +
                        $"F1  NOTE");
                    Console.WriteLine(
                        "    " +
                        new string('─', 76));

                    for (int i = 0;
                         i < _skeleton
                               .Bones.Count;
                         i++)
                    {
                        var b =
                            _skeleton.Bones[i];
                        string par = b.IsRoot
                            ? "ROOT"
                            : b.ParentIndex
                                .ToString("D3");
                        string chl = b.HasChild
                            ? b.ChildIndex
                                .ToString("D3")
                            : "none";
                        string note =
                            b.IsRoot
                            ? " ◄ ROOT"
                            : "";

                        Console.WriteLine(
                            $"    [{i,3}]  " +
                            $"{b.SelfIndex,4}  " +
                            $"{par,4}  " +
                            $"{chl,4}  " +
                            $"{b.BindX,10:F4}  " +
                            $"{b.BindY,10:F4}  " +
                            $"{b.BindZ,10:F4}  " +
                            $"0x{b.FlagsByte1:X2}" +
                            $"{note}");
                    }

                    Console.WriteLine(
                        "    " +
                        new string('─', 76));
                }
            }

            Console.WriteLine(
                new string('═', 64));
        }

        // ═════════════════════════════════════
        // SHOW MATERIAL TABLE (NEW)
        // ═════════════════════════════════════
        private void ShowMaterialTable()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Material Table: " +
                Path.GetFileName(_filepath));
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));

            if (_materials.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No material records " +
                    "found (small RDTB or " +
                    "no chunk 8)");
                Console.ResetColor();
                return;
            }

            Console.WriteLine(
                $"    Records: " +
                $"{_materials.Count}");
            Console.WriteLine();
            Console.WriteLine(
                $"    {"#",3}  " +
                $"{"BONE",6}  " +
                $"{"B",5}  " +
                $"{"C",5}  " +
                $"{"TEX",4}  " +
                $"SIGNATURE");
            Console.WriteLine(
                "    " +
                new string('─', 60));

            foreach (var m in _materials)
            {
                Console.WriteLine(
                    $"    [{m.Index:D2}]  " +
                    $"{m.BoneIndex,6}  " +
                    $"{m.FieldB,5}  " +
                    $"{m.FieldC,5}  " +
                    $"{m.TextureId,4}  " +
                    $"{BitConverter.ToString(m.Signature).Replace('-', ' ')}");
            }

            Console.WriteLine(
                new string('═', 64));
        }

        // ═════════════════════════════════════
        // SHOW EMBEDDED INFO (NEW)
        // ═════════════════════════════════════
        private void ShowEmbeddedInfo()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Embedded RDTB Scan: " +
                Path.GetFileName(_filepath));
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));
            Console.WriteLine(
                $"    File size: " +
                $"{_data.Length:N0} B");
            Console.WriteLine(
                $"    Found: " +
                $"{_embeddedRdtbs.Count}" +
                $" embedded RDTBs");
            Console.WriteLine();

            if (_embeddedRdtbs.Count == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No embedded RDTBs.");
                Console.ResetColor();
                return;
            }

            for (int i = 0;
                 i < _embeddedRdtbs.Count;
                 i++)
            {
                var e = _embeddedRdtbs[i];
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    === Embedded RDTB " +
                    $"#{i:D2} ===");
                Console.ResetColor();
                Console.WriteLine(
                    $"    Offset     : " +
                    $"0x{e.Offset:X8}");
                Console.WriteLine(
                    $"    Size       : " +
                    $"{e.Size:N0} B");
                Console.WriteLine(
                    $"    ptr_count  : " +
                    $"{e.PtrCount}");
                Console.WriteLine(
                    $"    bone_count : " +
                    $"{e.BoneCount}");
                Console.WriteLine(
                    $"    chunks     : " +
                    $"{e.ChunkOffsets.Count}");
                Console.WriteLine();
            }

            Console.WriteLine(
                new string('═', 64));
        }

        // ═════════════════════════════════════
        // EXTRACT ALL (ENHANCED v2.0)
        // Now exports embedded RDTBs too
        // ═════════════════════════════════════
        private void ExtractAll(
            string outputFolder)
        {
            Directory.CreateDirectory(
                outputFolder);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extracting RDTB v2.0: " +
                Path.GetFileName(_filepath));
            Console.ResetColor();
            Console.WriteLine(
                $"    Output : {outputFolder}");
            Console.WriteLine(
                $"    Chunks : {_chunks.Count}");
            Console.WriteLine(
                $"    Bones  : {_boneCount}");
            Console.WriteLine(
                $"    Mats   : " +
                $"{_materials.Count}");
            Console.WriteLine(
                $"    Embedded RDTBs: " +
                $"{_embeddedRdtbs.Count}");
            Console.WriteLine();

            // ── Write chunk files ────────────
            foreach (var c in _chunks)
            {
                string dest = Path.Combine(
                    outputFolder, c.Filename);
                File.WriteAllBytes(
                    dest, c.Data);

                string eof =
                    c.HasEofTerminator
                    ? " [EOF✓]" : "";
                string vif =
                    c.HasVIFData
                    ? $" [VIF×{c.VIFBlockCount}]"
                    : "";
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    [{c.Index,2}] " +
                    $"{c.Filename,-32} " +
                    $"({c.Size,10:N0} B)  " +
                    $"@ 0x{c.Offset:X8}" +
                    $"{eof}{vif}");
                Console.ResetColor();
            }

            // ── Write skeleton CSV ───────────
            WriteSkeletonCsv(outputFolder);

            // ── Write embedded RDTBs ─────────
            if (_embeddedRdtbs.Count > 0)
            {
                string embDir = Path.Combine(
                    outputFolder,
                    "_embedded_rdtbs");
                Directory.CreateDirectory(
                    embDir);

                for (int i = 0;
                     i < _embeddedRdtbs.Count;
                     i++)
                {
                    var e = _embeddedRdtbs[i];
                    string embPath =
                        Path.Combine(embDir,
                            $"embedded_{i:D2}" +
                            $".rdtb");
                    File.WriteAllBytes(
                        embPath, e.RawData);
                    Console.ForegroundColor =
                        ConsoleColor.Magenta;
                    Console.WriteLine(
                        $"    [EMB {i:D2}] " +
                        $"embedded_{i:D2}.rdtb" +
                        $"  ({e.Size:N0} B)" +
                        $"  @ 0x{e.Offset:X8}");
                    Console.ResetColor();
                }
            }

            // ── Write manifest ───────────────
            WriteManifest(outputFolder);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Extraction complete!");
            Console.ResetColor();
            Console.WriteLine(
                $"     Folder : {outputFolder}");
        }

        // ═════════════════════════════════════
        // WRITE SKELETON CSV (CORRECTED v2.0)
        // ═════════════════════════════════════
        private void WriteSkeletonCsv(
            string outputFolder)
        {
            if (_skeleton == null) return;

            string path = Path.Combine(
                outputFolder, "skeleton.csv");

            var sb = new StringBuilder();
            sb.AppendLine(
                "# RDTB Skeleton Export v2.0" +
                " (CORRECTED bone layout)");
            sb.AppendLine(
                "# Source: " +
                Path.GetFileName(_filepath));
            sb.AppendLine(
                "# Bones:  " +
                _skeleton.BoneCount);
            sb.AppendLine(
                "# Layout: byte0=self," +
                " byte1=flags," +
                " byte2=child," +
                " byte3=parent," +
                " float4=X," +
                " float8=Y," +
                " float12=Z");
            sb.AppendLine();
            sb.AppendLine(
                "idx,self,parent,child," +
                "bind_x,bind_y,bind_z," +
                "flags,is_root");

            for (int i = 0;
                 i < _skeleton.Bones.Count;
                 i++)
            {
                var b = _skeleton.Bones[i];
                sb.AppendLine(
                    $"{i}," +
                    $"{b.SelfIndex}," +
                    $"{b.ParentIndex}," +
                    $"{b.ChildIndex}," +
                    $"{b.BindX:F6}," +
                    $"{b.BindY:F6}," +
                    $"{b.BindZ:F6}," +
                    $"0x{b.FlagsByte1:X2}," +
                    $"{(b.IsRoot ? "yes" : "no")}");
            }

            File.WriteAllText(
                path, sb.ToString(),
                Encoding.UTF8);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"    skeleton.csv  " +
                $"({_skeleton.BoneCount}" +
                $" bones) [v2.0 CORRECTED]");
            Console.ResetColor();
        }

        // ═════════════════════════════════════
        // WRITE MANIFEST (ENHANCED v2.0)
        // ═════════════════════════════════════
        private void WriteManifest(
            string outputFolder)
        {
            string path = Path.Combine(
                outputFolder,
                "rdtb_manifest.json");

            var sb = new StringBuilder();
            sb.AppendLine("{");

            sb.AppendLine(
                $"  \"_tool\": " +
                $"\"{TOOL_VERSION}\",");
            sb.AppendLine(
                $"  \"_credits\": " +
                $"\"{TOOL_CREDITS}\",");
            sb.AppendLine(
                $"  \"_game\": " +
                $"\"{TOOL_GAME}\",");
            sb.AppendLine(
                $"  \"_bone_layout\": " +
                $"\"CORRECTED v2.0: " +
                $"[0]=self [1]=flags " +
                $"[2]=child [3]=parent " +
                $"[4-7]=X [8-11]=Y " +
                $"[12-15]=Z\",");
            sb.AppendLine(
                $"  \"source_file\": \"" +
                Path.GetFileName(_filepath)
                    .Replace("\\", "/") +
                "\",");
            sb.AppendLine(
                $"  \"source_size\": " +
                $"{_data.Length},");
            sb.AppendLine(
                $"  \"unk_08_hex\": \"" +
                BitConverter
                    .ToString(_unk08)
                    .Replace("-", "")
                    .ToLower() +
                "\",");
            sb.AppendLine(
                $"  \"ptr_count\": " +
                $"{_ptrCount},");
            sb.AppendLine(
                $"  \"bone_count\": " +
                $"{_boneCount},");
            sb.AppendLine(
                $"  \"material_count\": " +
                $"{_materials.Count},");
            sb.AppendLine(
                $"  \"embedded_rdtb_count\": " +
                $"{_embeddedRdtbs.Count},");

            // ── Chunks array ─────────────────
            sb.AppendLine("  \"chunks\": [");
            for (int i = 0;
                 i < _chunks.Count; i++)
            {
                var c = _chunks[i];
                bool last =
                    i == _chunks.Count - 1;

                sb.AppendLine("    {");
                sb.AppendLine(
                    $"      \"index\": " +
                    $"{c.Index},");
                sb.AppendLine(
                    $"      \"filename\": " +
                    $"\"{c.Filename}\",");
                sb.AppendLine(
                    $"      \"label\": " +
                    $"\"{c.Label}\",");
                sb.AppendLine(
                    $"      \"description\": " +
                    $"\"{c.Description}\",");
                sb.AppendLine(
                    $"      \"offset\": " +
                    $"{c.Offset},");
                sb.AppendLine(
                    $"      \"offset_hex\": " +
                    $"\"0x{c.Offset:X8}\",");
                sb.AppendLine(
                    $"      \"size\": " +
                    $"{c.Size},");
                sb.AppendLine(
                    $"      \"size_hex\": " +
                    $"\"0x{c.Size:X8}\",");
                sb.AppendLine(
                    $"      \"has_eof\": " +
                    (c.HasEofTerminator
                        ? "true" : "false") +
                    ",");
                sb.AppendLine(
                    $"      \"has_vif\": " +
                    (c.HasVIFData
                        ? "true" : "false") +
                    ",");
                sb.AppendLine(
                    $"      \"vif_count\": " +
                    $"{c.VIFBlockCount}");
                sb.AppendLine(
                    "    }" +
                    (last ? "" : ","));
            }
            sb.AppendLine("  ],");

            // ── Embedded RDTBs array ─────────
            sb.AppendLine(
                "  \"embedded_rdtbs\": [");
            for (int i = 0;
                 i < _embeddedRdtbs.Count;
                 i++)
            {
                var e = _embeddedRdtbs[i];
                bool last = i ==
                    _embeddedRdtbs.Count - 1;
                sb.AppendLine("    {");
                sb.AppendLine(
                    $"      \"index\": {i},");
                sb.AppendLine(
                    $"      \"offset\": " +
                    $"{e.Offset},");
                sb.AppendLine(
                    $"      \"offset_hex\": " +
                    $"\"0x{e.Offset:X8}\",");
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
                    $"{e.ChunkOffsets.Count},");
                sb.AppendLine(
                    $"      \"filename\": " +
                    $"\"_embedded_rdtbs/" +
                    $"embedded_{i:D2}.rdtb\"");
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
                "    rdtb_manifest.json " +
                "[v2.0 enhanced]");
            Console.ResetColor();
        }

        // ═════════════════════════════════════
        // CREATE FROM FOLDER
        // (kept from original, works correctly)
        // ═════════════════════════════════════
        private void CreateFromFolder(
            string inputFolder,
            string outputPath)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Building RDTB v2.0 from: " +
                inputFolder);
            Console.ResetColor();

            string mfPath = Path.Combine(
                inputFolder,
                "rdtb_manifest.json");

            if (!File.Exists(mfPath))
            {
                throw new FileNotFoundException(
                    "rdtb_manifest.json not " +
                    "found in: " +
                    inputFolder +
                    "\nRun -xrdtb first.");
            }

            var mf = ReadManifest(mfPath);

            Console.WriteLine(
                $"    Source   : " +
                $"{mf.SourceFile}");
            Console.WriteLine(
                $"    Bones    : " +
                $"{mf.BoneCount}");
            Console.WriteLine(
                $"    Chunks   : " +
                $"{mf.Chunks.Count}");
            Console.WriteLine();

            var chunkData = new List<byte[]>();
            foreach (var entry in mf.Chunks)
            {
                string cp = Path.Combine(
                    inputFolder,
                    entry.Filename);

                if (!File.Exists(cp))
                {
                    throw new FileNotFoundException(
                        "Missing chunk: " +
                        entry.Filename);
                }

                byte[] raw =
                    File.ReadAllBytes(cp);
                chunkData.Add(raw);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    [{entry.Index,2}] " +
                    $"{entry.Filename,-32} " +
                    $"({raw.Length,10:N0} B)");
                Console.ResetColor();
            }

            if (chunkData.Count >
                OFFSET_TBL_SLOTS)
            {
                throw new InvalidDataException(
                    $"Too many chunks " +
                    $"({chunkData.Count} > " +
                    $"{OFFSET_TBL_SLOTS})");
            }

            byte[] result = AssembleRDTB(
                chunkData, mf.Unk08Hex,
                mf.PtrCount, mf.BoneCount);

            File.WriteAllBytes(
                outputPath, result);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] RDTB created: " +
                outputPath);
            Console.ResetColor();
            Console.WriteLine(
                $"     Size     : " +
                result.Length.ToString("N0") +
                " bytes");
            Console.WriteLine(
                $"     Original : " +
                mf.SourceSize.ToString("N0") +
                " bytes");

            if (result.Length == mf.SourceSize)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "     Match    : ✓ " +
                    "identical size");
                Console.ResetColor();
            }
            else
            {
                int diff =
                    result.Length - mf.SourceSize;
                string sign =
                    diff > 0 ? "+" : "";
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"     Diff     : " +
                    $"{sign}{diff:N0} bytes");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════════
        // REPLACE SINGLE CHUNK
        // ═════════════════════════════════════
        private void DoReplaceChunk(
            int chunkIndex,
            string chunkFile)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Replace Chunk");
            Console.ResetColor();

            if (!File.Exists(chunkFile))
                throw new FileNotFoundException(
                    "Chunk file not found: " +
                    chunkFile);

            if (chunkIndex < 0 ||
                chunkIndex >= _chunks.Count)
                throw new ArgumentException(
                    $"Chunk index {chunkIndex}" +
                    $" out of range");

            byte[] newData =
                File.ReadAllBytes(chunkFile);
            var oldChunk = _chunks[chunkIndex];

            Console.WriteLine(
                $"    Old: {oldChunk.Size:N0} B");
            Console.WriteLine(
                $"    New: {newData.Length:N0} B");

            _chunks[chunkIndex] =
                new RDTBChunk
                {
                    Index = chunkIndex,
                    Offset = oldChunk.Offset,
                    Size = newData.Length,
                    Data = newData,
                };

            var allData = new List<byte[]>();
            foreach (var c in _chunks)
                allData.Add(c.Data);

            string unk08Hex = BitConverter
                .ToString(_unk08)
                .Replace("-", "")
                .ToLower();

            byte[] result = AssembleRDTB(
                allData, unk08Hex,
                _ptrCount, _boneCount);

            File.WriteAllBytes(
                _filepath, result);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Chunk replaced!");
            Console.ResetColor();
        }

        // ═════════════════════════════════════
        // ASSEMBLE RDTB
        // ═════════════════════════════════════
        private static byte[] AssembleRDTB(
            List<byte[]> chunkData,
            string unk08Hex,
            int ptrCount,
            int boneCount)
        {
            byte[] unk08 =
                HexStringToBytes(unk08Hex);

            if (chunkData == null ||
                chunkData.Count == 0)
                throw new ArgumentException(
                    "No chunks provided");

            // NO ALIGNMENT AT ALL.
            // The original RDTB places chunks
            // back-to-back with no gaps and no
            // 16-byte alignment. Even the first
            // chunk starts at 0x48 (not aligned).
            // Just place each chunk immediately
            // after the previous one.
            var offsets =
                new int[chunkData.Count];
            int cursor = HEADER_SIZE;
            for (int i = 0;
                 i < chunkData.Count; i++)
            {
                offsets[i] = cursor;
                cursor += chunkData[i].Length;
            }

            // Build header
            byte[] header =
                new byte[HEADER_SIZE];
            header[0] = (byte)'R';
            header[1] = (byte)'D';
            header[2] = (byte)'T';
            header[3] = (byte)'B';
            header[4] = 0x00;
            header[5] = 0x01;
            header[6] = 0x00;
            header[7] = 0x00;

            if (unk08 == null ||
                unk08.Length < 4)
                unk08 = new byte[] {
                    0x00, 0x76, 0x07, 0x40 };
            Array.Copy(
                unk08, 0, header, 8, 4);

            header[0x0C] =
                (byte)(ptrCount & 0xFF);
            header[0x0D] =
                (byte)((ptrCount >> 8) & 0xFF);
            header[0x0E] =
                (byte)(boneCount & 0xFF);
            header[0x0F] =
                (byte)((boneCount >> 8) & 0xFF);

            for (int i = 0;
                 i < offsets.Length; i++)
            {
                int pos =
                    OFFSET_TBL_START + i * 4;
                header[pos + 0] =
                    (byte)(offsets[i] & 0xFF);
                header[pos + 1] =
                    (byte)((offsets[i] >> 8)
                           & 0xFF);
                header[pos + 2] =
                    (byte)((offsets[i] >> 16)
                           & 0xFF);
                header[pos + 3] =
                    (byte)((offsets[i] >> 24)
                           & 0xFF);
            }

            // Assemble (gaps between chunks
            // stay as zero from new[])
            byte[] result =
                new byte[cursor];
            Array.Copy(
                header, 0, result, 0,
                HEADER_SIZE);
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

        // ═════════════════════════════════════
        // SHOW SKELETON TREE
        // ═════════════════════════════════════
        private void ShowSkeletonTree()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Skeleton Tree v2.0: " +
                Path.GetFileName(_filepath) +
                $"  ({_boneCount} bones)");
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));

            if (_skeleton == null)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    No skeleton data.");
                Console.ResetColor();
                return;
            }

            var bones = _skeleton.Bones;
            var children =
                new Dictionary<int, List<int>>();
            var roots = new List<int>();

            for (int i = 0;
                 i < bones.Count; i++)
                children[i] = new List<int>();

            for (int i = 0;
                 i < bones.Count; i++)
            {
                var b = bones[i];
                if (b.IsRoot)
                {
                    roots.Add(i);
                }
                else if (
                    b.ParentIndex <
                    bones.Count)
                {
                    children[b.ParentIndex]
                        .Add(i);
                }
            }

            if (roots.Count == 0)
            {
                for (int i = 0;
                     i < bones.Count; i++)
                    PrintBoneNode(
                        i, bones, children,
                        "  ", true);
            }
            else
            {
                for (int i = 0;
                     i < roots.Count; i++)
                    PrintBoneNode(
                        roots[i],
                        bones, children,
                        "  ",
                        i == roots.Count - 1);
            }

            Console.WriteLine(
                new string('═', 64));
        }

        private static void PrintBoneNode(
            int idx,
            List<RDTBBone> bones,
            Dictionary<int, List<int>> children,
            string prefix,
            bool isLast)
        {
            var b = bones[idx];
            string conn =
                isLast ? "└─" : "├─";

            Console.Write(prefix + conn);
            Console.ForegroundColor =
                b.IsRoot
                ? ConsoleColor.Yellow
                : ConsoleColor.White;
            Console.Write(
                $"[{idx,3}] " +
                $"s={b.SelfIndex,3}  " +
                $"X={b.BindX,7:F3}  " +
                $"Y={b.BindY,7:F3}  " +
                $"Z={b.BindZ,7:F3}");
            if (b.IsRoot)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.Write(" ◄ROOT");
            }
            Console.ResetColor();
            Console.WriteLine();

            string ext =
                isLast ? "   " : "│  ";
            var ch =
                children.ContainsKey(idx)
                ? children[idx]
                : new List<int>();

            for (int j = 0;
                 j < ch.Count; j++)
            {
                PrintBoneNode(
                    ch[j], bones, children,
                    prefix + ext,
                    j == ch.Count - 1);
            }
        }

        // ═════════════════════════════════════
        // COMPARE WITH ANOTHER RDTB
        // ═════════════════════════════════════
        private void CompareWith(
            RDTBArchive other)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Compare RDTB Files v2.0");
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));
            Console.WriteLine(
                $"    A: " +
                Path.GetFileName(_filepath) +
                $"  ({_data.Length:N0} B)");
            Console.WriteLine(
                $"    B: " +
                Path.GetFileName(
                    other._filepath) +
                $"  ({other._data.Length:N0} B)");
            Console.WriteLine();

            CmpField("file_size",
                _data.Length.ToString("N0"),
                other._data.Length.ToString("N0"),
                _data.Length ==
                other._data.Length);
            CmpField("ptr_count",
                _ptrCount.ToString(),
                other._ptrCount.ToString(),
                _ptrCount ==
                other._ptrCount);
            CmpField("bone_count",
                _boneCount.ToString(),
                other._boneCount.ToString(),
                _boneCount ==
                other._boneCount);
            CmpField("chunk_count",
                _chunks.Count.ToString(),
                other._chunks.Count.ToString(),
                _chunks.Count ==
                other._chunks.Count);

            Console.WriteLine();
            int mx = Math.Max(
                _chunks.Count,
                other._chunks.Count);

            for (int i = 0; i < mx; i++)
            {
                var ca = i < _chunks.Count
                    ? _chunks[i] : null;
                var cb =
                    i < other._chunks.Count
                    ? other._chunks[i] : null;

                int sza = ca?.Size ?? 0;
                int szb = cb?.Size ?? 0;
                bool same = sza == szb;

                Console.ForegroundColor = same
                    ? ConsoleColor.Green
                    : ConsoleColor.Red;
                Console.Write(
                    $"    {(same ? "✓" : "✗")}  " +
                    $"[{i,2}] " +
                    $"{GetChunkLabel(i),-18}  ");
                Console.ResetColor();
                Console.WriteLine(
                    $"A: {sza,10:N0} B  " +
                    $"B: {szb,10:N0} B");
            }

            Console.WriteLine(
                new string('═', 64));
        }

        private static void CmpField(
            string label,
            string a, string b,
            bool same)
        {
            Console.ForegroundColor = same
                ? ConsoleColor.Green
                : ConsoleColor.Red;
            Console.Write(
                $"    {(same ? "✓" : "✗")}  " +
                $"{label,-20}");
            Console.ResetColor();
            Console.WriteLine(
                $"A={a}   B={b}");
        }

        // ═════════════════════════════════════
        // VERIFY (byte-for-byte)
        // ═════════════════════════════════════
        private void VerifyAgainst(
            string rebuiltPath)
        {
            byte[] orig =
                File.ReadAllBytes(_filepath);
            byte[] reb =
                File.ReadAllBytes(rebuiltPath);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Verify RDTB v2.0");
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));
            Console.WriteLine(
                "    Original: " +
                Path.GetFileName(_filepath) +
                $"  ({orig.Length:N0} B)");
            Console.WriteLine(
                "    Rebuilt : " +
                Path.GetFileName(rebuiltPath) +
                $"  ({reb.Length:N0} B)");

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
                    "    IDENTICAL ✓");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    $"    First diff @ " +
                    $"0x{diffOff:X8}");
                Console.ResetColor();

                int total = 0;
                for (int i = 0;
                     i < minLen; i++)
                    if (orig[i] != reb[i])
                        total++;
                total += Math.Abs(
                    orig.Length - reb.Length);
                Console.WriteLine(
                    $"    Total diff: " +
                    $"{total:N0} bytes");
            }

            Console.WriteLine(
                new string('═', 64));
        }

        // ═════════════════════════════════════
        // SCAN FOLDER
        // ═════════════════════════════════════
        private static void DoScanFolder(
            string folderPath)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Folder Scan v2.0: " +
                folderPath);
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 72));

            if (!Directory.Exists(folderPath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  Folder not found.");
                Console.ResetColor();
                return;
            }

            string[] files =
                Directory.GetFiles(
                    folderPath,
                    "*.rdtb",
                    SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  No .rdtb files found.");
                Console.ResetColor();
                return;
            }

            Array.Sort(files);

            Console.WriteLine(
                $"  {"FILE",-32} " +
                $"{"SIZE",12}  " +
                $"{"CHUNKS",7}  " +
                $"{"BONES",6}  " +
                $"{"MATS",5}  " +
                $"{"EMB",4}  NOTE");
            Console.WriteLine(
                new string('─', 80));

            foreach (string f in files)
            {
                try
                {
                    var arc =
                        new RDTBArchive(f);
                    arc.Load();

                    string name =
                        Path.GetFileName(f);
                    if (name.Length > 32)
                        name =
                            name.Substring(0, 29)
                            + "...";

                    string note =
                        ClassifyRDTB(
                            arc._chunks,
                            arc._boneCount,
                            arc._data.Length);

                    Console.ForegroundColor =
                        GetNoteColor(note);
                    Console.WriteLine(
                        $"  {name,-32} " +
                        $"{arc._data.Length,12:N0}  " +
                        $"{arc._chunks.Count,7}  " +
                        $"{arc._boneCount,6}  " +
                        $"{arc._materials.Count,5}  " +
                        $"{arc._embeddedRdtbs.Count,4}  " +
                        $"{note}");
                    Console.ResetColor();
                }
                catch
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        $"  {Path.GetFileName(f),-32}" +
                        $" ERROR");
                    Console.ResetColor();
                }
            }

            Console.WriteLine(
                new string('═', 72));
        }

        private static string ClassifyRDTB(
            List<RDTBChunk> chunks,
            int boneCount,
            int fileSize)
        {
            if (fileSize < 50_000)
                return "PROP/TOOL";
            if (fileSize < 200_000 &&
                boneCount < 20)
                return "SIMPLE NPC";
            if (fileSize < 700_000 &&
                boneCount >= 20)
                return "NPC CHARACTER";
            if (fileSize >= 700_000)
                return "PLAYER/COMPLEX";
            return "UNKNOWN";
        }

        private static ConsoleColor
            GetNoteColor(string note)
        {
            if (note.Contains("PLAYER"))
                return ConsoleColor.Yellow;
            if (note.Contains("NPC CHARACTER"))
                return ConsoleColor.Green;
            if (note.Contains("PROP") ||
                note.Contains("TOOL"))
                return ConsoleColor.Cyan;
            return ConsoleColor.Gray;
        }

        // ═════════════════════════════════════
        // EOF CHECK
        // ═════════════════════════════════════
        private bool HasEofTerminator()
        {
            if (_data == null ||
                _data.Length < 16)
                return false;
            int o = _data.Length - 16;
            return
                _data[o + 0] == 0x00 &&
                _data[o + 1] == 0x00 &&
                _data[o + 2] == 0x00 &&
                _data[o + 3] == 0x70 &&
                _data[o + 4] == 0x00 &&
                _data[o + 5] == 0x00 &&
                _data[o + 6] == 0x00 &&
                _data[o + 7] == 0x00;
        }

        // ═════════════════════════════════════
        // MANIFEST READER
        // ═════════════════════════════════════
        private static RDTBManifest ReadManifest(
            string path)
        {
            string json = File.ReadAllText(
                path, Encoding.UTF8);

            var mf = new RDTBManifest
            {
                Chunks =
                    new List<RDTBManifestChunk>(),
                EmbeddedRdtbs =
                    new List<EmbeddedRDTBManifest>(),
            };

            mf.SourceFile = JsonReadString(
                json, "source_file");
            mf.SourceSize = JsonReadInt(
                json, "source_size");
            mf.Unk08Hex = JsonReadString(
                json, "unk_08_hex");
            mf.PtrCount = JsonReadInt(
                json, "ptr_count");
            mf.BoneCount = JsonReadInt(
                json, "bone_count");

            int chunksStart =
                json.IndexOf("\"chunks\":");
            if (chunksStart < 0)
                throw new InvalidDataException(
                    "Manifest missing chunks");

            int arrStart =
                json.IndexOf('[', chunksStart);
            // Find matching ]
            int depth = 0;
            int arrEnd = -1;
            for (int i = arrStart;
                 i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        arrEnd = i;
                        break;
                    }
                }
            }

            if (arrStart < 0 || arrEnd < 0)
                throw new InvalidDataException(
                    "Manifest: bad chunks");

            string chunksJson =
                json.Substring(
                    arrStart,
                    arrEnd - arrStart + 1);

            int pos = 0;
            while (pos < chunksJson.Length)
            {
                int objStart =
                    chunksJson.IndexOf(
                        '{', pos);
                if (objStart < 0) break;

                int objEnd =
                    chunksJson.IndexOf(
                        '}', objStart);
                if (objEnd < 0) break;

                string obj =
                    chunksJson.Substring(
                        objStart,
                        objEnd - objStart + 1);

                var chunk =
                    new RDTBManifestChunk
                    {
                        Index = JsonReadInt(
                            obj, "index"),
                        Filename = JsonReadString(
                            obj, "filename"),
                        Label = JsonReadString(
                            obj, "label"),
                        Offset = JsonReadInt(
                            obj, "offset"),
                        Size = JsonReadInt(
                            obj, "size"),
                        HasEof = JsonReadBool(
                            obj, "has_eof"),
                        HasVIF = JsonReadBool(
                            obj, "has_vif"),
                        VIFCount = JsonReadInt(
                            obj, "vif_count"),
                    };

                mf.Chunks.Add(chunk);
                pos = objEnd + 1;
            }

            mf.Chunks.Sort(
                (a, b) =>
                a.Index.CompareTo(b.Index));

            return mf;
        }

        // ─────────────────────────────────────
        // JSON helpers (simple, no dependency)
        // ─────────────────────────────────────
        private static string JsonReadString(
            string json, string key)
        {
            string search = $"\"{key}\"";
            int ki = json.IndexOf(search);
            if (ki < 0) return "";
            int colon =
                json.IndexOf(':', ki);
            if (colon < 0) return "";
            int q1 =
                json.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            int q2 =
                json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(
                q1 + 1, q2 - q1 - 1);
        }

        private static int JsonReadInt(
            string json, string key)
        {
            string search = $"\"{key}\":";
            int ki = json.IndexOf(search);
            if (ki < 0) return 0;
            int vs = ki + search.Length;
            while (vs < json.Length &&
                   (json[vs] == ' ' ||
                    json[vs] == '\t' ||
                    json[vs] == '\r' ||
                    json[vs] == '\n'))
                vs++;
            if (vs >= json.Length) return 0;
            int ve = vs;
            while (ve < json.Length &&
                   (char.IsDigit(json[ve]) ||
                    json[ve] == '-'))
                ve++;
            if (ve == vs) return 0;
            int.TryParse(
                json.Substring(vs, ve - vs),
                out int result);
            return result;
        }

        private static bool JsonReadBool(
            string json, string key)
        {
            string search = $"\"{key}\":";
            int ki = json.IndexOf(search);
            if (ki < 0) return false;
            int vs = ki + search.Length;
            while (vs < json.Length &&
                   json[vs] == ' ')
                vs++;
            return
                vs < json.Length &&
                json[vs] == 't';
        }

        // ═════════════════════════════════════
        // UTILITY
        // ═════════════════════════════════════
        private static byte[] HexStringToBytes(
            string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return new byte[4];
            hex = hex
                .Replace(" ", "")
                .Replace("-", "");
            if (hex.Length % 2 != 0)
                hex = "0" + hex;
            byte[] result =
                new byte[hex.Length / 2];
            for (int i = 0;
                 i < result.Length; i++)
            {
                result[i] = Convert.ToByte(
                    hex.Substring(i * 2, 2),
                    16);
            }
            return result;
        }
    }
}
