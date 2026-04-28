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
    internal class RDTBBone
    {
        // ─────────────────────────────────────────
        // CONFIRMED LAYOUT (from hex analysis)
        //  0x00  float32  bind_x
        //  0x04  float32  bind_y
        //  0x08  float32  bind_z
        //  0x0C  uint8    self_index
        //  0x0D  uint8    parent_index (0xFF = root)
        //  0x0E  uint8    child_index  (0xFF = none)
        //  0x0F  uint8    flags
        // ─────────────────────────────────────────
        public float BindX { get; set; }
        public float BindY { get; set; }
        public float BindZ { get; set; }
        public byte SelfIndex { get; set; }
        public byte ParentIndex { get; set; }
        public byte ChildIndex { get; set; }
        public byte Flags { get; set; }

        public bool IsRoot => ParentIndex == 0xFF;
        public bool HasChild => ChildIndex != 0xFF;

        public byte[] RawBytes { get; set; }

        // ─────────────────────────────────────────
        // Parse from raw bytes
        // ─────────────────────────────────────────
        public static RDTBBone FromBytes(
            byte[] data, int offset)
        {
            byte[] raw = new byte[16];
            Array.Copy(data, offset, raw, 0, 16);

            return new RDTBBone
            {
                BindX = BitConverter.ToSingle(
                                  data, offset + 0),
                BindY = BitConverter.ToSingle(
                                  data, offset + 4),
                BindZ = BitConverter.ToSingle(
                                  data, offset + 8),
                SelfIndex = data[offset + 12],
                ParentIndex = data[offset + 13],
                ChildIndex = data[offset + 14],
                Flags = data[offset + 15],
                RawBytes = raw,
            };
        }

        // ─────────────────────────────────────────
        // Serialize back to 16 bytes
        // ─────────────────────────────────────────
        public byte[] ToBytes()
        {
            byte[] buf = new byte[16];
            Array.Copy(
                BitConverter.GetBytes(BindX),
                0, buf, 0, 4);
            Array.Copy(
                BitConverter.GetBytes(BindY),
                0, buf, 4, 4);
            Array.Copy(
                BitConverter.GetBytes(BindZ),
                0, buf, 8, 4);
            buf[12] = SelfIndex;
            buf[13] = ParentIndex;
            buf[14] = ChildIndex;
            buf[15] = Flags;
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
                $"flags=0x{Flags:X2}";
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
        public List<RDTBManifestChunk>
                      Chunks
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
    }

    // ═════════════════════════════════════════════
    // RDTB ARCHIVE  (main class)
    // ═════════════════════════════════════════════
    public class RDTBArchive
    {
        // ─────────────────────────────────────────
        // CONSTANTS
        // ─────────────────────────────────────────
        private const int HEADER_SIZE = 0x48;
        private const int OFFSET_TBL_START = 0x10;
        private const int OFFSET_TBL_SLOTS = 14;
        private const int BONE_PTR_SIZE = 4;
        private const int BONE_REC_SIZE = 16;

        private const string TOOL_VERSION =
            "HMSTHModdingTool v1.3.0-Beta";
        private const string TOOL_CREDITS =
            "gdkchan (original), " +
            "DarthKrayt333 (upgrade)";
        private const string TOOL_GAME =
            "Harvest Moon Save The Homeland (PS2)";

        private static readonly byte[] MAGIC =
        {
            0x52, 0x44, 0x54, 0x42  // "RDTB"
        };
        private static readonly byte[] VERSION =
        {
            0x00, 0x01, 0x00, 0x00
        };
        private static readonly byte[] EOF_TERM =
        {
            0x00, 0x00, 0x00, 0x70,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        };

        // ─────────────────────────────────────────
        // PRIVATE FIELDS
        // ─────────────────────────────────────────
        private string _filepath;
        private byte[] _data;
        private byte[] _unk08;
        private int _ptrCount;
        private int _boneCount;
        private List<int> _offsets;
        private List<RDTBChunk> _chunks;
        private RDTBSkeleton _skeleton;

        // ─────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────
        public RDTBArchive(string filepath)
        {
            _filepath = filepath;
            _offsets = new List<int>();
            _chunks = new List<RDTBChunk>();
            _unk08 = new byte[4];
        }

        // ─────────────────────────────────────────
        // READ HELPERS
        // ─────────────────────────────────────────
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

        // ═════════════════════════════════════════
        // STATIC ENTRY POINTS
        // ═════════════════════════════════════════
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

        // ═════════════════════════════════════════
        // LOAD
        // ═════════════════════════════════════════
        public void Load()
        {
            _data =
                File.ReadAllBytes(_filepath);

            // ── Validate magic ───────────────────
            if (_data[0] != 'R' ||
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

            // ── Header fields ────────────────────
            _unk08 = GetBytes(8, 4);
            _ptrCount = ReadUInt16(0x0C);
            _boneCount = ReadUInt16(0x0E);

            // ── Offset table ─────────────────────
            LoadOffsets();

            // ── Slice chunks ─────────────────────
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

            // ── Parse skeleton from chunk 0 ──────
            ParseSkeleton();
        }

        // ═════════════════════════════════════════
        // LOAD OFFSETS  (safe)
        // ═════════════════════════════════════════
        private void LoadOffsets()
        {
            _offsets.Clear();

            for (int slot = 0;
                 slot < OFFSET_TBL_SLOTS;
                 slot++)
            {
                int pos =
                    OFFSET_TBL_START + slot * 4;
                int val = ReadInt32(pos);

                // Zero = unused slot = end
                if (val == 0) break;

                // Sanity check
                if (val < HEADER_SIZE ||
                    val > _data.Length)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        $"[!] Offset[{slot}] " +
                        $"= 0x{val:X8} " +
                        $"suspicious - stopping");
                    Console.ResetColor();
                    break;
                }

                _offsets.Add(val);
            }
        }

        // ═════════════════════════════════════════
        // PARSE SKELETON
        // ═════════════════════════════════════════
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

            // ── Read bone pointers ───────────────
            var ptrs = new List<uint>();
            for (int i = 0; i < _boneCount; i++)
            {
                ptrs.Add(
                    BitConverter.ToUInt32(
                        dat,
                        i * BONE_PTR_SIZE));
            }

            // ── Read bone records ────────────────
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

        // ═════════════════════════════════════════
        // SHOW INFO
        // ═════════════════════════════════════════
        private void ShowInfo(bool showBones)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Info: " +
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

            // ── EOF check ────────────────────────
            bool hasEof = HasEofTerminator();
            Console.ForegroundColor = hasEof
                ? ConsoleColor.Green
                : ConsoleColor.Yellow;
            Console.WriteLine(
                $"    EOF term      : " +
                (hasEof
                    ? "00 00 00 70 ... ✓"
                    : BitConverter
                        .ToString(GetBytes(
                            _data.Length - 16,
                            16))
                        .Replace('-', ' ') +
                      " (unexpected!)"));
            Console.ResetColor();

            // ── Chunk table ──────────────────────
            Console.WriteLine();
            Console.WriteLine(
                "    " +
                new string('─', 60));
            Console.WriteLine(
                $"    {"#",3}  " +
                $"{"OFFSET",10}  " +
                $"{"SIZE",10}  " +
                $"{"SIZE_B",12}  LABEL");
            Console.WriteLine(
                "    " +
                new string('─', 60));

            foreach (var c in _chunks)
            {
                string eof =
                    c.HasEofTerminator
                    ? " [EOF]" : "";
                Console.WriteLine(
                    $"    [{c.Index,2}]  " +
                    $"0x{c.Offset:X8}  " +
                    $"0x{c.Size:X8}  " +
                    $"{c.Size,12:N0} B  " +
                    $"{c.Label}{eof}");
            }

            Console.WriteLine(
                "    " +
                new string('─', 60));

            // ── Skeleton summary ─────────────────
            if (_skeleton != null)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"    Skeleton " +
                    $"({_skeleton.BoneCount}" +
                    $" bones)");
                Console.ResetColor();

                if (_chunks.Count > 0)
                {
                    var c0 = _chunks[0];
                    int ptrs =
                        _boneCount *
                        BONE_PTR_SIZE;
                    int recs =
                        _boneCount *
                        BONE_REC_SIZE;
                    Console.WriteLine(
                        $"    Ptr array : " +
                        $"0x{c0.Offset:X8}" +
                        $" + 0x{ptrs:X4}" +
                        $" = 0x{c0.Offset + ptrs:X8}");
                    Console.WriteLine(
                        $"    Rec array : " +
                        $"0x{c0.Offset + ptrs:X8}" +
                        $" + 0x{recs:X4}" +
                        $" = 0x{c0.Offset + ptrs + recs:X8}");

                    if (_chunks.Count > 1)
                    {
                        int used = ptrs + recs;
                        int remain =
                            c0.Size - used;
                        Console.WriteLine(
                            $"    Used      : " +
                            $"{used:N0} / " +
                            $"{c0.Size:N0} B" +
                            $"  ({remain:N0}" +
                            $" B extra)");
                    }
                }

                // ── Bone table ───────────────────
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
                        $"FLAGS  NOTE");
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
                            : b.Flags != 0
                                ? $" flags=" +
                                  $"0x{b.Flags:X2}"
                                : "";

                        Console.WriteLine(
                            $"    [{i,3}]  " +
                            $"{b.SelfIndex,4}  " +
                            $"{par,4}  " +
                            $"{chl,4}  " +
                            $"{b.BindX,10:F4}  " +
                            $"{b.BindY,10:F4}  " +
                            $"{b.BindZ,10:F4}  " +
                            $"0x{b.Flags:X2}  " +
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

        // ═════════════════════════════════════════
        // EXTRACT ALL
        // ═════════════════════════════════════════
        private void ExtractAll(
            string outputFolder)
        {
            Directory.CreateDirectory(
                outputFolder);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Extracting RDTB: " +
                Path.GetFileName(_filepath));
            Console.ResetColor();
            Console.WriteLine(
                $"    Output : {outputFolder}");
            Console.WriteLine(
                $"    Chunks : {_chunks.Count}");
            Console.WriteLine(
                $"    Bones  : {_boneCount}");
            Console.WriteLine();

            // ── Write chunk files ────────────────
            foreach (var c in _chunks)
            {
                string dest = Path.Combine(
                    outputFolder, c.Filename);
                File.WriteAllBytes(
                    dest, c.Data);

                string eof =
                    c.HasEofTerminator
                    ? " [EOF✓]" : "";
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"    [{c.Index,2}] " +
                    $"{c.Filename,-32} " +
                    $"({c.Size,10:N0} B)  " +
                    $"@ 0x{c.Offset:X8}" +
                    $"{eof}");
                Console.ResetColor();
            }

            // ── Write skeleton CSV ───────────────
            WriteSkeletonCsv(outputFolder);

            // ── Write manifest ───────────────────
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

        // ═════════════════════════════════════════
        // WRITE SKELETON CSV
        // ═════════════════════════════════════════
        private void WriteSkeletonCsv(
            string outputFolder)
        {
            if (_skeleton == null) return;

            string path = Path.Combine(
                outputFolder, "skeleton.csv");

            var sb = new StringBuilder();
            sb.AppendLine(
                "# RDTB Skeleton Export");
            sb.AppendLine(
                "# Source: " +
                Path.GetFileName(_filepath));
            sb.AppendLine(
                "# Bones:  " +
                _skeleton.BoneCount);
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
                    $"0x{b.Flags:X2}," +
                    $"{(b.IsRoot ? "yes" : "no")}");
            }

            File.WriteAllText(
                path, sb.ToString(),
                Encoding.UTF8);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"    skeleton.csv  " +
                $"({_skeleton.BoneCount} bones)");
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // WRITE MANIFEST (simple JSON)
        // ═════════════════════════════════════════
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
                $"  \"source_file\": \"" +
                Path.GetFileName(_filepath) +
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

            // ── Chunks array ─────────────────────
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
                        ? "true" : "false"));
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
                "    rdtb_manifest.json");
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // CREATE FROM FOLDER
        // ═════════════════════════════════════════
        private void CreateFromFolder(
            string inputFolder,
            string outputPath)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Building RDTB from: " +
                inputFolder);
            Console.ResetColor();

            // ── Load manifest ────────────────────
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

            // ── Load chunk files ─────────────────
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

            // ── Build and write ──────────────────
            byte[] result = AssembleRDTB(
                chunkData, mf.Unk08Hex,
                mf.PtrCount, mf.BoneCount);

            ValidateBuilt(result, mf);

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
                    "identical size to original");
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
                    $"{sign}{diff:N0} bytes " +
                    "(chunk was modified)");
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════════════
        // REPLACE SINGLE CHUNK
        // ═════════════════════════════════════════
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
            Console.WriteLine(
                new string('═', 64));
            Console.WriteLine(
                $"    RDTB  : " +
                Path.GetFileName(_filepath));
            Console.WriteLine(
                $"    Chunk : [{chunkIndex}] " +
                GetChunkLabel(chunkIndex));
            Console.WriteLine(
                $"    File  : " +
                Path.GetFileName(chunkFile));

            if (!File.Exists(chunkFile))
            {
                throw new FileNotFoundException(
                    "Chunk file not found: " +
                    chunkFile);
            }

            if (chunkIndex < 0 ||
                chunkIndex >= _chunks.Count)
            {
                throw new ArgumentException(
                    $"Chunk index {chunkIndex}" +
                    $" out of range " +
                    $"[0, {_chunks.Count - 1}]");
            }

            byte[] newData =
                File.ReadAllBytes(chunkFile);
            var oldChunk = _chunks[chunkIndex];

            Console.WriteLine(
                $"    Old size : " +
                $"{oldChunk.Size:N0} bytes " +
                $"(0x{oldChunk.Size:X8})");
            Console.WriteLine(
                $"    New size : " +
                $"{newData.Length:N0} bytes " +
                $"(0x{newData.Length:X8})");

            // ── Safety warnings ──────────────────
            if (chunkIndex == 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    [!] WARNING: Replacing" +
                    " skeleton chunk!");
                Console.WriteLine(
                    "        Bone count must " +
                    "match animation chunks.");
                Console.ResetColor();
            }

            if (chunkIndex == 2)
            {
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "    [i] Replacing main mesh.");
                Console.WriteLine(
                    "        Also replace chunk" +
                    " 11 (weight_uv) if from " +
                    "same source.");
                Console.ResetColor();
            }

            if (chunkIndex == 11)
            {
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "    [i] Replacing weight/" +
                    "UV chunk.");
                Console.WriteLine(
                    "        Make sure chunk 2" +
                    " (mesh_main) is also from" +
                    " the same source.");
                Console.ResetColor();
            }

            // ── Replace chunk data in list ───────
            _chunks[chunkIndex] =
                new RDTBChunk
                {
                    Index = chunkIndex,
                    Offset = oldChunk.Offset,
                    Size = newData.Length,
                    Data = newData,
                };

            // ── Rebuild chunk data list ──────────
            var allData = new List<byte[]>();
            foreach (var c in _chunks)
                allData.Add(c.Data);

            // ── Read manifest for header vals ────
            // Use current loaded values
            string unk08Hex = BitConverter
                .ToString(_unk08)
                .Replace("-", "")
                .ToLower();

            byte[] result = AssembleRDTB(
                allData, unk08Hex,
                _ptrCount, _boneCount);

            File.WriteAllBytes(
                _filepath, result);

            // ── Update internal state ────────────
            _data = result;
            LoadOffsets();
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

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Chunk replaced!");
            Console.ResetColor();
            Console.WriteLine(
                $"     RDTB    : " +
                _filepath);
            Console.WriteLine(
                $"     Chunk   : [{chunkIndex}]" +
                $" {GetChunkLabel(chunkIndex)}");
            Console.WriteLine(
                $"     Old sz  : " +
                $"{oldChunk.Size:N0} B");
            Console.WriteLine(
                $"     New sz  : " +
                $"{newData.Length:N0} B");
            Console.WriteLine(
                $"     RDTB sz : " +
                $"{result.Length:N0} B");
            Console.WriteLine(
                new string('═', 64));
        }

        // ═════════════════════════════════════════
        // ASSEMBLE RDTB (shared builder)
        // ═════════════════════════════════════════
        private static byte[] AssembleRDTB(
            List<byte[]> chunkData,
            string unk08Hex,
            int ptrCount,
            int boneCount)
        {
            // ── Calculate offsets ────────────────
            var offsets = new List<int>();
            int cursor = HEADER_SIZE;
            foreach (var raw in chunkData)
            {
                offsets.Add(cursor);
                cursor += raw.Length;
            }

            // ── Build header (72 bytes) ──────────
            byte[] hdr = new byte[HEADER_SIZE];

            // Magic
            hdr[0] = (byte)'R';
            hdr[1] = (byte)'D';
            hdr[2] = (byte)'T';
            hdr[3] = (byte)'B';

            // Version
            hdr[4] = 0x00;
            hdr[5] = 0x01;
            hdr[6] = 0x00;
            hdr[7] = 0x00;

            // unk_08
            byte[] unk08 =
                HexStringToBytes(unk08Hex);
            Array.Copy(
                unk08, 0, hdr, 8,
                Math.Min(4, unk08.Length));

            // ptr_count @ 0x0C
            Array.Copy(
                BitConverter.GetBytes(
                    (ushort)ptrCount),
                0, hdr, 0x0C, 2);

            // bone_count @ 0x0E
            Array.Copy(
                BitConverter.GetBytes(
                    (ushort)boneCount),
                0, hdr, 0x0E, 2);

            // Offset table @ 0x10
            for (int i = 0;
                 i < offsets.Count; i++)
            {
                int pos =
                    OFFSET_TBL_START + i * 4;
                Array.Copy(
                    BitConverter.GetBytes(
                        offsets[i]),
                    0, hdr, pos, 4);
            }

            // ── Assemble ─────────────────────────
            using (var ms = new MemoryStream())
            {
                ms.Write(hdr, 0, hdr.Length);
                foreach (var raw in chunkData)
                    ms.Write(
                        raw, 0, raw.Length);
                return ms.ToArray();
            }
        }

        // ═════════════════════════════════════════
        // VALIDATE BUILT FILE
        // ═════════════════════════════════════════
        private void ValidateBuilt(
            byte[] data,
            RDTBManifest mf)
        {
            int errors = 0;

            if (data[0] != 'R' ||
                data[1] != 'D' ||
                data[2] != 'T' ||
                data[3] != 'B')
            {
                TextOut.PrintError(
                    "Built file: bad magic!");
                errors++;
            }

            int off0 = BitConverter.ToInt32(
                data, 0x10);
            if (off0 != HEADER_SIZE)
            {
                TextOut.PrintError(
                    $"Offset[0] = " +
                    $"0x{off0:X8} " +
                    $"!= 0x{HEADER_SIZE:X8}");
                errors++;
            }

            int bc = BitConverter.ToUInt16(
                data, 0x0E);
            int need =
                bc * (BONE_PTR_SIZE +
                      BONE_REC_SIZE);
            int off1 = BitConverter.ToInt32(
                data, 0x14);
            int c0sz = off1 > off0
                ? off1 - off0 : 0;

            bool hasEof =
                data.Length >= 16 &&
                data[data.Length - 16] == 0x00 &&
                data[data.Length - 13] == 0x70;

            Console.WriteLine();
            Console.WriteLine(
                "    [Validation]");

            PrintCheck(
                "Magic RDTB",
                errors == 0);
            PrintCheck(
                $"Offset[0] = " +
                $"0x{HEADER_SIZE:X8}",
                off0 == HEADER_SIZE);
            PrintCheck(
                $"Skeleton {bc} bones " +
                $"({need} B in chunk[0])",
                c0sz >= need);
            PrintCheck(
                "EOF terminator " +
                "00 00 00 70",
                hasEof);

            if (errors > 0)
                throw new InvalidDataException(
                    $"Build validation failed" +
                    $" with {errors} error(s)");
        }

        private static void PrintCheck(
            string label, bool pass)
        {
            Console.ForegroundColor = pass
                ? ConsoleColor.Green
                : ConsoleColor.Red;
            Console.WriteLine(
                $"      {(pass ? "✓" : "✗")} " +
                $"{label}");
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // SHOW SKELETON TREE
        // ═════════════════════════════════════════
        private void ShowSkeletonTree()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Skeleton Tree: " +
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

            // ── Build children map ───────────────
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
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "    (no root bones " +
                    "found - listing all)");
                Console.ResetColor();
                for (int i = 0;
                     i < bones.Count; i++)
                {
                    PrintBoneNode(
                        i, bones, children,
                        "  ", true);
                }
            }
            else
            {
                for (int i = 0;
                     i < roots.Count; i++)
                {
                    PrintBoneNode(
                        roots[i],
                        bones, children,
                        "  ",
                        i == roots.Count - 1);
                }
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

        // ═════════════════════════════════════════
        // COMPARE WITH ANOTHER RDTB
        // ═════════════════════════════════════════
        private void CompareWith(
            RDTBArchive other)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] Compare RDTB Files");
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

            CmpField("magic",
                "RDTB", "RDTB", true);
            CmpField("version",
                "00 01 00 00",
                "00 01 00 00", true);
            CmpField("unk_08",
                BitConverter
                    .ToString(_unk08)
                    .Replace('-', ' '),
                BitConverter
                    .ToString(other._unk08)
                    .Replace('-', ' '),
                BitConverter
                    .ToString(_unk08) ==
                BitConverter
                    .ToString(other._unk08));
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
                other._chunks.Count
                    .ToString(),
                _chunks.Count ==
                other._chunks.Count);
            CmpField("file_size",
                _data.Length
                    .ToString("N0") + " B",
                other._data.Length
                    .ToString("N0") + " B",
                _data.Length ==
                other._data.Length);

            Console.WriteLine();
            Console.WriteLine(
                "    Chunk-by-chunk:");
            Console.WriteLine(
                "    " +
                new string('─', 58));

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
                int ofa = ca?.Offset ?? 0;
                int ofb = cb?.Offset ?? 0;
                bool same = sza == szb;

                Console.ForegroundColor = same
                    ? ConsoleColor.Green
                    : ConsoleColor.Red;
                Console.Write(
                    $"    {(same ? "✓" : "✗")}  " +
                    $"[{i,2}] " +
                    $"{GetChunkLabel(i),-14}  ");
                Console.ResetColor();
                Console.WriteLine(
                    $"A: 0x{ofa:X8} " +
                    $"({sza,10:N0} B)    " +
                    $"B: 0x{ofb:X8} " +
                    $"({szb,10:N0} B)");
            }

            if (_skeleton != null &&
                other._skeleton != null)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "    Skeleton comparison:");
                CmpField("bone_count",
                    _skeleton.BoneCount
                        .ToString(),
                    other._skeleton.BoneCount
                        .ToString(),
                    _skeleton.BoneCount ==
                    other._skeleton.BoneCount);

                int ra =
                    _skeleton.GetRoots().Count;
                int rb =
                    other._skeleton
                        .GetRoots().Count;
                CmpField("root_bones",
                    ra.ToString(),
                    rb.ToString(),
                    ra == rb);
            }

            Console.WriteLine(
                new string('═', 64));
        }

        private static void CmpField(
            string label,
            string a,
            string b,
            bool same)
        {
            Console.ForegroundColor = same
                ? ConsoleColor.Green
                : ConsoleColor.Red;
            Console.Write(
                $"    {(same ? "✓" : "✗")}  " +
                $"{label,-24}");
            Console.ResetColor();
            Console.WriteLine(
                $"A={a}   B={b}");
        }

        // ═════════════════════════════════════════
        // VERIFY (byte-for-byte)
        // ═════════════════════════════════════════
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
                "[+] Verify RDTB");
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 64));
            Console.WriteLine(
                "    Original : " +
                Path.GetFileName(_filepath) +
                $"  ({orig.Length:N0} B)");
            Console.WriteLine(
                "    Rebuilt  : " +
                Path.GetFileName(rebuiltPath) +
                $"  ({reb.Length:N0} B)");
            Console.WriteLine();

            if (orig.Length != reb.Length)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"    [!] Size mismatch: " +
                    $"{orig.Length:N0} vs " +
                    $"{reb.Length:N0} bytes");
                Console.ResetColor();
            }

            // ── Find first diff ──────────────────
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
                    "    IDENTICAL ✓ " +
                    "rebuild is byte-perfect!");
                Console.ResetColor();
                Console.WriteLine(
                    new string('═', 64));
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Red;
            Console.WriteLine(
                $"    [!] First diff @ " +
                $"0x{diffOff:X8} " +
                $"({diffOff:N0})");
            Console.ResetColor();

            // ── Context dump ─────────────────────
            int ctxStart =
                Math.Max(0, diffOff - 8);
            int ctxEnd = Math.Min(
                minLen, diffOff + 24);

            Console.WriteLine();
            Console.WriteLine(
                "    Context (±8 bytes):");
            Console.WriteLine(
                $"    {"Offset",10}  " +
                $"{"Original",-48}  Rebuilt");
            Console.WriteLine(
                "    " +
                new string('─', 70));

            for (int off = ctxStart;
                 off < ctxEnd;
                 off += 16)
            {
                int end2 = Math.Min(
                    off + 16, ctxEnd);
                var sbO = new StringBuilder();
                var sbR = new StringBuilder();

                for (int j = off;
                     j < end2; j++)
                {
                    if (j > off)
                    {
                        sbO.Append(' ');
                        sbR.Append(' ');
                    }
                    sbO.Append(
                        j < orig.Length
                        ? orig[j].ToString("X2")
                        : "--");
                    sbR.Append(
                        j < reb.Length
                        ? reb[j].ToString("X2")
                        : "--");
                }

                bool hasDiff =
                    off <= diffOff &&
                    diffOff < off + 16;
                Console.ForegroundColor =
                    hasDiff
                    ? ConsoleColor.Red
                    : ConsoleColor.Gray;
                Console.WriteLine(
                    $"    0x{off:X8}  " +
                    $"{sbO,-48}  {sbR}" +
                    (hasDiff ? " ◄" : ""));
                Console.ResetColor();
            }

            // ── Total diffs ──────────────────────
            int total = 0;
            for (int i = 0; i < minLen; i++)
                if (orig[i] != reb[i])
                    total++;
            total += Math.Abs(
                orig.Length - reb.Length);

            Console.WriteLine(
                $"\n    Total diff bytes:" +
                $" {total:N0}");

            // ── Which chunk? ─────────────────────
            Load();
            foreach (var c in _chunks)
            {
                if (diffOff >= c.Offset &&
                    diffOff <
                    c.Offset + c.Size)
                {
                    int local =
                        diffOff - c.Offset;
                    Console.WriteLine(
                        $"    In chunk " +
                        $"[{c.Index}] " +
                        $"({c.Label}) " +
                        $"@ 0x{c.Offset:X8}");
                    Console.WriteLine(
                        $"    Local offset: " +
                        $"0x{local:X8} " +
                        $"({local:N0} B " +
                        $"into chunk)");
                    break;
                }
            }

            Console.WriteLine(
                new string('═', 64));
        }

        // ═════════════════════════════════════════
        // SCAN FOLDER FOR ALL RDTB FILES
        // ═════════════════════════════════════════
        private static void DoScanFolder(
            string folderPath)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] RDTB Folder Scan: " +
                folderPath);
            Console.ResetColor();
            Console.WriteLine(
                new string('═', 72));
            Console.WriteLine(
                $"  {"FILE",-32} " +
                $"{"SIZE",12}  " +
                $"{"CHUNKS",7}  " +
                $"{"BONES",6}  " +
                $"NOTE");
            Console.WriteLine(
                new string('─', 72));

            if (!Directory.Exists(folderPath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  Folder not found: " +
                    folderPath);
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
                Console.WriteLine(
                    new string('═', 72));
                return;
            }

            Array.Sort(files);

            int countPlayer = 0;
            int countNpc = 0;
            int countProp = 0;
            int countSimple = 0;
            int countError = 0;

            foreach (string f in files)
            {
                try
                {
                    var arc =
                        new RDTBArchive(f);
                    arc.Load();

                    string note =
                        ClassifyRDTB(
                            arc._chunks,
                            arc._boneCount,
                            arc._data.Length);

                    // Count by type
                    if (note.Contains("PLAYER"))
                        countPlayer++;
                    else if (note.Contains(
                        "NPC CHARACTER"))
                        countNpc++;
                    else if (note.Contains(
                        "PROP") ||
                        note.Contains("TOOL"))
                        countProp++;
                    else
                        countSimple++;

                    string name =
                        Path.GetFileName(f);
                    if (name.Length > 32)
                        name = name
                            .Substring(0, 29) +
                            "...";

                    Console.ForegroundColor =
                        GetNoteColor(note);
                    Console.WriteLine(
                        $"  {name,-32} " +
                        $"{arc._data.Length,12:N0}  " +
                        $"{arc._chunks.Count,7}  " +
                        $"{arc._boneCount,6}  " +
                        $"{note}");
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    countError++;
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        $"  {Path.GetFileName(f),-32}" +
                        $" ERROR: {e.Message}");
                    Console.ResetColor();
                }
            }

            // ── Summary ──────────────────────────
            Console.WriteLine(
                new string('═', 72));
            Console.WriteLine(
                $"  Total scanned : " +
                $"{files.Length}");

            if (countPlayer > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    $"  Player/Complex: " +
                    $"{countPlayer}");
                Console.ResetColor();
            }

            if (countNpc > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    $"  NPC Characters: " +
                    $"{countNpc}");
                Console.ResetColor();
            }

            if (countProp > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    $"  Props/Tools   : " +
                    $"{countProp}");
                Console.ResetColor();
            }

            if (countSimple > 0)
            {
                Console.WriteLine(
                    $"  Simple/Other  : " +
                    $"{countSimple}");
            }

            if (countError > 0)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    $"  Errors        : " +
                    $"{countError}");
                Console.ResetColor();
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
                return "PROP/TOOL (tiny)";
            if (fileSize < 200_000 &&
                boneCount < 20)
                return "SIMPLE NPC or TOOL";
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
            if (note.Contains("SIMPLE"))
                return ConsoleColor.White;
            return ConsoleColor.Gray;
        }

        // ═════════════════════════════════════════
        // EOF CHECK
        // ═════════════════════════════════════════
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

        // ═════════════════════════════════════════
        // MANIFEST READER
        // ═════════════════════════════════════════
        private static RDTBManifest ReadManifest(
            string path)
        {
            string json = File.ReadAllText(
                path, Encoding.UTF8);

            var mf = new RDTBManifest
            {
                Chunks =
                    new List<RDTBManifestChunk>()
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

            // ── Parse chunks array ───────────────
            int chunksStart =
                json.IndexOf("\"chunks\":");
            if (chunksStart < 0)
                throw new InvalidDataException(
                    "Manifest missing " +
                    "chunks array");

            int arrStart =
                json.IndexOf(
                    '[', chunksStart);
            int arrEnd =
                json.LastIndexOf(']');

            if (arrStart < 0 || arrEnd < 0)
                throw new InvalidDataException(
                    "Manifest: bad " +
                    "chunks array");

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
                    };

                mf.Chunks.Add(chunk);
                pos = objEnd + 1;
            }

            mf.Chunks.Sort(
                (a, b) =>
                a.Index.CompareTo(b.Index));

            return mf;
        }

        // ─────────────────────────────────────────
        // JSON helpers
        // ─────────────────────────────────────────
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

        // ═════════════════════════════════════════
        // CHUNK LABELS / DESCRIPTIONS
        // ═════════════════════════════════════════
        public static string GetChunkLabel(
            int idx)
        {
            switch (idx)
            {
                case 0: return "skeleton";
                case 1: return "mesh_idx";
                case 2: return "mesh_main";
                case 11: return "weight_uv";
                case 12: return "anim_0";
                case 13: return "anim_1";
                default:
                    if (idx >= 3 && idx <= 6)
                        return
                            $"mesh_grp{idx - 2}";
                    if (idx >= 7 && idx <= 10)
                        return
                            $"idx_tbl_{idx - 7}";
                    return
                        $"chunk_{idx:D4}";
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
                        "bone records";
                case 1:
                    return
                        "Index buffer + " +
                        "sub-pointers";
                case 2:
                    return
                        "Main vertex/normal/" +
                        "UV data (largest)";
                case 11:
                    return
                        "Skin weights + " +
                        "texture coords";
                case 12:
                    return
                        "Animation matrices 0";
                case 13:
                    return
                        "Animation matrices 1";
                default:
                    if (idx >= 3 && idx <= 6)
                        return
                            $"Mesh group " +
                            $"{idx - 2} " +
                            $"(LOD/body part)";
                    if (idx >= 7 && idx <= 10)
                        return
                            "Small index/" +
                            "lookup table";
                    return "Unknown data";
            }
        }

        // ═════════════════════════════════════════
        // UTILITY
        // ═════════════════════════════════════════
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
