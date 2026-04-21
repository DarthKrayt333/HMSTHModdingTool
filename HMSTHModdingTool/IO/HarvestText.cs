using System;
using System.Globalization;
using System.IO;
using System.Text;

using HMSTHModdingTool.Properties;

namespace HMSTHModdingTool.IO
{
    class HarvestText
    {
        /// <summary>
        ///     Decodes a text from Harvest Moon: Save the Homeland.
        /// </summary>
        /// <param name="Data">The full path to the data file</param>
        /// <param name="Pointers">The full path to the pointers file</param>
        /// <returns>The decoded text as a string</returns>
        public static string Decode(string Data, string Pointers)
        {
            using (FileStream DataStream = new FileStream(Data, FileMode.Open))
            {
                using (FileStream PointersStream = new FileStream(Pointers, FileMode.Open))
                {
                    return Decode(DataStream, PointersStream);
                }
            }
        }

        /// <summary>
        ///     Decodes a text from Harvest Moon: Save the Homeland.
        /// </summary>
        /// <param name="Data">The Stream with the text to be decoded</param>
        /// <param name="Pointers">The Stream with the pointers to the text</param>
        /// <returns>The decoded text as a string</returns>
        public static string Decode(Stream Data, Stream Pointers)
        {
            BinaryReader Reader = new BinaryReader(Data);
            BinaryReader Pointer = new BinaryReader(Pointers);
            StringBuilder Output = new StringBuilder();

            string[] Table = GetTable();

            // Table[2] == "[end]\n\n"  (the end-of-dialog marker as it appears in text)
            string EndMarker = Table[2]; // "[end]\n\n"

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

                // Read tokens until we hit the [end] marker (value == 2)
                // or run out of data.
                while (Data.Position < Data.Length && Value != 2)
                {
                    if ((Mask >>= 1) == 0)
                    {
                        Header = (byte)Data.ReadByte();
                        Mask = 0x80;
                    }

                    // Read 8 or 16 bit character
                    if ((Header & Mask) == 0)
                    {
                        Value = (byte)Data.ReadByte();
                    }
                    else
                    {
                        Value = Reader.ReadUInt16();
                    }

                    // Append character (or hex code) to output,
                    // but do NOT append [end] here — we append it unconditionally below.
                    if (Value == 2)
                    {
                        // [end] found — exit inner loop; marker appended below.
                        break;
                    }
                    else if (Value == 7)
                    {
                        Output.Append(string.Format("[var]\\x{0:X4}", Data.ReadByte()));
                    }
                    else
                    {
                        if (Table[Value] == null)
                        {
                            Output.Append(string.Format("\\x{0:X4}", Value));
                            Output.Append(string.Format("\\x{0:X4}", Data.ReadByte()));
                        }
                        else
                        {
                            Output.Append(Table[Value]);
                        }
                    }
                }

                // Always emit [end]\n\n after every dialog slot,
                // even if the dialog body was empty (consecutive [end] entries).
                Output.Append(EndMarker);
            }

            return Output.ToString();
        }

        /// <summary>
        ///     Represents the encoded Harvest Moon text.
        ///     "Data" contains the encoded text itself.
        ///     "Pointers" contains pointers to the dialogs.
        /// </summary>
        public struct EncodedText
        {
            public byte[] Data;
            public byte[] Pointers;
        }

        /// <summary>
        ///     Encodes a text from Harvest Moon: Save the Homeland.
        /// </summary>
        /// <param name="Text">The string to be encoded</param>
        /// <param name="Data">The full path to the data file that should be created</param>
        /// <param name="Pointers">The full path to the pointers file that should be created</param>
        public static void Encode(string Text, string Data, string Pointers)
        {
            EncodedText Encoded = Encode(Text);
            File.WriteAllBytes(Data, Encoded.Data);
            File.WriteAllBytes(Pointers, Encoded.Pointers);
        }

