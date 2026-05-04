using System;
using System.IO;

namespace HMSTHModdingTool.BoyMods
{
    // ═══════════════════════════════════════════════════════════════
    // BOY MOD PRESETS
    // Applies pre-made skeleton binary data to 00_skeleton.bin
    // or directly patches into BOY_00000.rdtb
    //
    // BoyModV2 = Taller BoyModV2 - Default Farmer Version
    // BoyModV3 = Taller BoyModV3 - Uptight Farmer Version
    //
    // BOY 3D Tools by DarthKrayt333
    // HMSTHModdingTool
    // Harvest Moon: Save The Homeland (PS2)
    // ═══════════════════════════════════════════════════════════════
    public static class BoyModPresets
    {
        // ─────────────────────────────────────────
        // VERSION NAMES
        // ─────────────────────────────────────────
        public const string MOD_V2_NAME =
            "Taller Player Mod BoyModV2" +
            " - Default Farmer Version";

        public const string MOD_V3_NAME =
            "Taller Player Mod BoyModV3" +
            " - Uptight Farmer Version";

        // ─────────────────────────────────────────
        // BOY MOD V2 HEX DATA
        // Taller BoyModV2 - Default Farmer Version
        // ─────────────────────────────────────────
        private static readonly byte[] MOD_V2_DATA =
            ParseHex(
            "10 01 00 00 20 01 00 00 30 01 00 00 " +
            "40 01 00 00 50 01 00 00 60 01 00 00 " +
            "70 01 00 00 80 01 00 00 90 01 00 00 " +
            "A0 01 00 00 B0 01 00 00 C0 01 00 00 " +
            "D0 01 00 00 E0 01 00 00 F0 01 00 00 " +
            "00 02 00 00 10 02 00 00 20 02 00 00 " +
            "30 02 00 00 40 02 00 00 50 02 00 00 " +
            "60 02 00 00 70 02 00 00 80 02 00 00 " +
            "90 02 00 00 A0 02 00 00 B0 02 00 00 " +
            "C0 02 00 00 D0 02 00 00 E0 02 00 00 " +
            "F0 02 00 00 00 03 00 00 10 03 00 00 " +
            "20 03 00 00 30 03 00 00 40 03 00 00 " +
            "50 03 00 00 60 03 00 00 70 03 00 00 " +
            "80 03 00 00 90 03 00 00 A0 03 00 00 " +
            "B0 03 00 00 C0 03 00 00 D0 03 00 00 " +
            "E0 03 00 00 F0 03 00 00 00 04 00 00 " +
            "10 04 00 00 20 04 00 00 30 04 00 00 " +
            "40 04 00 00 50 04 00 00 60 04 00 00 " +
            "70 04 00 00 80 04 00 00 90 04 00 00 " +
            "A0 04 00 00 B0 04 00 00 C0 04 00 00 " +
            "D0 04 00 00 E0 04 00 00 F0 04 00 00 " +
            "00 05 00 00 10 05 00 00 20 05 00 00 " +
            "30 05 00 00 40 05 00 00 00 00 00 00 " +
            "00 00 8B 42 00 00 F0 BF 01 FF FF 00 " +
            "00 00 00 00 00 00 C8 36 00 00 48 B4 " +
            "02 FF 00 00 00 00 00 00 AC E1 5A 41 " +
            "26 00 04 41 FF 03 01 00 00 00 00 00 " +
            "0B 5C 7F 41 00 40 97 B5 04 32 01 00 " +
            "00 00 00 00 77 66 B6 40 EC 51 A6 B5 " +
            "05 FF 03 00 00 00 00 00 F8 FF B7 41 " +
            "A4 9A 79 40 06 0F 04 00 00 00 00 00 " +
            "08 00 9B 41 32 00 78 C1 FF 07 05 00 " +
            "00 00 00 00 3C 00 F0 40 31 00 34 C1 " +
            "FF 08 05 00 00 00 00 00 8A 00 C0 40 " +
            "D0 FF 2F 41 09 0C 05 00 00 40 83 B5 " +
            "22 92 1D C1 58 44 DE 3F 0A FF 08 00 " +
            "00 40 83 35 42 91 9D C0 CE 43 5E 3F " +
            "0B FF 09 00 00 00 00 00 C5 91 1D C1 " +
            "3F 46 DE 3F FF FF 0A 00 FB FF 7F BF " +
            "0A 00 EC 41 33 00 70 C1 FF 0D 05 00 " +
            "FE FF FF BF 0A 00 EC 41 30 00 70 C1 " +
            "FF 0E 05 00 FC FF 9F 40 0A 00 EC 41 " +
            "30 00 60 C1 FF FF 05 00 72 68 31 41 " +
            "A8 D0 61 41 0A 00 40 40 10 20 04 00 " +
            "68 66 26 40 00 00 00 00 00 00 16 B5 " +
            "11 FF 0F 00 8A 86 79 41 00 00 16 37 " +
            "00 00 00 00 12 FF 10 00 7C 66 A6 3F " +
            "00 00 00 00 00 00 00 00 13 FF 11 00 " +
            "BE 22 30 41 00 00 25 B7 00 00 00 00 " +
            "14 FF 12 00 11 99 AB 40 00 00 00 00 " +
            "00 00 00 00 15 FF 13 00 00 00 D0 40 " +
            "3A FF 27 C0 67 00 A8 C0 16 18 14 00 " +
            "6C 00 BD 40 60 69 06 BF CC 32 97 C0 " +
            "17 FF 15 00 14 FD 27 BF 00 00 00 00 " +
            "00 00 00 00 FF FF 16 00 91 66 6E 41 " +
            "00 00 00 00 00 00 48 35 19 1B 14 00 " +
            "47 33 C1 40 00 00 00 00 00 00 00 00 " +
            "1A FF 18 00 34 00 A8 BF 00 00 00 00 " +
            "00 00 C8 34 FF FF 19 00 31 00 5C 41 " +
            "DE FF 8F C0 00 00 C8 34 1C FF 14 00 " +
            "00 00 48 C1 02 00 20 C1 00 00 60 C1 " +
            "1D 1E 1B 00 00 00 00 00 00 00 00 00 " +
            "FF FF 9F C0 FF FF 1C 00 00 00 00 00 " +
            "00 00 00 00 FF FF 07 41 FF 1F 1B 00 " +
            "00 00 00 00 00 00 48 38 FE FF CF C0 " +
            "FF FF 1B 00 9C 1D 16 C1 A8 D0 61 41 " +
            "D6 CC 2C 40 21 FF 04 00 68 66 26 C0 " +
            "00 00 00 00 00 00 16 B5 22 FF 20 00 " +
            "8A 86 79 C1 00 00 16 37 00 00 00 00 " +
            "23 FF 21 00 7C 66 A6 BF 00 00 00 00 " +
            "00 00 00 00 24 FF 22 00 BE 22 30 C1 " +
            "00 00 25 B7 00 00 00 00 25 FF 23 00 " +
            "11 99 AB C0 00 00 00 00 00 00 00 00 " +
            "26 FF 24 00 00 00 D0 C0 3A FF 27 C0 " +
            "67 00 A8 C0 27 29 25 00 6C 00 BD C0 " +
            "60 69 06 BF CC 32 97 C0 28 FF 26 00 " +
            "14 FD 27 3F 00 00 00 00 00 00 00 00 " +
            "FF FF 27 00 91 66 6E C1 00 00 00 00 " +
            "00 00 48 35 2A 2C 25 00 47 33 C1 C0 " +
            "00 00 00 00 00 00 00 00 2B FF 29 00 " +
            "34 00 A8 3F 00 00 00 00 00 00 C8 34 " +
            "FF FF 2A 00 31 00 5C C1 F7 FF 8F C0 " +
            "00 00 96 35 2D FF 25 00 0B 00 60 C0 " +
            "11 00 00 C0 0B 00 60 C0 2E 2F 2C 00 " +
            "00 00 00 00 00 00 00 00 06 00 E0 40 " +
            "FF FF 2D 00 00 00 00 00 00 00 00 00 " +
            "00 00 1C C2 30 31 2C 00 00 00 00 00 " +
            "00 00 00 00 01 00 B4 C1 FF FF 2F 00 " +
            "FD FF 0F C1 00 00 00 00 03 00 84 C1 " +
            "FF FF 2C 00 98 D9 08 41 84 3D AE C0 " +
            "BE CC CC 3E 33 3B 01 00 00 00 72 35 " +
            "96 16 51 C0 00 00 00 00 34 FF 32 00 " +
            "FC FF AF 3F E1 D0 9C C1 CB CC 8C 3F " +
            "35 FF 33 00 00 00 5C 35 02 00 10 C0 " +
            "00 00 DC B3 36 FF 34 00 7A E1 1A 3E " +
            "FF FF 71 C1 4E E1 1A 40 37 FF 35 00 " +
            "FC 51 E8 3E 07 00 DC C0 3E E1 1A 3F " +
            "38 FF 36 00 00 00 F2 B5 FC FF 2F C0 " +
            "00 80 B5 B5 39 FF 37 00 04 00 80 3F " +
            "00 00 F8 C0 FE FF E7 C0 3A FF 38 00 " +
            "11 00 00 BF 2A 00 80 3E 26 00 40 3F " +
            "FF FF 39 00 99 D9 08 C1 84 3D AE C0 " +
            "BE CC CC 3E 3C FF 01 00 00 00 00 00 " +
            "96 16 51 C0 00 00 00 00 3D FF 3B 00 " +
            "FC FF AF BF E1 D0 9C C1 CB CC 8C 3F " +
            "3E FF 3C 00 00 00 5C B5 02 00 10 C0 " +
            "00 00 DC B3 3F FF 3D 00 3E E1 1A BE " +
            "FF FF 71 C1 4E E1 1A 40 40 FF 3E 00 " +
            "1A 52 E8 BE 07 00 DC C0 3E E1 1A 3F " +
            "41 FF 3F 00 00 00 F2 35 FC FF 2F C0 " +
            "00 80 B5 B5 42 FF 40 00 0B 00 80 BF " +
            "00 00 F8 C0 FE FF E7 C0 43 FF 41 00 " +
            "11 00 00 3F 2A 00 80 3E 0D 00 40 3F " +
            "FF FF 42 00"
        );

