using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using HMSTHModdingTool.Properties;

namespace HMSTHModdingTool.IO
{
    class HarvestText
    {
        private static readonly byte[] DAT_MAGIC =
            new byte[] { (byte)'H', (byte)'M', (byte)'H', (byte)'X' };

        private static string GetDatPath(string txtPath)
        {
            string dir = Path.GetDirectoryName(txtPath);
            string name = Path.GetFileNameWithoutExtension(txtPath) + ".dat";
            if (string.IsNullOrEmpty(dir))
                return name;
            return Path.Combine(dir, name);
        }

        // Type 0x01 = unknown pair
        // Type 0x02 = [var] pair
        // Type 0x03 = slot is suppressed from .txt (marker)
        // Type 0x04 = newline (0x00) token in a suppressed slot
        private struct HiddenEntry
        {
            public int DialogIndex;
            public int CharPosition;
            public byte Type;
            public ushort Primary;
            public byte Extra;
        }

        public static string Decode(string Data, string Pointers)
        {
            using (FileStream DataStream =
                new FileStream(Data, FileMode.Open))
            using (FileStream PointersStream =
                new FileStream(Pointers, FileMode.Open))
            {
                return Decode(DataStream, PointersStream);
            }
        }

        public static string Decode(Stream Data, Stream Pointers)
        {
            List<HiddenEntry> entries;
            return DecodeInternal(Data, Pointers, out entries);
        }

        public static string DecodeToFile(
            string DataPath,
            string PointersPath,
            string OutputTxtPath)
        {
            using (FileStream DataStream =
                new FileStream(DataPath, FileMode.Open))
            using (FileStream PointersStream =
                new FileStream(PointersPath, FileMode.Open))
            {
                List<HiddenEntry> entries;
                string visibleText =
                    DecodeInternal(DataStream, PointersStream, out entries);

                File.WriteAllText(OutputTxtPath, visibleText, Encoding.UTF8);

                string datPath = GetDatPath(OutputTxtPath);
                WriteDat(datPath, entries);

                return datPath;
            }
        }

