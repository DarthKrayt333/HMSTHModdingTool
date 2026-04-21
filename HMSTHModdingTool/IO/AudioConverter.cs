using System;
using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Converts a single looped .VAG audio file into
    ///     PS2-ready .BD / .HD / .SQ music files for
    ///     Harvest Moon: Save the Homeland.
    ///
    ///     The .HD and .SQ are exact pre-optimized templates
    ///     designed for single-VAG whole-music playback.
    ///     Both are copied exactly as-is — no modifications.
    ///     The .BD is the raw ADPCM data from the VAG file
    ///     with the VAGp header stripped.
    ///
    ///     Output is placed in a subfolder named after
    ///     the input VAG file, containing:
    ///       BaseName/
    ///         BaseName.BD
    ///         BaseName.HD
    ///         BaseName.SQ
    /// </summary>
    class AudioConverter
    {
        // ── VAGp header constants ─────────────────────────────────────────
        /// <summary>Size of the VAGp header to strip (always 0x30 bytes).</summary>
        private const int VAG_HEADER_SIZE = 0x30;
        /// <summary>Offset of sample rate in VAGp header (U32 Big Endian).</summary>
        private const int VAG_SAMPLE_RATE_OFFSET = 0x10;

        // ─────────────────────────────────────────────────────────────────
        /// <summary>
        ///     Converts a single .VAG file into .BD / .HD / .SQ
        ///     using the embedded pre-optimized HD and SQ templates.
        ///
        ///     BD = raw ADPCM data (VAGp header stripped).
        ///     HD = exact template, copied as-is, no modifications.
        ///     SQ = exact template, copied as-is, no modifications.
        ///
        ///     Output is placed into a new subfolder named after
        ///     the input VAG file, in the same parent directory.
        ///
        ///     Example:
        ///       Input : C:\MUSIC\spring.vag
        ///       Output: C:\MUSIC\Spring\Spring.BD
        ///               C:\MUSIC\Spring\Spring.HD
        ///               C:\MUSIC\Spring\Spring.SQ
        /// </summary>
        /// <param name="VagPath">Full path to the input .VAG file.</param>
        public static void ConvertVagToMusic(string VagPath)
        {
            // ── Validate input file exists ────────────────────────────────
            if (!File.Exists(VagPath))
                throw new FileNotFoundException(
                    "VAG file not found.", VagPath);

            // ── Read entire VAG file ──────────────────────────────────────
            byte[] VagData = File.ReadAllBytes(VagPath);

            // ── Validate VAGp magic bytes ─────────────────────────────────
            if (VagData.Length < VAG_HEADER_SIZE ||
                VagData[0] != (byte)'V' ||
                VagData[1] != (byte)'A' ||
                VagData[2] != (byte)'G' ||
                VagData[3] != (byte)'p')
            {
                throw new InvalidDataException(
                    "Invalid VAG file! " +
                    "Expected 'VAGp' magic at offset 0x00.\n" +
                    "Make sure the input file is a valid PS2 VAG.");
            }

            // ── Read sample rate from VAGp header ─────────────────────────
            uint SampleRate = ReadU32BE(VagData, VAG_SAMPLE_RATE_OFFSET);

            // ── Extract raw ADPCM data (strip 0x30 byte VAGp header) ──────
            int AdpcmOffset = VAG_HEADER_SIZE;
            int AdpcmLength = VagData.Length - VAG_HEADER_SIZE;
            byte[] AdpcmData = new byte[AdpcmLength];
            Array.Copy(VagData, AdpcmOffset, AdpcmData, 0, AdpcmLength);
            uint AdpcmSize = (uint)AdpcmLength;

            // ── Print VAG info ────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(string.Format(
                "  VAG Sample Rate : {0} Hz", SampleRate));
            Console.WriteLine(string.Format(
                "  VAG ADPCM Size  : 0x{0:X} bytes ({1} bytes)",
                AdpcmSize, AdpcmSize));
            Console.ResetColor();

            // ── Load HD template — exact bytes, no modifications ──────────
            byte[] HdData = GetHdTemplate();

            // ── Load SQ template — exact bytes, no modifications ──────────
            byte[] SqData = GetSqTemplate();

            // ── Get real filename casing from disk ────────────────────────
            string BaseName = Path.GetFileNameWithoutExtension(VagPath).ToUpper();
            string Dir = Path.GetDirectoryName(VagPath);
            string OutDir = Path.Combine(Dir, BaseName);

            // Create the output subfolder (safe if already exists)
            Directory.CreateDirectory(OutDir);

            string BdPath = Path.Combine(OutDir, BaseName + ".BD");
            string HdPath = Path.Combine(OutDir, BaseName + ".HD");
            string SqPath = Path.Combine(OutDir, BaseName + ".SQ");

            // ── Write output files ────────────────────────────────────────
            File.WriteAllBytes(BdPath, AdpcmData);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                "  Created : " + BaseName + "\\" + BaseName + ".BD");

            File.WriteAllBytes(HdPath, HdData);
            Console.WriteLine(
                "  Created : " + BaseName + "\\" + BaseName + ".HD");

            File.WriteAllBytes(SqPath, SqData);
            Console.WriteLine(
                "  Created : " + BaseName + "\\" + BaseName + ".SQ");
            Console.ResetColor();

            // ── Print output folder ───────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "\n  Output folder   : " + BaseName + "\\");
            Console.ResetColor();
        }

        // ─────────────────────────────────────────────────────────────────
        // HD TEMPLATE
        // Exact pre-optimized bytes for single-VAG whole-music playback.
        // Copied exactly as-is — no modifications at runtime.
        // ─────────────────────────────────────────────────────────────────
        private static byte[] GetHdTemplate()
        {
            return new byte[]
            {
                0x49, 0x45, 0x43, 0x53, 0x73, 0x72, 0x65, 0x56, // 0x00
                0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, // 0x08
                0x49, 0x45, 0x43, 0x53, 0x64, 0x61, 0x65, 0x48, // 0x10
                0x40, 0x00, 0x00, 0x00, 0x20, 0x01, 0x00, 0x00, // 0x18
                0x50, 0x88, 0x0A, 0x00, 0xD0, 0x00, 0x00, 0x00, // 0x20
                0xB0, 0x00, 0x00, 0x00, 0x70, 0x00, 0x00, 0x00, // 0x28
                0x50, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, // 0x30
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0x38
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0x40
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0x48
                0x49, 0x45, 0x43, 0x53, 0x69, 0x67, 0x61, 0x56, // 0x50
                0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0x58
                0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0x60
                0x22, 0x56, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0x68
                0x49, 0x45, 0x43, 0x53, 0x6C, 0x70, 0x6D, 0x53, // 0x70
                0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0x78
                0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x40, // 0x80
                0x7F, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x3C, // 0x88
                0x00, 0x40, 0x00, 0x0A, 0xFF, 0xFF, 0xFF, 0xAF, // 0x90
                0xCB, 0x5F, 0x00, 0x3C, 0x00, 0x3C, 0x00, 0x3C, // 0x98
                0x00, 0x3C, 0x00, 0x3C, 0x00, 0x00, 0x00, 0x00, // 0xA0
                0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0xFF, 0xFF, // 0xA8 ← extra 0x00 added
                0x49, 0x45, 0x43, 0x53, 0x74, 0x65, 0x73, 0x53, // 0xB0
                0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0xB8
                0x14, 0x00, 0x00, 0x00, 0x00, 0x01, 0x7F, 0x01, // 0xC0
                0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 0xC8
                0x49, 0x45, 0x43, 0x53, 0x67, 0x6F, 0x72, 0x50, // 0xD0
                0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0xD8
                0x14, 0x00, 0x00, 0x00, 0x24, 0x00, 0x00, 0x00, // 0xE0
                0x01, 0x14, 0xFF, 0x40, 0x00, 0x00, 0x00, 0x3C, // 0xE8
                0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 0xF0
                0x64, 0x00, 0x64, 0x00, 0x80, 0x00, 0x80, 0x00, // 0xF8
                0x80, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, // 0x100
                0x00, 0x00, 0x0C, 0x3C, 0x77, 0x00, 0x00, 0x06, // 0x108
                0x00, 0x06, 0x00, 0x3C, 0x00, 0x3C, 0x00, 0x3C, // 0x110
                0xFF, 0x40, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF  // 0x118
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // SQ TEMPLATE
        // Exact pre-optimized PS2 MIDI sequence for single-VAG
        // whole-music playback. Copied exactly as-is.
        // ─────────────────────────────────────────────────────────────────
        private static byte[] GetSqTemplate()
        {
            return new byte[]
            {
                0x49, 0x45, 0x43, 0x53, 0x73, 0x72, 0x65, 0x56,
                0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00,
                0x49, 0x45, 0x43, 0x53, 0x75, 0x71, 0x65, 0x53,
                0x20, 0x00, 0x00, 0x00, 0xB0, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0x30, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x49, 0x45, 0x43, 0x53, 0x69, 0x64, 0x69, 0x4D,
                0x90, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x14, 0x00, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00,
                0xE0, 0x01, 0x00, 0xFF, 0x51, 0x03, 0x04, 0x00,
                0x00, 0x00, 0xC0, 0x80, 0xC3, 0x03, 0x05, 0xB3,
                0x01, 0x80, 0xB0, 0x01, 0x00, 0x01, 0xB0, 0x07,
                0x6C, 0x01, 0xB0, 0x07, 0x6C, 0x05, 0x0A, 0x40,
                0x00, 0xB0, 0x0A, 0x1E, 0x01, 0x90, 0x3C, 0xB2,
                0xC2, 0x82, 0xC1, 0x01, 0x05, 0xB1, 0x01, 0x80,
                0xB2, 0x01, 0x00, 0x01, 0xB0, 0x07, 0x6C, 0x01,
                0xB0, 0x07, 0x6C, 0x05, 0x0A, 0x5A, 0x00, 0xB2,
                0x0A, 0x1E, 0x03, 0x63, 0x00, 0x01, 0x06, 0x01,
                0x89, 0x29, 0x91, 0x3E, 0x34, 0x83, 0x28, 0x81,
                0x3E, 0x01, 0xB0, 0x63, 0x01, 0x01, 0x06, 0x01,
                0x01, 0x26, 0x00, 0x00, 0xFF, 0x2F, 0x00, 0x00,
                0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // BINARY HELPERS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Reads a U32 Big Endian from a byte array.</summary>
        private static uint ReadU32BE(byte[] buf, int offset)
        {
            return (uint)(
                (buf[offset] << 24) |
                (buf[offset + 1] << 16) |
                (buf[offset + 2] << 8) |
                 buf[offset + 3]);
        }
    }
}