        // ─────────────────────────────────────────
        // BOY MOD V3 HEX DATA
        // Taller BoyModV3 - Uptight Farmer Version
        // ─────────────────────────────────────────
        private static readonly byte[] MOD_V3_DATA =
            ParseHex(
            "10 01 00 00 20 01 00 00 30 01 00 00 " +
            "40 01 00 00 50 01 00 00 60 01 00 00 " +
            "70 01 00 00 80 01 00 00 90 01 00 00 " +
            "A0 01 00 00 B0 01 00 00 C0 01 00 00 " +
            "D0 01 00 00 E0 01 00 00 F0 01 00 00 " +
            "00 02 00 00 10 02 00 00 20 02 00 00 " +
            "30 02 00 00 40 02 00 00 50 02 00 00 " +
            "60 02 00 00 70 02 00 00 80 02 00 00 " +
            "90 02 00 00 A0 02 00 00 B0 02 00 00 " +
            "C0 02 00 00 D0 02 00 00 E0 02 00 00 " +
            "F0 02 00 00 00 03 00 00 10 03 00 00 " +
            "20 03 00 00 30 03 00 00 40 03 00 00 " +
            "50 03 00 00 60 03 00 00 70 03 00 00 " +
            "80 03 00 00 90 03 00 00 A0 03 00 00 " +
            "B0 03 00 00 C0 03 00 00 D0 03 00 00 " +
            "E0 03 00 00 F0 03 00 00 00 04 00 00 " +
            "10 04 00 00 20 04 00 00 30 04 00 00 " +
            "40 04 00 00 50 04 00 00 60 04 00 00 " +
            "70 04 00 00 80 04 00 00 90 04 00 00 " +
            "A0 04 00 00 B0 04 00 00 C0 04 00 00 " +
            "D0 04 00 00 E0 04 00 00 F0 04 00 00 " +
            "00 05 00 00 10 05 00 00 20 05 00 00 " +
            "30 05 00 00 40 05 00 00 00 00 00 00 " +
            "00 00 8B 42 00 00 F0 BF 01 FF FF 00 " +
            "00 00 00 00 00 00 C8 36 00 00 48 B4 " +
            "02 FF 00 00 00 00 00 00 AC E1 5A 41 " +
            "26 00 04 41 FF 03 01 00 00 00 00 00 " +
            "0B 5C 7F 41 00 40 97 B5 04 32 01 00 " +
            "00 00 00 00 77 66 B6 40 EC 51 A6 B5 " +
            "05 FF 03 00 00 00 00 00 F8 FF B7 41 " +
            "A4 9A 79 40 06 0F 04 00 00 00 00 00 " +
            "08 00 9B 41 32 00 78 C1 FF 07 05 00 " +
            "00 00 00 00 3C 00 F0 40 31 00 34 C1 " +
            "FF 08 05 00 00 00 00 00 8A 00 C0 40 " +
            "D0 FF 2F 41 09 0C 05 00 00 40 83 B5 " +
            "22 92 1D C1 58 44 DE 3F 0A FF 08 00 " +
            "00 40 83 35 42 91 9D C0 CE 43 5E 3F " +
            "0B FF 09 00 00 00 00 00 C5 91 1D C1 " +
            "3F 46 DE 3F FF FF 0A 00 FB FF 7F BF " +
            "0A 00 EC 41 33 00 70 C1 FF 0D 05 00 " +
            "FE FF FF BF 0A 00 EC 41 30 00 70 C1 " +
            "FF 0E 05 00 FC FF 9F 40 0A 00 EC 41 " +
            "30 00 60 C1 FF FF 05 00 72 68 31 41 " +
            "D7 E7 7A 41 0A 00 40 40 10 20 04 00 " +
            "68 66 26 40 00 00 00 00 00 00 16 B5 " +
            "11 FF 0F 00 8A 86 79 41 00 00 16 37 " +
            "00 00 00 00 12 FF 10 00 7C 66 A6 3F " +
            "00 00 00 00 00 00 00 00 13 FF 11 00 " +
            "BE 22 30 41 00 00 25 B7 00 00 00 00 " +
            "14 FF 12 00 11 99 AB 40 00 00 00 00 " +
            "00 00 00 00 15 FF 13 00 00 00 D0 40 " +
            "3A FF 27 C0 67 00 A8 C0 16 18 14 00 " +
            "6C 00 BD 40 60 69 06 BF CC 32 97 C0 " +
            "17 FF 15 00 14 FD 27 BF 00 00 00 00 " +
            "00 00 00 00 FF FF 16 00 91 66 6E 41 " +
            "00 00 00 00 00 00 48 35 19 1B 14 00 " +
            "47 33 C1 40 00 00 00 00 00 00 00 00 " +
            "1A FF 18 00 34 00 A8 BF 00 00 00 00 " +
            "00 00 C8 34 FF FF 19 00 31 00 5C 41 " +
            "DE FF 8F C0 00 00 C8 34 1C FF 14 00 " +
            "00 00 48 C1 02 00 20 C1 00 00 60 C1 " +
            "1D 1E 1B 00 00 00 00 00 00 00 00 00 " +
            "FF FF 9F C0 FF FF 1C 00 00 00 00 00 " +
            "00 00 00 00 FF FF 07 41 FF 1F 1B 00 " +
            "00 00 00 00 00 00 48 38 FE FF CF C0 " +
            "FF FF 1B 00 91 CB 26 C1 D7 E7 7A 41 " +
            "0A 00 40 40 21 FF 04 00 68 66 26 C0 " +
            "00 00 00 00 00 00 16 B5 22 FF 20 00 " +
            "8A 86 79 C1 00 00 16 37 00 00 00 00 " +
            "23 FF 21 00 7C 66 A6 BF 00 00 00 00 " +
            "00 00 00 00 24 FF 22 00 BE 22 30 C1 " +
            "00 00 25 B7 00 00 00 00 25 FF 23 00 " +
            "11 99 AB C0 00 00 00 00 00 00 00 00 " +
            "26 FF 24 00 00 00 D0 C0 3A FF 27 C0 " +
            "67 00 A8 C0 27 29 25 00 6C 00 BD C0 " +
            "60 69 06 BF CC 32 97 C0 28 FF 26 00 " +
            "14 FD 27 3F 00 00 00 00 00 00 00 00 " +
            "FF FF 27 00 91 66 6E C1 00 00 00 00 " +
            "00 00 48 35 2A 2C 25 00 47 33 C1 C0 " +
            "00 00 00 00 00 00 00 00 2B FF 29 00 " +
            "34 00 A8 3F 00 00 00 00 00 00 C8 34 " +
            "FF FF 2A 00 31 00 5C C1 F7 FF 8F C0 " +
            "00 00 96 35 2D FF 25 00 0B 00 60 C0 " +
            "11 00 00 C0 0B 00 60 C0 2E 2F 2C 00 " +
            "00 00 00 00 00 00 00 00 06 00 E0 40 " +
            "FF FF 2D 00 00 00 00 00 00 00 00 00 " +
            "00 00 1C C2 30 31 2C 00 00 00 00 00 " +
            "00 00 00 00 01 00 B4 C1 FF FF 2F 00 " +
            "FD FF 0F C1 00 00 00 00 03 00 84 C1 " +
            "FF FF 2C 00 98 D9 08 41 84 3D AE C0 " +
            "BE CC CC 3E 33 3B 01 00 00 00 72 35 " +
            "96 16 51 C0 00 00 00 00 34 FF 32 00 " +
            "FC FF AF 3F E1 D0 9C C1 CB CC 8C 3F " +
            "35 FF 33 00 00 00 5C 35 02 00 10 C0 " +
            "00 00 DC B3 36 FF 34 00 7A E1 1A 3E " +
            "FF FF 71 C1 4E E1 1A 40 37 FF 35 00 " +
            "FC 51 E8 3E 07 00 DC C0 3E E1 1A 3F " +
            "38 FF 36 00 00 00 F2 B5 FC FF 2F C0 " +
            "00 80 B5 B5 39 FF 37 00 04 00 80 3F " +
            "00 00 F8 C0 FE FF E7 C0 3A FF 38 00 " +
            "11 00 00 BF 2A 00 80 3E 26 00 40 3F " +
            "FF FF 39 00 99 D9 08 C1 84 3D AE C0 " +
            "BE CC CC 3E 3C FF 01 00 00 00 00 00 " +
            "96 16 51 C0 00 00 00 00 3D FF 3B 00 " +
            "FC FF AF BF E1 D0 9C C1 CB CC 8C 3F " +
            "3E FF 3C 00 00 00 5C B5 02 00 10 C0 " +
            "00 00 DC B3 3F FF 3D 00 3E E1 1A BE " +
            "FF FF 71 C1 4E E1 1A 40 40 FF 3E 00 " +
            "1A 52 E8 BE 07 00 DC C0 3E E1 1A 3F " +
            "41 FF 3F 00 00 00 F2 35 FC FF 2F C0 " +
            "00 80 B5 B5 42 FF 40 00 0B 00 80 BF " +
            "00 00 F8 C0 FE FF E7 C0 43 FF 41 00 " +
            "11 00 00 3F 2A 00 80 3E 0D 00 40 3F " +
            "FF FF 42 00"
        );

