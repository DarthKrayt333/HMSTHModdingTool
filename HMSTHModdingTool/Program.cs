using HMSTHModdingTool;
using HMSTHModdingTool.BMP;
using HMSTHModdingTool.BoyMods;
using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.IO;
using HMSTHModdingTool.IO.Compression;
using HMSTHModdingTool.RDTB;
using HMSTHModdingTool.SRDB;
using System;
using System.Collections.Generic;
using System.IO;

namespace HMSTHModdingTool
{
    class Program
    {
        // ─────────────────────────────────────
        // VERSION INFO
        // ─────────────────────────────────────
        const string TOOL_NAME =
            "HMSTHModdingTool original as" +
            " HDATextTool by gdkchan";
        const string TOOL_VERSION =
            "v1.5.9-Beta";
        const string TOOL_AUTHOR =
            "gdkchan + DarthKrayt333" +
            " & HMSTH Community";

        // ═════════════════════════════════════
        // MAIN
        // ═════════════════════════════════════
        static void Main(string[] args)
        {
            // ─────────────────────────────────
            // INTERACTIVE MODE (double-click)
            // ─────────────────────────────────
            if (args.Length == 0)
            {
                string exeDir =
                    AppDomain.CurrentDomain
                        .BaseDirectory;
                Directory.SetCurrentDirectory(
                    exeDir);

                Console.ForegroundColor =
                    ConsoleColor.Green;
                Console.WriteLine(
                    "Running in interactive" +
                    " mode.");
                Console.ForegroundColor =
                    ConsoleColor.Gray;
                Console.WriteLine(
                    "Working directory: " +
                    exeDir);
                Console.ResetColor();
                Console.WriteLine();

                PrintUsage();

                while (true)
                {
                    Console.ForegroundColor =
                        ConsoleColor.White;
                    Console.Write(
                        "HMSTHModdingTool> ");
                    Console.ResetColor();

                    string input =
                        Console.ReadLine();

                    if (input == null)
                        continue;
                    input = input.Trim();
                    if (input == string.Empty)
                        continue;

                    if (input.ToLower() == "exit" ||
                        input.ToLower() == "quit" ||
                        input.ToLower() == "q")
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Cyan;
                        Console.WriteLine(
                            "Goodbye!");
                        Console.ResetColor();
                        break;
                    }

                    if (input.ToLower() == "help" ||
                        input.ToLower() == "?")
                    {
                        PrintUsage();
                        continue;
                    }

                    if (input.ToLower() == "cls" ||
                        input.ToLower() == "clear")
                    {
                        Console.Clear();
                        Console.ForegroundColor =
                            ConsoleColor.Cyan;
                        Console.WriteLine(
                            TOOL_NAME);
                        Console.WriteLine(
                            "Version " +
                            TOOL_VERSION);
                        Console.WriteLine(
                            "By " + TOOL_AUTHOR);
                        Console.ResetColor();
                        Console.WriteLine();
                        continue;
                    }

                    string[] parsedArgs =
                        ParseInput(input);
                    if (parsedArgs.Length == 0)
                        continue;

                    RunCommand(parsedArgs);
                    Console.WriteLine();
                }

                return;
            }

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(TOOL_NAME);
            Console.WriteLine(
                "Version " + TOOL_VERSION);
            Console.WriteLine(
                "By " + TOOL_AUTHOR);
            Console.ResetColor();
            Console.WriteLine();

            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            RunCommand(args);
        }

        // ═════════════════════════════════════
        // NORMALIZE COMMAND
        // ═════════════════════════════════════
        static string NormalizeCommand(
            string cmd)
        {
            if (cmd.StartsWith("--"))
                return cmd.Substring(2)
                    .ToLower();
            if (cmd.StartsWith("-"))
                return cmd.Substring(1)
                    .ToLower();
            return cmd.ToLower();
        }

