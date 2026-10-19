using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles the LBA table
    ///     recalculation on the ELF
    ///     from Harvest Moon.
    /// </summary>
    class HarvestElf
    {
        // USA: SLUS_202.51
        const uint LBATableStart_USA =
            0x162460;
        const uint LBATableEnd_USA =
            0x162D30;

        // JAP: SLPS_201.04
        const uint LBATableStart_JAP =
            0x162360;
        const uint LBATableEnd_JAP =
            0x162C30;

        // JAP DEMO: SLPM_601.47
        const uint LBATableStart_DEMO =
            0x1633E0;
        const uint LBATableEnd_DEMO =
            0x163CB0;

        const int SectorSize = 0x930;
        const int BytesPerSector = 0x800;

        /// <summary>
        ///     Fixes the LBA table on
        ///     the main ELF executable
        ///     (USA version).
        /// </summary>
        public static void Fix(
            string Elf,
            uint LBA,
            uint NewSize)
        {
            Fix(Elf, LBA, NewSize,
                GameVersion.USA);
        }

        /// <summary>
        ///     Fixes the LBA table
        ///     (legacy bool overload).
        /// </summary>
        public static void Fix(
            string Elf,
            uint LBA,
            uint NewSize,
            bool isJap)
        {
            Fix(Elf, LBA, NewSize,
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        /// <summary>
        ///     Fixes the LBA table on
        ///     the main ELF executable.
        /// </summary>
        public static void Fix(
            string Elf,
            uint LBA,
            uint NewSize,
            GameVersion version)
        {
            using (FileStream Input =
                new FileStream(
                    Elf, FileMode.Open))
            {
                Fix(Input, LBA, NewSize,
                    version);
            }
        }

        /// <summary>
        ///     Fixes the LBA table on
        ///     the main ELF executable
        ///     (USA version, stream).
        /// </summary>
        public static void Fix(
            Stream Elf,
            uint LBA,
            uint NewSize)
        {
            Fix(Elf, LBA, NewSize,
                GameVersion.USA);
        }

        /// <summary>
        ///     Fixes the LBA table
        ///     (legacy bool overload,
        ///     stream).
        /// </summary>
        public static void Fix(
            Stream Elf,
            uint LBA,
            uint NewSize,
            bool isJap)
        {
            Fix(Elf, LBA, NewSize,
                isJap
                    ? GameVersion.JAP
                    : GameVersion.USA);
        }

        /// <summary>
        ///     Fixes the LBA table on
        ///     the main ELF executable.
        /// </summary>
        public static void Fix(
            Stream Elf,
            uint LBA,
            uint NewSize,
            GameVersion version)
        {
            uint tableStart;
            uint tableEnd;
            string versionName;

            switch (version)
            {
                case GameVersion.JAP:
                    tableStart =
                        LBATableStart_JAP;
                    tableEnd =
                        LBATableEnd_JAP;
                    versionName =
                        "JAP (SLPS_201.04)";
                    break;
                case GameVersion.DEMO:
                    tableStart =
                        LBATableStart_DEMO;
                    tableEnd =
                        LBATableEnd_DEMO;
                    versionName =
                        "JAP DEMO" +
                        " (SLPM_601.47)";
                    break;
                default:
                    tableStart =
                        LBATableStart_USA;
                    tableEnd =
                        LBATableEnd_USA;
                    versionName =
                        "USA (SLUS_202.51)";
                    break;
            }

            TextOut.Print(
                $"Version:" +
                $" {versionName}");
            TextOut.Print(
                "LBA table range:" +
                $" 0x{tableStart:X6}" +
                $" - 0x{tableEnd:X6}");

            BinaryReader Reader =
                new BinaryReader(Elf);
            BinaryWriter Writer =
                new BinaryWriter(Elf);

            int Difference = 0;
            bool Found = false;
            Elf.Seek(tableStart,
                SeekOrigin.Begin);
            while (Elf.Position <
                   tableEnd)
            {
                uint LBAStart =
                    Reader.ReadUInt32();
                uint LBAEnd =
                    Reader.ReadUInt32();

                Elf.Seek(-8,
                    SeekOrigin.Current);
                Writer.Write(
                    (uint)(LBAStart +
                           Difference));
                Writer.Write(
                    (uint)(LBAEnd +
                           Difference));

                if (LBAStart == LBA)
                {
                    Found = true;
                    uint Size =
                        NewSize /
                        BytesPerSector;
                    if ((NewSize %
                         BytesPerSector)
                        != 0)
                        Size++;

                    Elf.Seek(-4,
                        SeekOrigin
                            .Current);
                    uint NewEnd =
                        (LBAStart +
                         Size) - 1;
                    Writer.Write(
                        NewEnd);
                    Difference =
                        (int)(NewEnd -
                              LBAEnd);
                }
            }

            if (!Found)
            {
                TextOut.PrintWarning(
                    "The LBA you" +
                    " entered was not" +
                    " found on the" +
                    " table!");
                TextOut.Print(
                    "Make sure you" +
                    " typed it in" +
                    " DECIMAL format.");
            }
            else
                TextOut.PrintSuccess(
                    "LBA found and" +
                    " values patched" +
                    " successfully!");
        }
    }
}