        // ─────────────────────────────────────────
        // BOY ORIGINAL HEX DATA
        // Original vanilla BOY skeleton
        // ─────────────────────────────────────────
        public const string MOD_ORIGINAL_NAME =
            "BOY Original - Vanilla PS2 Skeleton";

        private static readonly byte[] MOD_ORIGINAL_DATA =
            ParseHex(
            "10 01 00 00 20 01 00 00 30 01 00 00 " +
            "40 01 00 00 50 01 00 00 60 01 00 00 " +
            "70 01 00 00 80 01 00 00 90 01 00 00 " +
            "A0 01 00 00 B0 01 00 00 C0 01 00 00 " +
            "D0 01 00 00 E0 01 00 00 F0 01 00 00 " +
            "00 02 00 00 10 02 00 00 20 02 00 00 " +
            "30 02 00 00 40 02 00 00 50 02 00 00 " +
            "60 02 00 00 70 02 00 00 80 02 00 00 " +
            "90 02 00 00 A0 02 00 00 B0 02 00 00 " +
            "C0 02 00 00 D0 02 00 00 E0 02 00 00 " +
            "F0 02 00 00 00 03 00 00 10 03 00 00 " +
            "20 03 00 00 30 03 00 00 40 03 00 00 " +
            "50 03 00 00 60 03 00 00 70 03 00 00 " +
            "80 03 00 00 90 03 00 00 A0 03 00 00 " +
            "B0 03 00 00 C0 03 00 00 D0 03 00 00 " +
            "E0 03 00 00 F0 03 00 00 00 04 00 00 " +
            "10 04 00 00 20 04 00 00 30 04 00 00 " +
            "40 04 00 00 50 04 00 00 60 04 00 00 " +
            "70 04 00 00 80 04 00 00 90 04 00 00 " +
            "A0 04 00 00 B0 04 00 00 C0 04 00 00 " +
            "D0 04 00 00 E0 04 00 00 F0 04 00 00 " +
            "00 05 00 00 10 05 00 00 20 05 00 00 " +
            "30 05 00 00 40 05 00 00 00 00 00 00 " +
            "00 00 80 42 00 00 C0 BF 01 FF FF 00 " +
            "00 00 00 00 00 00 C8 36 00 00 48 B4 " +
            "02 FF 00 00 00 00 00 00 58 00 C0 40 " +
            "45 00 F0 40 FF 03 01 00 00 00 00 00 " +
            "E6 FF DF 40 00 80 89 B5 04 32 01 00 " +
            "00 00 00 00 0F 00 20 40 00 00 16 B5 " +
            "05 FF 03 00 00 00 00 00 F8 FF B7 41 " +
            "CD 00 40 40 06 0F 04 00 00 00 00 00 " +
            "08 00 9B 41 32 00 78 C1 FF 07 05 00 " +
            "00 00 00 00 3C 00 F0 40 31 00 34 C1 " +
            "FF 08 05 00 00 00 00 00 8A 00 C0 40 " +
            "D0 FF 2F 41 09 0C 05 00 00 40 83 B5 " +
            "22 92 1D C1 58 44 DE 3F 0A FF 08 00 " +
            "00 40 83 35 42 91 9D C0 CE 43 5E 3F " +
            "0B FF 09 00 00 00 00 00 C5 91 1D C1 " +
            "3F 46 DE 3F FF FF 0A 00 FB FF 7F BF " +
            "0A 00 EC 41 33 00 70 C1 FF 0D 05 00 " +
            "FE FF FF BF 0A 00 EC 41 30 00 70 C1 " +
            "FF 0E 05 00 FC FF 9F 40 0A 00 EC 41 " +
            "30 00 60 C1 FF FF 05 00 FF FF 1F 41 " +
            "01 00 80 41 0A 00 40 40 10 20 04 00 " +
            "01 00 00 40 00 00 00 00 00 00 C8 B4 " +
            "11 FF 0F 00 FE FF 87 41 00 00 C8 36 " +
            "00 00 00 00 12 FF 10 00 11 00 80 3F " +
            "00 00 00 00 00 00 00 00 13 FF 11 00 " +
            "FF FF 1F 41 00 00 C8 B6 00 00 00 00 " +
            "14 FF 12 00 42 FF 6F 40 00 00 00 00 " +
            "00 00 00 00 15 FF 13 00 00 00 D0 40 " +
            "3A FF 27 C0 67 00 A8 C0 16 18 14 00 " +
            "6C 00 BD 40 60 69 06 BF CC 32 97 C0 " +
            "17 FF 15 00 14 FD 27 BF 00 00 00 00 " +
            "00 00 00 00 FF FF 16 00 91 66 6E 41 " +
            "00 00 00 00 00 00 48 35 19 1B 14 00 " +
            "47 33 C1 40 00 00 00 00 00 00 00 00 " +
            "1A FF 18 00 34 00 A8 BF 00 00 00 00 " +
            "00 00 C8 34 FF FF 19 00 31 00 5C 41 " +
            "DE FF 8F C0 00 00 C8 34 1C FF 14 00 " +
            "00 00 48 C1 02 00 20 C1 00 00 60 C1 " +
            "1D 1E 1B 00 00 00 00 00 00 00 00 00 " +
            "FF FF 9F C0 FF FF 1C 00 00 00 00 00 " +
            "00 00 00 00 FF FF 07 41 FF 1F 1B 00 " +
            "00 00 00 00 00 00 48 38 FE FF CF C0 " +
            "FF FF 1B 00 FF FF 1F C1 01 00 80 41 " +
            "0A 00 40 40 21 FF 04 00 01 00 00 C0 " +
            "00 00 00 00 00 00 C8 B4 22 FF 20 00 " +
            "FE FF 87 C1 00 00 C8 36 00 00 00 00 " +
            "23 FF 21 00 11 00 80 BF 00 00 00 00 " +
            "00 00 00 00 24 FF 22 00 FF FF 1F C1 " +
            "00 00 C8 B6 00 00 00 00 25 FF 23 00 " +
            "42 FF 6F C0 00 00 00 00 00 00 00 00 " +
            "26 FF 24 00 00 00 D0 C0 3A FF 27 C0 " +
            "67 00 A8 C0 27 29 25 00 6C 00 BD C0 " +
            "60 69 06 BF CC 32 97 C0 28 FF 26 00 " +
            "14 FD 27 3F 00 00 00 00 00 00 00 00 " +
            "FF FF 27 00 91 66 6E C1 00 00 00 00 " +
            "00 00 48 35 2A 2C 25 00 47 33 C1 C0 " +
            "00 00 00 00 00 00 00 00 2B FF 29 00 " +
            "34 00 A8 3F 00 00 00 00 00 00 C8 34 " +
            "FF FF 2A 00 31 00 5C C1 F7 FF 8F C0 " +
            "00 00 96 35 2D FF 25 00 0B 00 60 C0 " +
            "11 00 00 C0 0B 00 60 C0 2E 2F 2C 00 " +
            "00 00 00 00 00 00 00 00 06 00 E0 40 " +
            "FF FF 2D 00 00 00 00 00 00 00 00 00 " +
            "00 00 1C C2 30 31 2C 00 00 00 00 00 " +
            "00 00 00 00 01 00 B4 C1 FF FF 2F 00 " +
            "FD FF 0F C1 00 00 00 00 03 00 84 C1 " +
            "FF FF 2C 00 FE FF 0B 41 10 00 90 C0 " +
            "ED FF FF 3E 33 3B 01 00 00 00 48 35 " +
            "0D 00 40 C0 00 00 00 00 34 FF 32 00 " +
            "FC FF 9F 3F FC FF 83 C1 FC FF 7F 3F " +
            "35 FF 33 00 00 00 48 35 02 00 20 C0 " +
            "00 00 C8 B3 36 FF 34 00 2A 00 00 3E " +
            "FF FF 5B C1 06 00 00 40 37 FF 35 00 " +
            "0D 00 C0 3E 06 00 C8 C0 F0 FF FF 3E " +
            "38 FF 36 00 00 00 C8 B5 FC FF 1F C0 " +
            "00 00 96 B5 39 FF 37 00 04 00 80 3F " +
            "00 00 F8 C0 FE FF E7 C0 3A FF 38 00 " +
            "11 00 00 BF 2A 00 80 3E 26 00 40 3F " +
            "FF FF 39 00 FF FF 0B C1 10 00 90 C0 " +
            "ED FF FF 3E 3C FF 01 00 00 00 00 00 " +
            "0D 00 40 C0 00 00 00 00 3D FF 3B 00 " +
            "FC FF 9F BF FC FF 83 C1 FC FF 7F 3F " +
            "3E FF 3C 00 00 00 48 B5 02 00 20 C0 " +
            "00 00 C8 B3 3F FF 3D 00 F0 FF FF BD " +
            "FF FF 5B C1 06 00 00 40 40 FF 3E 00 " +
            "26 00 C0 BE 06 00 C8 C0 F0 FF FF 3E " +
            "41 FF 3F 00 00 00 C8 35 FC FF 1F C0 " +
            "00 00 96 B5 42 FF 40 00 0B 00 80 BF " +
            "00 00 F8 C0 FE FF E7 C0 43 FF 41 00 " +
            "11 00 00 3F 2A 00 80 3E 0D 00 40 3F " +
            "FF FF 42 00"
        );

