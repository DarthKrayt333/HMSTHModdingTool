using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles the LBA table recalculation on the ELF from Harvest Moon.
    /// </summary>
    class HarvestElf
    {
        // USA: SLUS_202.51
        const uint LBATableStart_USA = 0x162460;
        const uint LBATableEnd_USA = 0x162D30;

        // JAP: SLPS_201.04
        const uint LBATableStart_JAP = 0x162360;
        const uint LBATableEnd_JAP = 0x162C30;

        const int SectorSize = 0x930;
        const int BytesPerSector = 0x800;

        /// <summary>
        ///     Fixes the LBA table on the main ELF executable (USA version).
        /// </summary>
        public static void Fix(string Elf, uint LBA, uint NewSize)
        {
            Fix(Elf, LBA, NewSize, false);
        }

        /// <summary>
        ///     Fixes the LBA table on the main ELF executable.
        /// </summary>
        /// <param name="Elf">The full path to the ELF file</param>
        /// <param name="LBA">The LBA of the modified file</param>
        /// <param name="NewSize">The new size of the file</param>
        /// <param name="isJap">True for Japanese version (SLPS_201.04)</param>
        public static void Fix(string Elf, uint LBA, uint NewSize, bool isJap)
        {
            using (FileStream Input = new FileStream(Elf, FileMode.Open))
            {
                Fix(Input, LBA, NewSize, isJap);
            }
        }

        /// <summary>
        ///     Fixes the LBA table on the main ELF executable (USA version).
        /// </summary>
        public static void Fix(Stream Elf, uint LBA, uint NewSize)
        {
            Fix(Elf, LBA, NewSize, false);
        }

        /// <summary>
        ///     Fixes the LBA table on the main ELF executable.
        /// </summary>
        /// <param name="Elf">The Stream with the ELF data</param>
        /// <param name="LBA">The LBA of the modified file</param>
        /// <param name="NewSize">The new size of the file</param>
        /// <param name="isJap">True for Japanese version (SLPS_201.04)</param>
        public static void Fix(Stream Elf, uint LBA, uint NewSize, bool isJap)
        {
            uint tableStart = isJap ? LBATableStart_JAP : LBATableStart_USA;
            uint tableEnd = isJap ? LBATableEnd_JAP : LBATableEnd_USA;
            string versionName = isJap ? "JAP (SLPS_201.04)" : "USA (SLUS_202.51)";

            TextOut.Print($"Version: {versionName}");
            TextOut.Print($"LBA table range: 0x{tableStart:X6} - 0x{tableEnd:X6}");

            BinaryReader Reader = new BinaryReader(Elf);
            BinaryWriter Writer = new BinaryWriter(Elf);

            int Difference = 0;
            bool Found = false;
            Elf.Seek(tableStart, SeekOrigin.Begin);
            while (Elf.Position < tableEnd)
            {
                uint LBAStart = Reader.ReadUInt32();
                uint LBAEnd = Reader.ReadUInt32();

                Elf.Seek(-8, SeekOrigin.Current);
                Writer.Write((uint)(LBAStart + Difference));
                Writer.Write((uint)(LBAEnd + Difference));

                if (LBAStart == LBA)
                {
                    Found = true;
                    uint Size = NewSize / BytesPerSector;
                    if ((NewSize % BytesPerSector) != 0) Size++;

                    Elf.Seek(-4, SeekOrigin.Current);
                    uint NewEnd = (LBAStart + Size) - 1;
                    Writer.Write(NewEnd);
                    Difference = (int)(NewEnd - LBAEnd);
                }
            }

            if (!Found)
            {
                TextOut.PrintWarning("The LBA you entered was not found on the table!");
                TextOut.Print("Make sure you typed it in DECIMAL format.");
            }
            else
                TextOut.PrintSuccess("LBA found and values patched successfully!");
        }
    }
}
