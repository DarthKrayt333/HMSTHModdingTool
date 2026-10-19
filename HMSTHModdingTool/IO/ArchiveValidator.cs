using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    /// Read-only structural validator for the archive formats understood by
    /// HMSTHModdingTool. This class deliberately does not repair or rewrite input.
    /// </summary>
    public static class ArchiveValidator
    {
        private sealed class Issue
        {
            public string Severity;
            public string Code;
            public string Message;
            public long? Offset;
        }

        private sealed class Report
        {
            public string FilePath;
            public string Format;
            public long FileSize;
            public readonly List<Issue> Issues = new List<Issue>();

            public int ErrorCount
            {
                get
                {
                    int count = 0;
                    foreach (Issue issue in Issues)
                        if (issue.Severity == "error") count++;
                    return count;
                }
            }

            public int WarningCount
            {
                get
                {
                    int count = 0;
                    foreach (Issue issue in Issues)
                        if (issue.Severity == "warning") count++;
                    return count;
                }
            }
        }

        /// <summary>
        /// Validates a single file and prints a concise report. If jsonPath is set,
        /// writes a machine-readable report without modifying the input file.
        /// Returns true only when the archive has no validation errors and the
        /// optional report could be written.
        /// </summary>
        public static bool Verify(string filePath, string jsonPath)
        {
            var report = new Report
            {
                FilePath = filePath ?? string.Empty,
                Format = "unknown"
            };

            if (string.IsNullOrWhiteSpace(filePath))
            {
                AddError(report, "INPUT_PATH_EMPTY", "No input file was specified.", null);
                PrintReport(report);
                return false;
            }

            string fullInputPath;
            try
            {
                fullInputPath = Path.GetFullPath(filePath);
            }
            catch (Exception ex)
            {
                AddError(report, "INPUT_PATH_INVALID", ex.Message, null);
                PrintReport(report);
                return false;
            }

            report.FilePath = fullInputPath;
            if (!File.Exists(fullInputPath))
            {
                AddError(report, "FILE_NOT_FOUND", "Input file does not exist.", null);
                PrintReport(report);
                return false;
            }

            try
            {
                using (var stream = new FileStream(fullInputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    report.FileSize = stream.Length;
                    if (stream.Length < 4)
                    {
                        report.Format = DetectHdaByExtension(fullInputPath) ? "HDA" : "unknown";
                        AddError(report, "FILE_TOO_SHORT", "File is too short to contain a format signature.", 0);
                    }
                    else
                    {
                        byte[] magic = ReadBytes(reader, 0, 4);
                        string signature = Encoding.ASCII.GetString(magic);

                        if (signature == "RDTB")
                        {
                            report.Format = "RDTB";
                            ValidateRdtb(reader, report);
                        }
                        else if (signature == "GDTB")
                        {
                            report.Format = "GDTB";
                            ValidateGdtb(reader, report);
                        }
                        else if (signature == "SRDB")
                        {
                            report.Format = "SRDB";
                            ValidateSrdb(reader, report);
                        }
                        else if (DetectHdaByExtension(fullInputPath) || LooksLikeHdaHeader(reader, stream.Length))
                        {
                            report.Format = "HDA";
                            ValidateHda(reader, report);
                        }
                        else
                        {
                            AddError(report, "UNKNOWN_FORMAT", "Unrecognized file signature. Supported formats: HDA, RDTB, GDTB, SRDB.", 0);
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                AddError(report, "IO_ERROR", ex.Message, null);
            }
            catch (UnauthorizedAccessException ex)
            {
                AddError(report, "ACCESS_DENIED", ex.Message, null);
            }
            catch (Exception ex)
            {
                // Malformed files should produce a diagnostic, not an unhandled parser exception.
                AddError(report, "VALIDATION_EXCEPTION", ex.GetType().Name + ": " + ex.Message, null);
            }

            bool reportWritten = true;
            if (!string.IsNullOrWhiteSpace(jsonPath))
                reportWritten = TryWriteJsonReport(report, jsonPath);

            PrintReport(report);
            if (!reportWritten)
                return false;
            return report.ErrorCount == 0;
        }

        private static bool DetectHdaByExtension(string path)
        {
            return string.Equals(Path.GetExtension(path), ".hda", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeHdaHeader(BinaryReader reader, long length)
        {
            if (length < 16) return false;
            uint baseOffset = ReadUInt32(reader, 0);
            if (baseOffset < 0x10 || baseOffset > 0x1000) return false;
            for (int i = 4; i < 16; i++)
                if (ReadByte(reader, i) != 0) return false;
            return true;
        }

        private static void ValidateHda(BinaryReader reader, Report report)
        {
            Stream stream = reader.BaseStream;
            if (stream.Length < 0x14)
            {
                AddError(report, "HDA_HEADER_TRUNCATED", "HDA header or first offset is truncated.", stream.Length);
                return;
            }

            uint baseOffset = ReadUInt32(reader, 0);
            if (baseOffset < 0x10 || baseOffset > 0x1000 || !CanRead(stream, baseOffset, 4))
            {
                AddError(report, "HDA_BASE_OFFSET_INVALID", "Base offset is outside the supported header range.", 0);
                return;
            }
            if (baseOffset != 0x10)
                AddWarning(report, "HDA_NONSTANDARD_BASE_OFFSET", "Base offset differs from the common 0x10 value; validation continues.", 0);

            uint tableSize = ReadUInt32(reader, baseOffset);
            if (tableSize < 4 || tableSize % 4 != 0)
            {
                AddError(report, "HDA_TABLE_SIZE_INVALID", "The first offset does not describe a valid 4-byte offset table.", baseOffset);
                return;
            }
            if (!CanRead(stream, baseOffset, tableSize))
            {
                AddError(report, "HDA_TABLE_OUT_OF_BOUNDS", "The offset table extends beyond the file.", baseOffset);
                return;
            }

            long slotCountLong = tableSize / 4;
            if (slotCountLong > int.MaxValue)
            {
                AddError(report, "HDA_TABLE_TOO_LARGE", "The offset table is too large to validate safely.", baseOffset);
                return;
            }

            var entryRanges = new List<Tuple<long, long>>();
            uint previousOffset = 0;
            bool havePreviousOffset = false;
            int nonEmptyEntries = 0;

            for (int i = 0; i < (int)slotCountLong; i++)
            {
                long tablePosition = (long)baseOffset + (long)i * 4;
                uint relativeOffset = ReadUInt32(reader, tablePosition);
                if (relativeOffset == 0) continue;

                if (relativeOffset < tableSize)
                {
                    AddError(report, "HDA_ENTRY_OVERLAPS_TABLE", "Entry " + i + " points inside the offset table.", tablePosition);
                    continue;
                }
                if (havePreviousOffset && relativeOffset < previousOffset)
                    AddWarning(report, "HDA_OFFSETS_UNSORTED", "Non-empty entry offsets are not in ascending order.", tablePosition);
                if (havePreviousOffset && relativeOffset == previousOffset)
                    AddWarning(report, "HDA_DUPLICATE_OFFSET", "Two table slots reference the same entry offset.", tablePosition);
                previousOffset = relativeOffset;
                havePreviousOffset = true;

                long absoluteOffset = (long)baseOffset + relativeOffset;
                if (!CanRead(stream, absoluteOffset, 0x10))
                {
                    AddError(report, "HDA_ENTRY_HEADER_OUT_OF_BOUNDS", "Entry " + i + " header extends beyond the file.", absoluteOffset);
                    continue;
                }

                uint compression = ReadUInt32(reader, absoluteOffset);
                uint decompressedSize = ReadUInt32(reader, absoluteOffset + 4);
                uint storedSize = ReadUInt32(reader, absoluteOffset + 8);
                if (compression > 1)
                    AddWarning(report, "HDA_COMPRESSION_UNKNOWN", "Entry " + i + " uses compression flag " + compression + ".", absoluteOffset);
                if (storedSize == 0)
                {
                    AddWarning(report, "HDA_EMPTY_ENTRY", "Entry " + i + " has a zero stored size.", absoluteOffset + 8);
                    continue;
                }
                if (!CanRead(stream, absoluteOffset + 0x10, storedSize))
                {
                    AddError(report, "HDA_ENTRY_DATA_OUT_OF_BOUNDS", "Entry " + i + " payload extends beyond the file.", absoluteOffset + 8);
                    continue;
                }
                if (decompressedSize == 0)
                    AddWarning(report, "HDA_ZERO_DECOMPRESSED_SIZE", "Entry " + i + " has a non-empty payload but declares zero decompressed bytes.", absoluteOffset + 4);

                long end = absoluteOffset + 0x10L + storedSize;
                entryRanges.Add(Tuple.Create(absoluteOffset, end));
                nonEmptyEntries++;
            }

            entryRanges.Sort(delegate(Tuple<long, long> a, Tuple<long, long> b) { return a.Item1.CompareTo(b.Item1); });
            for (int i = 1; i < entryRanges.Count; i++)
                if (entryRanges[i].Item1 < entryRanges[i - 1].Item2)
                    AddError(report, "HDA_ENTRIES_OVERLAP", "Entry payload ranges overlap.", entryRanges[i].Item1);

            if (nonEmptyEntries == 0)
                AddWarning(report, "HDA_NO_PAYLOADS", "No non-empty entry payloads were found.", baseOffset);
        }

        private static void ValidateRdtb(BinaryReader reader, Report report)
        {
            Stream stream = reader.BaseStream;
            const int headerSize = 0x48;
            const int slotCount = 14;
            if (stream.Length < headerSize)
            {
                AddError(report, "RDTB_HEADER_TRUNCATED", "RDTB header/14-slot offset table is truncated; expected at least 0x48 bytes.", stream.Length);
                return;
            }

            uint ptrCount = ReadUInt16(reader, 0x0C);
            uint boneCount = ReadUInt16(reader, 0x0E);
            var uniqueOffsets = new HashSet<long>();
            uint previous = 0;
            bool havePrevious = false;

            for (int i = 0; i < slotCount; i++)
            {
                long pos = 0x10L + i * 4L;
                uint offset = ReadUInt32(reader, pos);
                if (offset == 0 || offset == 0xFFFFFFFF) continue;

                if (offset < headerSize)
                {
                    AddError(report, "RDTB_OFFSET_BEFORE_DATA", "Slot " + i + " points inside the RDTB header.", pos);
                    continue;
                }
                if (offset > stream.Length)
                {
                    AddError(report, "RDTB_OFFSET_OUT_OF_BOUNDS", "Slot " + i + " points beyond the end of the file.", pos);
                    continue;
                }
                if (offset == stream.Length)
                    AddWarning(report, "RDTB_OFFSET_AT_EOF", "Slot " + i + " points exactly to EOF and has no chunk data.", pos);

                if (havePrevious && offset < previous)
                    AddWarning(report, "RDTB_OFFSETS_UNSORTED", "Valid slot offsets are not in ascending order; this can make chunk-boundary assumptions ambiguous.", pos);
                previous = offset;
                havePrevious = true;
                uniqueOffsets.Add(offset);
            }

            if (uniqueOffsets.Count == 0)
                AddWarning(report, "RDTB_NO_CHUNKS", "No usable chunk offsets were found.", 0x10);
            if (ptrCount > stream.Length / 4)
                AddWarning(report, "RDTB_POINTER_COUNT_UNUSUAL", "Pointer count is unusually large for this file.", 0x0C);
            if (boneCount > 4096)
                AddWarning(report, "RDTB_BONE_COUNT_UNUSUAL", "Bone count is unusually large; verify that the header is correct.", 0x0E);
        }

        private static void ValidateGdtb(BinaryReader reader, Report report)
        {
            Stream stream = reader.BaseStream;
            const int headerSize = 0x18;
            if (stream.Length < headerSize)
            {
                AddError(report, "GDTB_HEADER_TRUNCATED", "GDTB header is truncated; expected at least 0x18 bytes.", stream.Length);
                return;
            }

            int textureCount = ReadInt32(reader, 8);
            int imageTable = ReadInt32(reader, 12);
            int paletteTable = ReadInt32(reader, 16);
            int formatTable = ReadInt32(reader, 20);

            if (textureCount < 0)
            {
                AddError(report, "GDTB_TEXTURE_COUNT_INVALID", "Texture count is negative.", 8);
                return;
            }
            if (textureCount == 0)
            {
                AddWarning(report, "GDTB_EMPTY_ARCHIVE", "GDTB declares zero textures.", 8);
                return;
            }

            long tableBytes = (long)textureCount * 4L;
            bool imageTableValid = ValidateTableBounds(stream, imageTable, tableBytes, report, "GDTB_IMAGE_TABLE", 12);
            bool formatTableValid = ValidateTableBounds(stream, formatTable, tableBytes, report, "GDTB_FORMAT_TABLE", 20);
            bool paletteTableValid = paletteTable >= headerSize && paletteTable < stream.Length;
            if (!paletteTableValid)
                AddWarning(report, "GDTB_PALETTE_TABLE_OFFSET", "Palette table offset is outside the file; this is only an error if a texture uses a palette.", 16);

            if (!imageTableValid || !formatTableValid) return;

            for (int i = 0; i < textureCount; i++)
            {
                long imagePtrPos = (long)imageTable + i * 4L;
                int imageRelative = ReadInt32(reader, imagePtrPos);
                long imageOffset = (long)imageTable + imageRelative;
                if (imageRelative < 0 || !CanRead(stream, imageOffset, 1))
                {
                    AddError(report, "GDTB_IMAGE_OFFSET_INVALID", "Texture " + i + " image payload offset is outside the file.", imagePtrPos);
                    continue;
                }
                if (imageOffset < (long)imageTable + tableBytes)
                    AddError(report, "GDTB_IMAGE_OVERLAPS_TABLE", "Texture " + i + " image payload starts inside the image offset table.", imagePtrPos);

                long formatPtrPos = (long)formatTable + i * 4L;
                int formatRelative = ReadInt32(reader, formatPtrPos);
                long formatOffset = (long)formatTable + formatRelative;
                if (formatRelative < 0 || !CanRead(stream, formatOffset, 8))
                {
                    AddError(report, "GDTB_FORMAT_OFFSET_INVALID", "Texture " + i + " format metadata is outside the file.", formatPtrPos);
                    continue;
                }
                if (formatOffset < (long)formatTable + tableBytes)
                    AddError(report, "GDTB_FORMAT_OVERLAPS_TABLE", "Texture " + i + " format metadata starts inside the format offset table.", formatPtrPos);

                int width = ReadUInt16(reader, formatOffset);
                int height = ReadUInt16(reader, formatOffset + 2);
                int depthCode = ReadInt32(reader, formatOffset + 4);
                int bitDepth = BitDepthFromCode(depthCode);
                if (width <= 0 || height <= 0)
                {
                    AddError(report, "GDTB_DIMENSIONS_INVALID", "Texture " + i + " has zero width or height.", formatOffset);
                    continue;
                }
                if (bitDepth == 0)
                {
                    AddError(report, "GDTB_BIT_DEPTH_UNKNOWN", "Texture " + i + " uses unsupported bit-depth code 0x" + depthCode.ToString("X", CultureInfo.InvariantCulture) + ".", formatOffset + 4);
                    continue;
                }

                long imageSize = ((long)width * height * bitDepth + 7L) / 8L;
                if (!CanRead(stream, imageOffset, imageSize))
                    AddError(report, "GDTB_IMAGE_DATA_TRUNCATED", "Texture " + i + " pixel data exceeds the file boundary (" + width + "x" + height + ", " + bitDepth + " bpp).", imageOffset);

                if (bitDepth == 4 || bitDepth == 8)
                {
                    if (!paletteTableValid || !CanRead(stream, (long)paletteTable + i * 4L, 4))
                    {
                        AddError(report, "GDTB_PALETTE_POINTER_MISSING", "Texture " + i + " requires a palette pointer, but the palette table is unavailable.", paletteTable);
                        continue;
                    }
                    long palettePtrPos = (long)paletteTable + i * 4L;
                    int paletteRelative = ReadInt32(reader, palettePtrPos);
                    long paletteOffset = (long)paletteTable + paletteRelative;
                    long paletteSize = bitDepth == 4 ? 0x40L : 0x400L;
                    if (paletteRelative < 0 || !CanRead(stream, paletteOffset, paletteSize))
                        AddError(report, "GDTB_PALETTE_DATA_TRUNCATED", "Texture " + i + " palette data exceeds the file boundary.", palettePtrPos);
                    else if (paletteOffset < (long)paletteTable + tableBytes)
                        AddError(report, "GDTB_PALETTE_OVERLAPS_TABLE", "Texture " + i + " palette data starts inside the palette offset table.", palettePtrPos);
                }
            }
        }

        private static void ValidateSrdb(BinaryReader reader, Report report)
        {
            Stream stream = reader.BaseStream;
            const int offsetTableStart = 0x0C;
            if (stream.Length < 0x10)
            {
                AddError(report, "SRDB_HEADER_TRUNCATED", "SRDB header or first chunk offset is truncated.", stream.Length);
                return;
            }

            uint firstChunkOffset = ReadUInt32(reader, offsetTableStart);
            if (firstChunkOffset < 0x10 || firstChunkOffset > stream.Length)
            {
                AddError(report, "SRDB_FIRST_OFFSET_INVALID", "First chunk offset must be after the header and within the file.", offsetTableStart);
                return;
            }
            if (((long)firstChunkOffset - offsetTableStart) % 4L != 0)
            {
                AddError(report, "SRDB_TABLE_SIZE_INVALID", "First chunk offset does not align with the 4-byte offset table size.", offsetTableStart);
                return;
            }

            int offsetCount = (int)(((long)firstChunkOffset - offsetTableStart) / 4L);
            if (offsetCount <= 0 || !CanRead(stream, offsetTableStart, (long)offsetCount * 4L))
            {
                AddError(report, "SRDB_OFFSET_TABLE_INVALID", "SRDB chunk offset table is invalid or truncated.", offsetTableStart);
                return;
            }

            uint previous = 0;
            for (int i = 0; i < offsetCount; i++)
            {
                long pos = offsetTableStart + i * 4L;
                uint offset = ReadUInt32(reader, pos);
                if (offset < firstChunkOffset || offset >= stream.Length)
                {
                    AddError(report, "SRDB_CHUNK_OFFSET_OUT_OF_BOUNDS", "Chunk " + i + " starts outside the chunk data region.", pos);
                    continue;
                }
                if (i == 0 && offset != firstChunkOffset)
                    AddError(report, "SRDB_FIRST_CHUNK_MISMATCH", "The first table entry does not match the first chunk offset.", pos);
                if (i > 0 && offset <= previous)
                    AddError(report, "SRDB_CHUNK_OFFSETS_NOT_INCREASING", "Chunk offsets must be strictly increasing.", pos);
                previous = offset;
            }
        }

        private static bool ValidateTableBounds(Stream stream, int offset, long length, Report report, string code, long fieldOffset)
        {
            if (offset < 0x18 || !CanRead(stream, offset, length))
            {
                AddError(report, code + "_OUT_OF_BOUNDS", "Table offset/size lies outside the file.", fieldOffset);
                return false;
            }
            return true;
        }

        private static int BitDepthFromCode(int code)
        {
            switch (code)
            {
                case 0x14: return 4;
                case 0x13: return 8;
                case 0x02: return 16;
                case 0x01: return 24;
                case 0x11: return 32;
                default: return 0;
            }
        }

        private static bool CanRead(Stream stream, long offset, long count)
        {
            return offset >= 0 && count >= 0 && offset <= stream.Length && count <= stream.Length - offset;
        }

        private static byte ReadByte(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadByte();
        }

        private static ushort ReadUInt16(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadUInt16();
        }

        private static uint ReadUInt32(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadUInt32();
        }

        private static int ReadInt32(BinaryReader reader, long offset)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadInt32();
        }

        private static byte[] ReadBytes(BinaryReader reader, long offset, int count)
        {
            reader.BaseStream.Position = offset;
            return reader.ReadBytes(count);
        }

        private static void AddError(Report report, string code, string message, long? offset)
        {
            report.Issues.Add(new Issue { Severity = "error", Code = code, Message = message, Offset = offset });
        }

        private static void AddWarning(Report report, string code, string message, long? offset)
        {
            report.Issues.Add(new Issue { Severity = "warning", Code = code, Message = message, Offset = offset });
        }

        private static void PrintReport(Report report)
        {
            Console.WriteLine();
            Console.WriteLine("Archive validation: " + report.Format);
            Console.WriteLine("File: " + report.FilePath);
            Console.WriteLine("Size: " + report.FileSize.ToString("N0", CultureInfo.InvariantCulture) + " bytes");
            Console.WriteLine(new string('-', 64));

            if (report.Issues.Count == 0)
                Console.WriteLine("[PASS] No structural issues detected.");
            else
            {
                foreach (Issue issue in report.Issues)
                {
                    string location = issue.Offset.HasValue ? " @ 0x" + issue.Offset.Value.ToString("X", CultureInfo.InvariantCulture) : string.Empty;
                    Console.WriteLine("[" + issue.Severity.ToUpperInvariant() + "] " + issue.Code + location + ": " + issue.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Result: " + (report.ErrorCount == 0 ? "PASS" : "FAIL") +
                " | Errors: " + report.ErrorCount + " | Warnings: " + report.WarningCount);
        }

        private static bool TryWriteJsonReport(Report report, string jsonPath)
        {
            string fullJsonPath;
            try
            {
                fullJsonPath = Path.GetFullPath(jsonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Invalid JSON report path: " + ex.Message);
                return false;
            }

            if (string.Equals(fullJsonPath, report.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[ERROR] JSON report path must not be the same as the input file.");
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(fullJsonPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(fullJsonPath, ToJson(report), new UTF8Encoding(false));
                Console.WriteLine("JSON report: " + fullJsonPath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] Could not write JSON report: " + ex.Message);
                return false;
            }
        }

        private static string ToJson(Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"file\": \"" + JsonEscape(report.FilePath) + "\",");
            sb.AppendLine("  \"format\": \"" + JsonEscape(report.Format) + "\",");
            sb.AppendLine("  \"size\": " + report.FileSize.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"valid\": " + (report.ErrorCount == 0 ? "true" : "false") + ",");
            sb.AppendLine("  \"errors\": " + report.ErrorCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"warnings\": " + report.WarningCount.ToString(CultureInfo.InvariantCulture) + ",");
            sb.AppendLine("  \"issues\": [");
            for (int i = 0; i < report.Issues.Count; i++)
            {
                Issue issue = report.Issues[i];
                sb.Append("    { \"severity\": \"").Append(JsonEscape(issue.Severity))
                    .Append("\", \"code\": \"").Append(JsonEscape(issue.Code))
                    .Append("\", \"message\": \"").Append(JsonEscape(issue.Message)).Append("\"");
                if (issue.Offset.HasValue)
                    sb.Append(", \"offset\": ").Append(issue.Offset.Value.ToString(CultureInfo.InvariantCulture));
                sb.Append(" }");
                if (i + 1 < report.Issues.Count) sb.Append(',');
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return string.Empty;
            var sb = new StringBuilder(value.Length + 16);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
