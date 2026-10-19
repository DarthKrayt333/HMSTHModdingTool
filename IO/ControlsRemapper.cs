using HMSTHModdingTool.IO;
using System;
using System.IO;

namespace HMSTHModdingTool
{
    // ═══════════════════════════════════════════
    // CONTROLS REMAPPER
    // Toggles remapped<->original controls
    // Works on ISO, BIN, RAW, IMG files
    // and standalone ELF files
    // Auto-detects version and patch state
    // Run again to revert / toggle
    // ═══════════════════════════════════════════
    public static class ControlsRemapper
    {
        // ───────────────────────────────────────
        // EXACT BYTE DIFFERENCES
        // between original and patched ELF
        // (SLPS_201.04 / SLUS_202.51 /
        //  SLPM_601.47 - all same offsets)
        // offset = ELF file offset
        // orig   = original byte value
        // patch  = patched byte value
        // ───────────────────────────────────────
        static readonly (int offset,
            byte orig, byte patch)[]
            PATCH_TABLE =
        {
            (0x000047C8, 0x7A, 0xF1),
            (0x000047C9, 0x11, 0x59),
            (0x0000A474, 0x70, 0x14),
            (0x0000A475, 0x00, 0xF4),
            (0x0000A477, 0x24, 0x08),
            (0x0000A4E4, 0x08, 0x17),
            (0x0000A4E5, 0x00, 0xF4),
            (0x0000A4E6, 0xE0, 0x03),
            (0x0000A4E7, 0x03, 0x08),
            (0x00016840, 0x2D, 0x1C),
            (0x00016841, 0x40, 0x5A),
            (0x00016842, 0x80, 0x04),
            (0x00016843, 0x00, 0x08),
            (0x00016844, 0x20, 0x11),
            (0x00016846, 0xC2, 0x0C),
            (0x00016847, 0x2C, 0x3C),
            (0x00016848, 0x1C, 0x24),
            (0x00016849, 0x00, 0x68),
            (0x0001684A, 0x40, 0x8C),
            (0x0001684B, 0x14, 0x35),
            (0x0001684C, 0x2D, 0x04),
            (0x0001684D, 0x18, 0x00),
            (0x0001684E, 0x00, 0x8D),
            (0x0001684F, 0x01, 0x25),
            (0x00016850, 0x25, 0x00),
            (0x00016851, 0x10, 0x00),
            (0x00016852, 0xA8, 0x8E),
            (0x00016853, 0x00, 0x8D),
            (0x00016854, 0x0F, 0x7F),
            (0x00016856, 0x42, 0xCF),
            (0x00016857, 0x30, 0x31),
            (0x00016858, 0x19, 0xC2),
            (0x00016859, 0x00, 0x71),
            (0x0001685A, 0x40, 0x0E),
            (0x0001685B, 0x54, 0x00),
            (0x0001685C, 0xFF, 0x00),
            (0x0001685D, 0xFF, 0x00),
            (0x0001685E, 0xC6, 0xD8),
            (0x0001685F, 0x24, 0x8D),
            (0x00016860, 0x2D, 0x04),
            (0x00016861, 0x38, 0x00),
            (0x00016862, 0x00, 0xD9),
            (0x00016863, 0x01, 0x8D),
            (0x00016864, 0x00, 0x7F),
            (0x00016866, 0xA3, 0x01),
            (0x00016867, 0x78, 0x33),
            (0x00016868, 0xE0, 0x23),
            (0x00016869, 0xFF, 0x08),
            (0x0001686A, 0xC6, 0x81),
            (0x0001686B, 0x24, 0x01),
            (0x0001686C, 0x10, 0x08),
            (0x0001686E, 0xA5, 0x20),
            (0x0001686F, 0x24, 0x00),
            (0x00016870, 0x20, 0xC2),
            (0x00016871, 0x00, 0xC1),
            (0x00016872, 0xC4, 0x18),
            (0x00016873, 0x2C, 0x00),
            (0x00016874, 0x00, 0x04),
            (0x00016876, 0xE3, 0x00),
            (0x00016877, 0x7C, 0x10),
            (0x00016878, 0x10, 0x00),
            (0x0001687A, 0xE7, 0x19),
            (0x0001687B, 0x24, 0xA3),
            (0x0001687C, 0x00, 0x02),
            (0x0001687E, 0xA2, 0x00),
            (0x0001687F, 0x78, 0x10),
            (0x00016880, 0x10, 0x00),
            (0x00016882, 0xA5, 0x19),
            (0x00016883, 0x24, 0xA7),
            (0x00016886, 0xE2, 0x19),
            (0x00016887, 0x7C, 0xAF),
            (0x00016888, 0xF6, 0x08),
            (0x00016889, 0xFF, 0x00),
            (0x0001688A, 0x80, 0xCE),
            (0x0001688B, 0x10, 0x25),
            (0x0001688C, 0x10, 0xF3),
            (0x0001688D, 0x00, 0xFF),
            (0x0001688E, 0xE7, 0xE0),
            (0x0001688F, 0x24, 0x15),
            (0x00016890, 0x08, 0xFF),
            (0x00016891, 0x00, 0xFF),
            (0x00016892, 0xC2, 0xEF),
            (0x00016893, 0x2C, 0x25),
            (0x00016894, 0x09, 0xEE),
            (0x00016895, 0x00, 0xFF),
            (0x00016896, 0x40, 0x8D),
            (0x00016897, 0x14, 0x15),
            (0x00016898, 0x2D, 0x04),
            (0x00016899, 0x18, 0x00),
            (0x0001689A, 0xE0, 0x8C),
            (0x0001689B, 0x00, 0x25),
            (0x0001689C, 0x00, 0x7A),
            (0x0001689D, 0x00, 0x11),
            (0x0001689E, 0xA3, 0x04),
            (0x0001689F, 0xDC, 0x08),
            (0x000168A0, 0xF8, 0x00),
            (0x000168A1, 0xFF, 0x00),
            (0x000168A2, 0xC6, 0x00),
            (0x000168A3, 0x24, 0x00),
            (0x000168A4, 0x08, 0x05),
            (0x000168A5, 0x00, 0x1E),
            (0x000168A6, 0xA5, 0xB4),
            (0x000168A7, 0x24, 0x08),
            (0x000168A8, 0x08, 0x0A),
            (0x000168A9, 0x00, 0x96),
            (0x000168AA, 0xC2, 0xC0),
            (0x000168AB, 0x2C, 0x08),
            (0x000168AE, 0xE3, 0x00),
            (0x000168AF, 0xFC, 0x00),
            (0x000168B0, 0xFA, 0x00),
            (0x000168B1, 0xFF, 0x00),
            (0x000168B2, 0x40, 0x00),
            (0x000168B3, 0x10, 0x00),
            (0x000168B4, 0x08, 0x00),
            (0x000168B6, 0xE7, 0x00),
            (0x000168B7, 0x24, 0x00),
            (0x000168B8, 0x2D, 0x00),
            (0x000168B9, 0x18, 0x00),
            (0x000168BA, 0xE0, 0x00),
            (0x000168BC, 0xFF, 0x20),
            (0x000168BD, 0xFF, 0x28),
            (0x000168BE, 0xC6, 0xE8),
            (0x000168BF, 0x24, 0x07),
            (0x000168C0, 0xFF, 0x21),
            (0x000168C1, 0xFF, 0xC8),
            (0x000168C2, 0x02, 0xC0),
            (0x000168C3, 0x24, 0x00),
            (0x000168C4, 0x08, 0x20),
            (0x000168C5, 0x00, 0x2A),
            (0x000168C6, 0xC2, 0xE8),
            (0x000168C7, 0x10, 0x07),
            (0x000168C8, 0x2D, 0xFF),
            (0x000168C9, 0x20, 0x28),
            (0x000168CA, 0x40, 0x04),
            (0x000168CB, 0x00, 0x08),
            (0x000168CC, 0x00, 0x20),
            (0x000168CD, 0x00, 0x2C),
            (0x000168CE, 0xA2, 0xE8),
            (0x000168CF, 0x90, 0x07),
            (0x000168D0, 0xFF, 0x70),
            (0x000168D1, 0xFF, 0x00),
            (0x000168D2, 0xC6, 0x03),
            (0x000168D4, 0x01, 0x20),
            (0x000168D5, 0x00, 0x2E),
            (0x000168D6, 0xA5, 0xE8),
            (0x000168D7, 0x24, 0x07),
            (0x000168D8, 0x00, 0x02),
            (0x000168DA, 0x62, 0x24),
            (0x000168DB, 0xA0, 0x87),
            (0x000168DC, 0x00, 0x20),
            (0x000168DD, 0x00, 0x30),
            (0x000168DE, 0x00, 0xE8),
            (0x000168DF, 0x00, 0x07),
            (0x000168E0, 0xFA, 0xFF),
            (0x000168E1, 0xFF, 0x9F),
            (0x000168E2, 0xC4, 0x85),
            (0x000168E3, 0x14, 0x30),
            (0x000168E4, 0x01, 0x20),
            (0x000168E5, 0x00, 0x32),
            (0x000168E6, 0x63, 0xE8),
            (0x000168E7, 0x24, 0x07),
            (0x000168E8, 0x08, 0x0E),
            (0x000168EA, 0xE0, 0x26),
            (0x000168EB, 0x03, 0x93),
            (0x000168EC, 0x2D, 0x00),
            (0x000168ED, 0x10, 0x00),
            (0x000168EF, 0x01, 0x00),
            (0x00018180, 0x2D, 0x21),
            (0x00018181, 0x40, 0x10),
            (0x00018184, 0x25, 0x07),
            (0x00018185, 0x38, 0x00),
            (0x00018186, 0xA4, 0xC0),
            (0x00018187, 0x00, 0x10),
            (0x00018188, 0x10, 0xFF),
            (0x00018189, 0x00, 0xFF),
            (0x0001818A, 0x0A, 0xC6),
            (0x0001818C, 0x07, 0x00),
            (0x0001818E, 0xE2, 0xA8),
            (0x0001818F, 0x30, 0x90),
            (0x00018190, 0x08, 0x01),
            (0x00018192, 0x09, 0xA9),
            (0x00018194, 0x54, 0x0B),
            (0x00018195, 0x00, 0x28),
            (0x00018196, 0x40, 0x28),
            (0x00018197, 0x14, 0x01),
            (0x00018198, 0x0F, 0x00),
            (0x0001819A, 0xE2, 0x88),
            (0x0001819B, 0x30, 0xA0),
            (0x0001819C, 0x0A, 0xFA),
            (0x0001819D, 0x48, 0xFF),
            (0x0001819E, 0x42, 0xC0),
            (0x0001819F, 0x01, 0x14),
            (0x000181A0, 0x2C, 0x01),
            (0x000181A2, 0x40, 0x84),
            (0x000181A3, 0x14, 0x24),
            (0x000181A4, 0x2B, 0x08),
            (0x000181A5, 0x10, 0x00),
            (0x000181A6, 0xC9, 0xE0),
            (0x000181A7, 0x00, 0x03),
            (0x000181A8, 0x4F, 0x00),
            (0x000181AA, 0x40, 0x00),
            (0x000181AB, 0x14, 0x00),
            (0x000181AC, 0x00, 0x24),
            (0x000181AD, 0x00, 0x34),
            (0x000181AE, 0x00, 0xE8),
            (0x000181AF, 0x00, 0x07),
            (0x000181B0, 0x01, 0x0D),
            (0x000181B1, 0x01, 0x00),
            (0x000181B2, 0x07, 0x27),
            (0x000181B3, 0x3C, 0x93),
            (0x000181B4, 0x01, 0x24),
            (0x000181B5, 0x01, 0x36),
            (0x000181B6, 0xE7, 0xE8),
            (0x000181B7, 0x34, 0x07),
            (0x000181B8, 0x38, 0x00),
            (0x000181B9, 0x3C, 0x40),
            (0x000181BA, 0x07, 0x81),
            (0x000181BB, 0x00, 0x30),
            (0x000181BC, 0x01, 0x24),
            (0x000181BD, 0x01, 0x38),
            (0x000181BE, 0xE7, 0xE8),
            (0x000181BF, 0x34, 0x07),
            (0x000181C0, 0x38, 0x00),
            (0x000181C1, 0x3C, 0x20),
            (0x000181C2, 0x07, 0xA3),
            (0x000181C3, 0x00, 0x34),
            (0x000181C4, 0x01, 0x24),
            (0x000181C5, 0x01, 0x3A),
            (0x000181C6, 0xE7, 0xE8),
            (0x000181C7, 0x34, 0x07),
            (0x000181C8, 0x00, 0x0B),
            (0x000181C9, 0x00, 0x28),
            (0x000181CA, 0xA3, 0x61),
            (0x000181CB, 0x78, 0x00),
            (0x000181CC, 0x89, 0x24),
            (0x000181CD, 0x4B, 0x3C),
            (0x000181CE, 0xE7, 0xE8),
            (0x000181CF, 0x70, 0x07),
            (0x000181D0, 0xE9, 0x0D),
            (0x000181D1, 0x1C, 0x00),
            (0x000181D2, 0x03, 0x26),
            (0x000181D3, 0x70, 0xA3),
            (0x000181D4, 0x80, 0x24),
            (0x000181D5, 0x80, 0x3E),
            (0x000181D6, 0x07, 0xE8),
            (0x000181D7, 0x3C, 0x07),
            (0x000181D8, 0x80, 0x00),
            (0x000181D9, 0x80, 0x20),
            (0x000181DA, 0xE7, 0x81),
            (0x000181DB, 0x34, 0x30),
            (0x000181DC, 0x38, 0x24),
            (0x000181DD, 0x3C, 0x40),
            (0x000181DE, 0x07, 0xE8),
            (0x000181DF, 0x00, 0x07),
            (0x000181E0, 0x80, 0x00),
            (0x000181E1, 0x80, 0x40),
            (0x000181E2, 0xE7, 0xA3),
            (0x000181E4, 0x38, 0x24),
            (0x000181E5, 0x3C, 0x42),
            (0x000181E6, 0x07, 0xE8),
            (0x000181E7, 0x00, 0x07),
            (0x000181E8, 0x80, 0x0B),
            (0x000181E9, 0x80, 0x28),
            (0x000181EA, 0xE7, 0x61),
            (0x000181EB, 0x34, 0x00),
            (0x000181EC, 0x48, 0x24),
            (0x000181ED, 0x12, 0x44),
            (0x000181EE, 0x69, 0xE8),
            (0x000181EF, 0x70, 0x07),
            (0x000181F0, 0x89, 0x0E),
            (0x000181F1, 0x53, 0x00),
            (0x000181F2, 0xE7, 0x27),
            (0x000181F3, 0x70, 0xA3),
            (0x000181F4, 0x89, 0x24),
            (0x000181F5, 0x14, 0x46),
            (0x000181F6, 0x43, 0xE8),
            (0x000181F7, 0x70, 0x07),
            (0x000181F8, 0x89, 0x08),
            (0x000181F9, 0x14, 0x00),
            (0x000181FA, 0x4A, 0xE0),
            (0x000181FB, 0x70, 0x03),
            (0x000181FC, 0xA9, 0x24),
            (0x000181FD, 0x1B, 0x48),
            (0x000181FE, 0x44, 0xE8),
            (0x000181FF, 0x70, 0x07),
            (0x00018200, 0x25, 0x02),
            (0x00018201, 0x18, 0x00),
            (0x00018202, 0x43, 0x25),
            (0x00018203, 0x00, 0xA7),
            (0x00018204, 0x37, 0x00),
            (0x00018206, 0x60, 0x00),
            (0x00018207, 0x14, 0x00),
            (0x00018208, 0x2D, 0x00),
            (0x00018209, 0x38, 0x00),
            (0x0001820B, 0x01, 0x00),
            (0x0001820E, 0xA3, 0x00),
            (0x0001820F, 0x78, 0x00),
            (0x00018210, 0xF0, 0x00),
            (0x00018211, 0xFF, 0x00),
            (0x00018212, 0xC6, 0x00),
            (0x00018213, 0x24, 0x00),
            (0x00018214, 0x10, 0x00),
            (0x00018216, 0xA5, 0x00),
            (0x00018217, 0x24, 0x00),
            (0x00018218, 0x10, 0x00),
            (0x0001821A, 0xC2, 0x00),
            (0x0001821B, 0x2C, 0x00),
            (0x0001821E, 0xE3, 0x00),
            (0x0001821F, 0x7C, 0x00),
            (0x00018220, 0x30, 0x00),
            (0x00018222, 0x40, 0x00),
            (0x00018223, 0x14, 0x00),
            (0x00018224, 0x10, 0x00),
            (0x00018226, 0xE7, 0x00),
            (0x00018227, 0x24, 0x00),
            (0x0001822A, 0xA2, 0x00),
            (0x0001822B, 0x78, 0x00),
            (0x0001822C, 0xE9, 0x00),
            (0x0001822D, 0x1C, 0x00),
            (0x0001822E, 0x02, 0x00),
            (0x0001822F, 0x70, 0x00),
            (0x00018230, 0x48, 0x00),
            (0x00018231, 0x12, 0x00),
            (0x00018232, 0x49, 0x00),
            (0x00018233, 0x70, 0x00),
            (0x00018234, 0x89, 0x00),
            (0x00018235, 0x14, 0x00),
            (0x00018236, 0x43, 0x00),
            (0x00018237, 0x70, 0x00),
            (0x00018238, 0x89, 0x00),
            (0x00018239, 0x14, 0x00),
            (0x0001823A, 0x4A, 0x00),
            (0x0001823B, 0x70, 0x00),
            (0x0001823C, 0xA9, 0x00),
            (0x0001823D, 0x1B, 0x00),
            (0x0001823E, 0x44, 0x00),
            (0x0001823F, 0x70, 0x00),
            (0x00018240, 0x25, 0x00),
            (0x00018241, 0x10, 0x00),
            (0x00018242, 0x43, 0x00),
            (0x00018244, 0x1A, 0x00),
            (0x00018246, 0x40, 0x00),
            (0x00018247, 0x50, 0x00),
            (0x0001824A, 0xA3, 0x00),
            (0x0001824B, 0x78, 0x00),
            (0x0001824C, 0x26, 0x00),
            (0x0001824F, 0x10, 0x00),
            (0x00018250, 0x2D, 0x00),
            (0x00018251, 0x20, 0x00),
            (0x00018252, 0xE0, 0x00),
            (0x00018254, 0x24, 0x00),
            (0x00018256, 0x40, 0x00),
            (0x00018257, 0x14, 0x00),
            (0x0001825E, 0xA3, 0x00),
            (0x0001825F, 0xDC, 0x00),
            (0x00018260, 0x01, 0x00),
            (0x00018261, 0x01, 0x00),
            (0x00018262, 0x09, 0x00),
            (0x00018263, 0x3C, 0x00),
            (0x00018264, 0x01, 0x00),
            (0x00018265, 0x01, 0x00),
            (0x00018266, 0x29, 0x00),
            (0x00018267, 0x35, 0x00),
            (0x00018268, 0x38, 0x00),
            (0x00018269, 0x4C, 0x00),
            (0x0001826A, 0x09, 0x00),
            (0x0001826C, 0x01, 0x00),
            (0x0001826D, 0x01, 0x00),
            (0x0001826E, 0x29, 0x00),
            (0x0001826F, 0x35, 0x00),
            (0x00018270, 0x38, 0x00),
            (0x00018271, 0x4C, 0x00),
            (0x00018272, 0x09, 0x00),
            (0x00018274, 0x01, 0x00),
            (0x00018275, 0x01, 0x00),
            (0x00018276, 0x29, 0x00),
            (0x00018277, 0x35, 0x00),
            (0x00018278, 0x80, 0x00),
            (0x00018279, 0x80, 0x00),
            (0x0001827A, 0x0A, 0x00),
            (0x0001827B, 0x3C, 0x00),
            (0x0001827C, 0x80, 0x00),
            (0x0001827D, 0x80, 0x00),
            (0x0001827E, 0x4A, 0x00),
            (0x0001827F, 0x35, 0x00),
            (0x00018280, 0x38, 0x00),
            (0x00018281, 0x54, 0x00),
            (0x00018282, 0x0A, 0x00),
            (0x00018284, 0x80, 0x00),
            (0x00018285, 0x80, 0x00),
            (0x00018286, 0x4A, 0x00),
            (0x00018287, 0x35, 0x00),
            (0x00018288, 0x38, 0x00),
            (0x00018289, 0x54, 0x00),
            (0x0001828A, 0x0A, 0x00),
            (0x0001828C, 0x80, 0x00),
            (0x0001828D, 0x80, 0x00),
            (0x0001828E, 0x4A, 0x00),
            (0x0001828F, 0x35, 0x00),
            (0x00018290, 0x2F, 0x00),
            (0x00018291, 0x10, 0x00),
            (0x00018292, 0x69, 0x00),
            (0x00018294, 0x27, 0x00),
            (0x00018295, 0x18, 0x00),
            (0x00018296, 0x03, 0x00),
            (0x00018298, 0x24, 0x00),
            (0x00018299, 0x10, 0x00),
            (0x0001829A, 0x43, 0x00),
            (0x0001829C, 0x24, 0x00),
            (0x0001829D, 0x10, 0x00),
            (0x0001829E, 0x4A, 0x00),
            (0x000182A0, 0x10, 0x00),
            (0x000182A2, 0x40, 0x00),
            (0x000182A3, 0x14, 0x00),
            (0x000182A4, 0x2D, 0x00),
            (0x000182A5, 0x38, 0x00),
            (0x000182A7, 0x01, 0x00),
            (0x000182AA, 0xA3, 0x00),
            (0x000182AB, 0xDC, 0x00),
            (0x000182B0, 0xF8, 0x00),
            (0x000182B1, 0xFF, 0x00),
            (0x000182B2, 0xC6, 0x00),
            (0x000182B3, 0x24, 0x00),
            (0x000182B4, 0x08, 0x00),
            (0x000182B6, 0xA5, 0x00),
            (0x000182B7, 0x24, 0x00),
            (0x000182B8, 0x08, 0x00),
            (0x000182BA, 0xC2, 0x00),
            (0x000182BB, 0x2C, 0x00),
            (0x000182BE, 0xE3, 0x00),
            (0x000182BF, 0xFC, 0x00),
            (0x000182C0, 0x08, 0x00),
            (0x000182C2, 0x40, 0x00),
            (0x000182C3, 0x14, 0x00),
            (0x000182C4, 0x08, 0x00),
            (0x000182C6, 0xE7, 0x00),
            (0x000182C7, 0x24, 0x00),
            (0x000182CA, 0xA2, 0x00),
            (0x000182CB, 0xDC, 0x00),
            (0x000182CC, 0x27, 0x00),
            (0x000182CD, 0x18, 0x00),
            (0x000182CE, 0x02, 0x00),
            (0x000182D0, 0x2F, 0x00),
            (0x000182D1, 0x10, 0x00),
            (0x000182D2, 0x49, 0x00),
            (0x000182D4, 0x24, 0x00),
            (0x000182D5, 0x10, 0x00),
            (0x000182D6, 0x43, 0x00),
            (0x000182D8, 0x24, 0x00),
            (0x000182D9, 0x10, 0x00),
            (0x000182DA, 0x4A, 0x00),
            (0x000182DC, 0xF4, 0x00),
            (0x000182DD, 0xFF, 0x00),
            (0x000182DE, 0x40, 0x00),
            (0x000182DF, 0x50, 0x00),
            (0x000182E2, 0xA3, 0x00),
            (0x000182E3, 0xDC, 0x00),
            (0x000182E4, 0x2D, 0x00),
            (0x000182E5, 0x20, 0x00),
            (0x000182E6, 0xE0, 0x00),
            (0x000182E8, 0x12, 0x00),
            (0x000182EA, 0xC0, 0x00),
            (0x000182EB, 0x10, 0x00),
            (0x000182EC, 0x2D, 0x00),
            (0x000182ED, 0x10, 0x00),
            (0x000182EE, 0xC0, 0x00),
            (0x000182F2, 0xA2, 0x00),
            (0x000182F3, 0x90, 0x00),
            (0x000182F4, 0xFF, 0x00),
            (0x000182F5, 0xFF, 0x00),
            (0x000182F6, 0xC6, 0x00),
            (0x000182F7, 0x24, 0x00),
            (0x000182F8, 0x01, 0x00),
            (0x000182FA, 0xA5, 0x00),
            (0x000182FB, 0x24, 0x00),
            (0x000182FE, 0x82, 0x00),
            (0x000182FF, 0xA0, 0x00),
            (0x00018301, 0x16, 0x00),
            (0x00018302, 0x02, 0x00),
            (0x00018304, 0xF8, 0x00),
            (0x00018305, 0xFF, 0x00),
            (0x00018306, 0x40, 0x00),
            (0x00018307, 0x14, 0x00),
            (0x00018308, 0x01, 0x00),
            (0x0001830A, 0x84, 0x00),
            (0x0001830B, 0x24, 0x00),
            (0x0001830C, 0x2D, 0x00),
            (0x0001830D, 0x10, 0x00),
            (0x0001830E, 0xC0, 0x00),
            (0x00018310, 0x08, 0x00),
            (0x00018312, 0x40, 0x00),
            (0x00018313, 0x10, 0x00),
            (0x00018314, 0xFF, 0x00),
            (0x00018315, 0xFF, 0x00),
            (0x00018316, 0xC6, 0x00),
            (0x00018317, 0x24, 0x00),
            (0x0001831A, 0x80, 0x00),
            (0x0001831B, 0xA0, 0x00),
            (0x0001831C, 0x2D, 0x00),
            (0x0001831D, 0x10, 0x00),
            (0x0001831E, 0xC0, 0x00),
            (0x00018320, 0x01, 0x00),
            (0x00018322, 0x84, 0x00),
            (0x00018323, 0x24, 0x00),
            (0x0001832C, 0xFA, 0x00),
            (0x0001832D, 0xFF, 0x00),
            (0x0001832E, 0x40, 0x00),
            (0x0001832F, 0x14, 0x00),
            (0x00018330, 0xFF, 0x00),
            (0x00018331, 0xFF, 0x00),
            (0x00018332, 0xC6, 0x00),
            (0x00018333, 0x24, 0x00),
            (0x00018334, 0x08, 0x00),
            (0x00018336, 0xE0, 0x00),
            (0x00018337, 0x03, 0x00),
            (0x00018338, 0x2D, 0x00),
            (0x00018339, 0x10, 0x00),
            (0x0001833B, 0x01, 0x00),
        };