        // ─────────────────────────────────────────
        // PUBLIC ENTRY POINT - ORIGINAL BOY
        // ─────────────────────────────────────────
        public static void ApplyOriginal(string[] args)
        {
            ApplyMod(
                args,
                MOD_ORIGINAL_DATA,
                MOD_ORIGINAL_NAME,
                "Original");
        }

        // ═════════════════════════════════════════
        // PUBLIC ENTRY POINTS
        // ═════════════════════════════════════════

        public static void ApplyModV2(string[] args)
        {
            ApplyMod(args, MOD_V2_DATA,
                MOD_V2_NAME, "V2");
        }

        public static void ApplyModV3(string[] args)
        {
            ApplyMod(args, MOD_V3_DATA,
                MOD_V3_NAME, "V3");
        }

        // ═════════════════════════════════════════
        // SHARED APPLY LOGIC
        // args[1] = -bin or -rdtb or bin or rdtb
        // args[2] = filepath
        // ═════════════════════════════════════════
        private static void ApplyMod(
            string[] args,
            byte[] modData,
            string modName,
            string modVer)
        {
            // ── Header ────────────────────────────
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  ╔══════════════════════════════════" +
                "════════════════════════════════════╗");
            Console.WriteLine(
                $"  ║  {modName,-68}║");
            Console.WriteLine(
                "  ║  HMSTHModdingTool  |  " +
                "BOY 3D Tools by DarthKrayt333" +
                "                          ║");
            Console.WriteLine(
                "  ╚══════════════════════════════════" +
                "════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // ── Smart 2-arg shortcut ──────────────
            // "boymodv2 BOY_00000.rdtb"
            // "boymodv2 00_skeleton.bin"
            // Auto detect from extension!
            if (args.Length == 2)
            {
                string autoFile = args[1];

                if (!File.Exists(autoFile))
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        $"  ERROR: File not found:" +
                        $" {autoFile}");
                    Console.ResetColor();
                    return;
                }

