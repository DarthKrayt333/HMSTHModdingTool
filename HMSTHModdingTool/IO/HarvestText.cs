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

        public static string GetDatPathPublic(string txtPath)
        {
            return GetDatPath(txtPath);
        }

        // Type 0x01 = unknown pair
        // Type 0x02 = [var] pair (legacy — kept for old .dat compatibility)
        // Type 0x03 = slot is suppressed from .txt (marker)
        // Type 0x04 = newline (0x00) token in a suppressed slot
        //
        // CharPosition = old visible-string position logic
        // ControlIndex = number of [roll]/[dialog] controls seen before the
        //                hidden token, if it is at the start of a visible run
        // LineIndex    = number of raw newline tokens (0x00) since the last
        //                control, if it is at the start of a visible run
        //
        // If ControlIndex or LineIndex is -1, use old CharPosition-only logic.
        private struct HiddenEntry
        {
            public int DialogIndex;
            public int CharPosition;
            public int ControlIndex;
            public int LineIndex;
            public byte Type;
            public ushort Primary;
            public byte Extra;
        }

        private static bool IsDialogTagValue(int value)
        {
            return value == 1; // [dialog]
        }

        private static bool IsControlTagValue(int value)
        {
            return value == 1 || value == 4; // [dialog], [roll]
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

        public static void DecodeToFileHex(
            string DataPath,
            string PointersPath,
            string OutputTxtPath)
        {
            using (FileStream DataStream =
                new FileStream(DataPath, FileMode.Open))
            using (FileStream PointersStream =
                new FileStream(PointersPath, FileMode.Open))
            {
                string hexText =
                    DecodeInternalHex(DataStream, PointersStream);

                File.WriteAllText(OutputTxtPath, hexText, Encoding.UTF8);
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

                List<HiddenEntry> slotEntries = new List<HiddenEntry>();
                StringBuilder slotText = new StringBuilder();
                int slotVisiblePos = 0;

                // Run anchor tracking:
                // pendingRunStart is true:
                // - at slot start
                // - after raw newline token 0x00
                // - after [dialog]
                //
                // It is false after regular text and after [roll].
                int controlIndex = 0; // count [roll]/[dialog]
                int lineIndex = 0;    // raw newlines since last control
                bool pendingRunStart = true;

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
                        string hexTag = string.Format(
                            "[hex_{0:X2}]", varByte);
                        slotText.Append(hexTag);
                        slotVisiblePos += hexTag.Length;
                        hasVisibleText = true;
                        pendingRunStart = false;
                    }
                    else if (Table[Value] == null)
                    {
                        byte extraByte = (byte)Data.ReadByte();
                        slotEntries.Add(new HiddenEntry
                        {
                            DialogIndex = dialogIndex,
                            CharPosition = slotVisiblePos,
                            ControlIndex = pendingRunStart ? controlIndex : -1,
                            LineIndex = pendingRunStart ? lineIndex : -1,
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
                            slotEntries.Add(new HiddenEntry
                            {
                                DialogIndex = dialogIndex,
                                CharPosition = slotVisiblePos,
                                ControlIndex = pendingRunStart ? controlIndex : -1,
                                LineIndex = pendingRunStart ? lineIndex : -1,
                                Type = 0x04,
                                Primary = 0,
                                Extra = 0
                            });

                            slotText.Append(charStr);
                            slotVisiblePos += charStr.Length;

                            lineIndex++;
                            pendingRunStart = true;
                        }
                        else
                        {
                            hasVisibleText = true;
                            slotText.Append(charStr);
                            slotVisiblePos += charStr.Length;

                            if (IsDialogTagValue((int)Value))
                            {
                                controlIndex++;
                                lineIndex = 0;
                                pendingRunStart = true;
                            }
                            else if (IsControlTagValue((int)Value))
                            {
                                // [roll]
                                controlIndex++;
                                lineIndex = 0;
                                pendingRunStart = false;
                            }
                            else
                            {
                                pendingRunStart = false;
                            }
                        }
                    }
                }

                if (!hasVisibleText)
                {
                    foreach (HiddenEntry he in slotEntries)
                        Entries.Add(he);
                    Entries.Add(new HiddenEntry
                    {
                        DialogIndex = dialogIndex,
                        CharPosition = 0,
                        ControlIndex = 0,
                        LineIndex = 0,
                        Type = 0x03,
                        Primary = 0,
                        Extra = 0
                    });
                }
                else
                {
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

        private static string DecodeInternalHex(
            Stream Data,
            Stream Pointers)
        {
            BinaryReader Reader = new BinaryReader(Data);
            BinaryReader Pointer = new BinaryReader(Pointers);
            StringBuilder Output = new StringBuilder();

            string[] Table = GetTable();
            string EndMarker = Table[2];

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

                StringBuilder slotText = new StringBuilder();

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
                        slotText.AppendFormat("[var{0:X2}]", varByte);
                        hasVisibleText = true;
                    }
                    else if (Table[Value] == null)
                    {
                        byte extraByte = (byte)Data.ReadByte();

                        if (Value > 0xFF)
                            slotText.AppendFormat(
                                "[hex{0:X4}_{1:X2}]",
                                Value, extraByte);
                        else
                            slotText.AppendFormat(
                                "[hex{0:X2}_{1:X2}]",
                                (byte)Value, extraByte);

                        hasVisibleText = true;
                    }
                    else
                    {
                        string charStr = Table[Value];
                        slotText.Append(charStr);

                        if (Value != 0)
                            hasVisibleText = true;
                    }
                }

                if (hasVisibleText)
                {
                    Output.Append(slotText);
                    Output.Append(EndMarker);
                }
            }

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
                    w.Write((int)e.ControlIndex);
                    w.Write((int)e.LineIndex);
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

                long remaining = fs.Length - fs.Position;
                bool newFormat = (count == 0)
                    ? true
                    : (remaining == (long)count * 20);

                for (int i = 0; i < count; i++)
                {
                    HiddenEntry e = new HiddenEntry();
                    e.DialogIndex = r.ReadInt32();
                    e.CharPosition = r.ReadInt32();

                    if (newFormat)
                    {
                        e.ControlIndex = r.ReadInt32();
                        e.LineIndex = r.ReadInt32();
                    }
                    else
                    {
                        e.ControlIndex = -1;
                        e.LineIndex = -1;
                    }

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

        public static void EncodeFromFileHex(
            string InputTxtPath,
            string DataPath,
            string PointersPath)
        {
            string text = File.ReadAllText(InputTxtPath, Encoding.UTF8);
            EncodedText Encoded = EncodeInternalHex(text);
            File.WriteAllBytes(DataPath, Encoded.Data);
            File.WriteAllBytes(PointersPath, Encoded.Pointers);
        }

        private static void EnsureMaskSlot(
            Stream Data,
            ref byte Header,
            ref int Mask,
            ref long Position,
            ref long HeaderPosition)
        {
            if ((Mask >>= 1) == 0)
            {
                Data.WriteByte(0);
                Position = Data.Position;
                Data.Seek(HeaderPosition, SeekOrigin.Begin);
                Data.WriteByte(Header);
                Data.Seek(Position, SeekOrigin.Begin);
                HeaderPosition = Position - 1;
                Header = 0;
                Mask = 0x80;
            }
        }

        private static void WriteInlineHidden(
            Stream Data,
            BinaryWriter Writer,
            ref byte Header,
            ref int Mask,
            ref long Position,
            ref long HeaderPosition,
            HiddenEntry he)
        {
            EnsureMaskSlot(
                Data,
                ref Header,
                ref Mask,
                ref Position,
                ref HeaderPosition);

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
                    Header |= (byte)Mask;
                }
                else
                {
                    Data.WriteByte((byte)(he.Primary & 0xFF));
                }
                Data.WriteByte(he.Extra);
            }
        }

        private static int CountControls(
            string Dialog,
            string[] Table)
        {
            int controlIndex = 0;
            int i = 0;

            while (i < Dialog.Length)
            {
                if (i + 8 <= Dialog.Length &&
                    Dialog.Substring(i, 5) == "[hex_" &&
                    Dialog[i + 7] == ']')
                {
                    byte dummy;
                    if (byte.TryParse(
                            Dialog.Substring(i + 5, 2),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out dummy))
                    {
                        i += 8;
                        continue;
                    }
                }

                if (Dialog[i] == '[')
                {
                    bool matched = false;
                    for (int t = 0; t < Table.Length; t++)
                    {
                        string tv = Table[t];
                        if (tv == null) continue;
                        if (i + tv.Length > Dialog.Length) continue;
                        if (Dialog.Substring(i, tv.Length) != tv) continue;

                        if (IsControlTagValue(t))
                            controlIndex++;

                        i += tv.Length;
                        matched = true;
                        break;
                    }

                    if (matched)
                        continue;
                }

                if (i + 2 <= Dialog.Length &&
                    Dialog.Substring(i, 2) == "\r\n")
                {
                    i += 2;
                    continue;
                }

                if (Dialog[i] == '\n')
                {
                    i += 1;
                    continue;
                }

                i += 1;
            }

            return controlIndex;
        }

        // For each control count, what is the highest reachable LineIndex
        // at a visible run start in the edited text?
        //
        // Run starts:
        // - slot start -> (control=0, line=0)
        // - after raw newline -> line++
        // - after [dialog]    -> control++, line=0
        //
        // [roll] increments control count, but does NOT itself start a run.
        private static int[] GetMaxLineIndexPerControl(
            string Dialog,
            string[] Table,
            int totalControls)
        {
            int[] maxLine = new int[totalControls + 1];

            int controlIndex = 0;
            int lineIndex = 0;
            maxLine[0] = 0;

            int i = 0;
            while (i < Dialog.Length)
            {
                if (i + 8 <= Dialog.Length &&
                    Dialog.Substring(i, 5) == "[hex_" &&
                    Dialog[i + 7] == ']')
                {
                    byte dummy;
                    if (byte.TryParse(
                            Dialog.Substring(i + 5, 2),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture,
                            out dummy))
                    {
                        i += 8;
                        continue;
                    }
                }

                if (Dialog[i] == '[')
                {
                    bool matched = false;
                    for (int t = 0; t < Table.Length; t++)
                    {
                        string tv = Table[t];
                        if (tv == null) continue;
                        if (i + tv.Length > Dialog.Length) continue;
                        if (Dialog.Substring(i, tv.Length) != tv) continue;

                        if (IsDialogTagValue(t))
                        {
                            controlIndex++;
                            if (controlIndex > totalControls)
                                controlIndex = totalControls;
                            lineIndex = 0;
                            if (lineIndex > maxLine[controlIndex])
                                maxLine[controlIndex] = lineIndex;
                        }
                        else if (IsControlTagValue(t))
                        {
                            // [roll]
                            controlIndex++;
                            if (controlIndex > totalControls)
                                controlIndex = totalControls;
                            lineIndex = 0;
                            // no run start here
                        }

                        i += tv.Length;
                        matched = true;
                        break;
                    }

                    if (matched)
                        continue;
                }

                if (i + 2 <= Dialog.Length &&
                    Dialog.Substring(i, 2) == "\r\n")
                {
                    lineIndex++;
                    if (lineIndex > maxLine[controlIndex])
                        maxLine[controlIndex] = lineIndex;
                    i += 2;
                    continue;
                }

                if (Dialog[i] == '\n')
                {
                    lineIndex++;
                    if (lineIndex > maxLine[controlIndex])
                        maxLine[controlIndex] = lineIndex;
                    i += 1;
                    continue;
                }

                i += 1;
            }

            return maxLine;
        }

        private static EncodedText EncodeInternal(
            string text,
            List<HiddenEntry> entries)
        {
            EncodedText Output = new EncodedText();
            string[] Table = GetTable();
            string EndMarker = Table[2];

            // Normalize line endings to \r\n
            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

            // EndMarker is "[end]\r\n\r\n"
            // Also accept "[end]\r\n" or "[end]\n\n" or "[end]\n" at end of file
            string endTag = "[end]";
            string trimmed = text.TrimEnd('\r', '\n', ' ', '\t');
            if (trimmed.EndsWith(endTag))
                text = trimmed + "\r\n\r\n";

            string[] visibleDialogs = text.Split(
                new string[] { EndMarker },
                StringSplitOptions.None);

            int visibleCount = visibleDialogs.Length;
            while (visibleCount > 0 &&
                   visibleDialogs[visibleCount - 1] == string.Empty)
                visibleCount--;

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

            Dictionary<int, List<HiddenEntry>> inlineByOrig =
                new Dictionary<int, List<HiddenEntry>>();

            foreach (HiddenEntry e in entries)
            {
                if (e.Type == 0x03) continue;
                if (!inlineByOrig.ContainsKey(e.DialogIndex))
                    inlineByOrig[e.DialogIndex] = new List<HiddenEntry>();
                inlineByOrig[e.DialogIndex].Add(e);
            }

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
                        // ── Suppressed slot ───────────────────────────
                        if (mine.Count == 0)
                        {
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
                                EnsureMaskSlot(
                                    Data,
                                    ref Header,
                                    ref Mask,
                                    ref Position,
                                    ref HeaderPosition);

                                if (he.Type == 0x04)
                                {
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

                            Position = Data.Position;
                            if (Header != 0)
                            {
                                Data.Seek(HeaderPosition,
                                    SeekOrigin.Begin);
                                Data.WriteByte(Header);
                                Data.Seek(Position, SeekOrigin.Begin);
                            }

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

                    // ── Visible dialog ────────────────────────────────
                    string Dialog = visibleDialogs[visIdx];

                    List<HiddenEntry> anchored = new List<HiddenEntry>();
                    List<HiddenEntry> loose = new List<HiddenEntry>();

                    int totalControls = CountControls(Dialog, Table);
                    int[] maxLinePerControl =
                        GetMaxLineIndexPerControl(Dialog, Table, totalControls);

                    foreach (HiddenEntry he in mine)
                    {
                        if (he.ControlIndex >= 0 && he.LineIndex >= 0)
                        {
                            HiddenEntry temp = he;

                            if (temp.ControlIndex > totalControls)
                                temp.ControlIndex = totalControls;

                            if (temp.LineIndex > maxLinePerControl[temp.ControlIndex])
                                temp.LineIndex = maxLinePerControl[temp.ControlIndex];

                            anchored.Add(temp);
                        }
                        else
                        {
                            loose.Add(he);
                        }
                    }

                    anchored.Sort((a, b) =>
                    {
                        int c = a.ControlIndex.CompareTo(b.ControlIndex);
                        if (c != 0) return c;
                        c = a.LineIndex.CompareTo(b.LineIndex);
                        if (c != 0) return c;
                        return a.CharPosition.CompareTo(b.CharPosition);
                    });

                    loose.Sort((a, b) =>
                        a.CharPosition.CompareTo(b.CharPosition));

                    byte Header2 = 0;
                    int Mask2 = 0;
                    long Position2 = 0;
                    long HeaderPosition2 = Data.Position;

                    int visiblePos = 0;
                    int controlIndex = 0;
                    int lineIndex = 0;
                    int nextAnchored = 0;
                    int nextLoose = 0;
                    int i = 0;
                    bool pendingRunStart = true;

                    while (i <= Dialog.Length)
                    {
                        if (pendingRunStart)
                        {
                            while (nextAnchored < anchored.Count &&
                                   anchored[nextAnchored].ControlIndex == controlIndex &&
                                   anchored[nextAnchored].LineIndex == lineIndex)
                            {
                                WriteInlineHidden(
                                    Data, Writer,
                                    ref Header2, ref Mask2,
                                    ref Position2, ref HeaderPosition2,
                                    anchored[nextAnchored++]);
                            }
                        }

                        while (nextLoose < loose.Count &&
                               loose[nextLoose].CharPosition == visiblePos)
                        {
                            WriteInlineHidden(
                                Data, Writer,
                                ref Header2, ref Mask2,
                                ref Position2, ref HeaderPosition2,
                                loose[nextLoose++]);
                        }

                        if (i == Dialog.Length) break;

                        // ── [hex_XX] BEFORE mask allocation ──────────
                        if (i + 8 <= Dialog.Length &&
                            Dialog.Substring(i, 5) == "[hex_" &&
                            Dialog[i + 7] == ']')
                        {
                            byte varByte;
                            if (byte.TryParse(
                                    Dialog.Substring(i + 5, 2),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture,
                                    out varByte))
                            {
                                EnsureMaskSlot(
                                    Data,
                                    ref Header2,
                                    ref Mask2,
                                    ref Position2,
                                    ref HeaderPosition2);

                                Data.WriteByte(7);
                                Data.WriteByte(varByte);
                                visiblePos += 8;
                                i += 8;
                                pendingRunStart = false;
                                continue;
                            }
                        }

                        EnsureMaskSlot(
                            Data,
                            ref Header2,
                            ref Mask2,
                            ref Position2,
                            ref HeaderPosition2);

                        if (Dialog[i] == '[')
                        {
                            bool matched = false;
                            int charValue = -1;

                            for (int t = 0; t < Table.Length; t++)
                            {
                                string tv = Table[t];
                                if (tv == null) continue;
                                if (i + tv.Length > Dialog.Length)
                                    continue;
                                if (Dialog.Substring(i, tv.Length) != tv)
                                    continue;

                                charValue = t;

                                if (charValue > 0xFF)
                                {
                                    Writer.Write((ushort)charValue);
                                    Header2 |= (byte)Mask2;
                                }
                                else
                                {
                                    Data.WriteByte((byte)charValue);
                                }

                                visiblePos += tv.Length;
                                i += tv.Length;
                                matched = true;

                                if (IsDialogTagValue(charValue))
                                {
                                    controlIndex++;
                                    lineIndex = 0;
                                    pendingRunStart = true;
                                }
                                else if (IsControlTagValue(charValue))
                                {
                                    // [roll]
                                    controlIndex++;
                                    lineIndex = 0;
                                    pendingRunStart = false;
                                }
                                else
                                {
                                    pendingRunStart = false;
                                }

                                break;
                            }

                            if (matched)
                                continue;

                            Data.WriteByte(0x10);
                            visiblePos += 1;
                            i += 1;
                            pendingRunStart = false;
                            continue;
                        }

                        if (i + 2 <= Dialog.Length &&
                            Dialog.Substring(i, 2) == "\r\n")
                        {
                            Data.WriteByte(0);
                            visiblePos += 2;
                            i += 2;
                            lineIndex++;
                            pendingRunStart = true;
                            continue;
                        }

                        if (Dialog[i] == '\n')
                        {
                            Data.WriteByte(0);
                            visiblePos += 1;
                            i += 1;
                            lineIndex++;
                            pendingRunStart = true;
                            continue;
                        }

                        {
                            int charValue = -1;
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
                            pendingRunStart = false;
                        }

                    } // end character loop

                    while (nextAnchored < anchored.Count)
                    {
                        WriteInlineHidden(
                            Data, Writer,
                            ref Header2, ref Mask2,
                            ref Position2, ref HeaderPosition2,
                            anchored[nextAnchored++]);
                    }

                    while (nextLoose < loose.Count)
                    {
                        WriteInlineHidden(
                            Data, Writer,
                            ref Header2, ref Mask2,
                            ref Position2, ref HeaderPosition2,
                            loose[nextLoose++]);
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

                } // end original slot loop

                Align(Data, 4);
                Pointer.Write((uint)Data.Length);
                Align(Data, 0x10);
                Align(Pointers, 0x10);

                Output.Data = Data.ToArray();
                Output.Pointers = Pointers.ToArray();
            }

            return Output;
        }

        private static EncodedText EncodeInternalHex(string text)
        {
            EncodedText Output = new EncodedText();
            string[] Table = GetTable();
            string EndMarker = Table[2];

            text = text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

            string endTag = "[end]";
            string trimmed = text.TrimEnd('\r', '\n', ' ', '\t');
            if (trimmed.EndsWith(endTag))
                text = trimmed + "\r\n\r\n";

            string[] visibleDialogs = text.Split(
                new string[] { EndMarker },
                StringSplitOptions.None);

            int visibleCount = visibleDialogs.Length;
            while (visibleCount > 0 &&
                   visibleDialogs[visibleCount - 1] == string.Empty)
                visibleCount--;

            using (MemoryStream Data = new MemoryStream())
            using (MemoryStream Pointers = new MemoryStream())
            {
                BinaryWriter Writer = new BinaryWriter(Data);
                BinaryWriter Pointer = new BinaryWriter(Pointers);

                for (int origIdx = 0; origIdx < visibleCount; origIdx++)
                {
                    Align(Data, 4);
                    Pointer.Write((uint)Data.Position);

                    string Dialog = visibleDialogs[origIdx];

                    byte Header2 = 0;
                    int Mask2 = 0;
                    long Position2 = 0;
                    long HeaderPosition2 = Data.Position;

                    int i = 0;

                    while (i <= Dialog.Length)
                    {
                        if (i == Dialog.Length) break;

                        if (i + 7 <= Dialog.Length &&
                            Dialog.Substring(i, 4) == "[var" &&
                            Dialog[i + 6] == ']')
                        {
                            byte vb;
                            if (byte.TryParse(
                                    Dialog.Substring(i + 4, 2),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out vb))
                            {
                                EnsureMaskSlot(
                                    Data,
                                    ref Header2,
                                    ref Mask2,
                                    ref Position2,
                                    ref HeaderPosition2);

                                Data.WriteByte(0x07);
                                Data.WriteByte(vb);
                                i += 7;
                                continue;
                            }
                        }

                        if (i + 4 < Dialog.Length &&
                            Dialog[i] == '[' &&
                            Dialog.Substring(i + 1, 3).ToLower() == "hex")
                        {
                            int close = Dialog.IndexOf(']', i + 4);
                            if (close > i + 4)
                            {
                                string inner = Dialog.Substring(
                                    i + 4, close - (i + 4));
                                int under = inner.IndexOf('_');
                                if (under > 0 && under < inner.Length - 1)
                                {
                                    string primStr = inner.Substring(0, under);
                                    string extraStr = inner.Substring(under + 1);
                                    ushort prim;
                                    byte ext;
                                    if (ushort.TryParse(
                                            primStr, NumberStyles.HexNumber,
                                            CultureInfo.InvariantCulture,
                                            out prim) &&
                                        byte.TryParse(
                                            extraStr, NumberStyles.HexNumber,
                                            CultureInfo.InvariantCulture,
                                            out ext))
                                    {
                                        EnsureMaskSlot(
                                            Data,
                                            ref Header2,
                                            ref Mask2,
                                            ref Position2,
                                            ref HeaderPosition2);

                                        if (prim > 0xFF)
                                        {
                                            Writer.Write(prim);
                                            Header2 |= (byte)Mask2;
                                        }
                                        else
                                        {
                                            Data.WriteByte((byte)(prim & 0xFF));
                                        }
                                        Data.WriteByte(ext);
                                        i = close + 1;
                                        continue;
                                    }
                                }
                            }
                        }

                        EnsureMaskSlot(
                            Data,
                            ref Header2,
                            ref Mask2,
                            ref Position2,
                            ref HeaderPosition2);

                        if (Dialog[i] == '[')
                        {
                            bool matched = false;
                            int charValue = -1;

                            for (int t = 0; t < Table.Length; t++)
                            {
                                string tv = Table[t];
                                if (tv == null) continue;
                                if (i + tv.Length > Dialog.Length)
                                    continue;
                                if (Dialog.Substring(i, tv.Length) != tv)
                                    continue;

                                charValue = t;

                                if (charValue > 0xFF)
                                {
                                    Writer.Write((ushort)charValue);
                                    Header2 |= (byte)Mask2;
                                }
                                else
                                {
                                    Data.WriteByte((byte)charValue);
                                }

                                i += tv.Length;
                                matched = true;
                                break;
                            }

                            if (matched)
                                continue;

                            Data.WriteByte(0x10);
                            i += 1;
                            continue;
                        }

                        if (i + 2 <= Dialog.Length &&
                            Dialog.Substring(i, 2) == "\r\n")
                        {
                            Data.WriteByte(0);
                            i += 2;
                            continue;
                        }

                        if (Dialog[i] == '\n')
                        {
                            Data.WriteByte(0);
                            i += 1;
                            continue;
                        }

                        {
                            int charValue = -1;
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
