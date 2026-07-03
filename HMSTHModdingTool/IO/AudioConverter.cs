using System;
using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Converts a single looped .VAG audio file into
    ///     PS2-ready .BD / .HD / .SQ music files for
    ///     Harvest Moon: Save the Homeland.
    ///
    ///     The .HD is a pre-optimized template with
    ///     only the sample rate (2 bytes at offset 0x68)
    ///     patched from the VAG's actual sample rate.
    ///     Everything else stays exactly as-is.
    ///
    ///     The .SQ is copied exactly as-is (blank PS2
    ///     MIDI that loops VAG index 0 forever).
    ///
    ///     The .BD is the raw ADPCM data from the VAG
    ///     with the VAGp header stripped.
    ///
    ///     Output is placed in a subfolder named after
    ///     the input VAG file:
    ///       BaseName/
    ///         BaseName.BD
    ///         BaseName.HD
    ///         BaseName.SQ
    /// </summary>
    class AudioConverter
    {
        // ── PS2 SPU2 hardware maximum ─────────────────────
        // PlayStation 2 SPU2 audio chip can only
        // play up to 48000 Hz. Anything higher
        // causes overflow in the HD sample rate
        // field (which is only 2 bytes / U16 LE).
        private const uint PS2_MAX_SAMPLE_RATE = 48000;

        // ── VAGp header constants ─────────────────────────
        private const int VAG_HEADER_SIZE = 0x30;
        private const int VAG_SAMPLE_RATE_OFFSET = 0x10;

        // ── HD sample rate location ───────────────────────
        // The ONLY bytes in the HD template that change.
        // Located inside the VAGi (VAG info) chunk at
        // offset 0x68 - 0x69 (U16 Little Endian).
        private const int HD_SAMPLE_RATE_OFFSET = 0x68;

        // ─────────────────────────────────────────────────
        /// <summary>
        ///     Converts a single .VAG file into .BD / .HD / .SQ
        ///     using the embedded pre-optimized HD and SQ
        ///     templates.
        ///
        ///     BD = raw ADPCM data (VAGp header stripped).
        ///     HD = template with sample rate patched to match
        ///          the input VAG. Everything else stays exact.
        ///     SQ = template copied exactly as-is.
        ///
        ///     Output is placed into a new subfolder named
        ///     after the input VAG file.
        ///
        ///     Example:
        ///       Input : C:\MUSIC\spring.vag
        ///       Output: C:\MUSIC\SPRING\SPRING.BD
        ///               C:\MUSIC\SPRING\SPRING.HD
        ///               C:\MUSIC\SPRING\SPRING.SQ
        /// </summary>
        /// <param name="VagPath">
        ///     Full path to the input .VAG file.
        /// </param>
        public static void ConvertVagToMusic(
            string VagPath)
        {
            // ── Validate input file exists ────────────────
            if (!File.Exists(VagPath))
                throw new FileNotFoundException(
                    "VAG file not found.",
                    VagPath);

            // ── Read entire VAG file ──────────────────────
            byte[] VagData =
                File.ReadAllBytes(VagPath);

            // ── Validate VAGp magic bytes ─────────────────
            if (VagData.Length < VAG_HEADER_SIZE ||
                VagData[0] != (byte)'V' ||
                VagData[1] != (byte)'A' ||
                VagData[2] != (byte)'G' ||
                VagData[3] != (byte)'p')
            {
                throw new InvalidDataException(
                    "Invalid VAG file! " +
                    "Expected 'VAGp' magic at" +
                    " offset 0x00.\n" +
                    "Make sure the input file is a" +
                    " valid PS2 VAG.");
            }

            // ── Read sample rate from VAGp header ─────────
            uint SampleRate =
                ReadU32BE(
                    VagData,
                    VAG_SAMPLE_RATE_OFFSET);

            // ── PS2 SPU2 hardware limit ────────────────────
            // PS2 audio can play maximum 48000 Hz.
            // If VAG is higher, cap it to 48000 to
            // avoid overflow in the HD file
            // (which only has 2 bytes for sample rate).
            //const uint PS2_MAX_SAMPLE_RATE = 48000;

            if (SampleRate > PS2_MAX_SAMPLE_RATE)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(string.Format(
                    "  WARNING: VAG sample rate" +
                    " {0} Hz exceeds PS2 maximum" +
                    " ({1} Hz)!",
                    SampleRate,
                    PS2_MAX_SAMPLE_RATE));
                Console.WriteLine(string.Format(
                    "  Capping to {0} Hz for HD" +
                    " (audio will play slower).",
                    PS2_MAX_SAMPLE_RATE));
                Console.WriteLine(
                    "  Tip: Re-encode your VAG at" +
                    " 48000 Hz or lower for" +
                    " correct playback speed.");
                Console.ResetColor();

                SampleRate = PS2_MAX_SAMPLE_RATE;
            }

            // ── Extract raw ADPCM ─────────────────────────
            int AdpcmOffset = VAG_HEADER_SIZE;
            int AdpcmLength =
                VagData.Length - VAG_HEADER_SIZE;
            byte[] AdpcmData =
                new byte[AdpcmLength];
            Array.Copy(
                VagData,
                AdpcmOffset,
                AdpcmData,
                0,
                AdpcmLength);
            uint AdpcmSize =
                (uint)AdpcmLength;

            // ── Print VAG info ────────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Gray;
            Console.WriteLine(string.Format(
                "  VAG Sample Rate : {0} Hz",
                SampleRate));
            Console.WriteLine(string.Format(
                "  VAG ADPCM Size  :" +
                " 0x{0:X} bytes ({1} bytes)",
                AdpcmSize,
                AdpcmSize));
            Console.ResetColor();

            // ── Load HD template ──────────────────────────
            byte[] HdData = GetHdTemplate();

            // ── Patch sample rate in HD ────────────────────
            // At offset 0x68 (2 bytes, U16 LE)
            // This is the ONLY change we make to
            // the HD template. Everything else
            // stays exactly as the template says.
            HdData[HD_SAMPLE_RATE_OFFSET] =
                (byte)(SampleRate & 0xFF);
            HdData[HD_SAMPLE_RATE_OFFSET + 1] =
                (byte)((SampleRate >> 8) & 0xFF);

            Console.ForegroundColor =
                ConsoleColor.Gray;
            Console.WriteLine(string.Format(
                "  HD sample rate  :" +
                " patched to {0} Hz" +
                " (0x{1:X2} 0x{2:X2} at" +
                " offset 0x{3:X2})",
                SampleRate,
                HdData[HD_SAMPLE_RATE_OFFSET],
                HdData[HD_SAMPLE_RATE_OFFSET + 1],
                HD_SAMPLE_RATE_OFFSET));
            Console.ResetColor();

            // ── Load SQ template ──────────────────────────
            byte[] SqData = GetSqTemplate();

            // ── Get filename base ─────────────────────────
            string BaseName =
                Path.GetFileNameWithoutExtension(
                    VagPath).ToUpper();
            string Dir =
                Path.GetDirectoryName(VagPath);
            string OutDir =
                Path.Combine(Dir, BaseName);

            Directory.CreateDirectory(OutDir);

            string BdPath =
                Path.Combine(
                    OutDir, BaseName + ".BD");
            string HdPath =
                Path.Combine(
                    OutDir, BaseName + ".HD");
            string SqPath =
                Path.Combine(
                    OutDir, BaseName + ".SQ");

            // ── Write output files ────────────────────────
            File.WriteAllBytes(BdPath, AdpcmData);
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Created : " + BaseName +
                "\\" + BaseName + ".BD");

            File.WriteAllBytes(HdPath, HdData);
            Console.WriteLine(
                "  Created : " + BaseName +
                "\\" + BaseName + ".HD");

            File.WriteAllBytes(SqPath, SqData);
            Console.WriteLine(
                "  Created : " + BaseName +
                "\\" + BaseName + ".SQ");
            Console.ResetColor();

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "\n  Output folder   : " +
                BaseName + "\\");
            Console.ResetColor();
        }

        // ─────────────────────────────────────────────────
        // HD TEMPLATE
        // ─────────────────────────────────────────────────
        private static byte[] GetHdTemplate()
        {
            return new byte[]
            {
                0x49, 0x45, 0x43, 0x53,
                0x73, 0x72, 0x65, 0x56,
                0x10, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x02, 0x00,
                0x49, 0x45, 0x43, 0x53,
                0x64, 0x61, 0x65, 0x48,
                0x40, 0x00, 0x00, 0x00,
                0x20, 0x01, 0x00, 0x00,
                0x50, 0x88, 0x0A, 0x00,
                0xD0, 0x00, 0x00, 0x00,
                0xB0, 0x00, 0x00, 0x00,
                0x70, 0x00, 0x00, 0x00,
                0x50, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x49, 0x45, 0x43, 0x53,
                0x69, 0x67, 0x61, 0x56,
                0x20, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x14, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                // ↓ Offset 0x68-0x69 : sample rate
                //   (patched at runtime)
                0x22, 0x56,
                0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x49, 0x45, 0x43, 0x53,
                0x6C, 0x70, 0x6D, 0x53,
                0x40, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x14, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x01, 0x40,
                0x7F, 0x00, 0x01, 0x00,
                0x00, 0x01, 0x00, 0x3C,
                0x00, 0x40, 0x00, 0x0A,
                0xFF, 0xFF, 0xFF, 0xAF,
                0xCB, 0x5F, 0x00, 0x3C,
                0x00, 0x3C, 0x00, 0x3C,
                0x00, 0x3C, 0x00, 0x3C,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x03, 0xFF, 0xFF,
                0x49, 0x45, 0x43, 0x53,
                0x74, 0x65, 0x73, 0x53,
                0x20, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x14, 0x00, 0x00, 0x00,
                0x00, 0x01, 0x7F, 0x01,
                0x00, 0x00, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x49, 0x45, 0x43, 0x53,
                0x67, 0x6F, 0x72, 0x50,
                0x50, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x14, 0x00, 0x00, 0x00,
                0x24, 0x00, 0x00, 0x00,
                0x01, 0x14, 0xFF, 0x40,
                0x00, 0x00, 0x00, 0x3C,
                0x00, 0xFF, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x64, 0x00, 0x64, 0x00,
                0x80, 0x00, 0x80, 0x00,
                0x80, 0x00, 0x80, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x0C, 0x3C,
                0x77, 0x00, 0x00, 0x06,
                0x00, 0x06, 0x00, 0x3C,
                0x00, 0x3C, 0x00, 0x3C,
                0xFF, 0x40, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF
            };
        }

        // ─────────────────────────────────────────────────
        // SQ TEMPLATE
        // ─────────────────────────────────────────────────
        private static byte[] GetSqTemplate()
        {
            return new byte[]
            {
                0x49, 0x45, 0x43, 0x53,
                0x73, 0x72, 0x65, 0x56,
                0x10, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x02, 0x00,
                0x49, 0x45, 0x43, 0x53,
                0x75, 0x71, 0x65, 0x53,
                0x20, 0x00, 0x00, 0x00,
                0xB0, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x30, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x49, 0x45, 0x43, 0x53,
                0x69, 0x64, 0x69, 0x4D,
                0x90, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0x14, 0x00, 0x00, 0x00,
                0x06, 0x00, 0x00, 0x00,
                0xE0, 0x01, 0x00, 0xFF,
                0x51, 0x03, 0x04, 0x00,
                0x00, 0x00, 0xC0, 0x80,
                0xC3, 0x03, 0x05, 0xB3,
                0x01, 0x80, 0xB0, 0x01,
                0x00, 0x01, 0xB0, 0x07,
                0x6C, 0x01, 0xB0, 0x07,
                0x6C, 0x05, 0x0A, 0x40,
                0x00, 0xB0, 0x0A, 0x1E,
                0x01, 0x90, 0x3C, 0xB2,
                0xC2, 0x82, 0xC1, 0x01,
                0x05, 0xB1, 0x01, 0x80,
                0xB2, 0x01, 0x00, 0x01,
                0xB0, 0x07, 0x6C, 0x01,
                0xB0, 0x07, 0x6C, 0x05,
                0x0A, 0x5A, 0x00, 0xB2,
                0x0A, 0x1E, 0x03, 0x63,
                0x00, 0x01, 0x06, 0x01,
                0x89, 0x29, 0x91, 0x3E,
                0x34, 0x83, 0x28, 0x81,
                0x3E, 0x01, 0xB0, 0x63,
                0x01, 0x01, 0x06, 0x01,
                0x01, 0x26, 0x00, 0x00,
                0xFF, 0x2F, 0x00, 0x00,
                0x00, 0xFF, 0xFF, 0xFF,
                0xFF, 0xFF, 0xFF, 0xFF
            };
        }

        // ─────────────────────────────────────────────────
        // BINARY HELPERS
        // ─────────────────────────────────────────────────
        private static uint ReadU32BE(
            byte[] buf, int offset)
        {
            return (uint)(
                (buf[offset] << 24) |
                (buf[offset + 1] << 16) |
                (buf[offset + 2] << 8) |
                 buf[offset + 3]);
        }
    }
}