        private static string DecodeInternal(
            Stream Data,
            Stream Pointers,
            out List<HiddenEntry> entries)
        {
            BinaryReader Reader = new BinaryReader(Data);
            BinaryReader Pointer = new BinaryReader(Pointers);
            StringBuilder Output = new StringBuilder();

            List<HiddenEntry> Entries = new List<HiddenEntry>();
            string[] Table = GetTable();
            string EndMarker = Table[2];

            int dialogIndex = 0;

            uint NextOffset = Pointer.ReadUInt32();
            while (Pointers.Position < Pointers.Length)
            {
                uint Offset = NextOffset;
                NextOffset = Pointer.ReadUInt32();
                if (NextOffset == 0) break;

                Data.Seek(Offset, SeekOrigin.Begin);

                uint Value = 0;
                byte Header = 0;
                byte Mask = 0;
                bool hasVisibleText = false;

                // Collect everything for this slot before committing
                List<HiddenEntry> slotEntries = new List<HiddenEntry>();
                StringBuilder slotText = new StringBuilder();
                int slotVisiblePos = 0;

                while (Data.Position < Data.Length && Value != 2)
                {
                    if ((Mask >>= 1) == 0)
                    {
                        Header = (byte)Data.ReadByte();
                        Mask = 0x80;
                    }

                    if ((Header & Mask) == 0)
                        Value = (byte)Data.ReadByte();
                    else
                        Value = Reader.ReadUInt16();

                    if (Value == 2)
                    {
                        break;
                    }
                    else if (Value == 7)
                    {
                        byte varByte = (byte)Data.ReadByte();
                        slotEntries.Add(new HiddenEntry
                        {
                            DialogIndex = dialogIndex,
                            CharPosition = slotVisiblePos,
                            Type = 0x02,
                            Primary = 7,
                            Extra = varByte
                        });
                    }
                    else if (Table[Value] == null)
                    {
                        byte extraByte = (byte)Data.ReadByte();
                        slotEntries.Add(new HiddenEntry
                        {
                            DialogIndex = dialogIndex,
                            CharPosition = slotVisiblePos,
                            Type = 0x01,
                            Primary = (ushort)Value,
                            Extra = extraByte
                        });
                    }
                    else
                    {
                        string charStr = Table[Value];

                        if (Value == 0)
                        {
                            // Newline token — store as Type=0x04 so we can
                            // reproduce it exactly in suppressed slots.
                            // For visible slots we discard these entries
                            // since the text string already contains the newline.
                            slotEntries.Add(new HiddenEntry
                            {
                                DialogIndex = dialogIndex,
                                CharPosition = slotVisiblePos,
                                Type = 0x04,
                                Primary = 0,
                                Extra = 0
                            });
                        }
                        else
                        {
                            hasVisibleText = true;
                        }

                        slotText.Append(charStr);
                        slotVisiblePos += charStr.Length;
                    }
                }

                if (!hasVisibleText)
                {
                    // Suppressed slot — keep ALL entries including newlines
                    // Add Type=0x03 marker so encoder knows slot is suppressed
                    foreach (HiddenEntry he in slotEntries)
                        Entries.Add(he);
                    Entries.Add(new HiddenEntry
                    {
                        DialogIndex = dialogIndex,
                        CharPosition = 0,
                        Type = 0x03,
                        Primary = 0,
                        Extra = 0
                    });
                    // Do NOT write to .txt
                }
                else
                {
                    // Visible slot — keep inline entries (0x01, 0x02) only
                    // Discard Type=0x04 newline entries — text string has them
                    foreach (HiddenEntry he in slotEntries)
                    {
                        if (he.Type != 0x04)
                            Entries.Add(he);
                    }
                    Output.Append(slotText);
                    Output.Append(EndMarker);
                }

                dialogIndex++;
            }

            entries = Entries;
            return Output.ToString();
        }

        private static void WriteDat(string path, List<HiddenEntry> entries)
        {
            if (File.Exists(path))
            {
                try { File.SetAttributes(path, FileAttributes.Normal); }
                catch { }
            }

            using (FileStream fs = new FileStream(
                       path, FileMode.Create,
                       FileAccess.Write, FileShare.None))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                w.Write(DAT_MAGIC);
                w.Write((int)entries.Count);

                foreach (HiddenEntry e in entries)
                {
                    w.Write((int)e.DialogIndex);
                    w.Write((int)e.CharPosition);
                    w.Write((byte)e.Type);
                    w.Write((ushort)e.Primary);
                    w.Write((byte)e.Extra);
                }

                w.Flush();
                fs.Flush(true);
            }

            try { File.SetAttributes(path, FileAttributes.Normal); }
            catch { }
        }

        private static List<HiddenEntry> ReadDat(string path)
        {
            List<HiddenEntry> entries = new List<HiddenEntry>();

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Missing companion .dat file: " + path +
                    "\nThe .txt cannot be re-imported without its .dat metadata.",
                    path);

            try { File.SetAttributes(path, FileAttributes.Normal); }
            catch { }