        // ═════════════════════════════════════
        // DETECT -JAP FLAG
        // ═════════════════════════════════════
        static bool DetectJapFlag(
            string[] args, out int flagIndex)
        {
            flagIndex = -1;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i]
                    .TrimStart('-')
                    .ToLower();
                if (a == "jap" ||
                    a == "jp" ||
                    a == "japanese" ||
                    a == "ntscj")
                {
                    flagIndex = i;
                    return true;
                }
            }
            return false;
        }

        // ═════════════════════════════════════
        // DETECT VERSION FLAGS
        // -jap, -demo, -jp, etc.
        // ═════════════════════════════════════
        static GameVersion DetectVersionFlag(
            string[] args, out int flagIndex)
        {
            flagIndex = -1;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i]
                    .TrimStart('-')
                    .ToLower();
                if (a == "demo" ||
                    a == "taikenban" ||
                    a == "slpm")
                {
                    flagIndex = i;
                    return GameVersion.DEMO;
                }
                if (a == "jap" ||
                    a == "jp" ||
                    a == "japanese" ||
                    a == "ntscj")
                {
                    flagIndex = i;
                    return GameVersion.JAP;
                }
            }
            return GameVersion.USA;
        }

        /// <summary>
        ///     Removes the jap flag from the
        ///     args array and returns a clean
        ///     copy.
        /// </summary>
        static string[] RemoveJapFlag(
            string[] args, int flagIndex)
        {
            var list =
                new System.Collections
                    .Generic.List<string>(
                        args);
            list.RemoveAt(flagIndex);
            return list.ToArray();
        }

        // ═════════════════════════════════════
        // HELPERS FOR SRDB BATCHES
        // ═════════════════════════════════════
        static bool IsSRDBFile(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                byte[] hdr = new byte[4];
                using (var fs =
                    File.OpenRead(path))
                {
                    fs.Read(hdr, 0, 4);
                }
                return hdr[0] == 0x53 &&
                       hdr[1] == 0x52 &&
                       hdr[2] == 0x44 &&
                       hdr[3] == 0x42;
            }
            catch
            {
                return false;
            }
        }

        static bool IsSRDBFolder(
            string folder)
        {
            if (!Directory.Exists(folder))
                return false;
            string src = Path.Combine(
                folder, "_source.srdb");
            return File.Exists(src);
        }

        static List<byte[]>
            ParseSRDBForCSRDB(
        byte[] data)
        {
            if (data.Length < 4 ||
                data[0] != 0x53 ||
                data[1] != 0x52 ||
                data[2] != 0x44 ||
                data[3] != 0x42)
                throw new
                    InvalidDataException(
                    "Not SRDB");

            uint firstOff =
                BitConverter
                    .ToUInt32(
                        data, 0x0C);
            var chunkOffs =
                new List<uint>();
            int pos = 0x0C;
            while (pos + 4 <=
                (int)firstOff)
            {
                uint v =
                    BitConverter
                        .ToUInt32(
                            data, pos);
                if (v == 0) break;
                if (v > (uint)
                    data.Length)
                    break;
                chunkOffs.Add(v);
                pos += 4;
            }

            uint c2Start =
                chunkOffs[2];
            uint masterSize =
                BitConverter
                    .ToUInt32(
                        data,
                        (int)c2Start);

            var masterPtrs =
                new List<uint>();
            pos = (int)c2Start;
            while (pos <
                (int)(c2Start +
                      masterSize))
            {
                uint v =
                    BitConverter
                        .ToUInt32(
                            data, pos);
                if (v == 0) break;
                masterPtrs.Add(v);
                pos += 4;
            }

            var rdtbs =
                new List<byte[]>();
            for (int i = 0;
                 i < masterPtrs
                     .Count; i++)
            {
                uint s = c2Start +
                    masterPtrs[i];
                uint e;
                if (i + 1 <
                    masterPtrs.Count)
                    e = c2Start +
                        masterPtrs[
                            i + 1];
                else
                    e = (uint)
                        data.Length;
                int sz =
                    (int)(e - s);
                if (sz <= 0)
                    continue;
                byte[] rdtb =
                    new byte[sz];
                Array.Copy(
                    data, (int)s,
                    rdtb, 0, sz);
                rdtbs.Add(rdtb);
            }
            return rdtbs;
        }

        static byte[]
            RebuildSRDBFromList(
                byte[] original,
                List<byte[]>
                    newRdtbs)
        {
            uint firstOff =
                BitConverter
                    .ToUInt32(
                        original,
                        0x0C);
            var chunkOffs =
                new List<uint>();
            int pos = 0x0C;
            while (pos + 4 <=
                (int)firstOff)
            {
                uint v =
                    BitConverter
                        .ToUInt32(
                            original,
                            pos);
                if (v == 0) break;
                if (v > (uint)
                    original.Length)
                    break;
                chunkOffs.Add(v);
                pos += 4;
            }

            int headerSize =
                (int)chunkOffs[0];
            byte[] chunk0 =
                new byte[
                    chunkOffs[1] -
                    chunkOffs[0]];
            Array.Copy(original,
                (int)chunkOffs[0],
                chunk0, 0,
                chunk0.Length);
            byte[] chunk1 =
                new byte[
                    chunkOffs[2] -
                    chunkOffs[1]];
            Array.Copy(original,
                (int)chunkOffs[1],
                chunk1, 0,
                chunk1.Length);

            uint masterSize =
                BitConverter
                    .ToUInt32(
                        original,
                        (int)chunkOffs[2]);

            var nm =
                new List<int>();
            int cursor =
                (int)masterSize;
            foreach (var rdtb in
                newRdtbs)
            {
                nm.Add(cursor);
                cursor +=
                    rdtb.Length;
            }

            byte[] nc2 =
                new byte[cursor];
            for (int i = 0;
                 i < nm.Count; i++)
            {
                byte[] p =
                    BitConverter
                        .GetBytes(
                            (uint)nm[i]);
                Array.Copy(p, 0,
                    nc2, i * 4, 4);
            }
            for (int i = 0;
                 i < newRdtbs
                     .Count; i++)
                Array.Copy(
                    newRdtbs[i], 0,
                    nc2, nm[i],
                    newRdtbs[i]
                        .Length);

            int total =
                headerSize +
                chunk0.Length +
                chunk1.Length +
                nc2.Length;
            byte[] result =
                new byte[total];
            Array.Copy(original,
                0, result, 0, 12);

            int[] newOffs = {
                headerSize,
                headerSize +
                    chunk0.Length,
                headerSize +
                    chunk0.Length +
                    chunk1.Length,
            };
            int hp = 0x0C;
            foreach (int off in
                newOffs)
            {
                if (hp + 4 >
                    headerSize)
                    break;
                byte[] p =
                    BitConverter
                        .GetBytes(
                            (uint)off);
                Array.Copy(p, 0,
                    result, hp, 4);
                hp += 4;
            }
            Array.Copy(chunk0, 0,
                result, newOffs[0],
                chunk0.Length);
            Array.Copy(chunk1, 0,
                result, newOffs[1],
                chunk1.Length);
            Array.Copy(nc2, 0,
                result, newOffs[2],
                nc2.Length);
            return result;
        }

        // ═══════════════════════════════
        // AUTO DETECT JAP OR USA
        // FROM ISO FILE
        // ═══════════════════════════════
        static bool AutoDetectJap(
            string isoPath,
            out bool aborted)
        {
            aborted = false;
            try
            {
                string detectedElf;
                bool isJap =
                    HarvestIso
                        .AutoDetectVersion(
                            isoPath,
                            out detectedElf);

                if (isJap)
                {
                    Console.ForegroundColor =
                        ConsoleColor.Yellow;
                    Console.WriteLine(
                        "  [Auto-detected]" +
                        " Japanese version" +
                        " (SLPS_201.04)!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor =
                        ConsoleColor.Green;
                    Console.WriteLine(
                        "  [Auto-detected]" +
                        " USA version" +
                        " (SLUS_202.51).");
                    Console.ResetColor();
                }
                return isJap;
            }
            catch (InvalidDataException ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "\n  WARNING: " +
                    ex.Message);
                Console.WriteLine(
                    "  Cannot fix this" +
                    " ISO. Aborting.");
                Console.ResetColor();
                Console.WriteLine();
                aborted = true;
                return false;
            }
        }

        // ═══════════════════════════════
        // AUTO DETECT FROM ISO
        // Returns GameVersion
        // ═══════════════════════════════
        static GameVersion AutoDetectFromIso(
            string isoPath,
            out bool aborted)
        {
            aborted = false;
            try
            {
                string detectedElf;
                GameVersion v =
                    HarvestIso
                        .AutoDetectGameVersion(
                            isoPath,
                            out detectedElf);

                switch (v)
                {
                    case GameVersion.DEMO:
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "  [Auto-detected]" +
                            " JAP DEMO version" +
                            " (SLPM_601.47)!");
                        Console.ResetColor();
                        break;
                    case GameVersion.JAP:
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "  [Auto-detected]" +
                            " Japanese version" +
                            " (SLPS_201.04)!");
                        Console.ResetColor();
                        break;
                    default:
                        Console.ForegroundColor =
                            ConsoleColor.Green;
                        Console.WriteLine(
                            "  [Auto-detected]" +
                            " USA version" +
                            " (SLUS_202.51).");
                        Console.ResetColor();
                        break;
                }
                return v;
            }
            catch (InvalidDataException ex)
            {
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "\n  WARNING: " +
                    ex.Message);
                Console.WriteLine(
                    "  Cannot fix this" +
                    " ISO. Aborting.");
                Console.ResetColor();
                Console.WriteLine();
                aborted = true;
                return GameVersion.USA;
            }
        }

        // ═════════════════════════════════════
        // RUN COMMAND
        // ═════════════════════════════════════
        static void RunCommand(string[] args)
        {
            try
            {
                string cmd =
                    NormalizeCommand(args[0]);

                bool customFinish = false;

                switch (cmd)
                {
                    // ════════════════════════
                    // HDA COMMANDS
                    // ════════════════════════
                    case "xhda":
                        RequireArgs(args, 3,
                            "-xhda <file.hda>" +
                            " <out_folder>");
                        {
                            string xhdaOut =
                                args[2];
                            string xhdaDir =
                                Path
                                    .GetDirectoryName(
                                        xhdaOut);
                            string xhdaName =
                                Path.GetFileName(
                                    xhdaOut)
                                    .ToUpper();
                            xhdaOut =
                                string.IsNullOrEmpty(
                                    xhdaDir)
                                ? xhdaName
                                : Path.Combine(
                                    xhdaDir,
                                    xhdaName);
                            HarvestDataArchive
                                .Unpack(
                                    args[1],
                                    xhdaOut);
                        }
                        break;

                    case "chda":
                        if (args.Length >= 2 &&
                            (args[1].ToLower()
                                == "raw" ||
                             args[1].ToLower()
                                == "-raw" ||
                             args[1].ToLower()
                                == "uncomp" ||
                             args[1].ToLower()
                                == "-uncomp"))
                        {
                            RequireArgs(args, 4,
                                "-chda raw/uncomp" +
                                " <in_folder>" +
                                " <file.hda>");
                            string chdaOut =
                                args[3];
                            string chdaDir =
                                Path
                                    .GetDirectoryName(
                                        chdaOut);
                            string chdaName =
                                Path.GetFileName(
                                    chdaOut)
                                    .ToUpper();
                            chdaOut =
                                string.IsNullOrEmpty(
                                    chdaDir)
                                ? chdaName
                                : Path.Combine(
                                    chdaDir,
                                    chdaName);
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Packing" +
                                " uncompressed" +
                                " HDA...");
                            Console.ResetColor();
                            HarvestDataArchive
                                .Pack(
                                    chdaOut,
                                    args[2]);
                        }
                        else
                        {
                            RequireArgs(args, 3,
                                "-chda <in_folder>" +
                                " <file.hda>");
                            string chdaOut =
                                args[2];
                            string chdaDir =
                                Path
                                    .GetDirectoryName(
                                        chdaOut);
                            string chdaName =
                                Path.GetFileName(
                                    chdaOut)
                                    .ToUpper();
                            chdaOut =
                                string.IsNullOrEmpty(
                                    chdaDir)
                                ? chdaName
                                : Path.Combine(
                                    chdaDir,
                                    chdaName);
                            HarvestDataArchive
                                .PackCompressed(
                                    chdaOut,
                                    args[1]);
                        }
                        break;

                    // ════════════════════════
                    // SHORTCUT: raw / uncomp
                    // ════════════════════════
                    case "raw":
                    case "uncomp":
                        RequireArgs(args, 3,
                            "-raw <in_folder>" +
                            " <file.hda>");
                        {
                            string rawOut =
                                args[2];
                            string rawDir =
                                Path
                                    .GetDirectoryName(
                                        rawOut);
                            string rawName =
                                Path.GetFileName(
                                    rawOut)
                                    .ToUpper();
                            rawOut =
                                string.IsNullOrEmpty(
                                    rawDir)
                                ? rawName
                                : Path.Combine(
                                    rawDir,
                                    rawName);
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Packing" +
                                " uncompressed" +
                                " HDA...");
                            Console.ResetColor();
                            HarvestDataArchive
                                .Pack(
                                    rawOut,
                                    args[1]);
                        }
                        break;

                    // ════════════════════════
                    // SHORTCUT: comp
                    // ════════════════════════
                    case "comp":
                        RequireArgs(args, 3,
                            "-comp <in_folder>" +
                            " <file.hda>");
                        {
                            string compOut =
                                args[2];
                            string compDir =
                                Path
                                    .GetDirectoryName(
                                        compOut);
                            string compName =
                                Path.GetFileName(
                                    compOut)
                                    .ToUpper();
                            compOut =
                                string.IsNullOrEmpty(
                                    compDir)
                                ? compName
                                : Path.Combine(
                                    compDir,
                                    compName);
                            HarvestDataArchive
                                .PackCompressed(
                                    compOut,
                                    args[1]);
                        }
                        break;

                    // ════════════════════════
                    // SINGLE FILE COMPRESS
                    // ════════════════════════
                    case "compress":
                        RequireArgs(args, 3,
                            "-compress" +
                            " <input_file>" +
                            " <output_file>");
                        {
                            string inPath =
                                args[1];
                            string outPath =
                                args[2];

                            if (!File.Exists(
                                    inPath))
                            {
                                TextOut
                                    .PrintError(
                                    "Input file" +
                                    " not found: " +
                                    inPath);
                                return;
                            }

                            byte[] raw =
                                File.ReadAllBytes(
                                    inPath);

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Compressing...");
                            Console.ResetColor();

                            var sw = System
                                .Diagnostics
                                .Stopwatch
                                .StartNew();

                            byte[] comp =
                                HarvestCompression
                                    .Compress(
                                        raw,
                                        (cur,
                                         total) =>
                                        {
                                            double pct =
                                                total
                                                == 0
                                                ? 100
                                                : (double)
                                                  cur *
                                                  100.0
                                                  / total;
                                            Console
                                                .Error
                                                .Write(
                                                "\r  " +
                                                "{0:F1}%",
                                                pct);
                                        });

                            Console.Error
                                .Write("\r" +
                                new string(
                                    ' ', 40) +
                                "\r");
                            sw.Stop();

                            bool ok =
                                HarvestCompression
                                    .VerifyRoundTrip(
                                        raw, comp);

                            if (!ok ||
                                comp.Length >
                                raw.Length)
                            {
                                Console
                                    .ForegroundColor
                                    =
                                    ConsoleColor
                                        .Yellow;
                                Console.WriteLine(
                                    "  Using" +
                                    " literal" +
                                    " stream...");
                                Console
                                    .ResetColor();
                                comp =
                                    HarvestCompression
                                        .CompressAsLiterals(
                                            raw);
                            }

                            File.WriteAllBytes(
                                outPath, comp);

                            double ratio =
                                raw.Length == 0
                                ? 0
                                : (double)
                                  comp.Length /
                                  raw.Length *
                                  100.0;

                            Console.ForegroundColor =
                                ratio <= 100.1
                                ? ConsoleColor
                                    .Green
                                : ConsoleColor
                                    .Yellow;
                            Console.WriteLine(
                                "Done! " +
                                $"{raw.Length:N0}" +
                                $" → " +
                                $"{comp.Length:N0}" +
                                $" bytes" +
                                $" ({ratio:F1}%)" +
                                $" in" +
                                $" {sw.Elapsed.TotalSeconds:F2}s");
                            Console.ResetColor();
                        }
                        break;

                    // ════════════════════════
                    // SINGLE FILE UNCOMPRESS
                    // ════════════════════════
                    case "uncompress":
                        RequireArgs(args, 3,
                            "-uncompress" +
                            " <input_file>" +
                            " <output_file>");
                        {
                            string inPath =
                                args[1];
                            string outPath =
                                args[2];

                            if (!File.Exists(
                                    inPath))
                            {
                                TextOut
                                    .PrintError(
                                    "Input file" +
                                    " not found: " +
                                    inPath);
                                return;
                            }

                            byte[] comp =
                                File.ReadAllBytes(
                                    inPath);

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Decompressing...");
                            Console.ResetColor();

                            byte[] raw =
                                HarvestCompression
                                    .Decompress(
                                        comp);

                            File.WriteAllBytes(
                                outPath, raw);

                            Console.ForegroundColor =
                                ConsoleColor.Green;
                            Console.WriteLine(
                                $"Done!" +
                                $" {comp.Length:N0}" +
                                $" → " +
                                $"{raw.Length:N0}" +
                                $" bytes");
                            Console.ResetColor();
                        }
                        break;

                    // ════════════════════════
                    // TEXT COMMANDS
                    // ════════════════════════
                    case "xtxt":
                        {
                            bool datMode = false;
                            int argBase = 1;
                            if (args.Length > 1)
                            {
                                string a1 =
                                    args[1]
                                    .TrimStart('-')
                                    .ToLower();
                                if (a1 == "dat" ||
                                    a1 == "clean")
                                {
                                    datMode = true;
                                    argBase = 2;
                                }
                            }

                            if (args.Length <
                                argBase + 3)
                            {
                                Console.WriteLine();
                                Console
                                    .ForegroundColor
                                    =
                                    ConsoleColor
                                        .Cyan;
                                Console.WriteLine(
                                    "  Example:" +
                                    " -xtxt" +
                                    " <text.bin>" +
                                    " <ptr.bin>" +
                                    " <out.txt>");
                                Console
                                    .ResetColor();
                                Console.WriteLine();
                                customFinish =
                                    true;
                                break;
                            }

                            string xtxtData =
                                Path.GetFullPath(
                                    args[argBase]);
                            string xtxtPtrs =
                                Path.GetFullPath(
                                    args[argBase
                                        + 1]);

                            if (string.Equals(
                                    xtxtData,
                                    xtxtPtrs,
                                    StringComparison
                                        .OrdinalIgnoreCase))
                            {
                                Console.WriteLine();
                                Console
                                    .ForegroundColor
                                    =
                                    ConsoleColor
                                        .Yellow;
                                Console.WriteLine(
                                    "  These must" +
                                    " be two" +
                                    " different" +
                                    " files.");
                                Console
                                    .ResetColor();
                                Console.WriteLine();
                                customFinish =
                                    true;
                                break;
                            }

                            if (datMode)
                            {
                                string datPath =
                                    HarvestText
                                        .DecodeToFile(
                                            args[argBase],
                                            args[argBase + 1],
                                            args[argBase + 2]);
                                TextOut
                                    .PrintSuccess(
                                    "Finished!");
                            }
                            else
                            {
                                HarvestText
                                    .DecodeToFileHex(
                                        args[argBase],
                                        args[argBase + 1],
                                        args[argBase + 2]);
                                TextOut
                                    .PrintSuccess(
                                    "Finished!");
                            }
                            customFinish = true;
                            break;
                        }

                    case "ctxt":
                        {
                            bool datMode = false;
                            int argBase = 1;
                            if (args.Length > 1)
                            {
                                string a1 =
                                    args[1]
                                    .TrimStart('-')
                                    .ToLower();
                                if (a1 == "dat" ||
                                    a1 == "clean")
                                {
                                    datMode = true;
                                    argBase = 2;
                                }
                            }

                            if (args.Length <
                                argBase + 3)
                            {
                                Console.WriteLine();
                                Console
                                    .ForegroundColor
                                    =
                                    ConsoleColor
                                        .Cyan;
                                Console.WriteLine(
                                    "  Usage:" +
                                    " -ctxt" +
                                    " <in.txt>" +
                                    " <text.bin>" +
                                    " <ptr.bin>");
                                Console
                                    .ResetColor();
                                Console.WriteLine();
                                customFinish =
                                    true;
                                break;
                            }

                            if (datMode)
                            {
                                string txtFull =
                                    Path.GetFullPath(
                                        args[argBase]);
                                string datCheck =
                                    HarvestText
                                        .GetDatPathPublic(
                                            txtFull);
                                if (!File.Exists(
                                        datCheck))
                                {
                                    Console
                                        .ForegroundColor
                                        =
                                        ConsoleColor
                                            .Yellow;
                                    Console
                                        .WriteLine(
                                        "  .dat" +
                                        " file" +
                                        " not" +
                                        " found!");
                                    Console
                                        .ResetColor();
                                    customFinish
                                        = true;
                                    break;
                                }
                                HarvestText
                                    .EncodeFromFile(
                                        args[argBase],
                                        args[argBase + 1],
                                        args[argBase + 2]);
                            }
                            else
                            {
                                HarvestText
                                    .EncodeFromFileHex(
                                        args[argBase],
                                        args[argBase + 1],
                                        args[argBase + 2]);
                            }

                            TextOut.PrintSuccess(
                                "Finished!");
                            customFinish = true;
                            break;
                        }

                    // ════════════════════════
                    // ELF COMMANDS
                    // ════════════════════════
                    case "fixelf":
                        {
                            int flagIdx;
                            GameVersion gv =
                                DetectVersionFlag(
                                    args,
                                    out flagIdx);
                            string[] cleanArgs =
                                flagIdx >= 0
                                ? RemoveJapFlag(
                                    args, flagIdx)
                                : args;

                            RequireArgs(cleanArgs, 4,
                                "-fixelf" +
                                " [-jap|-demo]" +
                                " <ELF> <lba> <size>");

                            // Auto-detect from ELF
                            // filename if no flag given
                            if (flagIdx < 0)
                            {
                                string elfFileName =
                                    Path.GetFileName(
                                        cleanArgs[1])
                                    .ToUpper();

                                if (elfFileName ==
                                    "SLPM_601.47")
                                {
                                    gv =
                                        GameVersion.DEMO;
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "  [Auto-detected]" +
                                        " JAP DEMO" +
                                        " (SLPM_601.47)!");
                                    Console.ResetColor();
                                }
                                else if (elfFileName ==
                                         "SLPS_201.04")
                                {
                                    gv =
                                        GameVersion.JAP;
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "  [Auto-detected]" +
                                        " Japanese" +
                                        " (SLPS_201.04)!");
                                    Console.ResetColor();
                                }
                                else if (elfFileName ==
                                         "SLUS_202.51")
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Green;
                                    Console.WriteLine(
                                        "  [Auto-detected]" +
                                        " USA" +
                                        " (SLUS_202.51).");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "\n  WARNING: ELF" +
                                        " file is not" +
                                        " named" +
                                        " SLUS_202.51," +
                                        " SLPS_201.04," +
                                        " or SLPM_601.47.");
                                    Console.WriteLine(
                                        "  This does not" +
                                        " appear to be a" +
                                        " valid HMSTH ELF." +
                                        " Aborting.");
                                    Console.ResetColor();
                                    Console.WriteLine();
                                    break;
                                }
                            }

                            switch (gv)
                            {
                                case GameVersion.DEMO:
                                    Console.ForegroundColor =
                                        ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "  [DEMO] Using" +
                                        " JAP DEMO offsets" +
                                        " (SLPM_601.47)");
                                    Console.ResetColor();
                                    break;
                                case GameVersion.JAP:
                                    Console.ForegroundColor =
                                        ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "  [JAP] Using" +
                                        " Japanese offsets" +
                                        " (SLPS_201.04)");
                                    Console.ResetColor();
                                    break;
                            }

                            HarvestElf.Fix(
                                cleanArgs[1],
                                uint.Parse(cleanArgs[2]),
                                uint.Parse(cleanArgs[3]),
                                gv);
                        }
                        break;

                    // ════════════════════════
                    // AUTOMATED LBA FIXER
                    // (Reads real LBAs from ISO
                    //  and writes into SLUS_202.51
                    //  LBA table at 0x162460-0x162D30)
                    // ════════════════════════
                    case "fixlba":
                        {
                            int flagIdx;
                            GameVersion gv =
                                DetectVersionFlag(
                                    args,
                                    out flagIdx);
                            string[] cleanArgs =
                                flagIdx >= 0
                                ? RemoveJapFlag(
                                    args, flagIdx)
                                : args;

                            RequireArgs(cleanArgs, 2,
                                "-fixlba" +
                                " [-jap|-demo]" +
                                " <file.iso>");

                            string isoPath =
                                cleanArgs[1];
                            if (!Path.IsPathRooted(
                                    isoPath))
                                isoPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(
                                    isoPath);

                            // Auto-detect if no flag
                            if (flagIdx < 0)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Auto-detecting" +
                                    " ISO version...");
                                Console.ResetColor();

                                bool aborted;
                                gv = AutoDetectFromIso(
                                    isoPath,
                                    out aborted);
                                if (aborted) return;
                            }

                            string elfName;
                            switch (gv)
                            {
                                case GameVersion.DEMO:
                                    elfName =
                                        "SLPM_601.47";
                                    Console.ForegroundColor =
                                        ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "  [DEMO] Using" +
                                        " JAP DEMO offsets" +
                                        " (SLPM_601.47," +
                                        " 0x1633E0" +
                                        "-0x163CB0)");
                                    Console.ResetColor();
                                    break;
                                case GameVersion.JAP:
                                    elfName =
                                        "SLPS_201.04";
                                    Console.ForegroundColor =
                                        ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "  [JAP] Using" +
                                        " Japanese offsets" +
                                        " (SLPS_201.04," +
                                        " 0x162360" +
                                        "-0x162C30)");
                                    Console.ResetColor();
                                    break;
                                default:
                                    elfName =
                                        "SLUS_202.51";
                                    break;
                            }

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Auto-fixing LBA" +
                                " table in " +
                                elfName +
                                " inside ISO...");
                            Console.ResetColor();

                            int changes =
                                HarvestIso.FixLba(
                                    isoPath, gv);

                            if (changes == 0)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Green;
                                Console.WriteLine(
                                    "  LBA table already" +
                                    " correct. No changes" +
                                    " needed.");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Green;
                                Console.WriteLine(
                                    "  Patched " +
                                    changes +
                                    " LBA entries" +
                                    " in ISO.");
                                Console.ResetColor();
                            }
                        }
                        break;

                    // ════════════════════════
                    // FIXISOONLY - Just repairs
                    // ISO structure (renamed
                    // from old fixiso)
                    // ════════════════════════
                    case "fixisoonly":
                        RequireArgs(args, 2,
                            "-fixisoonly <file.iso>");
                        {
                            string isoPath = args[1];
                            if (!Path.IsPathRooted(isoPath))
                                isoPath = Path.Combine(
                                    Directory
                                        .GetCurrentDirectory(),
                                    isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(
                                    isoPath);

                            // ─── Check extension ────────
                            string fixOnlyExt = Path
                                .GetExtension(isoPath)
                                .ToLower();
                            bool isIsoFile =
                                fixOnlyExt == ".iso" ||
                                fixOnlyExt == ".img" ||
                                fixOnlyExt == ".bin" ||
                                fixOnlyExt == ".raw";

                            // ─── Auto-detect version ────
                            // needed for PS2 logo fix
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "  Auto-detecting " +
                                "version...");
                            Console.ResetColor();

                            bool aborted;
                            GameVersion gv =
                                AutoDetectFromIso(
                                    isoPath,
                                    out aborted);
                            if (aborted) break;

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            if (isIsoFile)
                            {
                                Console.WriteLine(
                                    " FIXISOONLY - ISO" +
                                    " Repair + PS2 Logo");
                            }
                            else
                            {
                                Console.WriteLine(
                                    " FIXISOONLY - PS2" +
                                    " Logo Fix Only");
                            }
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            Console.ResetColor();
                            Console.WriteLine();

                            // ─── Step 1: Fix ISO ────────
                            // Only for .iso / .img files
                            if (isIsoFile)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Yellow;
                                Console.WriteLine(
                                    "[STEP 1/2] Repairing" +
                                    " ISO structure...");
                                Console.ResetColor();

                                try
                                {
                                    IsoRepair.FixIso(isoPath);
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
                                    "[STEP 1/2] Skipping" +
                                    " ISO structure repair" +
                                    " (not a .iso file).");
                                Console.ResetColor();
                            }

                            // ─── Step 2: Fix PS2 logo ───
                            // Always runs
                            Console.WriteLine();
                            Console.ForegroundColor =
                                ConsoleColor.Yellow;
                            Console.WriteLine(
                                "[STEP 2/2] Fixing PS2" +
                                " logo + Master Disc" +
                                " markers...");
                            Console.ResetColor();

                            try
                            {
                                IsoLogoPatcher.PatchIso(
                                    isoPath, null, gv);
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
                        break;

                    case "fixiso":
                        {
                            int flagIdx;
                            GameVersion gv =
                                DetectVersionFlag(
                                    args, out flagIdx);
                            string[] cleanArgs =
                                flagIdx >= 0
                                ? RemoveJapFlag(args, flagIdx)
                                : args;

                            RequireArgs(cleanArgs, 2,
                                "-fixiso [-jap|-demo]" +
                                " <file.iso>");

                            string isoPath = cleanArgs[1];
                            if (!Path.IsPathRooted(isoPath))
                                isoPath = Path.Combine(
                                    Directory
                                        .GetCurrentDirectory(),
                                    isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(isoPath);

                            // Auto-detect if no flag
                            if (flagIdx < 0)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Auto-detecting " +
                                    "ISO version...");
                                Console.ResetColor();

                                bool aborted;
                                gv = AutoDetectFromIso(
                                    isoPath, out aborted);
                                if (aborted) return;
                            }

                            string elfName;
                            switch (gv)
                            {
                                case GameVersion.DEMO:
                                    elfName = "SLPM_601.47";
                                    break;
                                case GameVersion.JAP:
                                    elfName = "SLPS_201.04";
                                    break;
                                default:
                                    elfName = "SLUS_202.51";
                                    break;
                            }

                            string vLabel;
                            switch (gv)
                            {
                                case GameVersion.DEMO:
                                    vLabel = " [JAP DEMO]";
                                    break;
                                case GameVersion.JAP:
                                    vLabel = " [JAP]";
                                    break;
                                default:
                                    vLabel = " [USA]";
                                    break;
                            }

                            // ─── Check extension ────────────
                            string fixExt = Path
                                .GetExtension(isoPath)
                                .ToLower();
                            bool isIsoFile =
                                fixExt == ".iso" ||
                                fixExt == ".img" ||
                                fixExt == ".bin" ||
                                fixExt == ".raw";

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            Console.WriteLine(
                                " FIXISO - Full Auto" +
                                " Fix" + vLabel);
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            Console.ResetColor();

                            if (gv != GameVersion.USA)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Yellow;
                                Console.WriteLine(
                                    "  Version: " + elfName);
                                Console.ResetColor();
                            }

                            // ─── STEP 1: Fix ISO ────────────
                            // Only for .iso / .img files
                            if (isIsoFile)
                            {
                                Console.WriteLine();
                                Console.ForegroundColor =
                                    ConsoleColor.Yellow;
                                Console.WriteLine(
                                    "[STEP 1/3] Repairing" +
                                    " ISO structure...");
                                Console.ResetColor();

                                try
                                {
                                    IsoRepair.FixIso(isoPath);
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
                                Console.WriteLine();
                                Console.ForegroundColor =
                                    ConsoleColor.DarkGray;
                                Console.WriteLine(
                                    "[STEP 1/3] Skipping" +
                                    " ISO structure repair" +
                                    " (not a .iso file).");
                                Console.ResetColor();
                            }

                            // ─── STEP 2: Fix PS2 logo ───────
                            // Always runs
                            Console.WriteLine();
                            Console.ForegroundColor =
                                ConsoleColor.Yellow;
                            Console.WriteLine(
                                "[STEP 2/3] Fixing PS2" +
                                " logo + Master Disc" +
                                " markers...");
                            Console.ResetColor();

                            try
                            {
                                IsoLogoPatcher.PatchIso(
                                    isoPath, null, gv);
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

                            // ─── STEP 3: Fix LBA ────────────
                            // Always runs
                            Console.WriteLine();
                            Console.ForegroundColor =
                                ConsoleColor.Yellow;
                            Console.WriteLine(
                                "[STEP 3/3] Fixing LBA" +
                                " table in " +
                                elfName + "...");
                            Console.ResetColor();

                            try
                            {
                                int changes =
                                    HarvestIso.FixLba(
                                        isoPath, gv);

                                if (changes == 0)
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Green;
                                    Console.WriteLine(
                                        "  LBA table already" +
                                        " correct.");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Green;
                                    Console.WriteLine(
                                        "  Patched " +
                                        changes +
                                        " LBA entries.");
                                    Console.ResetColor();
                                }
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

                            // ─── Step 4: Convert to BIN ─────
                            // If file has .bin extension but
                            // is actually 2048-sector ISO
                            // convert it to real BIN format
                            string ext = Path
                                .GetExtension(isoPath)
                                .ToLower();

                            if (ext == ".bin" || ext == ".raw")
                            {
                                // Check if file is currently
                                // 2048-sector format
                                long fSize = new FileInfo(
                                    isoPath).Length;
                                bool isCurrentlyIso =
                                    (fSize % 2048 == 0) &&
                                    (fSize % 2352 != 0);

                                if (isCurrentlyIso)
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "[STEP 4/4] File has" +
                                        " .bin extension but" +
                                        " is 2048-sector ISO");
                                    Console.WriteLine(
                                        "  Converting to real" +
                                        " BIN/CUE format...");
                                    Console.ResetColor();

                                    try
                                    {
                                        // Read the ISO data
                                        byte[] isoData =
                                            File.ReadAllBytes(
                                                isoPath);

                                        // Convert to BIN using
                                        // HMSTHIsoConverter's
                                        // streaming method
                                        int numSec =
                                            (int)(fSize / 2048);
                                        long binSize =
                                            (long)numSec * 2352;

                                        Console.WriteLine(
                                            $"  Sectors: " +
                                            $"{numSec:N0}");
                                        Console.WriteLine(
                                            $"  BIN size: " +
                                            $"{binSize:N0}" +
                                            " bytes");

                                        byte[] sync = new byte[]
                                        {
                                            0x00, 0xFF, 0xFF, 0xFF,
                                            0xFF, 0xFF, 0xFF, 0xFF,
                                            0xFF, 0xFF, 0xFF, 0x00
                                        };

                                        byte[] secBuf =
                                            new byte[2352];

                                        // Delete old file first
                                        File.Delete(isoPath);

                                        using (var fs =
                                            new FileStream(
                                                isoPath,
                                                FileMode.Create,
                                                FileAccess.Write,
                                                FileShare.None,
                                                65536))
                                        {
                                            for (int s = 0;
                                                 s < numSec; s++)
                                            {
                                                Array.Clear(
                                                    secBuf, 0,
                                                    2352);

                                                Array.Copy(sync, 0,
                                                    secBuf, 0, 12);

                                                int lba = s + 150;
                                                int minute =
                                                    lba / (60 * 75);
                                                int second =
                                                    (lba / 75) % 60;
                                                int frame =
                                                    lba % 75;

                                                secBuf[12] = (byte)(
                                                    ((minute / 10) << 4) |
                                                    (minute % 10));
                                                secBuf[13] = (byte)(
                                                    ((second / 10) << 4) |
                                                    (second % 10));
                                                secBuf[14] = (byte)(
                                                    ((frame / 10) << 4) |
                                                    (frame % 10));
                                                secBuf[15] = 0x02;

                                                // Subheader
                                                secBuf[16] = 0x00;
                                                secBuf[17] = 0x00;
                                                secBuf[18] = 0x08;
                                                secBuf[19] = 0x00;
                                                secBuf[20] = 0x00;
                                                secBuf[21] = 0x00;
                                                secBuf[22] = 0x08;
                                                secBuf[23] = 0x00;

                                                Array.Copy(
                                                    isoData,
                                                    s * 2048,
                                                    secBuf, 24,
                                                    2048);

                                                fs.Write(
                                                    secBuf, 0,
                                                    2352);

                                                if (s % 50000 == 0
                                                    && s > 0)
                                                {
                                                    double pct =
                                                        (double)s /
                                                        numSec *
                                                        100.0;
                                                    Console.Write(
                                                        $"\r  " +
                                                        $"Writing" +
                                                        $" BIN: " +
                                                        $"{pct:F1}%");
                                                }
                                            }
                                        }

                                        Console.WriteLine();

                                        // Also create .cue file
                                        string cuePath =
                                            Path.ChangeExtension(
                                                isoPath, ".cue");
                                        string cueContent =
                                            "FILE \"" +
                                            Path.GetFileName(
                                                isoPath) +
                                            "\" BINARY\r\n" +
                                            "  TRACK 01" +
                                            " MODE1/2352\r\n" +
                                            "    INDEX 01" +
                                            " 00:00:00\r\n";
                                        File.WriteAllText(
                                            cuePath,
                                            cueContent);

                                        Console.ForegroundColor =
                                            ConsoleColor.Green;
                                        Console.WriteLine(
                                            "  BIN/CUE created!");
                                        Console.WriteLine(
                                            "  Also created: " +
                                            Path.GetFileName(
                                                cuePath));
                                        Console.ResetColor();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.ForegroundColor =
                                            ConsoleColor.Yellow;
                                        Console.WriteLine(
                                            "  Step 4 warning: " +
                                            ex.Message);
                                        Console.ResetColor();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine();
                                    Console.ForegroundColor =
                                        ConsoleColor.DarkGray;
                                    Console.WriteLine(
                                        "[STEP 4/4] File is" +
                                        " already in BIN format" +
                                        " - no conversion needed.");
                                    Console.ResetColor();
                                }
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
                        }
                        break;

                    // ════════════════════════
                    // PS2 LOGO FIXER
                    // ════════════════════════
                    case "fixps2logo":
                        {
                            int flagIdx;
                            GameVersion gv =
                                DetectVersionFlag(
                                    args,
                                    out flagIdx);
                            string[] cleanLogo =
                                flagIdx >= 0
                                ? RemoveJapFlag(
                                    args, flagIdx)
                                : args;

                            RequireArgs(cleanLogo, 2,
                                "-fixps2logo" +
                                " [-jap|-demo]" +
                                " <file.iso>");

                            string isoPath =
                                cleanLogo[1];
                            if (!Path.IsPathRooted(
                                    isoPath))
                                isoPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(
                                    isoPath);

                            // Auto-detect if no flag
                            if (flagIdx < 0)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Auto-detecting" +
                                    " ISO version...");
                                Console.ResetColor();

                                bool aborted;
                                gv = AutoDetectFromIso(
                                    isoPath,
                                    out aborted);
                                if (aborted) break;
                            }

                            IsoLogoPatcher.PatchIso(
                                isoPath, null, gv);
                        }
                        break;

                    // ════════════════════════
                    // CONVERT ISO ↔ BIN/CUE
                    // ════════════════════════
                    case "convertiso":
                        RequireArgs(args, 3,
                            "-convertiso <input>" +
                            " <output.iso|.bin>");
                        HMSTHIsoConverter.ConvertIsoBin(
                            args[1], args[2]);
                        customFinish = true;
                        break;

                    // ════════════════════════
                    // DVD/CD CONVERTER
                    // ════════════════════════
                    case "todvdiso":
                    case "todvd":
                    case "cd2dvd":
                    case "cdtodvd":
                        RequireArgs(args, 3,
                            "-todvd <input> <output.iso>");
                        HMSTHIsoConverter.ToDVD(
                            args[1], args[2]);
                        customFinish = true;
                        break;

                    case "tocdiso":
                    case "tocd":
                    case "dvd2cd":
                    case "dvdtocd":
                        RequireArgs(args, 3,
                            "-tocd <input> <output.iso>");
                        HMSTHIsoConverter.ToCD(
                            args[1], args[2]);
                        customFinish = true;
                        break;

                    // ════════════════════════
                    // BAD ENDING REMOVER
                    // Port of standalone Python
                    // HMSTH-Bad-Ending-Remover
                    // Removes: 1-year cap, bad
                    // ending, Winter->Spring
                    // transition crash.
                    // ════════════════════════
                    case "abadend":
                    case "analyzebadend":
                    case "abe":
                        RequireArgs(args, 2,
                            "-abadend <file>" +
                            " (ELF/ISO/BIN)");
                        {
                            string beFile =
                                args[1];
                            if (!Path
                                    .IsPathRooted(
                                        beFile))
                                beFile =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        beFile);
                            HMSTHModdingTool
                                .BadEndingRemover
                                .BadEndingRemover
                                .Analyze(beFile);
                        }
                        customFinish = true;
                        break;

                    case "fixbadend":
                    case "removebadend":
                    case "applybadend":
                    case "fbe":
                        RequireArgs(args, 2,
                            "-fixbadend <file>" +
                            " (ELF/ISO/BIN)" +
                            " [--nobackup]");
                        {
                            string beFile =
                                args[1];
                            bool doBackup =
                                true;
                            for (int i = 2;
                                 i < args
                                     .Length;
                                 i++)
                            {
                                string a = args[i]
                                    .ToLower()
                                    .TrimStart('-');
                                if (a ==
                                    "nobackup" ||
                                    a ==
                                    "no-backup" ||
                                    a == "nb")
                                    doBackup =
                                        false;
                            }
                            if (!Path
                                    .IsPathRooted(
                                        beFile))
                                beFile =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        beFile);
                            HMSTHModdingTool
                                .BadEndingRemover
                                .BadEndingRemover
                                .Apply(
                                    beFile,
                                    doBackup);
                        }
                        customFinish = true;
                        break;

                    case "vbadend":
                    case "verifybadend":
                    case "vbe":
                        RequireArgs(args, 2,
                            "-vbadend <file>" +
                            " (ELF/ISO/BIN)");
                        {
                            string beFile =
                                args[1];
                            if (!Path
                                    .IsPathRooted(
                                        beFile))
                                beFile =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        beFile);
                            HMSTHModdingTool
                                .BadEndingRemover
                                .BadEndingRemover
                                .Verify(beFile);
                        }
                        customFinish = true;
                        break;

                    case "rbadend":
                    case "restorebadend":
                    case "rbe":
                        RequireArgs(args, 2,
                            "-rbadend <file>" +
                            " (restores .bak)");
                        {
                            string beFile =
                                args[1];
                            if (!Path
                                    .IsPathRooted(
                                        beFile))
                                beFile =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        beFile);
                            HMSTHModdingTool
                                .BadEndingRemover
                                .BadEndingRemover
                                .Restore(beFile);
                        }
                        customFinish = true;
                        break;

                    // ════════════════════════
                    // CONTROLS REMAPPER
                    // ════════════════════════
                    case "controls":
                    case "remap":
                    case "remapcontrols":
                        RequireArgs(args, 2,
                            "-controls <file.iso>" +
                            " or <SLUS_202.51>");
                        ControlsRemapper.Run(args[1]);
                        customFinish = true;
                        break;

                    // ════════════════════════
                    // FAKEYEAR - Change year
                    // on files with a bigger year
                    // than 2001
                    // Usage:
                    //   fakeyear <file.iso>
                    //     (defaults to 2001)
                    //   fakeyear <year>
                    //              <file.iso>
                    // ════════════════════════
                    case "fakeyear":
                        RequireArgs(args, 2,
                            "-fakeyear [year]" +
                            " <file.iso>");
                        {
                            int fakeYear;
                            string isoPath;

                            // Try parse first arg
                            // as year
                            if (args.Length >= 3 &&
                                int.TryParse(
                                    args[1],
                                    out fakeYear))
                            {
                                // Format:
                                // fakeyear <year>
                                //   <file>
                                isoPath = args[2];
                            }
                            else
                            {
                                // Format:
                                // fakeyear <file>
                                // (default year
                                //  to 2001)
                                fakeYear = 2001;
                                isoPath = args[1];

                                Console.ForegroundColor
                                    = ConsoleColor
                                        .DarkGray;
                                Console.WriteLine(
                                    "  (No year" +
                                    " specified," +
                                    " using default:" +
                                    " 2001)");
                                Console.ResetColor();
                            }

                            if (!Path
                                    .IsPathRooted(
                                        isoPath))
                                isoPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        isoPath);
                            // Preserve real filename case
                            isoPath =
                                HarvestIso.GetRealPath(
                                    isoPath);

                            IsoFakeYear.Run(
                                isoPath,
                                fakeYear);
                        }
                        break;


                    // ════════════════════════
                    // RDTB COMMANDS
                    // ════════════════════════
                    case "irdtb":
                        RequireArgs(args, 2,
                            "-irdtb <file.rdtb>");
                        RDTBArchive.Info(
                            args[1]);
                        break;

                    case "irdtbnb":
                        RequireArgs(args, 2,
                            "-irdtbnb" +
                            " <file.rdtb>");
                        RDTBArchive.InfoNoBones(
                            args[1]);
                        break;

                    case "xrdtb":
                        RequireArgs(args, 3,
                            "-xrdtb <file.rdtb>" +
                            " <out_folder>");
                        RDTBArchive.Extract(
                            args[1], args[2]);
                        break;

                    case "crdtb":
                        RequireArgs(args, 3,
                            "-crdtb <in_folder>" +
                            " <file.rdtb>");
                        RDTBArchive.Create(
                            args[1], args[2]);
                        break;

                    case "srdtb":
                        RequireArgs(args, 2,
                            "-srdtb <file.rdtb>");
                        RDTBArchive.Skeleton(
                            args[1]);
                        break;

                    case "rrdtb":
                        RequireArgs(args, 3,
                            "-rrdtb" +
                            " <file_a.rdtb>" +
                            " <file_b.rdtb>");
                        RDTBArchive.Compare(
                            args[1], args[2]);
                        break;

                    case "vrdtb":
                        RequireArgs(args, 3,
                            "-vrdtb" +
                            " <original.rdtb>" +
                            " <rebuilt.rdtb>");
                        RDTBArchive.Verify(
                            args[1], args[2]);
                        break;

                    case "rcrdtb":
                        RequireArgs(args, 4,
                            "-rcrdtb" +
                            " <file.rdtb>" +
                            " <index>" +
                            " <chunk.bin>");
                        {
                            int rcIdx;
                            if (!int.TryParse(
                                    args[2],
                                    out rcIdx))
                            {
                                TextOut
                                    .PrintError(
                                    "Invalid" +
                                    " index: " +
                                    args[2]);
                                return;
                            }
                            RDTBArchive
                                .ReplaceChunk(
                                    args[1],
                                    rcIdx,
                                    args[3]);
                        }
                        break;

                    case "scanrdtb":
                        RequireArgs(args, 2,
                            "-scanrdtb" +
                            " <folder>");
                        RDTBArchive.ScanFolder(
                            args[1]);
                        break;

                    // ─── NEW RDTB COMMANDS ──
                    case "mrdtb":
                        // Show material table
                        RequireArgs(args, 2,
                            "-mrdtb <file.rdtb>");
                        RDTBArchive.Materials(
                            args[1]);
                        break;

                    case "detect":
                    case "detectrdtb":
                        // Detect embedded RDTBs
                        // in any file
                        RequireArgs(args, 2,
                            "-detect <file>");
                        RDTBArchive
                            .DetectEmbedded(
                            args[1]);
                        break;

                    // ════════════════════════
                    // GDTB COMMANDS
                    // ════════════════════════
                    case "igdtb":
                        RequireArgs(args, 2,
                            "-igdtb <file.gdtb>");
                        GDTBArchive.Info(
                            args[1]);
                        break;

                    case "xgdtb":
                        RequireArgs(args, 3,
                            "-xgdtb <file.gdtb>" +
                            " <out_folder>");
                        GDTBArchive.Extract(
                            args[1], args[2]);
                        break;

                    case "cgdtb":
                        RequireArgs(args, 3,
                            "-cgdtb <in_folder>" +
                            " <file.gdtb>");
                        GDTBArchive.Create(
                            args[1], args[2]);
                        break;

                    case "rgdtb":
                        RequireArgs(args, 4,
                            "-rgdtb <index>" +
                            " <texture.bmp>" +
                            " <file.gdtb>");
                        int rIdx;
                        if (!int.TryParse(
                                args[1],
                                out rIdx))
                        {
                            TextOut.PrintError(
                                "Invalid" +
                                " index: " +
                                args[1]);
                            return;
                        }
                        GDTBArchive.Replace(
                            args[3],
                            rIdx,
                            args[2]);
                        break;

                    case "rfgdtb":
                        int startIdx = 0;
                        if (args.Length >= 4 &&
                            int.TryParse(
                                args[2],
                                out startIdx))
                        {
                            RequireArgs(args, 4,
                                "-rfgdtb" +
                                " <folder>" +
                                " <start>" +
                                " <file.gdtb>");
                            GDTBArchive
                                .ReplaceFolder(
                                    args[3],
                                    args[1],
                                    startIdx);
                        }
                        else
                        {
                            RequireArgs(args, 3,
                                "-rfgdtb" +
                                " <folder>" +
                                " <file.gdtb>");
                            GDTBArchive
                                .ReplaceFolder(
                                    args[2],
                                    args[1], 0);
                        }
                        break;

                    case "cngdtb":
                        RequireArgs(args, 3,
                            "-cngdtb <number>" +
                            " <file.gdtb>");
                        int newCnt;
                        if (!int.TryParse(
                                args[1],
                                out newCnt))
                        {
                            TextOut.PrintError(
                                "Invalid" +
                                " number: " +
                                args[1]);
                            return;
                        }
                        GDTBArchive.ChangeCount(
                            args[2], newCnt);
                        break;

                    // ════════════════════════
                    // BMP CONVERTER COMMANDS
                    // ════════════════════════
                    case "tops2bmp":
                        RequireArgs(args, 2,
                            "-tops2bmp" +
                            " <image.bmp>");
                        PS2BMPConverter.ToPS2(
                            args[1]);
                        break;

                    case "towinbmp":
                        RequireArgs(args, 2,
                            "-towinbmp" +
                            " <image.bmp>");
                        PS2BMPConverter
                            .ToWindows(args[1]);
                        break;

                    // ════════════════════════
                    // BMP PALETTE COMMANDS
                    // ════════════════════════
                    case "xbmppal":
                        RequireArgs(args, 3,
                            "-xbmppal" +
                            " <image.bmp>" +
                            " <palette_name>");
                        BMPPalette.Extract(
                            args[1], args[2]);
                        break;

                    case "rbmppal":
                        RequireArgs(args, 3,
                            "-rbmppal" +
                            " <palette_file>" +
                            " <image.bmp>");
                        BMPPalette.Import(
                            args[1], args[2]);
                        break;

                    // ════════════════════════
                    // SRDB COMMANDS
                    // ════════════════════════


                    // ════════════════════════
                    // XSRDB - Extract embedded
                    // RDTBs from SRDB (new)
                    // ════════════════════════
                    case "xsrdb":
                        RequireArgs(args, 3,
                            "-xsrdb <file.srdb>"
                            + " <out_folder>");
                        {
                            string xsIn =
                                args[1];
                            string xsOut =
                                args[2];

                            if (!File.Exists(
                                    xsIn))
                            {
                                TextOut
                                    .PrintError(
                                    "File not "
                                    + "found: "
                                    + xsIn);
                                break;
                            }

                            byte[] xsData =
                                File
                                    .ReadAllBytes(
                                        xsIn);

                            Console
                                .ForegroundColor
                                = ConsoleColor
                                    .Cyan;
                            Console.WriteLine(
                                "[+] Extract "
                                + "SRDB (master "
                                + "table)");
                            Console
                                .ResetColor();
                            Console.WriteLine(
                                "    SRDB: "
                                + Path
                                    .GetFileName(
                                        xsIn));
                            Console.WriteLine(
                                "    Out:  "
                                + xsOut);

                            Directory
                                .CreateDirectory(
                                    xsOut);

                            File.Copy(xsIn,
                                Path.Combine(
                                    xsOut,
                                    "_source.srdb"),
                                true);

                            var xsRdtbs =
                                ParseSRDBForCSRDB(
                                    xsData);

                            Console.WriteLine(
                                "    RDTBs: "
                                + xsRdtbs.Count);
                            Console.WriteLine();

                            for (int i = 0;
                                 i < xsRdtbs
                                     .Count;
                                 i++)
                            {
                                string fn =
                                    "embedded_"
                                    + i.ToString(
                                        "D2")
                                    + ".rdtb";
                                string fp =
                                    Path.Combine(
                                        xsOut,
                                        fn);
                                File
                                    .WriteAllBytes(
                                        fp,
                                        xsRdtbs[i]);
                                Console
                                    .ForegroundColor
                                    = ConsoleColor
                                        .Green;
                                Console.WriteLine(
                                    "    ["
                                    + i.ToString(
                                        "D2")
                                    + "] " + fn
                                    + "  "
                                    + xsRdtbs[i]
                                        .Length
                                        .ToString(
                                            "N0")
                                    + " B");
                                Console
                                    .ResetColor();
                            }

                            // Write layout
                            var layout =
                                new System.Text
                                    .StringBuilder();
                            layout.AppendLine(
                                "# SRDB Layout");
                            layout.AppendLine(
                                "source="
                                + Path
                                    .GetFileName(
                                        xsIn));
                            layout.AppendLine(
                                "source_size="
                                + xsData.Length);
                            layout.AppendLine(
                                "n_rdtbs="
                                + xsRdtbs.Count);
                            layout.AppendLine();
                            layout.AppendLine(
                                "# index size "
                                + "filename");
                            for (int i = 0;
                                 i < xsRdtbs
                                     .Count;
                                 i++)
                            {
                                layout.AppendLine(
                                    i + " "
                                    + xsRdtbs[i]
                                        .Length
                                    + " embedded_"
                                    + i.ToString(
                                        "D2")
                                    + ".rdtb");
                            }
                            File.WriteAllText(
                                Path.Combine(
                                    xsOut,
                                    "_layout.txt"),
                                layout.ToString());

                            Console.WriteLine();
                            Console
                                .ForegroundColor
                                = ConsoleColor
                                    .Green;
                            Console.WriteLine(
                                "[OK] Extracted "
                                + xsRdtbs.Count
                                + " RDTBs");
                            Console
                                .ResetColor();
                        }
                        break;

                    // ════════════════════════
                    // CSRDB - Repack embedded
                    // RDTBs into SRDB (new)
                    // ════════════════════════
                    case "csrdb":
                        RequireArgs(args, 3,
                            "-csrdb <in_folder>"
                            + " <file.srdb>");
                        {
                            string csrdbIn =
                                args[1];
                            string csrdbOut =
                                args[2];

                            // Check if folder
                            // has _source.srdb
                            // (new format from
                            //  xsrdb extract)
                            string srcSrdb =
                                Path.Combine(
                                    csrdbIn,
                                    "_source.srdb");

                            if (File.Exists(
                                    srcSrdb))
                            {
                                // Use master
                                // table method
                                Console
                                    .ForegroundColor
                                    = ConsoleColor
                                        .Cyan;
                                Console.WriteLine(
                                    "[CSRDB] Using"
                                    + " master table"
                                    + " rebuild");
                                Console
                                    .ResetColor();

                                byte[] origData =
                                    File
                                        .ReadAllBytes(
                                            srcSrdb);

                                // Parse to get
                                // RDTB count
                                var sInfo =
                                    ParseSRDBForCSRDB(
                                        origData);

                                // Load each
                                // embedded RDTB
                                var newList =
                                    new List<
                                        byte[]>();
                                for (int i = 0;
                                     i < sInfo
                                         .Count;
                                     i++)
                                {
                                    string fn =
                                        Path.Combine(
                                            csrdbIn,
                                            "embedded_"
                                            + i.ToString(
                                                "D2")
                                            + ".rdtb");
                                    if (File.Exists(
                                            fn))
                                    {
                                        byte[] rd =
                                            File
                                                .ReadAllBytes(
                                                    fn);
                                        newList.Add(
                                            rd);
                                        Console
                                            .WriteLine(
                                            "  ["
                                            + i.ToString(
                                                "D2")
                                            + "] "
                                            + Path
                                                .GetFileName(
                                                    fn)
                                            + "  "
                                            + rd.Length
                                                .ToString(
                                                    "N0")
                                            + " B");
                                    }
                                    else
                                    {
                                        newList.Add(
                                            sInfo[i]);
                                        Console
                                            .WriteLine(
                                            "  ["
                                            + i.ToString(
                                                "D2")
                                            + "] MISSING"
                                            + " - using"
                                            + " original");
                                    }
                                }

                                byte[] result =
                                    RebuildSRDBFromList(
                                        origData,
                                        newList);

                                File.WriteAllBytes(
                                    csrdbOut,
                                    result);

                                Console
                                    .ForegroundColor
                                    = ConsoleColor
                                        .Green;
                                Console.WriteLine(
                                    "\n[OK] SRDB: "
                                    + csrdbOut);
                                Console
                                    .ResetColor();
                                Console.WriteLine(
                                    "    Orig: "
                                    + origData
                                        .Length
                                        .ToString(
                                            "N0")
                                    + " B");
                                Console.WriteLine(
                                    "    New:  "
                                    + result
                                        .Length
                                        .ToString(
                                            "N0")
                                    + " B");
                            }
                            else
                            {
                                // Fall back to
                                // old method
                                SRDBArchive
                                    .Create(
                                        csrdbIn,
                                        csrdbOut);
                            }
                        }
                        break;

                    // ════════════════════════
                    // XSRDB2 - Old raw-chunk
                    // extractor (kept for ref)
                    // ════════════════════════
                    case "xsrdb2":
                        RequireArgs(args, 3,
                            "-xsrdb2" +
                            " <file.srdb>" +
                            " <out_folder>");
                        SRDBArchive.Extract2(
                            args[1], args[2]);
                        break;

                    // ════════════════════════
                    // CSRDB2 - Old raw-chunk
                    // repacker (kept for ref)
                    // ════════════════════════
                    case "csrdb2":
                        RequireArgs(args, 3,
                            "-csrdb2" +
                            " <in_folder>" +
                            " <file.srdb>");
                        SRDBArchive.Create2(
                            args[1], args[2]);
                        break;

                    case "xsrdb3d":
                        RequireArgs(args, 4,
                            "-xsrdb3d" +
                            " <file.srdb>" +
                            " <file.gdtb>" +
                            " <base>");
                        SRDBArchive.Extract3D(
                            args[1],
                            args[2],
                            args[3]);
                        break;

                    case "csrdb3d":
                        {
                            float scale3d = 1.0f;
                            var cleanArgs3d =
                                new System
                                    .Collections
                                    .Generic
                                    .List<string>();
                            int i3d = 1;
                            while (i3d <
                                   args.Length)
                            {
                                string a =
                                    args[i3d];
                                if (a.ToLower()
                                    == "--scale"
                                    || a.ToLower()
                                    == "-scale"
                                    || a.ToLower()
                                    == "-s")
                                {
                                    if (i3d + 1 <
                                        args.Length)
                                    {
                                        float.TryParse(
                                            args[i3d + 1],
                                            out scale3d);
                                        if (scale3d
                                            <= 0)
                                            scale3d
                                                = 1.0f;
                                        i3d += 2;
                                        continue;
                                    }
                                }
                                cleanArgs3d.Add(a);
                                i3d++;
                            }
                            if (cleanArgs3d
                                    .Count != 2)
                            {
                                Console.WriteLine(
                                    "Usage:" +
                                    " csrdb3d" +
                                    " <folder>" +
                                    " <out_folder>" +
                                    " [--scale N]");
                                return;
                            }
                            SRDBArchive
                                .Create3D(
                                    cleanArgs3d[0],
                                    cleanArgs3d[1],
                                    scale3d);
                        }
                        break;

                    case "vsrdb":
                        RequireArgs(args, 3,
                            "-vsrdb <orig.srdb>" +
                            " <rebuilt.srdb>");
                        SRDBArchive.Verify(
                            args[1], args[2]);
                        break;

                    case "dumpdiff":
                        RequireArgs(args, 4,
                            "-dumpdiff <orig>" +
                            " <rebuilt> <offset>");
                        {
                            byte[] o =
                                File.ReadAllBytes(
                                    args[1]);
                            byte[] r =
                                File.ReadAllBytes(
                                    args[2]);
                            int off = Convert
                                .ToInt32(
                                    args[3], 16);
                            Console.WriteLine();
                            Console.WriteLine(
                                "Offset 0x" +
                                off.ToString(
                                    "X8"));
                            Console.WriteLine(
                                "ORIG: ");
                            for (int i = 0;
                                 i < 64; i++)
                            {
                                if (off + i >=
                                    o.Length)
                                    break;
                                Console.Write(
                                    o[off + i]
                                    .ToString(
                                        "X2") +
                                    " ");
                                if ((i + 1) % 16
                                    == 0)
                                    Console
                                        .WriteLine();
                            }
                            Console.WriteLine();
                            Console.WriteLine(
                                "REBUILT:");
                            for (int i = 0;
                                 i < 64; i++)
                            {
                                if (off + i >=
                                    r.Length)
                                    break;
                                Console.Write(
                                    r[off + i]
                                    .ToString(
                                        "X2") +
                                    " ");
                                if ((i + 1) % 16
                                    == 0)
                                    Console
                                        .WriteLine();
                            }
                            Console.WriteLine();
                        }
                        break;

                    // ─── NEW SRDB COMMANDS ──
                    case "isrdb":
                        // Info/detect embedded
                        // RDTBs in SRDB
                        RequireArgs(args, 2,
                            "-isrdb <file.srdb>");
                        SRDBArchive.Info(
                            args[1]);
                        break;

                    case "xsrdbrdtb":
                        // Extract all embedded
                        // RDTBs from SRDB
                        RequireArgs(args, 3,
                            "-xsrdbrdtb" +
                            " <file.srdb>" +
                            " <out_folder>");
                        SRDBArchive
                            .ExtractEmbeddedRdtbs(
                                args[1],
                                args[2]);
                        break;

                        // ═════════════════════════════════════
                        // DETECT FILE TYPE BY MAGIC BYTES
                        // ═════════════════════════════════════
                        string DetectFileType(string path)
                        {
                            if (!File.Exists(path))
                                return "unknown";
                            try
                            {
                                byte[] hdr = new byte[8];
                                using (var fs = File.OpenRead(path))
                                    fs.Read(hdr, 0, 8);
                                // RDTB magic: 52 44 54 42
                                if (hdr[0] == 0x52 && hdr[1] == 0x44 &&
                                    hdr[2] == 0x54 && hdr[3] == 0x42)
                                    return "rdtb";
                                // SRDB magic: 53 52 44 42
                                if (hdr[0] == 0x53 && hdr[1] == 0x52 &&
                                    hdr[2] == 0x44 && hdr[3] == 0x42)
                                    return "srdb";
                                // Check extension fallback
                                string ext = Path.GetExtension(path)
                                    .ToLower();
                                if (ext == ".rdtb") return "rdtb";
                                if (ext == ".srdb") return "srdb";
                            }
                            catch { }
                            return "unknown";
                        }

                    // ════════════════════════
                    // 3D MODEL COMMANDS
                    // ════════════════════════
                    case "x3d":
                        {
                            bool splitMode = false;
                            int argStart = 1;
                            if (args.Length > 1
                                && args[1].ToLower().TrimStart('-')
                                    == "split")
                            {
                                splitMode = true;
                                argStart = 2;
                            }
                            var rem3d = new string[
                                args.Length - argStart];
                            Array.Copy(args, argStart,
                                rem3d, 0, rem3d.Length);

                            if (rem3d.Length == 3)
                            {
                                // Auto-detect file type
                                string fileType =
                                    DetectFileType(rem3d[0]);
                                if (fileType == "srdb")
                                {
                                    // Route to SRDB 3D extractor
                                    Console.ForegroundColor =
                                        ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "[Auto-detect] SRDB file" +
                                        " detected -> xsrdb3d");
                                    Console.ResetColor();
                                    SRDBArchive.Extract3D(
                                        rem3d[0],
                                        rem3d[1],
                                        rem3d[2]);

                                    // ═══════════════════════════
                                    // FIX: Post-process to align
                                    // textures in _obj folders
                                    // ═══════════════════════════
                                    string outDirFix =
                                        Path.GetDirectoryName(
                                            Path.GetFullPath(
                                                rem3d[0]));
                                    string[] folderSuffixes =
                                        new string[]
                                        {
                                            "_embedded_rdtbs_obj",
                                            "_all_obj",
                                        };
                                    foreach (string fSuffix in
                                        folderSuffixes)
                                    {
                                        string fPath =
                                            Path.Combine(
                                                outDirFix,
                                                rem3d[2] + fSuffix);
                                        SRDBEmbedObjTextureFixer
                                            .ApplyForSRDB(fPath);
                                    }
                                }
                                else
                                {
                                    // Default RDTB path
                                    if (splitMode)
                                        Model3D.ExtractSplit(
                                            rem3d[0],
                                            rem3d[1],
                                            rem3d[2]);
                                    else
                                        Model3D.Extract(
                                            rem3d[0],
                                            rem3d[1],
                                            rem3d[2]);

                                    // NEW: Also extract
                                    // 5th batch folder
                                    RDTBBatchFolder
                                        .ExtractBatchFolder(
                                            rem3d[0],
                                            rem3d[1],
                                            rem3d[2]);

                                    // ═══════════════════════════
                                    // FIX: Post-process to align
                                    // textures in _obj folder
                                    // for embedded/small RDTBs
                                    // ═══════════════════════════
                                    string outDirFix2 =
                                        Path.GetDirectoryName(
                                            Path.GetFullPath(
                                                rem3d[0]));
                                    string[] rdtbSuffixes =
                                        new string[]
                                        {
                                            "_obj",
                                            "_all_obj",
                                        };
                                    foreach (string fSuffix in
                                        rdtbSuffixes)
                                    {
                                        string fPath =
                                            Path.Combine(
                                                outDirFix2,
                                                rem3d[2] + fSuffix);
                                        HMSTHModdingTool.SRDB
                                            .SRDBEmbedObjTextureFixer
                                            .ApplyForRDTB(
                                                rem3d[0],
                                                fPath,
                                                rem3d[2]);
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine(
                                    "Usage: x3d <rdtb_or_srdb>" +
                                    " <gdtb> <base>");
                            }
                        }
                        break;


                    // ════════════════════════
                    // BATCH TOOLS (NEW v1.4.6)
                    // ════════════════════════
                    case "scanbatch":
                        RequireArgs(args, 3,
                            "-scanbatch <file.rdtb>"
                            + " <batch_index>");
                        {
                            int sbIdx;
                            if (!int.TryParse(
                                    args[2],
                                    out sbIdx))
                            {
                                TextOut
                                    .PrintError(
                                    "Invalid"
                                    + " index: "
                                    + args[2]);
                                return;
                            }
                            RDTBBatchTools
                                .ScanBatch(
                                    args[1],
                                    sbIdx);
                        }
                        break;

                    case "xbatch":
                    case "extractbatch":
                        RequireArgs(args, 4,
                            "-xbatch <file.rdtb>"
                            + " <batch_index>"
                            + " <out.obj>");
                        {
                            int xbIdx;
                            if (!int.TryParse(
                                    args[2],
                                    out xbIdx))
                            {
                                TextOut
                                    .PrintError(
                                    "Invalid"
                                    + " index: "
                                    + args[2]);
                                return;
                            }
                            RDTBBatchTools
                                .ExtractBatch(
                                    args[1],
                                    xbIdx,
                                    args[3]);
                        }
                        break;

                    case "xmodel":
                    case "extractmodel":
                        RequireArgs(args, 4,
                            "-xmodel <file.rdtb>"
                            + " <batch_index>"
                            + " <out.obj>");
                        {
                            int xmIdx;
                            if (!int.TryParse(
                                    args[2],
                                    out xmIdx))
                            {
                                TextOut
                                    .PrintError(
                                    "Invalid"
                                    + " index: "
                                    + args[2]);
                                return;
                            }
                            RDTBBatchTools
                                .ExtractModel(
                                    args[1],
                                    xmIdx,
                                    args[3]);
                        }
                        break;

                    // ════════════════════════
                    // XBATCHES / CBATCHES
                    // (Dedicated commands for
                    // per-batch folder format)
                    // ════════════════════════
                    case "xbatches":
                    case "extractbatches":
                        if (IsSRDBFile(args[1]))
                        {
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "[Auto-detect] SRDB"
                                + " -> SRDB extractor");
                            Console.ResetColor();
                            SRDBBatchExtractor
                                .ExtractBatches(
                                    args[1],
                                    args[2],
                                    args[3]);
                            break;
                        }
                        RequireArgs(args, 4,
                            "-xbatches <rdtb>"
                            + " <gdtb> <base>");
                        RDTBBatchFolder
                            .ExtractBatchFolder(
                                args[1],
                                args[2],
                                args[3]);
                        break;

                    case "cbatches":
                    case "createbatches":
                        if (args.Length >= 2 &&
                            IsSRDBFolder(args[1]))
                        {
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "[Auto-detect] SRDB"
                                + " folder -> SRDB"
                                + " rebuilder");
                            Console.ResetColor();
                            RequireArgs(args, 3,
                                "-cbatches <folder>"
                                + " <out_folder>");
                            string srdtGdtb =
                                args.Length >= 4
                                ? args[3] : null;
                            SRDBBatchExtractor
                                .RebuildSRDB(
                                    args[1],
                                    args[2],
                                    srdtGdtb);
                            break;
                        }

                        {
                            string cbNormals = "match";
                            float[] cbCustom = null;
                            bool cbDelAll = false;

                            var cbNormalsCopy =
                                new System.Collections
                                    .Generic.Dictionary<int, int>();

                            string cbFormat = "default";
                            var cbClean =
                                new System.Collections
                                    .Generic.List<string>();
                            int icb = 1;
                            while (icb < args.Length)
                            {
                                string a = args[icb];
                                string al = a.ToLower();
                                if (al == "--normals"
                                    && icb + 1
                                    < args.Length)
                                {
                                    cbNormals =
                                        args[icb + 1]
                                        .ToLower();
                                    icb += 2;
                                    continue;
                                }

                                if (al == "--normals--forcenew"
                                    || al == "--forcenew"
                                    || al == "--normals-forcenew")
                                {
                                    cbNormals = "forcenew";
                                    icb++;
                                    continue;
                                }

                                if (al == "--verbose" || al == "-v")
                                {
                                    // Verbose flag - passed through
                                    // to Build via environment
                                    Environment.SetEnvironmentVariable(
                                        "HMSTH_VERBOSE", "1");
                                    icb++;
                                    continue;
                                }

                                if (al ==
                                    "--normals-xyz"
                                    && icb + 1
                                    < args.Length)
                                {
                                    var parts =
                                        args[icb + 1]
                                        .Split(',');
                                    if (parts.Length
                                        >= 3)
                                    {
                                        cbCustom
                                            = new
                                            float[3];
                                        float.TryParse(
                                            parts[0],
                                            out cbCustom[0]);
                                        float.TryParse(
                                            parts[1],
                                            out cbCustom[1]);
                                        float.TryParse(
                                            parts[2],
                                            out cbCustom[2]);
                                        cbNormals
                                            = "custom";
                                    }
                                    icb += 2;
                                    continue;
                                }
                                if (al == "-all" ||
                                    al == "--all")
                                {
                                    cbDelAll = true;
                                    icb++;
                                    continue;
                                }
                                if (al == "--small" ||
                                    al == "-small")
                                {
                                    cbFormat = "small";
                                    icb++;
                                    continue;
                                }
                                if (al == "--mirrored"
                                    || al == "-mirrored"
                                    || al == "--mirror"
                                    || al == "-mirror")
                                {
                                    cbFormat = "auto";
                                    icb++;
                                    continue;
                                }
                                if (al == "--big" ||
                                    al == "-big")
                                {
                                    cbFormat = "big";
                                    icb++;
                                    continue;
                                }

                                if ((al == "--normals-copy"
                                     || al == "-normals-copy"
                                     || al == "--copy-normals"
                                     || al == "-copy-normals")
                                     && icb + 1 < args.Length)
                                {
                                    // Format: DEST:SRC or DEST=SRC
                                    // Multiple: repeat the flag
                                    //   --normals-copy 73:5
                                    //   --normals-copy 74:5
                                    string mapping =
                                        args[icb + 1];
                                    string[] parts =
                                        mapping.Split(
                                            new[] { ':', '=' },
                                            2);
                                    if (parts.Length == 2)
                                    {
                                        int destBi, srcBi;
                                        if (int.TryParse(
                                                parts[0].Trim(),
                                                out destBi)
                                            && int.TryParse(
                                                parts[1].Trim(),
                                                out srcBi))
                                        {
                                            cbNormalsCopy[destBi]
                                                = srcBi;
                                            Console.ForegroundColor =
                                                ConsoleColor
                                                    .DarkGray;
                                            Console.WriteLine(
                                                "  [normals-copy]"
                                                + " batch " + destBi
                                                + " <- batch "
                                                + srcBi);
                                            Console.ResetColor();
                                        }
                                    }
                                    icb += 2;
                                    continue;
                                }

                                cbClean.Add(a);
                                icb++;
                            }

                            // cbatches ALWAYS defaults
                            // to mirror regardless of
                            // source format
                            if (cbFormat == "default")
                            {
                                cbFormat = "auto";
                            }

                            // Force match normals as default
                            if (cbNormals == "match" ||
                                string.IsNullOrEmpty(cbNormals))
                            {
                                cbNormals = "match";
                            }

                            if (cbClean.Count == 2)
                            {
                                RDTBBatchFolder
                                    .BuildFromBatchFolder(
                                        cbClean[0],
                                        cbClean[1],
                                        cbNormals,
                                        cbCustom,
                                        cbDelAll,
                                        cbFormat,
                                        cbNormalsCopy);
                            }
                            else
                            {
                                Console.WriteLine(
                                    "Usage: cbatches"
                                    + " <folder>"
                                    + " <out_folder>"
                                    + " [--normals MODE]"
                                    + " [--normals-xyz X,Y,Z]"
                                    + " [--normals-copy"
                                    + " DEST:SRC]"
                                    + " [-all]"
                                    + " [--small]"
                                    + " [--mirrored]"
                                    + " [--big]");
                            }
                        }
                        break;

                    // ════════════════════════
                    // XSRDB - Extract SRDB
                    // batches to per-RDTB
                    // folders
                    // ════════════════════════
                    case "xsrdbbatches":
                        RequireArgs(args, 4,
                            "-xsrdbbatches" +
                            " <file.srdb>" +
                            " <file.gdtb>" +
                            " <out_dir>");
                        SRDBBatchExtractor
                            .ExtractBatches(
                                args[1],
                                args[2],
                                args[3]);
                        break;

                    // ════════════════════════
                    // CSRDB - Rebuild SRDB
                    // from per-RDTB folders
                    // ════════════════════════
                    case "csrdbbatches":
                        RequireArgs(args, 3,
                            "-csrdbbatches" +
                            " <in_dir>" +
                            " <out_folder>" +
                            " [out.gdtb]");
                        {
                            string csGdtb =
                                args.Length >= 4
                                ? args[3]
                                : null;
                            SRDBBatchExtractor
                                .RebuildSRDB(
                                    args[1],
                                    args[2],
                                    csGdtb);
                        }
                        break;

                    // ════════════════════════
                    // RDTB FORMAT CONVERTERS
                    // ════════════════════════
                    case "fmtrdtb":
                    case "formatrdtb":
                        RequireArgs(args, 2,
                            "-fmtrdtb <file.rdtb>");
                        RDTBConverter.ShowFormat(
                            args[1]);
                        break;

                    case "big2small":
                    case "rdtbbig2small":
                        RequireArgs(args, 3,
                            "-big2small <in.rdtb>"
                            + " <out.rdtb>");
                        RDTBConverter.BigToSmall(
                            args[1], args[2]);
                        break;

                    case "small2big":
                    case "rdtbsmall2big":
                        RequireArgs(args, 3,
                            "-small2big <in.rdtb>"
                            + " <out.rdtb>");
                        RDTBConverter.SmallToBig(
                            args[1], args[2]);
                        break;

                    case "small2mirror":
                    case "small2mirrored":
                    case "rdtbsmall2mirror":
                        RequireArgs(args, 3,
                            "-small2mirror <in.rdtb>"
                            + " <out.rdtb>");
                        RDTBConverter.SmallToMirrored(
                            args[1], args[2]);
                        break;

                    case "mirror2small":
                    case "mirrored2small":
                    case "rdtbmirror2small":
                        RequireArgs(args, 3,
                            "-mirror2small <in.rdtb>"
                            + " <out.rdtb>");
                        RDTBConverter.MirroredToSmall(
                            args[1], args[2]);
                        break;

                    case "big2mirror":
                    case "big2mirrored":
                    case "rdtbbig2mirror":
                        RequireArgs(args, 3,
                            "-big2mirror <in.rdtb>"
                            + " <out.rdtb>");
                        RDTBConverter.BigToMirrored(
                            args[1], args[2]);
                        break;

                    case "mirror2big":
                    case "mirrored2big":
                    case "rdtbmirror2big":
                        RequireArgs(args, 3,
                            "-mirror2big <in.rdtb>"
                            + " <out.rdtb>");
                        RDTBConverter.MirroredToBig(
                            args[1], args[2]);
                        break;


                    // ════════════════════════
                    // 3D MODEL DIAGNOSTICS
                    // ════════════════════════
                    case "imodel":
                    case "inspectmodel":
                        RequireArgs(args, 2,
                            "-imodel <file.rdtb>");
                        RDTBInspector.InspectModel(
                            args[1]);
                        break;

                    case "iobj":
                    case "inspectobj":
                        RequireArgs(args, 2,
                            "-iobj <file.obj>");
                        RDTBInspector.InspectObj(
                            args[1]);
                        break;

                    case "idae":
                    case "inspectdae":
                        RequireArgs(args, 2,
                            "-idae <file.dae>");
                        RDTBInspector.InspectDae(
                            args[1]);
                        break;

                    // ════════════════════════
                    // AUDIO COMMANDS
                    // ════════════════════════
                    case "cmusic":
                        RequireArgs(args, 2,
                            "-cmusic" +
                            " <input.vag>");
                        {
                            string vagPath =
                                args[1];
                            if (!Path
                                    .IsPathRooted(
                                        vagPath))
                                vagPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        vagPath);
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Converting" +
                                " VAG to" +
                                " BD/HD/SQ...");
                            Console.ResetColor();
                            AudioConverter
                                .ConvertVagToMusic(
                                    vagPath);
                        }
                        break;

                    case "xvag":
                        if (args.Length >= 2 &&
                            args[1].ToLower()
                            == "all")
                        {
                            RequireArgs(args, 5,
                                "-xvag all" +
                                " <bd_file>" +
                                " <hd_file>" +
                                " <out_folder>");
                            string hdAllX =
                                args[3];
                            string bdAllX =
                                args[2];
                            string outFolderX =
                                args[4];
                            if (!Path
                                    .IsPathRooted(
                                        hdAllX))
                                hdAllX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdAllX);
                            if (!Path
                                    .IsPathRooted(
                                        bdAllX))
                                bdAllX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdAllX);
                            if (!Path
                                    .IsPathRooted(
                                        outFolderX))
                                outFolderX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        outFolderX);
                            AudioBank
                                .ExtractAllVags(
                                    hdAllX,
                                    bdAllX,
                                    outFolderX);
                        }
                        else
                        {
                            RequireArgs(args, 4,
                                "-xvag <bd_file>" +
                                " <hd_file>" +
                                " <index>" +
                                " [output.vag]");
                            string bdX = args[1];
                            string hdX = args[2];
                            int idxX =
                                int.Parse(args[3]);
                            string outVag =
                                args.Length >= 5
                                && !string
                                    .IsNullOrEmpty(
                                        args[4])
                                ? args[4]
                                : $"{idxX:000}.vag";
                            if (!Path
                                    .IsPathRooted(
                                        hdX))
                                hdX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdX);
                            if (!Path
                                    .IsPathRooted(
                                        bdX))
                                bdX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdX);
                            if (!Path
                                    .IsPathRooted(
                                        outVag))
                                outVag =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        outVag);
                            AudioBank.ExtractVag(
                                hdX, bdX,
                                idxX, outVag);
                        }
                        break;

                    case "rvag":
                        if (args.Length >= 2 &&
                            args[1].ToLower()
                            == "all")
                        {
                            RequireArgs(args, 5,
                                "-rvag all" +
                                " <folder_vags>" +
                                " <bd_file>" +
                                " <hd_file>");
                            string folderVags =
                                args[2];
                            string bdAllI =
                                args[3];
                            string hdAllI =
                                args[4];
                            if (!Path
                                    .IsPathRooted(
                                        folderVags))
                                folderVags =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        folderVags);
                            if (!Path
                                    .IsPathRooted(
                                        bdAllI))
                                bdAllI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdAllI);
                            if (!Path
                                    .IsPathRooted(
                                        hdAllI))
                                hdAllI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdAllI);
                            AudioBank
                                .ReplaceAllVags(
                                    hdAllI,
                                    bdAllI,
                                    folderVags);
                        }
                        else
                        {
                            RequireArgs(args, 5,
                                "-rvag <index>" +
                                " <input.vag>" +
                                " <bd_file>" +
                                " <hd_file>");
                            int idxI =
                                int.Parse(args[1]);
                            string inVag =
                                args[2];
                            string bdI = args[3];
                            string hdI = args[4];
                            if (!Path
                                    .IsPathRooted(
                                        hdI))
                                hdI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdI);
                            if (!Path
                                    .IsPathRooted(
                                        bdI))
                                bdI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdI);
                            if (!Path
                                    .IsPathRooted(
                                        inVag))
                                inVag =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        inVag);
                            AudioBank.ImportVag(
                                hdI, bdI,
                                idxI, inVag);
                        }
                        break;

                    // ════════════════════════
                    // BOY ADVANCED BONE SCALER
                    // ════════════════════════
                    case "boyscale":
                        BoyScaler.Run(args);
                        customFinish = true;
                        break;

                    // ════════════════════════
                    // BOY MOD PRESETS
                    // ════════════════════════
                    case "boymodv2":
                        BoyModPresets
                            .ApplyModV2(args);
                        break;

                    case "boymodv3":
                        BoyModPresets
                            .ApplyModV3(args);
                        break;

                    case "boymodv4":
                        BoyModPresets
                            .ApplyModV4(args);
                        break;

                    case "boyoriginal":
                    case "boyrestore":
                    case "boyback":
                    case "boyorig":
                        BoyModPresets
                            .ApplyOriginal(args);
                        break;

                    // ════════════════════════
                    // NPC'S ADVANCED BONE SCALER
                    // ════════════════════════
                    case "bonescale":
                        UniversalBoneScaler.Run(args);
                        customFinish = true;
                        break;


                    // ════════════════════════
                    // UNKNOWN COMMAND
                    // ════════════════════════
                    default:
                        Console.WriteLine();
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "  Unknown command:" +
                            " " + args[0]);
                        Console.WriteLine();
                        Console.WriteLine(
                            "  Type 'help' to" +
                            " see all commands.");
                        Console.ResetColor();
                        Console.WriteLine();
                        return;
                }

                if (!customFinish)
                    TextOut.PrintSuccess(
                        "Finished!");
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  File not found: " +
                    e.FileName);
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Folder not found!");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Blue;
                Console.WriteLine(
                    "  Access denied!" +
                    " Check file/folder.");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (ArgumentException e)
            {
                Console.WriteLine();
                string msg = e.Message;
                if (msg.Contains(
                    "Not enough arguments"))
                {
                    string[] parts =
                        msg.Split('\n');
                    Console.ForegroundColor =
                        ConsoleColor.Blue;
                    Console.WriteLine(
                        "  " +
                        parts[0].Trim());
                    if (parts.Length > 1)
                    {
                        Console.ForegroundColor =
                            ConsoleColor.Cyan;
                        Console.WriteLine(
                            "  " +
                            parts[1].Trim());
                    }
                }
                else
                {
                    Console.ForegroundColor =
                        ConsoleColor.Blue;
                    Console.WriteLine(
                        "  " + msg);
                }
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (InvalidDataException e)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Invalid data: " +
                    e.Message);
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (FormatException)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Invalid number!");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (IOException e)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  IO error: " +
                    e.Message);
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Error: " + e.Message);
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        // ═════════════════════════════════════
        // PARSE INPUT LINE
        // ═════════════════════════════════════
        static string[] ParseInput(string input)
        {
            input = input.Trim();

            var tokens =
                new System.Collections
                    .Generic.List<string>();
            bool inQuotes = false;
            var current =
                new System.Text.StringBuilder();

            for (int i = 0;
                 i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(
                            current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            if (tokens.Count == 0)
                return new string[0];

            string first =
                tokens[0].ToLower();

            var knownCommands =
                new System.Collections
                    .Generic.HashSet<string>
                {
                    "xhda",       "chda",
                    "raw",        "uncomp",
                    "comp",
                    "compress",   "uncompress",
                    "xtxt",       "ctxt",
                    "fixelf",     "fixlba",
                    "fixiso",     "fixisoonly",
                    "convertiso", "fixps2logo",
                    "todvdiso", "todvd",
                    "cd2dvd",   "cdtodvd",
                    "tocdiso",  "tocd",
                    "dvd2cd",   "dvdtocd",
                    "abadend", "analyzebadend", "abe",
                    "fixbadend", "removebadend",
                    "applybadend", "fbe",
                    "vbadend", "verifybadend", "vbe",
                    "rbadend", "restorebadend", "rbe",
                    "controls",
                    "remap",      "remapcontrols",
                    "fakeyear",
                    "irdtb",      "irdtbnb",
                    "xrdtb",      "crdtb",
                    "srdtb",      "rrdtb",
                    "vrdtb",      "rcrdtb",
                    "scanrdtb",
                    "mrdtb",
                    "detect",     "detectrdtb",
                    "igdtb",      "xgdtb",
                    "cgdtb",      "rgdtb",
                    "rfgdtb",     "cngdtb",
                    "tops2bmp",   "towinbmp",
                    "xbmppal",    "rbmppal",
                    "xsrdb",      "csrdb",
                    "xsrdb2",     "csrdb2",
                    "xsrdb3d",    "csrdb3d",
                    "isrdb",      "vsrdb",
                    "dumpdiff",
                    "xsrdbrdtb",  "cmusic",
                    "xvag",       "rvag",
                    "boyscale",   "bonescale",
                    "boymodv2",   "boymodv3",
                    "boymodv4",
                    "boyoriginal","boyrestore",
                    "boyback",    "boyorig",
                    "x3d",        "c3d",
                    "scanbatch",
                    "xbatch", "extractbatch",
                    "xmodel", "extractmodel",
                    "xbatches", "extractbatches",
                    "cbatches", "createbatches",
                    "fmtrdtb",      "formatrdtb",
                    "big2small",    "rdtbbig2small",
                    "small2big",    "rdtbsmall2big",
                    "small2mirror", "small2mirrored",
                    "rdtbsmall2mirror",
                    "mirror2small", "mirrored2small",
                    "rdtbmirror2small",
                    "big2mirror",   "big2mirrored",
                    "rdtbbig2mirror",
                    "mirror2big",   "mirrored2big",
                    "rdtbmirror2big",
                    "imodel",    "inspectmodel",
                    "iobj",      "inspectobj",
                    "idae",      "inspectdae",
                    "xsrdbbatches", "csrdbbatches",
                };

            bool firstIsCommand =
                first.StartsWith("-") ||
                knownCommands.Contains(first);

            bool secondIsAll =
                tokens.Count >= 2 &&
                tokens[1].ToLower() == "all";

            if (!firstIsCommand &&
                !secondIsAll &&
                tokens.Count > 1)
            {
                tokens.RemoveAt(0);
            }

            return tokens.ToArray();
        }

        // ═════════════════════════════════════
        // REQUIRE ARGS
        // ═════════════════════════════════════
        static void RequireArgs(
            string[] args,
            int required,
            string usage)
        {
            if (args.Length < required)
            {
                throw new ArgumentException(
                    "Not enough arguments!\n" +
                    "    Usage: tool.exe " +
                    usage);
            }
        }

        // ═════════════════════════════════════
        // PRINT USAGE
        // ═════════════════════════════════════
        static void PrintUsage()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(TOOL_NAME + " " + TOOL_VERSION);
            Console.ResetColor();
            Console.WriteLine("QUICK GUIDE");
            Console.WriteLine("Syntax: tool.exe <command> <input> [options] <output>");
            Console.WriteLine("Paths containing spaces must be quoted.");
            Console.WriteLine("In interactive mode, type a command without 'tool.exe'.");
            Console.WriteLine("Type help to show this guide; type exit to quit.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("1. HDA ARCHIVES");
            Console.ResetColor();
            Console.WriteLine("  xhda <file.hda> <output_folder>       Extract an HDA archive.");
            Console.WriteLine("  chda <input_folder> <file.hda>        Build an HDA archive.");
            Console.WriteLine("  chda raw <input_folder> <file.hda>    Build an uncompressed HDA.");
            Console.WriteLine("  Example: tool.exe xhda DATA.HDA .\\DATA");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("2. TEXT");
            Console.ResetColor();
            Console.WriteLine("  xtxt <text.bin> <ptr.bin> <out.txt>   Export game text for editing.");
            Console.WriteLine("  ctxt <in.txt> <text.bin> <ptr.bin>    Rebuild text files after editing.");
            Console.WriteLine("  Add -dat after xtxt/ctxt to use a lossless .dat sidecar.");
            Console.WriteLine("  Example: tool.exe xtxt TEXT.BIN PTR.BIN dialogue.txt");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("3. FILE COMPRESSION");
            Console.ResetColor();
            Console.WriteLine("  compress <input> <output>             Compress one file.");
            Console.WriteLine("  uncompress <input> <output>           Decompress one file.");
            Console.WriteLine("  Example: tool.exe uncompress DATA.BIN DATA_unpacked.BIN");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("4. ISO / ELF FIXES");
            Console.ResetColor();
            Console.WriteLine("  fixlba <file.iso>                     Repair the ISO's LBA table.");
            Console.WriteLine("  fixiso <file.iso>                     Apply supported ISO fixes.");
            Console.WriteLine("  fixelf <SLUS/SLPS> <lba> <size>       Update ELF file location/size.");
            Console.WriteLine("  Add -jap for the Japanese release; use -demo for the Japanese demo.");
            Console.WriteLine("  Example: tool.exe fixlba -jap HMSTH_JAP.iso");
            Console.WriteLine("  Always make a backup before modifying an ISO or ELF.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("5. SRDB / RDTB / GDTB ASSETS");
            Console.ResetColor();
            Console.WriteLine("  xsrdb <file.srdb> <folder>            Extract SRDB data.");
            Console.WriteLine("  csrdb <folder> <file.srdb>            Rebuild SRDB data.");
            Console.WriteLine("  xsrdb3d <file.srdb> <file.gdtb> <base> Extract 3D assets.");
            Console.WriteLine("  xsrdbrdtb <file.srdb> <folder>        Extract RDTB files from SRDB.");
            Console.WriteLine("  xrdtb <file.rdtb> <folder>            Extract RDTB contents.");
            Console.WriteLine("  crdtb <folder> <file.rdtb>            Build an RDTB file.");
            Console.WriteLine("  irdtb <file.rdtb>                     Inspect RDTB structure.");
            Console.WriteLine("  xgdtb <file.gdtb> <folder>            Extract GDTB textures.");
            Console.WriteLine("  cgdtb <folder> <file.gdtb>            Build a GDTB file.");
            Console.WriteLine("  Example: tool.exe xgdtb TEXTURES.GDTB .\\textures");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("6. BMP / PALETTES");
            Console.ResetColor();
            Console.WriteLine("  tops2bmp <input> <output.bmp>         Convert a PS2 image to BMP.");
            Console.WriteLine("  towinbmp <input.bmp> <output>         Convert BMP to the tool's PS2 format.");
            Console.WriteLine("  xbmppal <palette> <image.bmp>          Apply/extract palette data (see command syntax).");
            Console.WriteLine("  rbmppal <palette> <image.bmp>          Rebuild palette-related image data.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("7. MODELS / BATCH TOOLS");
            Console.ResetColor();
            Console.WriteLine("  xbatch / xmodel                       Extract batch/model assets.");
            Console.WriteLine("  scanbatch / scanrdtb                  Find and report asset entries.");
            Console.WriteLine("  imodel / iobj / idae                  Inspect model, OBJ, or DAE files.");
            Console.WriteLine("  Use the command-specific arguments shown in the source/help examples.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("8. GAMEPLAY / MODDING UTILITIES");
            Console.ResetColor();
            Console.WriteLine("  controls <input> <output>             Remap supported controls.");
            Console.WriteLine("  fakeyear <file.iso>                   Apply the supported fake-year patch.");
            Console.WriteLine("  bad-ending commands: abadend, fixbadend, vbadend, rbadend");
            Console.WriteLine("  Boy scaling commands: boyscale, boymodv2, boymodv3, boymodv4, boyoriginal");
            Console.WriteLine("  Bone scaling: bonescale (requires the arguments requested by the tool).");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("9. COMMON WORKFLOW");
            Console.ResetColor();
            Console.WriteLine("  1) Extract the archive/assets.");
            Console.WriteLine("  2) Edit the exported text or assets.");
            Console.WriteLine("  3) Rebuild with the matching c* command.");
            Console.WriteLine("  4) Test in the game/emulator; keep an untouched backup.");
            Console.WriteLine();
            Console.WriteLine("More commands are available: run the command with its required arguments");
            Console.WriteLine("or consult the project's source for exact syntax. Command names are case-insensitive.");
            Console.WriteLine("Interactive shortcuts: help, clear, exit");
            Console.WriteLine();
        }
    }
}