        // ───────────────────────────────────────
        // Detection: check first patch byte
        // ───────────────────────────────────────
        const int DETECT_OFF = 0x000047C8;
        const byte DETECT_ORIG = 0x7A;
        const byte DETECT_PATCH = 0xF1;

        // ═══════════════════════════════════════
        // PUBLIC ENTRY POINT
        // Accepts ISO, BIN, IMG, RAW
        // or standalone ELF file
        // ═══════════════════════════════════════
        public static void Run(string path)
        {
            if (!File.Exists(path))
            {
                TextOut.PrintError(
                    "File not found: " +
                    path);
                return;
            }

            string name = Path
                .GetFileName(path)
                .ToUpper();

            bool isElf =
                name == "SLUS_202.51" ||
                name == "SLPS_201.04" ||
                name == "SLPM_601.47";

            if (isElf)
                PatchElf(path, name);
            else
                PatchImage(path);
        }

        // ═══════════════════════════════════════
        // PATCH STANDALONE ELF FILE
        // ═══════════════════════════════════════
        static void PatchElf(
            string path, string name)
        {
            GameVersion gv =
                DetectVersionFromName(name);

            byte[] elf =
                File.ReadAllBytes(path);

            if (DETECT_OFF >=
                elf.Length)
            {
                TextOut.PrintError(
                    "ELF file too small!");
                return;
            }

            bool applied =
                elf[DETECT_OFF] ==
                DETECT_PATCH;

            PrintBanner(gv, applied);

            ApplyToggleDirect(
                elf, 0, applied);

            File.WriteAllBytes(path, elf);
            PrintSaved(path);
        }

