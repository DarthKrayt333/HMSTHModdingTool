using System;
using System.IO;

namespace HMSTHModdingTool.BMP
{
    static class BMPPalette
    {
        private const int PaletteSize4Bit = 16 * 4;
        private const int PaletteSize8Bit = 256 * 4;
        private const int BmpFileHeaderSize = 14;
        private const int BmpInfoHeaderSize = 40;
        private const int BmpMinSize = BmpFileHeaderSize + BmpInfoHeaderSize;

        public static void Extract(string bmpPath, string outName)
        {
            BmpInfo info = ReadBmpInfo(bmpPath);
            byte[] bmpData = info.RawData;
            int ps = info.PaletteStart;
            int pe = ps + info.PaletteSize;

            byte[] palette = new byte[info.PaletteSize];
            Array.Copy(bmpData, ps, palette, 0, info.PaletteSize);

            File.WriteAllBytes(outName, palette);

            int nEntries = info.PaletteSize / 4;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Extracted " + info.BitDepth + "-bit palette");
            Console.ResetColor();
            Console.WriteLine("[*] Source BMP       : " + bmpPath);
            Console.WriteLine("[*] Palette entries  : " + nEntries);
            Console.WriteLine("[*] Palette bytes    : " + info.PaletteSize);
            Console.WriteLine("[*] Output file      : " + outName);
            Console.WriteLine();

            PrintHexDump(palette, "  ");
        }

        public static void Import(string palPath, string bmpPath)
        {
            byte[] palBytes = File.ReadAllBytes(palPath);
            int palSize = palBytes.Length;

            int palBpp;
            if (palSize == PaletteSize4Bit)
                palBpp = 4;
            else if (palSize == PaletteSize8Bit)
                palBpp = 8;
            else
                throw new InvalidDataException(
                    "Palette file '" + palPath + "' has unexpected size " + palSize + " bytes.\n" +
                    "    Expected  64 bytes (4-bit / 16-colour palette)\n" +
                    "    Expected 1024 bytes (8-bit / 256-colour palette)");

            BmpInfo info = ReadBmpInfo(bmpPath);
            int bmpBpp = info.BitDepth;

            if (palBpp != bmpBpp)
            {
                throw new InvalidDataException(
                    "Bit-depth mismatch!\n" +
                    "    BMP image  : " + bmpPath + "  ->  " + bmpBpp + "-bit image\n" +
                    "    Palette    : " + palPath + "  ->  " + palBpp + "-bit (" + palSize + "-byte) palette\n" +
                    "    This is a " + bmpBpp + "-bit image and your palette is a " + palBpp + "-bit image palette.\n" +
                    "    You cannot import a " + palBpp + "-bit palette into a " + bmpBpp + "-bit image.");
            }

            byte[] bmpData = info.RawData;
            int ps = info.PaletteStart;
            int pe = ps + info.PaletteSize;

            Array.Copy(palBytes, 0, bmpData, ps, info.PaletteSize);

            File.WriteAllBytes(bmpPath, bmpData);

            int nEntries = info.PaletteSize / 4;

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[+] Imported " + bmpBpp + "-bit palette into BMP");
            Console.ResetColor();
            Console.WriteLine("[*] Palette file     : " + palPath);
            Console.WriteLine("[*] Target BMP       : " + bmpPath);
            Console.WriteLine("[*] Palette entries  : " + nEntries);
            Console.WriteLine("[*] Palette bytes    : " + info.PaletteSize);
            Console.WriteLine("[*] Palette region   : 0x" + ps.ToString("X4") + " - 0x" + pe.ToString("X4"));
            Console.WriteLine();
            Console.WriteLine("  New palette hex dump:");
            PrintHexDump(palBytes, "  ");
        }

        private static BmpInfo ReadBmpInfo(string path)
        {
            byte[] raw = File.ReadAllBytes(path);

            if (raw.Length < BmpMinSize)
                throw new InvalidDataException("'" + path + "' is too small to be a valid BMP file.");

            if (raw[0] != 'B' || raw[1] != 'M')
                throw new InvalidDataException("'" + path + "' is not a BMP file (bad signature: " + raw[0].ToString("X2") + " " + raw[1].ToString("X2") + ").");

            int bitDepth = BitConverter.ToUInt16(raw, 28);

            if (bitDepth != 4 && bitDepth != 8)
                throw new InvalidDataException(
                    "'" + path + "' is a " + bitDepth + "-bit BMP.\n" +
                    "    Only 4-bit and 8-bit BMP files have palettes.");

            int dibSize = (int)BitConverter.ToUInt32(raw, 14);
            int paletteStart = BmpFileHeaderSize + dibSize;
            int paletteSize = bitDepth == 4 ? PaletteSize4Bit : PaletteSize8Bit;

            int paletteEnd = paletteStart + paletteSize;
            if (paletteEnd > raw.Length)
                throw new InvalidDataException(
                    "'" + path + "': palette region (0x" + paletteStart.ToString("X4") + "-0x" + paletteEnd.ToString("X4") + ") " +
                    "exceeds file size (" + raw.Length + " bytes). File may be corrupt.");

            return new BmpInfo
            {
                BitDepth = bitDepth,
                PaletteStart = paletteStart,
                PaletteSize = paletteSize,
                RawData = raw,
            };
        }

        private static void PrintHexDump(byte[] data, string indent = "", int bytesPerRow = 16)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            for (int i = 0; i < data.Length; i += bytesPerRow)
            {
                Console.Write(indent);
                int rowEnd = Math.Min(i + bytesPerRow, data.Length);
                for (int j = i; j < rowEnd; j++)
                {
                    Console.Write(data[j].ToString("X2"));
                    if (j < rowEnd - 1) Console.Write(" ");
                }
                Console.WriteLine();
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        private class BmpInfo
        {
            public int BitDepth;
            public int PaletteStart;
            public int PaletteSize;
            public byte[] RawData;
        }
    }
}