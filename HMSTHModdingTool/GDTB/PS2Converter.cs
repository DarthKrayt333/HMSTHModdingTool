using System;

namespace HMSTHModdingTool.GDTB
{
    /// <summary>
    /// Handles PS2 to Windows BMP conversion
    /// and back. Supports 4/8/16/24/32-bit.
    /// </summary>
    public class PS2Converter
    {
        // ═════════════════════════════════════════
        // FLIP IMAGE VERTICALLY
        // ═════════════════════════════════════════
        public byte[] FlipImageVertically(
            byte[] pixels,
            int width,
            int height,
            int bitDepth)
        {
            int bpr = GDTBTexture
                .GetBytesPerRow(width, bitDepth);

            byte[] flip = new byte[pixels.Length];

            for (int y = 0; y < height; y++)
            {
                int srcRow =
                    (height - 1 - y) * bpr;
                int dstRow = y * bpr;
                Array.Copy(
                    pixels, srcRow,
                    flip, dstRow,
                    bpr);
            }
            return flip;
        }

        // ═════════════════════════════════════════
        // REVERSE 4-BIT NIBBLES
        // ═════════════════════════════════════════
        public byte[] Reverse4BitNibbles(
            byte[] pixels)
        {
            byte[] result =
                new byte[pixels.Length];

            for (int i = 0;
                 i < pixels.Length; i++)
            {
                int high =
                    (pixels[i] >> 4) & 0x0F;
                int low =
                    pixels[i] & 0x0F;
                result[i] =
                    (byte)((low << 4) | high);
            }
            return result;
        }

        // ═════════════════════════════════════════
        // SWAP RED AND BLUE - PALETTE
        // ═════════════════════════════════════════
        public byte[] SwapRedBlue(byte[] palette)
        {
            byte[] result =
                new byte[palette.Length];

            for (int i = 0;
                 i + 3 < palette.Length;
                 i += 4)
            {
                result[i] = palette[i + 2];
                result[i + 1] = palette[i + 1];
                result[i + 2] = palette[i];
                result[i + 3] = palette[i + 3];
            }
            return result;
        }

        // ═════════════════════════════════════════
        // SWAP RED AND BLUE - 24-BIT PIXELS
        // ═════════════════════════════════════════
        public byte[] SwapRedBlue24Bit(
            byte[] pixels)
        {
            byte[] result =
                new byte[pixels.Length];

            for (int i = 0;
                 i + 2 < pixels.Length;
                 i += 3)
            {
                result[i] = pixels[i + 2];
                result[i + 1] = pixels[i + 1];
                result[i + 2] = pixels[i];
            }
            return result;
        }

        // ═════════════════════════════════════════
        // SWAP RED AND BLUE - 32-BIT PIXELS
        // ═════════════════════════════════════════
        public byte[] SwapRedBlue32Bit(
            byte[] pixels)
        {
            byte[] result =
                new byte[pixels.Length];

            for (int i = 0;
                 i + 3 < pixels.Length;
                 i += 4)
            {
                result[i] = pixels[i + 2];
                result[i + 1] = pixels[i + 1];
                result[i + 2] = pixels[i];
                result[i + 3] = pixels[i + 3];
            }
            return result;
        }

        // ═════════════════════════════════════════
        // SWAP RED AND BLUE - 16-BIT ARGB1555
        // ═════════════════════════════════════════
        public byte[] SwapRedBlue16Bit(
            byte[] pixels)
        {
            byte[] result =
                new byte[pixels.Length];

            for (int i = 0;
                 i + 1 < pixels.Length;
                 i += 2)
            {
                int val =
                    pixels[i] |
                    (pixels[i + 1] << 8);

                int a = (val >> 15) & 0x01;
                int r = (val >> 10) & 0x1F;
                int g = (val >> 5) & 0x1F;
                int b = val & 0x1F;

                int newVal =
                    (a << 15) |
                    (b << 10) |
                    (g << 5) |
                     r;

                result[i] =
                    (byte)(newVal & 0xFF);
                result[i + 1] =
                    (byte)((newVal >> 8) & 0xFF);
            }
            return result;
        }