        // ═══════════════════════════════════════
        // PATCH ISO / BIN / IMG / RAW
        // Auto-detects sector format
        // ═══════════════════════════════════════
        static void PatchImage(string path)
        {
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "Scanning image for " +
                "HMSTH ELF...");
            Console.ResetColor();

            byte[] img =
                File.ReadAllBytes(path);

            // ─── Detect format ──────────────
            int sectorSize =
                DetectSectorSize(img);
            int dataOff =
                GetSectorDataOffset(
                    img, sectorSize);

            string fmt = sectorSize == 2048
                ? "ISO (2048 bytes/sector)"
                : $"BIN/RAW " +
                  $"({sectorSize} " +
                  $"bytes/sector, " +
                  $"data at +{dataOff})";

            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                $"  Format: {fmt}");
            Console.ResetColor();

            // ─── Find main ELF ──────────────
            int elfOff = FindMainElf(
                img, sectorSize, dataOff);

            if (elfOff < 0)
            {
                TextOut.PrintError(
                    "HMSTH main ELF " +
                    "not found in image!");
                return;
            }

            // ─── Detect version ─────────────
            string elfName;
            GameVersion gv =
                DetectVersionFromImage(
                    img,
                    sectorSize,
                    dataOff,
                    out elfName);

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                $"  Found {elfName} " +
                $"at offset " +
                $"0x{elfOff:X}");
            Console.ResetColor();

            // ─── Check state ────────────────
            // For BIN: use sector-aware check
            bool applied;
            if (sectorSize == 2048)
            {
                if (elfOff + DETECT_OFF >=
                    img.Length)
                {
                    TextOut.PrintError(
                        "ELF detection " +
                        "offset out of range!");
                    return;
                }
                applied =
                    img[elfOff + DETECT_OFF] ==
                    DETECT_PATCH;
            }
            else
            {
                // BIN: compute sector-aware
                // absolute offset for detection
                int elfSectorIdxD =
                    (elfOff - dataOff) /
                    sectorSize;
                int chunkIdxD =
                    DETECT_OFF / 2048;
                int byteInChunkD =
                    DETECT_OFF % 2048;
                int absSectorD =
                    elfSectorIdxD + chunkIdxD;
                int absOffD =
                    absSectorD * sectorSize +
                    dataOff + byteInChunkD;
                if (absOffD < 0 ||
                    absOffD >= img.Length)
                {
                    TextOut.PrintError(
                        "ELF detection " +
                        "offset out of range!");
                    return;
                }
                applied =
                    img[absOffD] == DETECT_PATCH;
            }

            PrintBanner(gv, applied);

            // ─── Apply / Revert ─────────────
            if (sectorSize == 2048)
            {
                ApplyToggleDirect(
                    img, elfOff, applied);
            }
            else
            {
                ApplyToggleBin(
                    img,
                    elfOff,
                    sectorSize,
                    dataOff,
                    applied);
            }

            // ─── Save ───────────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "Writing image...");
            Console.ResetColor();

            File.WriteAllBytes(path, img);
            PrintSaved(path);

            // ─── Auto-fix after patch ───────────
            // Always fix logo + LBA
            // Only fix ISO structure if .iso/.img
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                " Running auto-fix...");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();

            string remapExt = Path
                .GetExtension(path)
                .ToLower();
            bool remapIsIso =
                remapExt == ".iso" ||
                remapExt == ".img";

            string realPath =
                HarvestIso.GetRealPath(path);

            // Step 1: Fix ISO structure
            // Only for .iso / .img
            if (remapIsIso)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "[1/3] Repairing " +
                    "ISO structure...");
                Console.ResetColor();
                try
                {
                    IsoRepair.FixIso(realPath);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  Step 1 warning: " +
                        ex.Message);
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor =
                    ConsoleColor.DarkGray;
                Console.WriteLine(
                    "[1/3] Skipping ISO " +
                    "structure repair " +
                    "(not a .iso file).");
                Console.ResetColor();
            }

            // Step 2: Fix PS2 logo
            // Always runs
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[2/3] Fixing PS2 logo" +
                " + Master Disc " +
                "markers...");
            Console.ResetColor();
            try
            {
                IsoLogoPatcher.PatchIso(
                    realPath, null, gv);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Step 2 warning: " +
                    ex.Message);
                Console.ResetColor();
            }

            // Step 3: Fix LBA table
            // Always runs
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "[3/3] Fixing LBA " +
                "table...");
            Console.ResetColor();
            try
            {
                int changes =
                    HarvestIso.FixLba(
                        realPath, gv);
                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    changes == 0
                    ? "  LBA already correct."
                    : $"  Patched {changes}" +
                      " LBA entries.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Step 3 warning: " +
                    ex.Message);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                " ALL DONE! Image is" +
                " ready to play.");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═══════════════════════════════════════
        // APPLY / REVERT - DIRECT (ISO / ELF)
        // ELF bytes are contiguous in buffer
        // baseOff = start of ELF in buffer
        // ═══════════════════════════════════════
        static void ApplyToggleDirect(
            byte[] data,
            int baseOff,
            bool revert)
        {
            int count = 0;
            foreach (var p in PATCH_TABLE)
            {
                int off = baseOff + p.offset;
                if (off < 0 ||
                    off >= data.Length)
                    continue;
                data[off] = revert
                    ? p.orig
                    : p.patch;
                count++;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                revert
                ? $"  [OK] Reverted " +
                  $"{count} bytes " +
                  "to original controls."
                : $"  [OK] Applied " +
                  $"{count} bytes " +
                  "for remapped controls.");
            Console.ResetColor();
        }

        // ═══════════════════════════════════════
        // APPLY / REVERT - BIN/RAW
        // ELF bytes are split across sectors
        // Each sector = sectorSize bytes total
        // Data starts at dataOff within sector
        // ═══════════════════════════════════════
        static void ApplyToggleBin(
            byte[] data,
            int elfDataStart,
            int sectorSize,
            int dataOff,
            bool revert)
        {
            // elfDataStart = absolute offset
            // in data[] where ELF data begins
            // (already points past sync header)

            // Find which sector this is
            int elfSectorIdx =
                (elfDataStart - dataOff) /
                sectorSize;

            int count = 0;
            foreach (var p in PATCH_TABLE)
            {
                // ELF file offset of this byte
                int elfByteOff = p.offset;

                // Which 2048-byte chunk
                int chunkIdx =
                    elfByteOff / 2048;

                // Byte offset within chunk
                int byteInChunk =
                    elfByteOff % 2048;

                // Absolute sector in image
                int absSector =
                    elfSectorIdx + chunkIdx;

                // Absolute offset in image
                int absOff =
                    absSector * sectorSize +
                    dataOff +
                    byteInChunk;

                if (absOff < 0 ||
                    absOff >= data.Length)
                    continue;

                data[absOff] = revert
                    ? p.orig
                    : p.patch;
                count++;
            }

            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                revert
                ? $"  [OK] Reverted " +
                  $"{count} bytes " +
                  "to original controls."
                : $"  [OK] Applied " +
                  $"{count} bytes " +
                  "for remapped controls.");
            Console.ResetColor();
        }

        // ═══════════════════════════════════════
        // FIND MAIN ELF IN IMAGE
        // Looks for ELF with entry 0x00100008
        // Returns absolute data offset of ELF
        // ═══════════════════════════════════════
        static int FindMainElf(
            byte[] data,
            int sectorSize,
            int dataOff)
        {
            int numSectors =
                data.Length / sectorSize;

            for (int s = 0;
                 s < numSectors; s++)
            {
                int off = s * sectorSize
                    + dataOff;

                if (off + 0x20 >
                    data.Length)
                    break;

                // Check ELF magic
                if (data[off] != 0x7F ||
                    data[off + 1] != (byte)'E' ||
                    data[off + 2] != (byte)'L' ||
                    data[off + 3] != (byte)'F')
                    continue;

                // Check entry point
                uint entry = (uint)(
                    data[off + 0x18] |
                    (data[off + 0x19] << 8) |
                    (data[off + 0x1A] << 16) |
                    (data[off + 0x1B] << 24));

                if (entry == 0x00100008u)
                    return off;
            }
            return -1;
        }

        // ═══════════════════════════════════════
        // DETECT SECTOR SIZE
        // Check sync header for BIN/RAW
        // ═══════════════════════════════════════
        static int DetectSectorSize(
            byte[] data)
        {
            // BIN/RAW sync header:
            // 00 FF FF FF FF FF FF FF
            // FF FF FF 00 ...
            if (data.Length >= 16 &&
                data[0] == 0x00 &&
                data[1] == 0xFF &&
                data[2] == 0xFF &&
                data[3] == 0xFF &&
                data[4] == 0xFF &&
                data[5] == 0xFF &&
                data[6] == 0xFF &&
                data[7] == 0xFF &&
                data[8] == 0xFF &&
                data[9] == 0xFF &&
                data[10] == 0xFF &&
                data[11] == 0x00)
            {
                return 2352;
            }
            return 2048;
        }

        // ═══════════════════════════════════════
        // GET DATA OFFSET WITHIN SECTOR
        // ISO=0, BIN Mode1=16, BIN Mode2=24
        // ═══════════════════════════════════════
        static int GetSectorDataOffset(
            byte[] data, int sectorSize)
        {
            if (sectorSize == 2048)
                return 0;

            // Check mode byte at offset 15
            if (data.Length > 15)
            {
                byte mode = data[15];
                // Mode 2 Form 1/2
                if (mode == 0x02)
                    return 24;
                // Mode 1
                return 16;
            }
            return 16;
        }

        // ═══════════════════════════════════════
        // DETECT VERSION FROM IMAGE DIRECTORY
        // ═══════════════════════════════════════
        static GameVersion
            DetectVersionFromImage(
            byte[] data,
            int sectorSize,
            int dataOff,
            out string elfName)
        {
            byte[] demo = System.Text
                .Encoding.ASCII
                .GetBytes("SLPM_601.47");
            byte[] jap = System.Text
                .Encoding.ASCII
                .GetBytes("SLPS_201.04");
            byte[] usa = System.Text
                .Encoding.ASCII
                .GetBytes("SLUS_202.51");

            // Check root directories
            // CD sector 22, DVD sector 261
            // Also check path tables
            // CD sector 18, DVD sector 257
            int[] scanLbas = new int[]
                { 22, 261, 18, 257 };

            foreach (int lba in scanLbas)
            {
                // Convert LBA to absolute
                // offset (works for both
                // ISO and BIN)
                int secStart =
                    lba * sectorSize + dataOff;

                if (secStart < 0 ||
                    secStart + 2048 >
                    data.Length)
                    continue;

                int secEnd = secStart + 2048;

                for (int i = secStart;
                     i < secEnd - 11; i++)
                {
                    if (Match(data, i, demo))
                    {
                        elfName =
                            "SLPM_601.47";
                        return
                            GameVersion.DEMO;
                    }
                    if (Match(data, i, jap))
                    {
                        elfName =
                            "SLPS_201.04";
                        return
                            GameVersion.JAP;
                    }
                    if (Match(data, i, usa))
                    {
                        elfName =
                            "SLUS_202.51";
                        return
                            GameVersion.USA;
                    }
                }
            }

            // Fallback: search first
            // 500 sectors
            int limitSec = Math.Min(
                500,
                data.Length / sectorSize);

            for (int s = 0;
                 s < limitSec; s++)
            {
                int secStart =
                    s * sectorSize + dataOff;
                if (secStart + 2048 >
                    data.Length)
                    break;
                int secEnd = secStart + 2048;

                for (int i = secStart;
                     i < secEnd - 11; i++)
                {
                    if (Match(data, i, demo))
                    {
                        elfName =
                            "SLPM_601.47";
                        return
                            GameVersion.DEMO;
                    }
                    if (Match(data, i, jap))
                    {
                        elfName =
                            "SLPS_201.04";
                        return
                            GameVersion.JAP;
                    }
                    if (Match(data, i, usa))
                    {
                        elfName =
                            "SLUS_202.51";
                        return
                            GameVersion.USA;
                    }
                }
            }

            elfName = "SLUS_202.51";
            return GameVersion.USA;
        }

        // ═══════════════════════════════════════
        // DETECT VERSION FROM ELF FILENAME
        // ═══════════════════════════════════════
        static GameVersion
            DetectVersionFromName(
            string name)
        {
            string n = name.ToUpper();
            if (n.Contains("SLPM"))
                return GameVersion.DEMO;
            if (n.Contains("SLPS"))
                return GameVersion.JAP;
            return GameVersion.USA;
        }

        // ═══════════════════════════════════════
        // BYTE PATTERN MATCH HELPER
        // ═══════════════════════════════════════
        static bool Match(
            byte[] data, int off,
            byte[] pattern)
        {
            if (off + pattern.Length >
                data.Length)
                return false;
            for (int i = 0;
                 i < pattern.Length; i++)
                if (data[off + i] !=
                    pattern[i])
                    return false;
            return true;
        }

        // ═══════════════════════════════════════
        // CONSOLE OUTPUT HELPERS
        // ═══════════════════════════════════════
        static void PrintBanner(
            GameVersion gv, bool applied)
        {
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.WriteLine(
                " HMSTH Controls " +
                "Remapper");
            Console.WriteLine(
                "═══════════════════" +
                "═══════════════════");
            Console.ResetColor();

            string ver;
            switch (gv)
            {
                case GameVersion.DEMO:
                    ver = "JAP DEMO " +
                          "(SLPM_601.47)";
                    break;
                case GameVersion.JAP:
                    ver = "Japanese " +
                          "(SLPS_201.04)";
                    break;
                default:
                    ver = "USA " +
                          "(SLUS_202.51)";
                    break;
            }

            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                $"  Version: {ver}");
            Console.WriteLine(
                "  State:   " +
                (applied
                    ? "REMAPPED controls"
                    : "ORIGINAL controls"));
            Console.WriteLine(
                "  Action:  " +
                (applied
                    ? "Reverting to " +
                      "original controls"
                    : "Applying remap " +
                      "patch"));
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintSaved(string path)
        {
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Saved: " + path);
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
