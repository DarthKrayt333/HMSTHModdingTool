using System;
using System.Collections.Generic;
using System.IO;
using HMSTHModdingTool.IO;
using HMSTHModdingTool.GDTB;

namespace HMSTHModdingTool.BMP
{
    /// <summary>
    /// Standalone PS2 BMP Converter
    /// Converts between PS2 and Windows
    /// BMP formats
    /// Supports 4-bit and 8-bit BMPs
    /// </summary>
    public class PS2BMPConverter
    {
        // ─────────────────────────────────────────
        // PRIVATE FIELDS
        // ─────────────────────────────────────────
        private static readonly PS2Converter _ps2
            = new PS2Converter();

        // ═════════════════════════════════════════
        // CONVERT TO PS2
        // ═════════════════════════════════════════
        public static void ToPS2(string inputBmp)
        {
            if (!File.Exists(inputBmp))
            {
                TextOut.PrintError(
                    "File not found: " + inputBmp);
                return;
            }

            string baseName =
                Path.GetFileNameWithoutExtension(
                    inputBmp);
            string dir =
                Path.GetDirectoryName(inputBmp)
                ?? ".";
            string output = Path.Combine(
                dir, baseName + "_ps2.bmp");

            Console.WriteLine();
            Console.WriteLine(
                "[+] Converting to PS2: " +
                inputBmp);

            var bmp = ReadBmp(inputBmp);

            if (bmp.BitDepth != 4 &&
                bmp.BitDepth != 8)
            {
                TextOut.PrintError(
                    "Only 4/8-bit supported! " +
                    "Got " + bmp.BitDepth +
                    "-bit");
                return;
            }

            Console.WriteLine(
                "    Resolution: " +
                bmp.Width + "x" + bmp.Height);
            Console.WriteLine(
                "    Bit Depth:  " +
                bmp.BitDepth + "-bit");

            if (bmp.BitDepth == 4)
                Console.WriteLine(
                    "    [4-bit] Flip + " +
                    "Nibbles + Swap R/B...");
            else
                Console.WriteLine(
                    "    [8-bit] Flip + " +
                    "Swap R/B + Swizzle...");

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

            WriteBmp(
                output,
                bmp.Width,
                bmp.Height,
                bmp.BitDepth,
                pal, pp);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] PS2 saved: " + output);
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // CONVERT TO WINDOWS
        // ═════════════════════════════════════════
        public static void ToWindows(
            string inputBmp)
        {
            if (!File.Exists(inputBmp))
            {
                TextOut.PrintError(
                    "File not found: " + inputBmp);
                return;
            }

            string baseName =
                Path.GetFileNameWithoutExtension(
                    inputBmp);

            if (baseName.EndsWith(
                "_ps2",
                StringComparison
                    .OrdinalIgnoreCase))
            {
                baseName = baseName.Substring(
                    0, baseName.Length - 4);
            }

            string dir =
                Path.GetDirectoryName(inputBmp)
                ?? ".";
            string output = Path.Combine(
                dir, baseName + "_win.bmp");

            Console.WriteLine();
            Console.WriteLine(
                "[+] Converting to Windows: " +
                inputBmp);

            var bmp = ReadBmp(inputBmp);

            if (bmp.BitDepth != 4 &&
                bmp.BitDepth != 8)
            {
                TextOut.PrintError(
                    "Only 4/8-bit supported! " +
                    "Got " + bmp.BitDepth +
                    "-bit");
                return;
            }

            Console.WriteLine(
                "    Resolution: " +
                bmp.Width + "x" + bmp.Height);
            Console.WriteLine(
                "    Bit Depth:  " +
                bmp.BitDepth + "-bit");

            if (bmp.BitDepth == 4)
                Console.WriteLine(
                    "    [4-bit] Nibbles + " +
                    "Flip + Swap R/B...");
            else
                Console.WriteLine(
                    "    [8-bit] Unswizzle + " +
                    "Swap R/B + Flip...");

            byte[] pp;
            byte[] pal;

            _ps2.PS2ToWindows(
                bmp.Width,
                bmp.Height,
                bmp.BitDepth,
                bmp.Pixels,
                bmp.Palette,
                out pp,
                out pal);

            WriteBmp(
                output,
                bmp.Width,
                bmp.Height,
                bmp.BitDepth,
                pal, pp);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "[OK] Windows saved: " + output);
            Console.ResetColor();
        }

        // ═════════════════════════════════════════
        // READ BMP
        // ═════════════════════════════════════════
        private static BmpFileData ReadBmp(
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

            int colors = bd == 8 ? 256 : 16;
            int palSize = colors * 4;

            byte[] palette = new byte[palSize];
            Array.Copy(
                data, 54,
                palette, 0,
                palSize);

            int bpr = (width * bd) / 8;
            int rs =
                ((width * bd + 31) / 32) * 4;

            var pixList = new List<byte>();

            for (int y = 0; y < height; y++)
            {
                int rowStart = pxOff + y * rs;
                for (int b = 0; b < bpr; b++)
                    pixList.Add(
                        data[rowStart + b]);
            }

            return new BmpFileData
            {
                Width = width,
                Height = height,
                BitDepth = bd,
                Palette = palette,
                Pixels = pixList.ToArray()
            };
        }

        // ═════════════════════════════════════════
        // WRITE BMP
        // ═════════════════════════════════════════
        private static void WriteBmp(
            string path,
            int width,
            int height,
            int bitDepth,
            byte[] palette,
            byte[] pixels)
        {
            int colors =
                bitDepth == 8 ? 256 : 16;
            int palSize = colors * 4;
            int bpr = (width * bitDepth) / 8;
            int rs =
                ((width * bitDepth + 31)
                 / 32) * 4;
            int imgSize = rs * height;

            using (var ms = new MemoryStream())
            using (var bw =
                new BinaryWriter(ms))
            {
                // BMP Header
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write((int)(
                    14 + 40 +
                    palSize + imgSize));
                bw.Write((int)0);
                bw.Write((int)(
                    14 + 40 + palSize));

                // DIB Header
                bw.Write((int)40);
                bw.Write((int)width);
                bw.Write((int)height);
                bw.Write((short)1);
                bw.Write((short)bitDepth);
                bw.Write((int)0);
                bw.Write((int)imgSize);
                bw.Write((int)2835);
                bw.Write((int)2835);
                bw.Write((int)colors);
                bw.Write((int)0);

                // Palette
                byte[] pal = new byte[palSize];
                int len = Math.Min(
                    palette.Length, palSize);
                Array.Copy(palette, pal, len);
                bw.Write(pal);

                // Pixel Data
                int pad = rs - bpr;
                for (int y = 0;
                     y < height; y++)
                {
                    int start = y * bpr;
                    bw.Write(pixels, start, bpr);
                    for (int p = 0;
                         p < pad; p++)
                        bw.Write((byte)0);
                }

                File.WriteAllBytes(
                    path, ms.ToArray());
            }
        }

        // ═════════════════════════════════════════
        // HELPER DATA CLASS
        // ═════════════════════════════════════════
        private class BmpFileData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int BitDepth { get; set; }
            public byte[] Palette { get; set; }
            public byte[] Pixels { get; set; }
        }
    }
}