            using (FileStream fs = new FileStream(
                       path, FileMode.Open,
                       FileAccess.Read, FileShare.Read))
            using (BinaryReader r = new BinaryReader(fs))
            {
                byte[] magic = r.ReadBytes(4);
                if (magic.Length != 4 ||
                    magic[0] != DAT_MAGIC[0] ||
                    magic[1] != DAT_MAGIC[1] ||
                    magic[2] != DAT_MAGIC[2] ||
                    magic[3] != DAT_MAGIC[3])
                    throw new InvalidDataException(
                        "Invalid .dat file (wrong magic): " + path);

                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    HiddenEntry e = new HiddenEntry();
                    e.DialogIndex = r.ReadInt32();
                    e.CharPosition = r.ReadInt32();
                    e.Type = r.ReadByte();
                    e.Primary = r.ReadUInt16();
                    e.Extra = r.ReadByte();
                    entries.Add(e);
                }
            }

            return entries;
        }

        public struct EncodedText
        {
            public byte[] Data;
            public byte[] Pointers;
        }

        public static void Encode(string Text, string Data, string Pointers)
        {
            EncodedText Encoded = Encode(Text);
            File.WriteAllBytes(Data, Encoded.Data);
            File.WriteAllBytes(Pointers, Encoded.Pointers);
        }

        public static EncodedText Encode(string Text)
        {
            return EncodeInternal(Text, new List<HiddenEntry>());
        }

        public static void EncodeFromFile(
            string InputTxtPath,
            string DataPath,
            string PointersPath)
        {
            string text = File.ReadAllText(InputTxtPath, Encoding.UTF8);
            string datPath = GetDatPath(InputTxtPath);
            List<HiddenEntry> entries = ReadDat(datPath);
            EncodedText Encoded = EncodeInternal(text, entries);
            File.WriteAllBytes(DataPath, Encoded.Data);
            File.WriteAllBytes(PointersPath, Encoded.Pointers);
        }

        private static EncodedText EncodeInternal(
            string text,
            List<HiddenEntry> entries)
        {
            EncodedText Output = new EncodedText();
            string[] Table = GetTable();
            string EndMarker = Table[2];

            string[] visibleDialogs = text.Split(
                new string[] { EndMarker },
                StringSplitOptions.None);

            int visibleCount = visibleDialogs.Length;
            while (visibleCount > 0 &&
                   visibleDialogs[visibleCount - 1] == string.Empty)
                visibleCount--;

            // Collect suppressed slot indices from Type=0x03 markers
            SortedSet<int> nonVisibleOriginalIndices = new SortedSet<int>();
            foreach (HiddenEntry e in entries)
                if (e.Type == 0x03)
                    nonVisibleOriginalIndices.Add(e.DialogIndex);

            int totalSlots = visibleCount + nonVisibleOriginalIndices.Count;

            int[] origToVisible = new int[totalSlots];
            int visibleCursor = 0;
            for (int origIdx = 0; origIdx < totalSlots; origIdx++)
            {
                if (nonVisibleOriginalIndices.Contains(origIdx))
                    origToVisible[origIdx] = -1;
                else
                    origToVisible[origIdx] = visibleCursor++;
            }

            // Build per-slot entry lookup
            // For suppressed slots: includes Type=0x01, 0x02, 0x04
            // For visible slots: includes Type=0x01, 0x02 only
            Dictionary<int, List<HiddenEntry>> inlineByOrig =
                new Dictionary<int, List<HiddenEntry>>();

            foreach (HiddenEntry e in entries)
            {
                if (e.Type == 0x03) continue; // skip markers
                if (!inlineByOrig.ContainsKey(e.DialogIndex))
                    inlineByOrig[e.DialogIndex] = new List<HiddenEntry>();
                inlineByOrig[e.DialogIndex].Add(e);
            }

            foreach (List<HiddenEntry> list in inlineByOrig.Values)
                list.Sort((a, b) => a.CharPosition.CompareTo(b.CharPosition));

            using (MemoryStream Data = new MemoryStream())
            using (MemoryStream Pointers = new MemoryStream())
            {
                BinaryWriter Writer = new BinaryWriter(Data);
                BinaryWriter Pointer = new BinaryWriter(Pointers);

                for (int origIdx = 0; origIdx < totalSlots; origIdx++)
                {
                    Align(Data, 4);
                    Pointer.Write((uint)Data.Position);

                    int visIdx = origToVisible[origIdx];

                    List<HiddenEntry> mine;
                    if (!inlineByOrig.TryGetValue(origIdx, out mine))
                        mine = new List<HiddenEntry>();

                    if (visIdx == -1)
                    {
                        // Suppressed slot — encode all entries in order
                        // Type=0x01/0x02 = inline pairs
                        // Type=0x04 = newline (0x00 byte)
                        // Then end marker

                        if (mine.Count == 0)
                        {
                            // Truly empty
                            Data.WriteByte(0x00);
                            Data.WriteByte(0x02);
                        }
                        else
                        {
                            byte Header = 0;
                            int Mask = 0;
                            long Position = 0;
                            long HeaderPosition = Data.Position;

                            foreach (HiddenEntry he in mine)
                            {
                                if ((Mask >>= 1) == 0)
                                {
                                    Data.WriteByte(0);
                                    Position = Data.Position;
                                    Data.Seek(HeaderPosition,
                                        SeekOrigin.Begin);
                                    Data.WriteByte(Header);
                                    Data.Seek(Position, SeekOrigin.Begin);
                                    HeaderPosition = Position - 1;
                                    Header = 0;
                                    Mask = 0x80;
                                }

                                if (he.Type == 0x04)
                                {
                                    // Newline token
                                    Data.WriteByte(0x00);
                                }
                                else if (he.Type == 0x02)
                                {
                                    Data.WriteByte(7);
                                    Data.WriteByte(he.Extra);
                                }
                                else if (he.Type == 0x01)
                                {
                                    if (he.Primary > 0xFF)
                                    {
                                        Writer.Write(he.Primary);
                                        Header |= (byte)Mask;
                                    }
                                    else
                                    {
                                        Data.WriteByte(
                                            (byte)(he.Primary & 0xFF));
                                    }
                                    Data.WriteByte(he.Extra);
                                }
                            }

                            // Flush pending header
                            Position = Data.Position;
                            if (Header != 0)
                            {
                                Data.Seek(HeaderPosition, SeekOrigin.Begin);
                                Data.WriteByte(Header);
                                Data.Seek(Position, SeekOrigin.Begin);
                            }

                            // End marker
                            if ((Mask >>= 1) == 0)
                            {
                                Data.WriteByte(0);
                                HeaderPosition = Data.Position - 1;
                                Mask = 0x80;
                            }
                            Data.WriteByte(2);

                            Position = Data.Position;
                            Data.Seek(Position, SeekOrigin.Begin);
                        }
                        continue;
                    }

                    // Visible dialog — encode text + inline entries
                    string Dialog = visibleDialogs[visIdx];

                    byte Header2 = 0;
                    int Mask2 = 0;
                    long Position2 = 0;
                    long HeaderPosition2 = Data.Position;

                    int visiblePos = 0;
                    int nextEntry = 0;
                    int i = 0;

                    while (i <= Dialog.Length)
                    {
                        while (nextEntry < mine.Count &&
                               mine[nextEntry].CharPosition == visiblePos)
                        {
                            HiddenEntry he = mine[nextEntry++];

                            if ((Mask2 >>= 1) == 0)
                            {
                                Data.WriteByte(0);
                                Position2 = Data.Position;
                                Data.Seek(HeaderPosition2, SeekOrigin.Begin);
                                Data.WriteByte(Header2);
                                Data.Seek(Position2, SeekOrigin.Begin);
                                HeaderPosition2 = Position2 - 1;
                                Header2 = 0;
                                Mask2 = 0x80;
                            }

                            if (he.Type == 0x02)
                            {
                                Data.WriteByte(7);
                                Data.WriteByte(he.Extra);
                            }
                            else if (he.Type == 0x01)
                            {
                                if (he.Primary > 0xFF)
                                {
                                    Writer.Write(he.Primary);
                                    Header2 |= (byte)Mask2;
                                }
                                else
                                {
                                    Data.WriteByte(
                                        (byte)(he.Primary & 0xFF));
                                }
                                Data.WriteByte(he.Extra);
                            }
                        }

                        if (i == Dialog.Length) break;

                        if ((Mask2 >>= 1) == 0)
                        {
                            Data.WriteByte(0);
                            Position2 = Data.Position;
                            Data.Seek(HeaderPosition2, SeekOrigin.Begin);
                            Data.WriteByte(Header2);
                            Data.Seek(Position2, SeekOrigin.Begin);
                            HeaderPosition2 = Position2 - 1;
                            Header2 = 0;
                            Mask2 = 0x80;
                        }

                        if (i + 2 <= Dialog.Length &&
                            Dialog.Substring(i, 2) == "\r\n")
                        {
                            Data.WriteByte(0);
                            visiblePos += 2;
                            i += 2;
                            continue;
                        }

                        if (Dialog[i] == '\n')
                        {
                            Data.WriteByte(0);
                            visiblePos += 1;
                            i += 1;
                            continue;
                        }

                        int charValue = -1;

                        if (Dialog[i] == '[')
                        {
                            bool matched = false;
                            for (int t = 0; t < Table.Length; t++)
                            {
                                string tv = Table[t];
                                if (tv == null) continue;
                                if (i + tv.Length > Dialog.Length) continue;
                                if (Dialog.Substring(i, tv.Length) == tv)
                                {
                                    charValue = t;
                                    visiblePos += tv.Length;
                                    i += tv.Length;
                                    matched = true;
                                    break;
                                }
                            }

                            if (!matched)
                            {
                                Data.WriteByte(0x10);
                                visiblePos += 1;
                                i += 1;
                            }
                            else
                            {
                                if (charValue > 0xFF)
                                {
                                    Writer.Write((ushort)charValue);
                                    Header2 |= (byte)Mask2;
                                }
                                else
                                {
                                    Data.WriteByte((byte)charValue);
                                }
                            }
                        }
                        else
                        {
                            string ch = Dialog.Substring(i, 1);
                            charValue = Array.IndexOf(Table, ch);

                            if (charValue > -1)
                            {
                                if (charValue > 0xFF)
                                {
                                    Writer.Write((ushort)charValue);
                                    Header2 |= (byte)Mask2;
                                }
                                else
                                {
                                    Data.WriteByte((byte)charValue);
                                }
                            }
                            else
                            {
                                Data.WriteByte(0x10);
                            }

                            visiblePos += 1;
                            i += 1;
                        }
                    }

                    Position2 = Data.Position;
                    if (Header2 != 0)
                    {
                        Data.Seek(HeaderPosition2, SeekOrigin.Begin);
                        Data.WriteByte(Header2);
                        Data.Seek(Position2, SeekOrigin.Begin);
                    }

                    if ((Mask2 >>= 1) == 0)
                    {
                        Data.WriteByte(0);
                        HeaderPosition2 = Data.Position - 1;
                        Mask2 = 0x80;
                    }

                    Data.WriteByte(2);

                    Position2 = Data.Position;
                    Data.Seek(Position2, SeekOrigin.Begin);
                }

                Align(Data, 4);
                Pointer.Write((uint)Data.Length);
                Align(Data, 0x10);
                Align(Pointers, 0x10);

                Output.Data = Data.ToArray();
                Output.Pointers = Pointers.ToArray();
            }

            return Output;
        }

        private static string[] GetTable()
        {
            string[] Table = new string[0x10000];
            string[] LineBreaks = new string[] { "\n", "\r\n" };
            string[] TableElements = Resources.CharacterTable.Split(
                LineBreaks, StringSplitOptions.RemoveEmptyEntries);

            foreach (string Element in TableElements)
            {
                string[] Parameters = Element.Split('=');
                int Value = Convert.ToInt32(Parameters[0], 16);
                string Character = Parameters[1];

                Character = Character.Replace("\\n", "\r\n");
                Character = Character.Replace("\\equal", "=");

                Table[Value] = Character;
            }

            return Table;
        }

        private static void Align(Stream Stream, int Bytes)
        {
            int Mask = Bytes - 1;
            while ((Stream.Position & Mask) != 0)
                Stream.WriteByte(0);
        }
    }
}
