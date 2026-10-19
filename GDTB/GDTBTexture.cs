using System;
using System.Collections.Generic;

namespace HMSTHModdingTool.GDTB
{
    /// <summary>
    /// Represents a single texture entry
    /// in a GDTB archive
    /// </summary>
    public class GDTBTexture
    {
        // ─────────────────────────────────────────
        // PROPERTIES
        // ─────────────────────────────────────────
        public int Index { get; set; }
        public int BodyOffset { get; set; }
        public int PaletteOffset { get; set; }
        public int PaletteSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int BitDepth { get; set; }
        public int ImageSize { get; set; }

        // ─────────────────────────────────────────
        // BIT DEPTH CODE TABLES
        // ─────────────────────────────────────────
        public static readonly
            Dictionary<int, int> BitDepthToHex
            = new Dictionary<int, int>
        {
            {  4, 0x14 },
            {  8, 0x13 },
            { 16, 0x02 },
            { 24, 0x01 },
            { 32, 0x11 },
        };

        public static readonly
            Dictionary<int, int> HexToBitDepth
            = new Dictionary<int, int>
        {
            { 0x14,  4 },
            { 0x13,  8 },
            { 0x02, 16 },
            { 0x01, 24 },
            { 0x11, 32 },
        };

        // ─────────────────────────────────────────
        // HELPER PROPERTIES
        // ─────────────────────────────────────────
        public bool IsPaletted
        {
            get
            {
                return BitDepth == 4 ||
                       BitDepth == 8;
            }
        }

        // ─────────────────────────────────────────
        // STATIC HELPER METHODS
        // ─────────────────────────────────────────
        public static int GetPaletteSize(
            int bitDepth)
        {
            if (bitDepth == 4) return 0x040;
            if (bitDepth == 8) return 0x400;
            return 0;
        }

        public static int GetImageSize(
            int width,
            int height,
            int bitDepth)
        {
            int total = width * height * bitDepth;
            int size = (total + 7) / 8;
            return Math.Max(size, 1);
        }

        public static int GetBytesPerRow(
            int width,
            int bitDepth)
        {
            int bpr =
                ((width * bitDepth) + 7) / 8;
            return Math.Max(bpr, 1);
        }

        public static int GetBmpRowSize(
            int width,
            int bitDepth)
        {
            int rs =
                ((width * bitDepth + 31) / 32) * 4;
            return Math.Max(rs, 4);
        }

        public static int PadTo16(int size)
        {
            return ((size + 15) / 16) * 16;
        }

        public static byte[] Align16(byte[] data)
        {
            int remainder = data.Length % 0x10;
            if (remainder == 0) return data;
            int padLen = 0x10 - remainder;
            byte[] result =
                new byte[data.Length + padLen];
            Array.Copy(data, result, data.Length);
            return result;
        }

        // ─────────────────────────────────────────
        // TO STRING
        // ─────────────────────────────────────────
        public override string ToString()
        {
            return
                "[" + Index.ToString("D2") + "] " +
                Width + "x" + Height + " " +
                BitDepth + "-bit " +
                "(" + ImageSize + " bytes)";
        }
    }
}