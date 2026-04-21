using System.IO;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    ///     Handles the LBA table recalculation on the ELF from Harvest Moon.
    /// </summary>
    class HarvestElf
    {
        const uint LBATableStart = 0x162460;
        const uint LBATableEnd = 0x162d30;
        const int SectorSize = 0x930;
        const int BytesPerSector = 0x800;

        /// <summary>
        ///     Fixes the LBA table on the main ELF executable of the Harvest Moon: Save the Homeland game.
        /// </summary>
        /// <param name="Elf">The full path to the ELF file</param>
        /// <param name="LBA">The LBA of the modified file</param>
        /// <param name="NewSize">The new size of the file</param>
        public static void Fix(string Elf, uint LBA, uint NewSize)
        {
            using (FileStream Input = new FileStream(Elf, FileMode.Open))
            {
                Fix(Input, LBA, NewSize);
            }
        }

        /// <summary>
        ///     Fixes the LBA table on the main ELF executable of the Harvest Moon: Save the Homeland game.
        /// </summary>
        /// <param name="Elf">The Stream with the ELF data</param>
        /// <param name="LBA">The LBA of the modified file</param>
        /// <param name="NewSize">The new size of the file</param>
        public static void Fix(Stream Elf, uint LBA, uint NewSize)
        {
            BinaryReader Reader = new BinaryReader(Elf);
            BinaryWriter Writer = new BinaryWriter(Elf);

            int Difference = 0;
            bool Found = false;
            Elf.Seek(LBATableStart, SeekOrigin.Begin);
            while (Elf.Position < LBATableEnd)
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