        /// <summary>
        ///     Encodes a text from Harvest Moon: Save the Homeland.
        /// </summary>
        /// <param name="Text">The string to be encoded</param>
        /// <returns>The encoded data</returns>
        public static EncodedText Encode(string Text)
        {
            EncodedText Output = new EncodedText();
            string[] Table = GetTable();

            // Table[2] is "[end]\n\n" — the separator between dialogs in the text file.
            string EndMarker = Table[2];

            using (MemoryStream Data = new MemoryStream())
            using (MemoryStream Pointers = new MemoryStream())
            {
                BinaryWriter Writer = new BinaryWriter(Data);
                BinaryWriter Pointer = new BinaryWriter(Pointers);

                // FIX: Use StringSplitOptions.None so that empty entries
                //      (produced by two consecutive [end] markers) are preserved.
                //      Without this fix, RemoveEmptyEntries silently drops them,
                //      meaning consecutive [end][end] only encodes one [end].
                string[] Dialogs = Text.Split(
                    new string[] { EndMarker },
                    StringSplitOptions.None);

                // Determine how many dialogs to actually encode.
                // The split of "...[end]\n\n[end]\n\n" always produces a final
                // empty trailing entry (after the last [end]) which we must skip,
                // otherwise we'd write one extra spurious dialog.
                // We detect the trailing empties conservatively:
                //   - If the original text ends with EndMarker, the last split
                //     entry is empty and should be ignored.
                //   - But intermediate empty entries (consecutive [end]) MUST
                //     be preserved and encoded.
                int DialogCount = Dialogs.Length;
                // Trim exactly the empty entries that are purely trailing
                // (i.e. the text ended with one or more EndMarkers that produce
                //  empty strings at the tail of the array with no real content).
                while (DialogCount > 0 && Dialogs[DialogCount - 1] == string.Empty)
                {
                    DialogCount--;
                }

                for (int d = 0; d < DialogCount; d++)
                {
                    string Dialog = Dialogs[d];

                    Align(Data, 4);
                    Pointer.Write((uint)Data.Position);

                    byte Header = 0;
                    int Mask = 0;
                    long Position = 0;
                    long HeaderPosition = Data.Position;

                    // If Dialog is empty (consecutive [end] in source text),
                    // we still need to write the end-of-dialog marker below.
                    // The inner encoding loop simply has nothing to iterate.

                    int i = 0;
                    while (i < Dialog.Length)
                    {
                        // New header byte every 8 tokens
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

                        // ── Line breaks ───────────────────────────────────────────────
                        if (i + 2 <= Dialog.Length && Dialog.Substring(i, 2) == "\r\n")
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

                        // ── [var]\xNNNN  (NNNN is 4 hex digits) ──────────────────────
                        if (i + 11 <= Dialog.Length &&
                            Dialog.Substring(i, 5) == "[var]" &&
                            Dialog.Substring(i + 5, 2) == "\\x")
                        {
                            string hexNNNN = Dialog.Substring(i + 7, 4);
                            ushort varWord = ushort.Parse(hexNNNN, NumberStyles.HexNumber);
                            byte varByte = (byte)(varWord & 0xFF);

                            Data.WriteByte(7);
                            Data.WriteByte(varByte);

                            i += 11;
                            continue;
                        }

                        // ── \xNNNN\xMMMM pair ─────────────────────────────────────────
                        if (i + 12 <= Dialog.Length &&
                            Dialog.Substring(i, 2) == "\\x" &&
                            Dialog.Substring(i + 6, 2) == "\\x")
                        {
                            string hex1 = Dialog.Substring(i + 2, 4);
                            string hex2 = Dialog.Substring(i + 8, 4);

                            ushort v1 = ushort.Parse(hex1, NumberStyles.HexNumber);
                            ushort v2 = ushort.Parse(hex2, NumberStyles.HexNumber);

                            if (v1 > 0xFF)
                            {
                                Writer.Write(v1);
                                Header |= (byte)Mask;
                            }
                            else
                            {
                                Data.WriteByte((byte)(v1 & 0xFF));
                            }

                            Data.WriteByte((byte)(v2 & 0xFF));

                            i += 12;
                            continue;
                        }

                        // ── Standalone \xNNNN ─────────────────────────────────────────
                        if (i + 6 <= Dialog.Length && Dialog.Substring(i, 2) == "\\x")
                        {
                            string hex = Dialog.Substring(i + 2, 4);
                            ushort v = ushort.Parse(hex, NumberStyles.HexNumber);

                            if (v > 0xFF)
                            {
                                Writer.Write(v);
                                Header |= (byte)Mask;
                            }
                            else
                            {
                                Data.WriteByte((byte)(v & 0xFF));
                            }

                            i += 6;
                            continue;
                        }

                        // ── Normal table parsing ──────────────────────────────────────
                        int charValue = -1;

                        if (Dialog[i] == '[')
                        {
                            bool matched = false;
                            for (int TblIndex = 0; TblIndex < Table.Length; TblIndex++)
                            {
                                string TblValue = Table[TblIndex];
                                if (TblValue == null) continue;
                                if (i + TblValue.Length > Dialog.Length) continue;

                                if (Dialog.Substring(i, TblValue.Length) == TblValue)
                                {
                                    charValue = TblIndex;
                                    i += TblValue.Length;
                                    matched = true;
                                    break;
                                }
                            }

                            if (!matched)
                            {
                                Data.WriteByte(0x10);
                                i += 1;
                            }
                            else
                            {
                                if (charValue > 0xFF)
                                {
                                    Writer.Write((ushort)charValue);
                                    Header |= (byte)Mask;
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
                                    Header |= (byte)Mask;
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
                    } // end while (i < Dialog.Length)

                    // ── End-of-dialog marker ──────────────────────────────────────────
                    // Write the pending header byte if needed, then emit value 0x02.
                    // This must happen even when Dialog is empty (consecutive [end]).

                    // Flush pending header
                    Position = Data.Position;
                    if (Header != 0)
                    {
                        Data.Seek(HeaderPosition, SeekOrigin.Begin);
                        Data.WriteByte(Header);
                        Data.Seek(Position, SeekOrigin.Begin);
                    }

                    // The end marker (value == 2) is a single uncompressed byte.
                    // It needs its own mask slot.
                    if ((Mask >>= 1) == 0)
                    {
                        // Need a fresh header byte for the end marker
                        Data.WriteByte(0);          // header placeholder (value 0 = all 8-bit)
                        HeaderPosition = Data.Position - 1;
                        Mask = 0x80;
                    }

                    // Write the end-of-dialog byte (0x02) as an 8-bit token
                    // (mask bit stays 0, no header bit set).
                    Data.WriteByte(2);

                    // Finalize the last header byte
                    Position = Data.Position;
                    // Header for the end-marker slot is always 0 (8-bit token),
                    // so we only need to seek back if there's something pending.
                    // (Already flushed above; this is a no-op but kept for safety.)
                    Data.Seek(Position, SeekOrigin.Begin);

                } // end foreach Dialog

                Align(Data, 4);
                Pointer.Write((uint)Data.Length);
                Align(Data, 0x10);
                Align(Pointers, 0x10);

                Output.Data = Data.ToArray();
                Output.Pointers = Pointers.ToArray();
            }

            return Output;
        }

        /// <summary>
        ///     Encodes a chunk of normal text using the game's compression.
        /// </summary>
        private static byte[] EncodeTextChunk(string Text, string[] Table)
        {
            using (MemoryStream Data = new MemoryStream())
            {
                BinaryWriter Writer = new BinaryWriter(Data);

                byte Header = 0;
                int Mask = 0;
                long Position = 0;
                long HeaderPosition = Data.Position;

                for (int Index = 0; Index < Text.Length; Index++)
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

                    if (Index + 2 <= Text.Length && Text.Substring(Index, 2) == "\r\n")
                    {
                        Data.WriteByte(0);
                        Index++;
                    }
                    else if (Text[Index] == '\n')
                    {
                        Data.WriteByte(0);
                    }
                    else
                    {
                        int Value = -1;
                        string Character = Text.Substring(Index, 1);

                        if (Character == "[")
                        {
                            for (int TblIndex = 0; TblIndex < Table.Length; TblIndex++)
                            {
                                string TblValue = Table[TblIndex];
                                if (TblValue == null || Index + TblValue.Length > Text.Length) continue;

                                if (Text.Substring(Index, TblValue.Length) == TblValue)
                                {
                                    Value = TblIndex;
                                    Index += TblValue.Length - 1;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Value = Array.IndexOf(Table, Character);
                        }

                        if (Value > -1)
                        {
                            if (Value > 0xff)
                            {
                                Writer.Write((ushort)Value);
                                Header |= (byte)Mask;
                            }
                            else
                            {
                                Data.WriteByte((byte)Value);
                            }
                        }
                        else
                        {
                            Data.WriteByte(0x10);
                        }
                    }
                }

                Position = Data.Position;
                if (Header != 0)
                {
                    Data.Seek(HeaderPosition, SeekOrigin.Begin);
                    Data.WriteByte(Header);
                }
                Data.Seek(Position, SeekOrigin.Begin);
                if (Mask == 1) Data.WriteByte(0);

                return Data.ToArray();
            }
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
            while ((Stream.Position & Mask) != 0) Stream.WriteByte(0);
        }
    }
}