        // ═════════════════════════════════════════
        // SWIZZLE PALETTE
        // SAME FUNCTION BOTH DIRECTIONS
        // ═════════════════════════════════════════
        public byte[] SwizzlePalette(
            byte[] palette)
        {
            if (palette.Length < 1024)
                return palette;

            byte[][] colors = new byte[256][];
            for (int i = 0; i < 256; i++)
            {
                colors[i] = new byte[4];
                Array.Copy(
                    palette, i * 4,
                    colors[i], 0, 4);
            }

            byte[] result = new byte[1024];

            for (int block = 0;
                 block < 8; block++)
            {
                int b = block * 32;

                for (int i = 0; i < 8; i++)
                {
                    // Group 1: stays [0-7]
                    Array.Copy(
                        colors[b + i], 0,
                        result,
                        (b + i) * 4, 4);

                    // Group 2: [8-15] -> [16-23]
                    Array.Copy(
                        colors[b + 8 + i], 0,
                        result,
                        (b + 16 + i) * 4, 4);

                    // Group 3: [16-23] -> [8-15]
                    Array.Copy(
                        colors[b + 16 + i], 0,
                        result,
                        (b + 8 + i) * 4, 4);

                    // Group 4: stays [24-31]
                    Array.Copy(
                        colors[b + 24 + i], 0,
                        result,
                        (b + 24 + i) * 4, 4);
                }
            }
            return result;
        }

        // ═════════════════════════════════════════
        // PS2 TO WINDOWS
        // ═════════════════════════════════════════
        public void PS2ToWindows(
            int width,
            int height,
            int bitDepth,
            byte[] pixels,
            byte[] palette,
            out byte[] outPixels,
            out byte[] outPalette)
        {
            switch (bitDepth)
            {
                case 4:
                    outPixels =
                        FlipImageVertically(
                            Reverse4BitNibbles(
                                pixels),
                            width, height,
                            bitDepth);
                    outPalette =
                        SwapRedBlue(palette);
                    break;

                case 8:
                    outPixels =
                        FlipImageVertically(
                            pixels,
                            width, height,
                            bitDepth);
                    outPalette =
                        SwapRedBlue(
                            SwizzlePalette(
                                palette));
                    break;

                case 16:
                    outPixels =
                        FlipImageVertically(
                            SwapRedBlue16Bit(
                                pixels),
                            width, height,
                            bitDepth);
                    outPalette = new byte[0];
                    break;

                case 24:
                    outPixels =
                        FlipImageVertically(
                            SwapRedBlue24Bit(
                                pixels),
                            width, height,
                            bitDepth);
                    outPalette = new byte[0];
                    break;

                case 32:
                    outPixels =
                        FlipImageVertically(
                            SwapRedBlue32Bit(
                                pixels),
                            width, height,
                            bitDepth);
                    outPalette = new byte[0];
                    break;

                default:
                    throw new ArgumentException(
                        "Unsupported bit depth: " +
                        bitDepth);
            }
        }

        // ═════════════════════════════════════════
        // WINDOWS TO PS2
        // ═════════════════════════════════════════
        public void WindowsToPS2(
            int width,
            int height,
            int bitDepth,
            byte[] pixels,
            byte[] palette,
            out byte[] outPixels,
            out byte[] outPalette)
        {
            switch (bitDepth)
            {
                case 4:
                    outPixels =
                        Reverse4BitNibbles(
                            FlipImageVertically(
                                pixels,
                                width, height,
                                bitDepth));
                    outPalette =
                        SwapRedBlue(palette);
                    break;

                case 8:
                    outPixels =
                        FlipImageVertically(
                            pixels,
                            width, height,
                            bitDepth);
                    outPalette =
                        SwizzlePalette(
                            SwapRedBlue(palette));
                    break;

                case 16:
                    outPixels =
                        SwapRedBlue16Bit(
                            FlipImageVertically(
                                pixels,
                                width, height,
                                bitDepth));
                    outPalette = new byte[0];
                    break;

                case 24:
                    outPixels =
                        SwapRedBlue24Bit(
                            FlipImageVertically(
                                pixels,
                                width, height,
                                bitDepth));
                    outPalette = new byte[0];
                    break;

                case 32:
                    outPixels =
                        SwapRedBlue32Bit(
                            FlipImageVertically(
                                pixels,
                                width, height,
                                bitDepth));
                    outPalette = new byte[0];
                    break;

                default:
                    throw new ArgumentException(
                        "Unsupported bit depth: " +
                        bitDepth);
            }
        }
    }
}