                string ext = Path.GetExtension(
                    autoFile).ToLower();

                if (ext == ".rdtb")
                {
                    ApplyToRdtb(
                        autoFile, modData,
                        modName, modVer);
                    return;
                }
                else if (ext == ".bin")
                {
                    ApplyToBin(
                        autoFile, modData,
                        modName, modVer);
                    return;
                }
                else
                {
                    Console.ForegroundColor =
                        ConsoleColor.Red;
                    Console.WriteLine(
                        "  ERROR: Cannot auto-detect" +
                        " file type.");
                    Console.WriteLine(
                        "  Use -bin or -rdtb" +
                        " explicitly.");
                    Console.ResetColor();
                    Console.WriteLine();
                    PrintModHelp(modVer, modName);
                    return;
                }
            }

            // ── Standard 3-arg mode ───────────────
            if (args.Length < 3)
            {
                PrintModHelp(modVer, modName);
                return;
            }

            string modeRaw = args[1]
                .TrimStart('-')
                .ToLower();
            string filepath = args[2];

            if (!File.Exists(filepath))
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    $"  ERROR: File not found:" +
                    $" {filepath}");
                Console.ResetColor();
                return;
            }

            if (modeRaw == "bin")
            {
                ApplyToBin(
                    filepath, modData,
                    modName, modVer);
            }
            else if (modeRaw == "rdtb")
            {
                ApplyToRdtb(
                    filepath, modData,
                    modName, modVer);
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    $"  ERROR: Unknown mode" +
                    $" '{args[1]}'");
                Console.WriteLine(
                    "  Use -bin or -rdtb");
                Console.ResetColor();
                Console.WriteLine();
                PrintModHelp(modVer, modName);
            }
        }

        // ═════════════════════════════════════════
        // APPLY TO 00_skeleton.bin
        // Replaces entire file content
        // ═════════════════════════════════════════
        private static void ApplyToBin(
            string filepath,
            byte[] modData,
            string modName,
            string modVer)
        {
            Console.WriteLine(
                "  Mode   : Apply to .bin file");
            Console.WriteLine(
                $"  File   : " +
                Path.GetFileName(filepath));
            Console.WriteLine(
                $"  Size   : " +
                $"{new FileInfo(filepath).Length:N0}" +
                " bytes (original)");
            Console.WriteLine(
                $"  Mod    : {modName}");
            Console.WriteLine(
                $"  New sz : {modData.Length:N0} bytes");
            Console.WriteLine();

            // ── Backup ────────────────────────────
            string backup = filepath + ".original";
            if (!File.Exists(backup))
            {
                File.Copy(filepath, backup);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Backup created: " +
                    Path.GetFileName(backup));
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(
                    "  Backup exists : " +
                    Path.GetFileName(backup));
            }

            // ── Write mod data ────────────────────
            File.WriteAllBytes(filepath, modData);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  ✓ Applied {modName}");
            Console.ResetColor();
            Console.WriteLine();

            // ── Success message ───────────────────
            Console.ForegroundColor =
            ConsoleColor.Magenta;

            if (modVer == "Original")
            {
                Console.WriteLine(
                    "  Restored BOY to" +
                    " Original Vanilla Skeleton!" +
                    " Build it into game!!!! :)");
            }
            else
            {
                Console.WriteLine(
                    $"  Applied taller Player Mod" +
                    $" BoyMod{modVer} -" +
                    $" {GetShortName(modName)}," +
                    $" build it into game!!!! :)");
            }

            Console.ResetColor();
            Console.WriteLine();

            PrintBuildStepsBin();
        }

        // ═════════════════════════════════════════
        // APPLY TO BOY_00000.rdtb
        // Patches chunk 0 (skeleton) in-place
        // ═════════════════════════════════════════
        private static void ApplyToRdtb(
            string filepath,
            byte[] modData,
            string modName,
            string modVer)
        {
            Console.WriteLine(
                "  Mode   : Apply to .rdtb file");
            Console.WriteLine(
                $"  File   : " +
                Path.GetFileName(filepath));
            Console.WriteLine(
                $"  Mod    : {modName}");
            Console.WriteLine();

            byte[] rdtb =
                File.ReadAllBytes(filepath);

            // ── Validate RDTB magic ───────────────
            if (rdtb.Length < 8 ||
                rdtb[0] != 0x52 ||
                rdtb[1] != 0x44 ||
                rdtb[2] != 0x54 ||
                rdtb[3] != 0x42)
            {
                Console.ForegroundColor =
                    ConsoleColor.Red;
                Console.WriteLine(
                    "  ERROR: Not a valid RDTB" +
                    " file! (bad magic bytes)");
                Console.WriteLine(
                    "  Expected: 52 44 54 42" +
                    " (RDTB)");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  ✓ Valid RDTB file");
            Console.ResetColor();

            // ── Read chunk offsets ────────────────
            // Offset table at 0x10
            // Slot 0 = chunk 0 start
            // Slot 1 = chunk 1 start = chunk 0 end
            int chunk0Offset =
                BitConverter.ToInt32(rdtb, 0x10);
            int chunk1Offset =
                BitConverter.ToInt32(rdtb, 0x14);

            int oldChunk0Size =
                chunk1Offset - chunk0Offset;
            int newChunk0Size = modData.Length;
            int sizeDiff =
                newChunk0Size - oldChunk0Size;

            Console.WriteLine(
                $"  Chunk 0 offset : " +
                $"0x{chunk0Offset:X8}" +
                $" ({chunk0Offset:N0})");
            Console.WriteLine(
                $"  Old chunk 0 sz : " +
                $"{oldChunk0Size:N0} bytes");
            Console.WriteLine(
                $"  New chunk 0 sz : " +
                $"{newChunk0Size:N0} bytes");
            Console.WriteLine(
                $"  Size diff      : " +
                $"{sizeDiff:+0;-0;0} bytes");
            Console.WriteLine();

            // ── Backup ────────────────────────────
            string backup = filepath + ".original";
            if (!File.Exists(backup))
            {
                File.WriteAllBytes(backup, rdtb);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "  Backup created: " +
                    Path.GetFileName(backup));
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(
                    "  Backup exists : " +
                    Path.GetFileName(backup));
            }

            // ── Build new RDTB ────────────────────
            int restSize =
                rdtb.Length - chunk1Offset;

            byte[] newRdtb = new byte[
                rdtb.Length + sizeDiff];

            // Copy header (everything before chunk 0)
            Array.Copy(
                rdtb, 0,
                newRdtb, 0,
                chunk0Offset);

            // Copy new skeleton chunk 0
            Array.Copy(
                modData, 0,
                newRdtb, chunk0Offset,
                newChunk0Size);

            // Copy remaining chunks
            Array.Copy(
                rdtb, chunk1Offset,
                newRdtb,
                chunk0Offset + newChunk0Size,
                restSize);

            // ── Update offset table ───────────────
            // Slot 0 stays same (chunk0Offset)
            // Slots 1-13 need += sizeDiff
            for (int slot = 1; slot < 14; slot++)
            {
                int pos = 0x10 + slot * 4;
                int existingOff =
                    BitConverter.ToInt32(
                        newRdtb, pos);

                if (existingOff == 0) break;

                int newOff = existingOff + sizeDiff;
                byte[] offBytes =
                    BitConverter.GetBytes(newOff);
                Array.Copy(
                    offBytes, 0,
                    newRdtb, pos, 4);
            }

            // ── Write new RDTB ────────────────────
            File.WriteAllBytes(filepath, newRdtb);

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  ✓ Applied {modName}");
            Console.WriteLine(
                $"  Old RDTB size : " +
                $"{rdtb.Length:N0} bytes");
            Console.WriteLine(
                $"  New RDTB size : " +
                $"{newRdtb.Length:N0} bytes");
            Console.ResetColor();
            Console.WriteLine();

            // ── Success message ───────────────────
            Console.ForegroundColor =
            ConsoleColor.Magenta;

            if (modVer == "Original")
            {
                Console.WriteLine(
                    "  Restored BOY to" +
                    " Original Vanilla Skeleton!" +
                    " Build it into game!!!! :)");
            }
            else
            {
                Console.WriteLine(
                    $"  Applied taller Player Mod" +
                    $" BoyMod{modVer} -" +
                    $" {GetShortName(modName)}," +
                    $" build it into game!!!! :)");
            }

            Console.ResetColor();
            Console.WriteLine();

            PrintBuildStepsRdtb();
        }

        // ═════════════════════════════════════════
        // NEXT STEPS
        // ═════════════════════════════════════════
        private static void PrintBuildStepsBin()
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ┌─ NEXT STEPS ──────────────────────────────────────────┐");
            Console.WriteLine(
                "  │  1. tool.exe -crdtb <folder> BOY_00000.rdtb           │");
            Console.WriteLine(
                "  │  2. tool.exe -chda BOY BOY.HDA                        │");
            Console.WriteLine(
                "  │  3. Test in game! Enjoy the new look! :)               │");
            Console.WriteLine(
                "  └───────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintBuildStepsRdtb()
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  ┌─ NEXT STEPS ──────────────────────────────────────────┐");
            Console.WriteLine(
                "  │  1. tool.exe -chda BOY BOY.HDA                        │");
            Console.WriteLine(
                "  │  2. Test in game! Enjoy the new look! :)               │");
            Console.WriteLine(
                "  └───────────────────────────────────────────────────────┘");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═════════════════════════════════════════
        // PRINT MOD HELP
        // ═════════════════════════════════════════
        private static void PrintModHelp(
            string modVer,
            string modName)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                $"  Usage for BoyMod{modVer}:");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine(
                "  Apply to 00_skeleton.bin:");
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                $"    tool.exe" +
                $" -boymod{modVer.ToLower()}" +
                $" -bin 00_skeleton.bin");
            Console.WriteLine(
                $"    tool.exe" +
                $" boymod{modVer.ToLower()}" +
                $" bin 00_skeleton.bin");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine(
                "  Apply directly to RDTB:");
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                $"    tool.exe" +
                $" -boymod{modVer.ToLower()}" +
                $" -rdtb BOY_00000.rdtb");
            Console.WriteLine(
                $"    tool.exe" +
                $" boymod{modVer.ToLower()}" +
                $" rdtb BOY_00000.rdtb");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                $"  Mod: {modName}");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════
        private static string GetShortName(
            string modName)
        {
            int dash = modName.IndexOf(" - ");
            if (dash >= 0)
                return modName.Substring(dash + 3);
            return modName;
        }

        // ═════════════════════════════════════════
        // PARSE HEX STRING TO BYTE ARRAY
        // ═════════════════════════════════════════
        private static byte[] ParseHex(string hex)
        {
            hex = hex
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ");

            string[] parts = hex.Split(
                new char[]{
                    ' ', '\n', '\r', '\t' },
                StringSplitOptions
                    .RemoveEmptyEntries);

            var result =
                new System.Collections.Generic
                    .List<byte>();

            foreach (string p in parts)
            {
                if (p.Length == 2)
                {
                    result.Add(
                        Convert.ToByte(p, 16));
                }
            }

            return result.ToArray();
        }
    }
}
