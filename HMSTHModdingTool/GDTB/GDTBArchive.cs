using HMSTHModdingTool.BMP;
using HMSTHModdingTool.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HMSTHModdingTool.GDTB
{
    // ═════════════════════════════════════════════
    // HELPER DATA CLASSES
    // ═════════════════════════════════════════════
    internal class BmpData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int BitDepth { get; set; }
        public byte[] Palette { get; set; }
        public byte[] Pixels { get; set; }
    }

    internal class RawTexture
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int BitDepth { get; set; }
        public byte[] Pixels { get; set; }
        public byte[] Palette { get; set; }
    }

    /// <summary>
    /// Handles GDTB texture archive
    /// Extract, Create, Replace,
    /// Info, ChangeCount
    /// </summary>
    public class GDTBArchive
    {
        // ─────────────────────────────────────────
        // PRIVATE FIELDS
        // ─────────────────────────────────────────
        private string _filepath;
        private byte[] _data;
        private int _textureCount;
        private int _originalCount;
        private int _imageOffsetTable;
        private int _paletteChunkOffset;
        private int _formatChunkOffset;
        private List<GDTBTexture> _textures;
        private PS2Converter _ps2;

        // ─────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────
        public GDTBArchive(string filepath)
        {
            _filepath = filepath;
            _textures = new List<GDTBTexture>();
            _ps2 = new PS2Converter();
        }

        // ─────────────────────────────────────────
        // PRIVATE READER HELPERS
        // ─────────────────────────────────────────
        private int ReadInt32(int offset)
        {
            return BitConverter.ToInt32(
                _data, offset);
        }

        private int ReadUInt16(int offset)
        {
            return BitConverter.ToUInt16(
                _data, offset);
        }

        private byte[] GetBytes(
            int offset,
            int length)
        {
            byte[] result = new byte[length];
            Array.Copy(
                _data, offset,
                result, 0,
                length);
            return result;
        }

        // ─────────────────────────────────────────
        // PLACEHOLDER HELPER
        // ─────────────────────────────────────────
        private static RawTexture MakePlaceholder()
        {
            return new RawTexture
            {
                Width = 1,
                Height = 1,
                BitDepth = 8,
                Pixels = new byte[1],
                Palette = new byte[1024],
            };
        }

        // ═════════════════════════════════════════
        // LOAD
        // ═════════════════════════════════════════
        public void Load()
        {
            _data = File.ReadAllBytes(_filepath);

            if (_data[0] != 'G' ||
                _data[1] != 'D' ||
                _data[2] != 'T' ||
                _data[3] != 'B')
            {
                throw new InvalidDataException(
                    "Not a valid GDTB file: " +
                    _filepath);
            }

            _originalCount =
                ReadInt32(8);
            _textureCount =
                _originalCount;
            _imageOffsetTable =
                ReadInt32(12);
            _paletteChunkOffset =
                ReadInt32(16);
            _formatChunkOffset =
                ReadInt32(20);

            _textures.Clear();

            for (int i = 0;
                 i < _textureCount; i++)
            {
                try
                {
                    _textures.Add(
                        ParseTexture(i));
                }
                catch
                {
                    break;
                }
            }

            _textureCount = _textures.Count;
        }

        // ═════════════════════════════════════════
        // PARSE TEXTURE
        // ═════════════════════════════════════════
        private GDTBTexture ParseTexture(
            int index)
        {
            var tex = new GDTBTexture
            {
                Index = index
            };

            int ptr =
                _imageOffsetTable + index * 4;
            int val = ReadInt32(ptr);
            tex.BodyOffset =
                val + _imageOffsetTable;

            int fptr =
                _formatChunkOffset + index * 4;
            int fval = ReadInt32(fptr);
            int foff =
                _formatChunkOffset + fval;

            tex.Width = ReadUInt16(foff);
            tex.Height = ReadUInt16(foff + 2);
            int bdf = ReadInt32(foff + 4);

            if (!GDTBTexture.HexToBitDepth
                    .ContainsKey(bdf))
            {
                throw new InvalidDataException(
                    "Unknown bit depth: 0x" +
                    bdf.ToString("X2") +
                    " in texture " + index);
            }

            tex.BitDepth =
                GDTBTexture.HexToBitDepth[bdf];
            tex.ImageSize =
                GDTBTexture.GetImageSize(
                    tex.Width,
                    tex.Height,
                    tex.BitDepth);
            tex.PaletteSize =
                GDTBTexture.GetPaletteSize(
                    tex.BitDepth);

            if (tex.IsPaletted)
            {
                int pptr =
                    _paletteChunkOffset +
                    index * 4;
                int pval = ReadInt32(pptr);
                tex.PaletteOffset =
                    _paletteChunkOffset + pval;
            }

            return tex;
        }

        // ═════════════════════════════════════════
        // STATIC ENTRY POINTS
        // ═════════════════════════════════════════
        public static void Info(string gdtbPath)
        {
            var arc = new GDTBArchive(gdtbPath);
            arc.Load();
            arc.ShowInfo();
        }

        public static void Extract(
            string gdtbPath,
            string outputFolder)
        {
            var arc = new GDTBArchive(gdtbPath);
            arc.Load();
            arc.ExtractAll(outputFolder);
        }

        public static void Create(
            string inputFolder,
            string gdtbPath)
        {
            var arc = new GDTBArchive(gdtbPath);
            arc.CreateFromFolder(
                inputFolder, gdtbPath);
        }

        public static void Replace(
            string gdtbPath,
            int index,
            string bmpPath)
        {
            var arc = new GDTBArchive(gdtbPath);
            arc.Load();
            arc.ReplaceTexture(index, bmpPath);
        }

        public static void ReplaceFolder(
            string gdtbPath,
            string folder,
            int startIndex)
        {
            var arc = new GDTBArchive(gdtbPath);
            arc.Load();
            arc.ReplaceFolderTextures(
                folder, startIndex);
        }

        public static void ChangeCount(
            string gdtbPath,
            int newCount)
        {
            var arc = new GDTBArchive(gdtbPath);
            arc.Load();
            arc.ChangeTextureCount(newCount);
        }

        // ═════════════════════════════════════════
        // SHOW INFO
        // ═════════════════════════════════════════
        private void ShowInfo()
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "[+] GDTB Info: " +
                Path.GetFileName(_filepath));
            Console.ResetColor();
            Console.WriteLine(
                new string('=', 60));
            Console.WriteLine(
                "    File size:     " +
                _data.Length.ToString("N0") +
                " bytes (" +
                (_data.Length / 1024.0)
                    .ToString("F2") +
                " KB)");
            Console.WriteLine(
                "    Header count:  " +
                _originalCount);
            Console.WriteLine(
                "    Valid parsed:  " +
                _textureCount);

            byte[] countBytes =
                BitConverter.GetBytes(
                    _originalCount);
            Console.WriteLine(
                "    Hex at 0x08:   " +
                BitConverter
                    .ToString(countBytes)
                    .Replace('-', ' '));
            Console.WriteLine(
                "    Palette chunk: 0x" +
                _paletteChunkOffset
                    .ToString("X8"));
            Console.WriteLine(
                "    Format chunk:  0x" +
                _formatChunkOffset
                    .ToString("X8"));
            Console.WriteLine(
                new string('=', 60));

            if (_textures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "    Valid Textures:");
                Console.WriteLine(
                    "    " +
                    new string('-', 50));
                foreach (var t in _textures)
                    Console.WriteLine(
                        "    " + t.ToString());
                Console.WriteLine(
                    "    " +
                    new string('-', 50));
            }

            if (_originalCount > _textureCount)
            {
                int diff =
                    _originalCount -
                    _textureCount;
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine();
                Console.WriteLine(
                    "    [" + diff +
                    " slot(s) empty " +
                    "(indices " +
                    _textureCount +
                    " to " +
                    (_originalCount - 1) +
                    ")]");
                Console.WriteLine(
                    "    Use replace or " +
                    "replacefolder to fill");
                Console.ResetColor();
            }
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
            Console.WriteLine(
                "[+] Extracting " +
                _textureCount +
                " texture(s) to: " +
                outputFolder);
            Console.WriteLine();

            for (int i = 0;
                 i < _textureCount; i++)
            {
                byte[] bmpData =
                    ExtractTextureToBmp(i);
                if (bmpData == null) continue;

                string outPath = Path.Combine(
                    outputFolder,
                    "texture_" +
                    i.ToString("D2") + ".bmp");

                File.WriteAllBytes(
                    outPath, bmpData);

                var t = _textures[i];
                Console.WriteLine(
                    "    [" +
                    i.ToString("D2") + "] " +
                    t.Width + "x" + t.Height +
                    " " + t.BitDepth + "-bit" +
                    " -> " + outPath);
            }
        }

        // ═════════════════════════════════════════
        // EXTRACT SINGLE TEXTURE TO BMP
        // ═════════════════════════════════════════
        private byte[] ExtractTextureToBmp(
            int index)
        {
            if (index < 0 ||
                index >= _textureCount)
            {
                TextOut.PrintError(
                    "Invalid index " + index +
                    " (valid: 0 to " +
                    (_textureCount - 1) + ")");
                return null;
            }

            var tex = _textures[index];

            byte[] img = GetBytes(
                tex.BodyOffset,
                tex.ImageSize);

            byte[] pal = tex.IsPaletted
                ? GetBytes(
                    tex.PaletteOffset,
                    tex.PaletteSize)
                : new byte[0];

            byte[] pix;
            byte[] palette;

            _ps2.PS2ToWindows(
                tex.Width,
                tex.Height,
                tex.BitDepth,
                img, pal,
                out pix,
                out palette);

            return BuildBmp(
                tex.Width,
                tex.Height,
                tex.BitDepth,
                pix,
                palette);
        }

        // ═════════════════════════════════════════
        // BUILD BMP
        // ═════════════════════════════════════════
        private static byte[] BuildBmp(
            int width,
            int height,
            int bitDepth,
            byte[] pixels,
            byte[] palette)
        {
            int colors;
            int paletteSize;

            if (bitDepth == 4)
            {
                colors = 16;
                paletteSize = 64;
            }
            else if (bitDepth == 8)
            {
                colors = 256;
                paletteSize = 1024;
            }
            else
            {
                colors = 0;
                paletteSize = 0;
            }

            int rowSize =
                GDTBTexture.GetBmpRowSize(
                    width, bitDepth);
            int imageSize = rowSize * height;

            using (var ms = new MemoryStream())
            using (var bw =
                new BinaryWriter(ms))
            {
                // BMP File Header
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write((int)(
                    14 + 40 +
                    paletteSize + imageSize));
                bw.Write((int)0);
                bw.Write((int)(
                    14 + 40 + paletteSize));

                // DIB Header
                bw.Write((int)40);
                bw.Write((int)width);
                bw.Write((int)height);
                bw.Write((short)1);
                bw.Write((short)bitDepth);
                bw.Write((int)0);
                bw.Write((int)imageSize);
                bw.Write((int)2835);
                bw.Write((int)2835);
                bw.Write((int)colors);
                bw.Write((int)0);

                // Palette
                if (paletteSize > 0)
                {
                    byte[] pal =
                        new byte[paletteSize];
                    int cpLen = Math.Min(
                        palette.Length,
                        paletteSize);
                    Array.Copy(
                        palette, pal, cpLen);
                    bw.Write(pal);
                }

                // Pixel Data
                int bpr =
                    GDTBTexture.GetBytesPerRow(
                        width, bitDepth);
                int pad = rowSize - bpr;

                for (int y = 0;
                     y < height; y++)
                {
                    int start = y * bpr;
                    int len = Math.Min(
                        bpr,
                        pixels.Length - start);
                    if (len <= 0) break;

                    bw.Write(pixels, start, len);

                    for (int p = 0;
                         p < pad; p++)
                        bw.Write((byte)0);
                }

                return ms.ToArray();
            }
        }

        // ═════════════════════════════════════════
        // LOAD BMP FROM FILE
        // ═════════════════════════════════════════
        private static BmpData LoadBmp(
            string path)
        {
            byte[] data =
                File.ReadAllBytes(path);

            if (data[0] != 'B' ||
                data[1] != 'M')
                throw new InvalidDataException(
                    "Not a valid BMP: " + path);

            int pxOff =
                BitConverter.ToInt32(data, 10);
            int width =
                BitConverter.ToInt32(data, 18);
            int hRaw =
                BitConverter.ToInt32(data, 22);
            int height = Math.Abs(hRaw);
            int bd =
                BitConverter.ToUInt16(data, 28);

            if (bd != 4 && bd != 8 &&
                bd != 16 && bd != 24 &&
                bd != 32)
            {
                throw new InvalidDataException(
                    "Unsupported bit depth: " +
                    bd + "-bit in " + path);
            }

            byte[] palette;
            if (bd == 4 || bd == 8)
            {
                int colors =
                    bd == 8 ? 256 : 16;
                palette =
                    new byte[colors * 4];
                Array.Copy(
                    data, 54,
                    palette, 0,
                    colors * 4);
            }
            else
            {
                palette = new byte[0];
            }

            int bpr =
                GDTBTexture.GetBytesPerRow(
                    width, bd);
            int rs =
                GDTBTexture.GetBmpRowSize(
                    width, bd);

            var pixList = new List<byte>();

            if (hRaw > 0)
            {
                for (int y = 0;
                     y < height; y++)
                {
                    int rowStart =
                        pxOff + y * rs;
                    for (int b = 0;
                         b < bpr; b++)
                        pixList.Add(
                            data[rowStart + b]);
                }
            }
            else
            {
                for (int y = height - 1;
                     y >= 0; y--)
                {
                    int rowStart =
                        pxOff + y * rs;
                    for (int b = 0;
                         b < bpr; b++)
                        pixList.Add(
                            data[rowStart + b]);
                }
            }

            return new BmpData
            {
                Width = width,
                Height = height,
                BitDepth = bd,
                Palette = palette,
                Pixels = pixList.ToArray()
            };
        }

        // ═════════════════════════════════════════
        // LOAD ALL RAW TEXTURES
        // ═════════════════════════════════════════
        private List<RawTexture> LoadAllRaw()
        {
            var result = new List<RawTexture>();

            for (int i = 0;
                 i < _textures.Count; i++)
            {
                try
                {
                    var t = _textures[i];

                    byte[] img = GetBytes(
                        t.BodyOffset,
                        t.ImageSize);

                    byte[] pal = t.IsPaletted
                        ? GetBytes(
                            t.PaletteOffset,
                            t.PaletteSize)
                        : new byte[0];

                    result.Add(new RawTexture
                    {
                        Width = t.Width,
                        Height = t.Height,
                        BitDepth = t.BitDepth,
                        Pixels = img,
                        Palette = pal,
                    });
                }
                catch (Exception e)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "[!] Warning texture " +
                        i + ": " + e.Message);
                    Console.ResetColor();
                }
            }

            return result;
        }

        // ═════════════════════════════════════════
        // REPLACE SINGLE TEXTURE
        // ═════════════════════════════════════════
        private void ReplaceTexture(
            int index,
            string bmpPath)
        {
            if (index < 0)
            {
                TextOut.PrintError(
                    "Index cannot be negative");
                return;
            }

            if (index >= _originalCount)
            {
                TextOut.PrintError(
                    "Index " + index +
                    " out of range. " +
                    "Use change to expand");
                return;
            }

            if (!File.Exists(bmpPath))
            {
                TextOut.PrintError(
                    "BMP not found: " + bmpPath);
                return;
            }

            var newTex = LoadBmp(bmpPath);

            Console.WriteLine();
            Console.WriteLine(
                "[+] Replace texture " + index);
            Console.WriteLine(
                "    BMP: " + bmpPath);
            Console.WriteLine(
                "    New: " +
                newTex.Width + "x" +
                newTex.Height + " " +
                newTex.BitDepth + "-bit");

            if (index < _textureCount)
            {
                var old = _textures[index];
                Console.WriteLine(
                    "    Old: " +
                    old.Width + "x" +
                    old.Height + " " +
                    old.BitDepth + "-bit");

                bool same =
                    newTex.Width ==
                        old.Width &&
                    newTex.Height ==
                        old.Height &&
                    newTex.BitDepth ==
                        old.BitDepth;

                if (same)
                {
                    Console.WriteLine(
                        "    Same -> in-place");
                    InplaceReplace(
                        index, newTex);
                }
                else
                {
                    Console.WriteLine(
                        "    Different -> rebuild");
                    RebuildReplace(
                        index, newTex);
                }
            }
            else
            {
                Console.WriteLine(
                    "    New slot -> rebuild");
                RebuildReplace(index, newTex);
            }
        }

        // ═════════════════════════════════════════
        // IN-PLACE REPLACE
        // ═════════════════════════════════════════
        private void InplaceReplace(
            int index,
            BmpData newTex)
        {
            var tex = _textures[index];

            byte[] pp;
            byte[] pal;

            _ps2.WindowsToPS2(
                newTex.Width,
                newTex.Height,
                newTex.BitDepth,
                newTex.Pixels,
                newTex.Palette,
                out pp,
                out pal);

            if (tex.IsPaletted)
            {
                int po = tex.PaletteOffset;
                int colors =
                    tex.BitDepth == 8 ? 256 : 16;
                byte[] oldPal =
                    GetBytes(po, colors * 4);
                byte[] final =
                    new byte[pal.Length];

                for (int i = 0;
                     i + 3 < pal.Length;
                     i += 4)
                {
                    int ai = i / 4;
                    byte alpha = ai < colors
                        ? oldPal[i + 3]
                        : pal[i + 3];
                    final[i] = pal[i];
                    final[i + 1] = pal[i + 1];
                    final[i + 2] = pal[i + 2];
                    final[i + 3] = alpha;
                }

                Array.Copy(
                    final, 0,
                    _data, po,
                    final.Length);
            }

            Array.Copy(
                pp, 0,
                _data, tex.BodyOffset,
                pp.Length);

            File.WriteAllBytes(_filepath, _data);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Texture " + index +
                " replaced in-place");
            Console.WriteLine(
                "     Saved: " + _filepath);
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // REBUILD REPLACE
        // ═════════════════════════════════════════
        private void RebuildReplace(
            int index,
            BmpData newTex)
        {
            var allTex = LoadAllRaw();

            while (allTex.Count <= index)
                allTex.Add(MakePlaceholder());

            byte[] pp;
            byte[] pal;

            _ps2.WindowsToPS2(
                newTex.Width,
                newTex.Height,
                newTex.BitDepth,
                newTex.Pixels,
                newTex.Palette,
                out pp,
                out pal);

            allTex[index] = new RawTexture
            {
                Width = newTex.Width,
                Height = newTex.Height,
                BitDepth = newTex.BitDepth,
                Pixels = pp,
                Palette = pal,
            };

            while (allTex.Count < _originalCount)
                allTex.Add(MakePlaceholder());

            Console.WriteLine(
                "[+] Rebuilding GDTB with " +
                allTex.Count + " texture(s)...");

            byte[] newData = BuildGDTB(allTex);
            int oldSize = _data.Length;

            File.WriteAllBytes(_filepath, newData);
            _data = newData;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Texture " + index +
                " replaced (rebuild)");
            Console.WriteLine(
                "     Old: " +
                oldSize.ToString("N0") +
                " bytes");
            Console.WriteLine(
                "     New: " +
                newData.Length.ToString("N0") +
                " bytes");
            Console.WriteLine(
                "     Saved: " + _filepath);
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // REPLACE FOLDER TEXTURES
        // ═════════════════════════════════════════
        private void ReplaceFolderTextures(
            string folder,
            int startIndex)
        {
            Console.WriteLine();
            Console.WriteLine(
                "[+] Replace From Folder");
            Console.WriteLine(
                new string('=', 60));
            Console.WriteLine(
                "    GDTB:        " +
                Path.GetFileName(_filepath));
            Console.WriteLine(
                "    Folder:      " + folder);
            Console.WriteLine(
                "    Start index: " + startIndex);
            Console.WriteLine(
                new string('=', 60));

            if (!Directory.Exists(folder))
            {
                TextOut.PrintError(
                    "Folder not found: " +
                    folder);
                return;
            }

            string[] files = Directory
                .GetFiles(folder, "*.bmp")
                .OrderBy(f =>
                    Path.GetFileName(f)
                        .ToLower())
                .ToArray();

            if (files.Length == 0)
            {
                TextOut.PrintError(
                    "No BMP files in: " +
                    folder);
                return;
            }

            Console.WriteLine();
            Console.WriteLine(
                "    Found " +
                files.Length +
                " BMP file(s):\n");

            for (int i = 0;
                 i < files.Length; i++)
            {
                int slot = startIndex + i;
                Console.WriteLine(
                    "    [" +
                    i.ToString("D3") + "] " +
                    Path.GetFileName(files[i])
                        .PadRight(40) +
                    " -> slot " + slot);
            }

            int lastSlot =
                startIndex + files.Length - 1;

            if (lastSlot >= _originalCount)
            {
                TextOut.PrintError(
                    "Files exceed count!\n" +
                    "    Last slot: " +
                    lastSlot + "\n" +
                    "    Max slot:  " +
                    (_originalCount - 1) + "\n" +
                    "    Run: change " +
                    (lastSlot + 1) + " first");
                return;
            }

            var allTex = LoadAllRaw();
            while (allTex.Count < _originalCount)
                allTex.Add(MakePlaceholder());

            Console.WriteLine();
            Console.WriteLine(
                "[+] Processing files...\n");

            int okCount = 0;
            int failCount = 0;

            for (int i = 0;
                 i < files.Length; i++)
            {
                int slot = startIndex + i;
                string file = files[i];

                try
                {
                    var bmp = LoadBmp(file);

                    byte[] pp;
                    byte[] pal;

                    _ps2.WindowsToPS2(
                        bmp.Width,
                        bmp.Height,
                        bmp.BitDepth,
                        bmp.Pixels,
                        bmp.Palette,
                        out pp,
                        out pal);

                    allTex[slot] = new RawTexture
                    {
                        Width = bmp.Width,
                        Height = bmp.Height,
                        BitDepth = bmp.BitDepth,
                        Pixels = pp,
                        Palette = pal,
                    };

                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "    [OK] Slot " +
                        slot.ToString("D3") +
                        " <- " +
                        Path.GetFileName(file) +
                        " (" +
                        bmp.Width + "x" +
                        bmp.Height + " " +
                        bmp.BitDepth + "-bit)");
                    Console.ResetColor();
                    okCount++;
                }
                catch (Exception e)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "    [!!] Slot " +
                        slot.ToString("D3") +
                        " <- " +
                        Path.GetFileName(file) +
                        " FAILED: " + e.Message);
                    Console.ResetColor();
                    failCount++;
                }
            }

            if (okCount == 0)
            {
                TextOut.PrintError(
                    "No files loaded - abort");
                return;
            }

            Console.WriteLine();
            Console.WriteLine(
                "[+] Rebuilding GDTB...");

            byte[] newData = BuildGDTB(allTex);
            int oldSize = _data.Length;

            File.WriteAllBytes(_filepath, newData);
            _data = newData;

            Console.WriteLine();
            Console.WriteLine(
                new string('=', 60));
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Folder Replace Done!");
            Console.ResetColor();
            Console.WriteLine(
                "    GDTB:     " +
                Path.GetFileName(_filepath));
            Console.WriteLine(
                "    Success:  " +
                okCount + " replaced");
            if (failCount > 0)
                Console.WriteLine(
                    "    Failed:   " +
                    failCount + " skipped");
            Console.WriteLine(
                "    Slots:    " +
                startIndex + " to " + lastSlot);
            Console.WriteLine(
                "    Old size: " +
                oldSize.ToString("N0") +
                " bytes (" +
                (oldSize / 1024.0)
                    .ToString("F2") + " KB)");
            Console.WriteLine(
                "    New size: " +
                newData.Length.ToString("N0") +
                " bytes (" +
                (newData.Length / 1024.0)
                    .ToString("F2") + " KB)");

            if (newData.Length != oldSize)
            {
                int diff = Math.Abs(
                    newData.Length - oldSize);
                string sign =
                    newData.Length > oldSize
                    ? "+" : "-";
                Console.WriteLine(
                    "    Change:   " +
                    sign +
                    diff.ToString("N0") +
                    " bytes");
            }
            Console.WriteLine(
                new string('=', 60));
        }

        // ═════════════════════════════════════════
        // CHANGE TEXTURE COUNT
        // ═════════════════════════════════════════
        private void ChangeTextureCount(
            int newCount)
        {
            Console.WriteLine();

            if (newCount < 1)
            {
                TextOut.PrintError(
                    "Count must be at least 1");
                return;
            }

            int oldCount = _originalCount;
            byte[] oldHex =
                BitConverter.GetBytes(oldCount);
            byte[] newHex =
                BitConverter.GetBytes(newCount);

            Console.WriteLine(
                "[+] Change Texture Count");
            Console.WriteLine(
                "    File:      " +
                Path.GetFileName(_filepath));
            Console.WriteLine(
                "    Old count: " + oldCount +
                "  (hex: " +
                BitConverter.ToString(oldHex)
                    .Replace('-', ' ') + ")");
            Console.WriteLine(
                "    New count: " + newCount +
                "  (hex: " +
                BitConverter.ToString(newHex)
                    .Replace('-', ' ') + ")");

            if (newCount == oldCount)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "[!] Already " + oldCount +
                    " - nothing to change");
                Console.ResetColor();
                return;
            }

            var allTex = LoadAllRaw();

            if (newCount > oldCount)
            {
                int added = newCount - oldCount;
                Console.WriteLine(
                    "    Action: INCREASE by " +
                    added + " slot(s)");
                Console.WriteLine(
                    "    New slots: " +
                    oldCount + " to " +
                    (newCount - 1));
                Console.WriteLine();
                Console.WriteLine(
                    "[+] Rebuilding with " +
                    newCount + " slots...");

                for (int i = 0; i < added; i++)
                {
                    int slot = oldCount + i;
                    Console.WriteLine(
                        "    [+] Adding slot " +
                        slot +
                        " (placeholder)");
                    allTex.Add(MakePlaceholder());
                }
            }
            else
            {
                int removed = oldCount - newCount;
                Console.WriteLine(
                    "    Action: DECREASE by " +
                    removed + " slot(s)");
                Console.WriteLine(
                    "    Keeping: 0 to " +
                    (newCount - 1));
                Console.WriteLine(
                    "    Removing: " +
                    newCount + " to " +
                    (oldCount - 1));
                Console.WriteLine();
                Console.WriteLine(
                    "[+] Trimming to " +
                    newCount + " slots...");

                allTex = allTex
                    .Take(newCount)
                    .ToList();
            }

            byte[] newData = BuildGDTB(allTex);
            int oldSize = _data.Length;

            File.WriteAllBytes(_filepath, newData);
            _data = newData;
            _originalCount = newCount;
            _textureCount = allTex.Count;

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Count: " +
                oldCount + " -> " + newCount);
            Console.ResetColor();
            Console.WriteLine(
                "     Old: " +
                oldSize.ToString("N0") +
                " bytes");
            Console.WriteLine(
                "     New: " +
                newData.Length.ToString("N0") +
                " bytes");

            if (newCount > oldCount)
            {
                Console.WriteLine();
                Console.WriteLine(
                    "    Fill new slots with:");
                for (int i = 0;
                     i < newCount - oldCount; i++)
                {
                    int slot = oldCount + i;
                    Console.WriteLine(
                        "    tool.exe -rgdtb " +
                        slot + " image.bmp " +
                        Path.GetFileName(
                            _filepath));
                }
            }
        }

        // ═════════════════════════════════════════
        // CREATE FROM FOLDER
        // ═════════════════════════════════════════
        private void CreateFromFolder(
            string inputFolder,
            string outputPath)
        {
            Console.WriteLine();
            Console.WriteLine(
                "[+] Creating GDTB from: " +
                inputFolder);

            string[] files = Directory
                .GetFiles(inputFolder, "*.bmp")
                .OrderBy(f =>
                    Path.GetFileName(f)
                        .ToLower())
                .ToArray();

            if (files.Length == 0)
            {
                TextOut.PrintError(
                    "No BMP files in: " +
                    inputFolder);
                return;
            }

            Console.WriteLine(
                "    Found " + files.Length +
                " texture(s)\n");
            Console.WriteLine(
                "[+] Loading BMPs...\n");

            var allTex = new List<RawTexture>();

            for (int i = 0;
                 i < files.Length; i++)
            {
                var bmp = LoadBmp(files[i]);

                byte[] pp;
                byte[] pal;

                _ps2.WindowsToPS2(
                    bmp.Width,
                    bmp.Height,
                    bmp.BitDepth,
                    bmp.Pixels,
                    bmp.Palette,
                    out pp,
                    out pal);

                allTex.Add(new RawTexture
                {
                    Width = bmp.Width,
                    Height = bmp.Height,
                    BitDepth = bmp.BitDepth,
                    Pixels = pp,
                    Palette = pal,
                });

                int padded =
                    GDTBTexture.PadTo16(
                        pp.Length);
                Console.WriteLine(
                    "    [" +
                    i.ToString("D2") + "] " +
                    bmp.Width + "x" +
                    bmp.Height + " " +
                    bmp.BitDepth + "-bit" +
                    " (data:" + pp.Length +
                    "b padded:" + padded +
                    "b) <- " +
                    Path.GetFileName(files[i]));
            }

            Console.WriteLine();
            Console.WriteLine(
                "[+] Building GDTB...");

            byte[] newData = BuildGDTB(allTex);

            File.WriteAllBytes(outputPath, newData);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] GDTB created: " +
                outputPath);
            Console.ResetColor();
            Console.WriteLine(
                "     Textures: " + allTex.Count);
            Console.WriteLine(
                "     Size: " +
                newData.Length.ToString("N0") +
                " bytes (" +
                (newData.Length / 1024.0)
                    .ToString("F2") + " KB)");
        }

        // ═════════════════════════════════════════
        // BUILD GDTB
        // ═════════════════════════════════════════
        private byte[] BuildGDTB(
            List<RawTexture> tdata)
        {
            int n = tdata.Count;

            using (var ms = new MemoryStream())
            using (var bw =
                new BinaryWriter(ms))
            {
                // Header
                bw.Write((byte)'G');
                bw.Write((byte)'D');
                bw.Write((byte)'T');
                bw.Write((byte)'B');
                bw.Write((byte)0x00);
                bw.Write((byte)0x01);
                bw.Write((byte)0x00);
                bw.Write((byte)0x00);
                bw.Write((int)n);
                bw.Write((int)0x18);

                long palPtrPos = ms.Position;
                bw.Write((int)0);

                long fmtPtrPos = ms.Position;
                bw.Write((int)0);

                // Image Offset Table
                int tableEnd = 0x18 + n * 4;
                int first =
                    tableEnd % 0x10 == 0
                    ? tableEnd
                    : tableEnd +
                      (0x10 - tableEnd % 0x10);

                int[] imgOffs = new int[n];
                int cur = first;

                for (int i = 0; i < n; i++)
                {
                    imgOffs[i] = cur;
                    cur += GDTBTexture.PadTo16(
                        tdata[i].Pixels.Length);
                }

                for (int i = 0; i < n; i++)
                    bw.Write(
                        (int)(imgOffs[i] - 0x18));

                while ((int)ms.Position < first)
                    bw.Write((byte)0);

                // Image Bodies
                for (int i = 0; i < n; i++)
                {
                    bw.Write(tdata[i].Pixels);
                    int pad = (16 - (
                        tdata[i].Pixels.Length
                        % 16)) % 16;
                    for (int p = 0;
                         p < pad; p++)
                        bw.Write((byte)0);
                }

                // Palette Chunk
                int pca = (int)ms.Position;
                long curPos = ms.Position;
                ms.Position = palPtrPos;
                bw.Write((int)pca);
                ms.Position = curPos;

                int peb = n * 4;
                int pdo;

                if (peb % 0x10 == 0)
                {
                    pdo = peb;
                }
                else
                {
                    int at = peb + 4;
                    pdo = at % 0x10 == 0
                        ? at
                        : at + (0x10 - at % 0x10);
                }

                int cp = pdo;

                for (int i = 0; i < n; i++)
                {
                    bool hasPal =
                        tdata[i].BitDepth == 4 ||
                        tdata[i].BitDepth == 8;

                    if (hasPal)
                    {
                        bw.Write((int)cp);
                        cp += GDTBTexture.PadTo16(
                            tdata[i]
                                .Palette.Length);
                    }
                    else
                    {
                        bw.Write((int)0);
                    }
                }

                if (peb % 0x10 != 0)
                {
                    bw.Write((int)0);
                    while (
                        ((int)ms.Position - pca)
                        < pdo)
                        bw.Write((byte)0);
                }

                for (int i = 0; i < n; i++)
                {
                    bool hasPal =
                        tdata[i].BitDepth == 4 ||
                        tdata[i].BitDepth == 8;
                    if (!hasPal) continue;

                    bw.Write(tdata[i].Palette);
                    int pad = (16 - (
                        tdata[i].Palette.Length
                        % 16)) % 16;
                    for (int p = 0;
                         p < pad; p++)
                        bw.Write((byte)0);
                }

                while (
                    (int)ms.Position % 0x10 != 0)
                    bw.Write((byte)0);

                // Format Chunk
                int fca = (int)ms.Position;
                curPos = ms.Position;
                ms.Position = fmtPtrPos;
                bw.Write((int)fca);
                ms.Position = curPos;

                int feb = n * 4;
                int fdo;

                if (feb % 0x10 == 0)
                {
                    fdo = feb;
                }
                else
                {
                    int at = feb + 4;
                    fdo = at % 0x10 == 0
                        ? at
                        : at + (0x10 - at % 0x10);
                }

                int cf = fdo;

                for (int i = 0; i < n; i++)
                {
                    bw.Write((int)cf);
                    cf += 0x10;
                }

                if (feb % 0x10 != 0)
                {
                    bw.Write((int)0);
                    while (
                        ((int)ms.Position - fca)
                        < fdo)
                        bw.Write((byte)0);
                }

                for (int i = 0; i < n; i++)
                {
                    bw.Write(
                        (short)tdata[i].Width);
                    bw.Write(
                        (short)tdata[i].Height);
                    bw.Write((int)GDTBTexture
                        .BitDepthToHex[
                            tdata[i].BitDepth]);
                    bw.Write(new byte[8]);
                }

                return ms.ToArray();
            }
        }
    }
}