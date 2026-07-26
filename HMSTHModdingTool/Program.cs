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
            "v1.5.0-Beta";
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
                            int japIdx;
                            bool isJap = DetectJapFlag(
                                args, out japIdx);
                            string[] cleanArgs = isJap
                                ? RemoveJapFlag(args, japIdx)
                                : args;

                            RequireArgs(cleanArgs, 4,
                                "-fixelf [-jap] <ELF>" +
                                " <lba> <size>");

                            // ─── AUTO-DETECT from ELF filename
                            // if no -jap flag specified
                            if (!isJap)
                            {
                                string elfFileName =
                                    Path.GetFileName(
                                        cleanArgs[1])
                                    .ToUpper();

                                if (elfFileName ==
                                    "SLPS_201.04")
                                {
                                    isJap = true;
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "  [Auto-detected]" +
                                        " Japanese version" +
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
                                        " USA version" +
                                        " (SLUS_202.51).");
                                    Console.ResetColor();
                                }
                                else
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "\n  WARNING: ELF" +
                                        " file is not named" +
                                        " SLUS_202.51 or" +
                                        " SLPS_201.04.");
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

                            if (isJap)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  [JAP] Using" +
                                    " Japanese version" +
                                    " offsets" +
                                    " (SLPS_201.04)");
                                Console.ResetColor();
                            }

                            HarvestElf.Fix(
                                cleanArgs[1],
                                uint.Parse(cleanArgs[2]),
                                uint.Parse(cleanArgs[3]),
                                isJap);
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
                            int japIdx;
                            bool isJap = DetectJapFlag(
                                args, out japIdx);
                            string[] cleanArgs = isJap
                                ? RemoveJapFlag(args, japIdx)
                                : args;

                            RequireArgs(cleanArgs, 2,
                                "-fixlba [-jap] <file.iso>");

                            string isoPath = cleanArgs[1];
                            if (!Path.IsPathRooted(isoPath))
                                isoPath = Path.Combine(
                                    Directory
                                        .GetCurrentDirectory(),
                                    isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(isoPath);

                            // ─── AUTO-DETECT if no -jap flag
                            if (!isJap)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Auto-detecting" +
                                    " ISO version...");
                                Console.ResetColor();

                                try
                                {
                                    string detectedElf;
                                    bool autoJap =
                                        HarvestIso
                                            .AutoDetectVersion(
                                                isoPath,
                                                out detectedElf);

                                    if (autoJap)
                                    {
                                        isJap = true;
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
                                }
                                catch (InvalidDataException ex)
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Yellow;
                                    Console.WriteLine(
                                        "\n  WARNING: " +
                                        ex.Message);
                                    Console.WriteLine(
                                        "  Cannot fix LBA." +
                                        " Aborting.");
                                    Console.ResetColor();
                                    Console.WriteLine();
                                    return;
                                }
                            }

                            if (isJap)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  [JAP] Using" +
                                    " Japanese version" +
                                    " (SLPS_201.04," +
                                    " 0x162360-0x162C30)");
                                Console.ResetColor();
                            }

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Auto-fixing LBA table" +
                                " in " +
                                (isJap
                                    ? "SLPS_201.04"
                                    : "SLUS_202.51") +
                                " inside ISO...");
                            Console.ResetColor();

                            int changes =
                                HarvestIso.FixLba(
                                    isoPath, isJap);

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
                                    "  Patched " + changes +
                                    " LBA entries in ISO.");
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
                            "-fixisoonly" +
                            " <file.iso>");
                        {
                            string isoPath =
                                args[1];
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

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Repairing ISO" +
                                " structure...");
                            Console.ResetColor();

                            IsoRepair.FixIso(
                                isoPath);
                        }
                        break;

                    // ════════════════════════
                    // FIXISO - Does everything:
                    // 1. Repairs ISO structure
                    // 2. Patches PS2 logo
                    // 3. Fixes LBA table
                    // ════════════════════════
                    case "fixiso":
                        {
                            int japIdx;
                            bool isJap = DetectJapFlag(
                                args, out japIdx);
                            string[] cleanArgs = isJap
                                ? RemoveJapFlag(args, japIdx)
                                : args;

                            RequireArgs(cleanArgs, 2,
                                "-fixiso [-jap] <file.iso>");

                            string isoPath = cleanArgs[1];
                            if (!Path.IsPathRooted(isoPath))
                                isoPath = Path.Combine(
                                    Directory
                                        .GetCurrentDirectory(),
                                    isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(isoPath);

                            // ─── AUTO-DETECT VERSION
                            // if -jap not manually specified
                            if (!isJap)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Auto-detecting" +
                                    " ISO version...");
                                Console.ResetColor();

                                try
                                {
                                    string detectedElf;
                                    bool autoJap =
                                        HarvestIso
                                            .AutoDetectVersion(
                                                isoPath,
                                                out detectedElf);

                                    if (autoJap)
                                    {
                                        isJap = true;
                                        Console.ForegroundColor =
                                            ConsoleColor.Yellow;
                                        Console.WriteLine(
                                            "  [Auto-detected]" +
                                            " Japanese version" +
                                            " (SLPS_201.04)" +
                                            " found in ISO!");
                                        Console.ResetColor();
                                    }
                                    else
                                    {
                                        Console.ForegroundColor =
                                            ConsoleColor.Green;
                                        Console.WriteLine(
                                            "  [Auto-detected]" +
                                            " USA version" +
                                            " (SLUS_202.51)" +
                                            " found in ISO.");
                                        Console.ResetColor();
                                    }
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
                                    return;
                                }
                            }

                            string elfName = isJap
                                ? "SLPS_201.04"
                                : "SLUS_202.51";

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            Console.WriteLine(
                                " FIXISO - Full Auto Fix" +
                                (isJap
                                    ? " [JAP]"
                                    : " [USA]"));
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            Console.ResetColor();

                            if (isJap)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Yellow;
                                Console.WriteLine(
                                    "  Japanese version:" +
                                    " SLPS_201.04");
                                Console.ResetColor();
                            }

                            // ─── STEP 1: Fix ISO structure
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

                            // ─── STEP 2: Fix PS2 logo
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
                                    isoPath, null, isJap);
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

                            // ─── STEP 3: Fix LBA table
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
                                        isoPath, isJap);

                                if (changes == 0)
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Green;
                                    Console.WriteLine(
                                        "  LBA table" +
                                        " already correct.");
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

                            Console.WriteLine();
                            Console.ForegroundColor =
                                ConsoleColor.Green;
                            Console.WriteLine(
                                "═══════════════════" +
                                "═══════════════════");
                            Console.WriteLine(
                                " ALL DONE! ISO is" +
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
                            int japIdxLogo;
                            bool isJapLogo =
                                DetectJapFlag(
                                    args,
                                    out japIdxLogo);
                            string[] cleanLogo = isJapLogo
                                ? RemoveJapFlag(
                                    args, japIdxLogo)
                                : args;

                            RequireArgs(cleanLogo, 2,
                                "-fixps2logo" +
                                " [-jap] <file.iso>");

                            string isoPath = cleanLogo[1];
                            if (!Path.IsPathRooted(isoPath))
                                isoPath = Path.Combine(
                                    Directory
                                        .GetCurrentDirectory(),
                                    isoPath);
                            isoPath =
                                HarvestIso.GetRealPath(
                                    isoPath);

                            // ─── AUTO-DETECT if no -jap flag
                            if (!isJapLogo)
                            {
                                Console.ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Auto-detecting" +
                                    " ISO version...");
                                Console.ResetColor();

                                bool aborted;
                                isJapLogo = AutoDetectJap(
                                    isoPath, out aborted);

                                if (aborted) break;
                            }

                            IsoLogoPatcher.PatchIso(
                                isoPath, null, isJapLogo);
                        }
                        break;

                    // ════════════════════════
                    // ISO / BIN CONVERTER
                    // ════════════════════════
                    case "convertiso":
                        RequireArgs(args, 3,
                            "-convertiso" +
                            " <input>" +
                            " <output>");
                        {
                            string inPath =
                                args[1];
                            string outPath =
                                args[2];
                            if (!Path
                                    .IsPathRooted(
                                        inPath))
                                inPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        inPath);
                            if (!Path
                                    .IsPathRooted(
                                        outPath))
                                outPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        outPath);

                            IsoConverter.Convert(
                                inPath,
                                outPath);
                        }
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
            // ── HDA ───────────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== HDA Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xhda  <file.hda>" +
                " <out_folder>");
            Console.WriteLine(
                "  -chda  <in_folder>" +
                " <file.hda>");
            Console.WriteLine(
                "  -chda raw/uncomp" +
                " <in_folder> <file.hda>");
            Console.WriteLine();

            // ── Compression ───────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== Single File" +
                " Compression ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -compress   " +
                "<input_file> <output_file>");
            Console.WriteLine(
                "  -uncompress " +
                "<input_file> <output_file>");
            Console.WriteLine();

            // ── Text ──────────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== Text Commands ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xtxt <text.bin>" +
                " <ptr.bin> <out.txt>");
            Console.WriteLine(
                "  -ctxt <in.txt>" +
                " <text.bin> <ptr.bin>");
            Console.WriteLine();

            // ── ELF ───────────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== ELF Commands ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -fixelf <SLUS>" +
                " <lba> <size>");
            Console.WriteLine();

            // ── ISO LBA AUTO-FIXER (NEW) ──────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== ISO LBA Auto-Fixer" +
                " (NEW) ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -fixlba <file.iso>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Auto-reads real LBAs" +
                " from ISO and writes new");
            Console.WriteLine(
                "    LBA table into" +
                " SLUS_202.51 at offset" +
                " 0x162460-0x162D30.");
            Console.WriteLine(
                "    ONLY the LBA table" +
                " is modified. Nothing" +
                " else touched.");
            Console.WriteLine(
                "    Supports ISO, BIN," +
                " RAW 2048/2352/2336" +
                " formats.");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Example:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe fixlba" +
                " HMSTH_MODDED.iso");
            Console.ResetColor();
            Console.WriteLine();

            // ── ISO LBA AUTO-FIXER FOR JAPANESE VERSION (NEW) ──────
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Japanese Version" +
                " Support (NEW v1.4.9):");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Add -jap flag for" +
                " Japanese version" +
                " (SLPS_201.04).");
            Console.WriteLine(
                "    LBA table at" +
                " 0x162360-0x162C30" +
                " (vs USA 0x162460" +
                "-0x162D30).");
            Console.WriteLine(
                "    Works with or" +
                " without hyphen:" +
                " -jap / jap / -jp");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe fixlba" +
                " -jap HMSTH_JAP.iso");
            Console.WriteLine(
                "    tool.exe fixiso" +
                " -jap HMSTH_JAP.iso");
            Console.WriteLine(
                "    tool.exe fixelf" +
                " -jap SLPS_201.04" +
                " 1234 56789");
            Console.WriteLine(
                "    tool.exe fixlba" +
                " HMSTH_USA.iso" +
                "    (USA, no flag)");
            Console.ResetColor();
            Console.WriteLine();


            // ── ISO REPAIR / PS2 LOGO / CONVERT ───
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== ISO Commands" +
                " ===");
            Console.ResetColor();

            Console.WriteLine(
                "  -fixiso <file.iso>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    ALL-IN-ONE: Repairs" +
                " ISO + Patches PS2" +
                " logo + Fixes LBA" +
                " table");
            Console.WriteLine(
                "    Automatically runs" +
                " all 3 fixes in the" +
                " correct order.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -fixisoonly" +
                " <file.iso>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Only repairs ISO" +
                " structure (no logo," +
                " no LBA fix).");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -fixps2logo" +
                " <file.iso>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Only fixes PS2" +
                " logo + Master Disc" +
                " markers.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -fixlba" +
                " <file.iso>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Only fixes LBA" +
                " table in SLUS_202.51.");
            Console.ResetColor();
            Console.WriteLine();

            // ── FAKE YEAR OF THE FILES ───
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== Change Year" +
                " ===");
            Console.ResetColor();

            Console.WriteLine(
                "  -fakeyear" +
                " [year] <file.iso>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Changes year on" +
                " all files with year" +
                " > 2001 to your year.");
            Console.WriteLine(
                "    Leaves files with" +
                " year <= 2001" +
                " unchanged.");
            Console.WriteLine(
                "    Only changes year" +
                " - month/day/time" +
                " stay the same.");
            Console.WriteLine(
                "    Also patches the" +
                " ISO's own PVD dates.");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    fakeyear 2000" +
                " HMSTH.iso");
            Console.WriteLine(
                "    fakeyear HMSTH.iso" +
                "  (defaults to 2001)");
            Console.ResetColor();

            // ── Audio / Music ──────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "=== Audio / Music ===");
            Console.ResetColor();

            Console.WriteLine(
                "  -cmusic <input.vag>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Converts a looped" +
                " .VAG file into PS2" +
                " .BD/.HD/.SQ music.");
            Console.WriteLine(
                "    Sample rate is" +
                " auto-detected from" +
                " the VAG.");
            Console.WriteLine(
                "    Output goes into a" +
                " subfolder named after" +
                " the VAG.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -xvag <bd> <hd>" +
                " <index> [output.vag]");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Extracts a single" +
                " VAG from BD/HD bank.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -xvag all <bd> <hd>" +
                " <out_folder>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Extracts ALL VAGs" +
                " from a BD/HD bank" +
                " into a folder.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -rvag <index>" +
                " <input.vag> <bd> <hd>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Replaces a single" +
                " VAG at <index> in" +
                " BD/HD bank.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine(
                "  -rvag all <folder>" +
                " <bd> <hd>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Replaces ALL VAGs" +
                " in a BD/HD bank from" +
                " a folder of .VAG" +
                " files.");
            Console.ResetColor();

            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Music Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe cmusic" +
                " mymusic.vag");
            Console.WriteLine(
                "    tool.exe xvag" +
                " SE.BD SE.HD 5" +
                " sound.vag");
            Console.WriteLine(
                "    tool.exe xvag all" +
                " SE.BD SE.HD" +
                " ./extracted");
            Console.WriteLine(
                "    tool.exe rvag 0" +
                " new.vag SE.BD SE.HD");
            Console.WriteLine(
                "    tool.exe rvag all" +
                " ./mods SE.BD SE.HD");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Audio Format Info:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    BD = Body (raw" +
                " ADPCM audio data)");
            Console.WriteLine(
                "    HD = Header (bank" +
                " info, sample rates)");
            Console.WriteLine(
                "    SQ = Sequence (PS2" +
                " MIDI that plays BD)");
            Console.ResetColor();
            Console.WriteLine();

            // ── RDTB ──────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== RDTB Model Archive" +
                " (v2.0 CORRECTED) ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -irdtb   <file.rdtb>");
            Console.WriteLine(
                "  -irdtbnb <file.rdtb>");
            Console.WriteLine(
                "  -xrdtb   <file.rdtb>" +
                " <out_folder>");
            Console.WriteLine(
                "  -crdtb   <in_folder>" +
                " <file.rdtb>");
            Console.WriteLine(
                "  -srdtb   <file.rdtb>" +
                "  (skeleton tree)");
            Console.WriteLine(
                "  -rrdtb   <file_a.rdtb>" +
                " <file_b.rdtb>  (compare)");
            Console.WriteLine(
                "  -vrdtb   <orig.rdtb>" +
                " <rebuilt.rdtb>  (verify)");
            Console.WriteLine(
                "  -rcrdtb  <file.rdtb>" +
                " <index> <chunk.bin>");
            Console.WriteLine(
                "  -scanrdtb <folder>");
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  NEW v2.0:");
            Console.ResetColor();
            Console.WriteLine(
                "  -mrdtb   <file.rdtb>" +
                "  (material table)");
            Console.WriteLine(
                "  -detect  <any_file>" +
                "  (find embedded RDTBs)");
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  Bone layout CORRECTED:" +
                " byte0=self byte3=parent" +
                " bytes4-15=XYZ");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  NEW Diagnostics:");
            Console.ResetColor();
            Console.WriteLine(
                "  -imodel <file.rdtb>"
                + "  (per-batch tri/vc"
                + " stats + memory)");
            Console.WriteLine(
                "  -iobj   <file.obj>"
                + "   (vertices, tris,"
                + " groups, bounds)");
            Console.WriteLine(
                "  -idae   <file.dae>"
                + "   (geometries, mats,"
                + " textures)");
            Console.ResetColor();
            Console.WriteLine();

            // ── GDTB ──────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "=== GDTB Texture Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -igdtb  <file.gdtb>");
            Console.WriteLine(
                "  -xgdtb  <file.gdtb>" +
                " <out_folder>");
            Console.WriteLine(
                "  -cgdtb  <in_folder>" +
                " <file.gdtb>");
            Console.WriteLine(
                "  -rgdtb  <index>" +
                " <tex.bmp> <file.gdtb>");
            Console.WriteLine(
                "  -rfgdtb <folder>" +
                " <file.gdtb>");
            Console.WriteLine(
                "  -cngdtb <number>" +
                " <file.gdtb>");
            Console.WriteLine();

            // ── BMP ───────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "=== PS2 BMP Converter ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -tops2bmp <image.bmp>");
            Console.WriteLine(
                "  -towinbmp <image.bmp>");
            Console.WriteLine(
                "  -xbmppal  <image.bmp>" +
                " <palette_name>");
            Console.WriteLine(
                "  -rbmppal  <palette_file>" +
                " <image.bmp>");
            Console.WriteLine();

            // ── SRDB ──────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "=== SRDB Map Archive" +
                " (3D Models) ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xsrdb   <file.srdb>" +
                " <out_folder>");
            Console.WriteLine(
                "    Extract raw SRDB chunks");
            Console.WriteLine(
                "  -csrdb   <in_folder>" +
                " <file.srdb>");
            Console.WriteLine(
                "    Repack SRDB from chunks");
            Console.WriteLine(
                "  -xsrdb3d <file.srdb>" +
                " <file.gdtb> <base>");
            Console.WriteLine(
                "    Extract 3D models." +
                " Creates 4 folders:");
            Console.WriteLine(
                "      <base>_embedded_rdtbs_obj/");
            Console.WriteLine(
                "      <base>_embedded_rdtbs_dae/");
            Console.WriteLine(
                "      <base>_all_obj/");
            Console.WriteLine(
                "      <base>_all_dae/");
            Console.WriteLine(
                "  -csrdb3d <in_folder>" +
                " <out_folder> [--scale N]");
            Console.WriteLine(
                "    Rebuild SRDB from OBJ files");
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  NEW v2.0:");
            Console.ResetColor();
            Console.WriteLine(
                "  -isrdb      <file.srdb>" +
                "  (info + detect embedded RDTBs)");
            Console.WriteLine(
                "  -xsrdbrdtb  <file.srdb>" +
                " <out_folder>");
            Console.WriteLine(
                "    Extract all embedded" +
                " RDTBs from SRDB");
            Console.WriteLine();

            // ── SRDB 3D Batch Modding ──────────────────────
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "=== SRDB Batch " +
                "Modding ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xsrdbbatches" +
                " <srdb> <gdtb>" +
                " <out_dir>");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Extract SRDB into" +
                " per-RDTB folders with" +
                " OBJ+MTL+textures");
            Console.WriteLine(
                "    ready for CBATCHES" +
                " modding workflow.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(
                "  -csrdbbatches" +
                " <in_dir> <out.srdb>" +
                " [out.gdtb]");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Rebuild SRDB from" +
                " folder. Uses" +
                " _modded.rdtb if" +
                " present in each");
            Console.WriteLine(
                "    embedded_NN/, else" +
                " _source.rdtb." +
                " Updates master table.");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  SRDB Modding" +
                " Workflow:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    1. xsrdbbatches" +
                " map.srdb map.gdtb" +
                " srdb_out");
            Console.WriteLine(
                "    2. Edit OBJs in" +
                " Blender under any" +
                " embedded_NN/" +
                "model_XX/");
            Console.WriteLine(
                "    3. cbatches" +
                " srdb_out\\embedded_09" +
                " embedded_09_out");
            Console.WriteLine(
                "    4. Copy the" +
                " output .rdtb to" +
                " srdb_out\\" +
                "embedded_09\\" +
                "_modded.rdtb");
            Console.WriteLine(
                "    5. csrdbbatches" +
                " srdb_out final.srdb" +
                " final.gdtb");
            Console.ResetColor();
            Console.WriteLine();


            // ── 3D Model ──────────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== 3D Model Tools ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -x3d <file.rdtb>"
                + " <file.gdtb> <base>");
            Console.WriteLine(
                "    Extract for VIEWING"
                + " only. Creates:");
            Console.WriteLine(
                "      <base>_all_obj/"
                + "  (single OBJ + textures)");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  For MODDING use:");
            Console.ResetColor();
            Console.WriteLine(
                "  -xbatches <rdtb>"
                + " <gdtb> <base>");
            Console.WriteLine(
                "    Extract per-batch"
                + " OBJ files (round-"
                + "trip safe)");
            Console.WriteLine(
                "  -cbatches <folder>"
                + " <out_folder>"
                + " [--small] [--big]"
                + " [--mirrored]");
            Console.WriteLine(
                "    Rebuild RDTB+GDTB"
                + " (default: mirror)");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  3D Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe x3d" +
                " BOY_00000.rdtb" +
                " BOY_00001.gdtb BOY");
            Console.WriteLine(
                "    tool.exe x3d" +
                " HAYATO_00000.rdtb" +
                " HAYATO_00001.gdtb KURT");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine(
                "  -scanbatch <rdtb>"
                + " <batch>  (find model"
                + " group)");
            Console.WriteLine(
                "  -xbatch <rdtb>"
                + " <batch> <out.obj>");
            Console.WriteLine(
                "  -xmodel <rdtb>"
                + " <batch> <out.obj>"
                + "  (all siblings)");

            Console.WriteLine(
                "  -xbatches <rdtb>"
                + " <gdtb> <base>"
                + "  (per-batch folder)");
            Console.WriteLine(
                "  -cbatches <folder>"
                + " <out_folder>"
                + " [--normals MODE]"
                + " [--normals-xyz X,Y,Z]"
                + " [-all]");
            Console.WriteLine(
                "    --normals--forcenew");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "      Use Blender normals"
                + " as-is. Skips the"
                + " automatic nearest-"
                + "neighbor transfer from"
                + " the original batch.");
            Console.ResetColor();

            // ── RDTB FORMAT CONVERTERS ──
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== RDTB Format Converters"
                + " ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -fmtrdtb      <file.rdtb>"
                + "  (detect format)");
            Console.WriteLine(
                "  -big2small    <in.rdtb>"
                + " <out.rdtb>");
            Console.WriteLine(
                "  -small2big    <in.rdtb>"
                + " <out.rdtb>");
            Console.WriteLine(
                "  -big2mirror   <in.rdtb>"
                + " <out.rdtb>");
            Console.WriteLine(
                "  -mirror2big   <in.rdtb>"
                + " <out.rdtb>");
            Console.WriteLine(
                "  -small2mirror <in.rdtb>"
                + " <out.rdtb>");
            Console.WriteLine(
                "  -mirror2small <in.rdtb>"
                + " <out.rdtb>");
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Format flags for c3d /"
                + " cbatches:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    --small      output as"
                + " small RDTB (single mesh)");
            Console.WriteLine(
                "    --mirrored   output as"
                + " mirrored RDTB (slots"
                + " 9/10=8, 12/13=11)");
            Console.WriteLine(
                "    --big        output as"
                + " big RDTB (all 14 chunks)");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe c3d"
                + " FLAT_3d_batches_obj"
                + " ./out --small");
            Console.WriteLine(
                "    tool.exe cbatches"
                + " FLAT_3d_batches_obj"
                + " ./out --mirrored");
            Console.WriteLine(
                "    tool.exe big2small"
                + " BOY_00000.rdtb"
                + " BOY_SMALL.rdtb");
            Console.WriteLine(
                "    tool.exe small2big"
                + " FLAT_00000.rdtb"
                + " FLAT_BIG.rdtb");
            Console.WriteLine(
                "    tool.exe fmtrdtb"
                + " BOY_00000.rdtb");
            Console.ResetColor();
            Console.WriteLine();

            // ── Audio ─────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "=== Audio / Music ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xvag <bd> <hd>" +
                " <index> [output.vag]");
            Console.WriteLine(
                "  -rvag <index> <input.vag>" +
                " <bd> <hd>");
            Console.WriteLine(
                "  -xvag all <bd> <hd>" +
                " <out_folder>");
            Console.WriteLine(
                "  -rvag all <folder>" +
                " <bd> <hd>");
            Console.WriteLine();

            // ── BOY Scaler ────────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== BOY Advanced Bone" +
                " Scaler ===");
            Console.ResetColor();

            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "  Scales BOY player"
                + " bones only.");
            Console.WriteLine(
                "  Uses BoyScaler"
                + " (BOY-specific logic).");
            Console.ResetColor();
            Console.WriteLine(
                "  -boyscale <skeleton.bin>"
                + " [options]");
            Console.WriteLine(
                "    --b<N> <v>"
                + "        Bone N all axes");
            Console.WriteLine(
                "    --b<N>x/y/z <v>"
                + "   Bone N one axis");
            Console.WriteLine(
                "    --spine --neck"
                + " --arms --legs ...");
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe boyscale"
                + " 00_skeleton.bin"
                + " --b2y 1.20 --b3y 1.20");
            Console.WriteLine(
                "    tool.exe boyscale"
                + " 00_skeleton.bin"
                + " --legsy 1.25");
            Console.ResetColor();
            Console.WriteLine();

            // ── NPC Universal Bone Scaler ─────
            Console.ForegroundColor =
                ConsoleColor.Magenta;

            Console.WriteLine(
                "=== NPC / Universal Bone"
                + " Scaler v3.1 ===");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "  Works with ANY RDTB"
                + " skeleton (Boy, NPC,");
            Console.WriteLine(
                "  small RDTB, big RDTB,"
                + " mirrored RDTB).");
            Console.WriteLine(
                "  Run on extracted"
                + " 00_skeleton.bin");
            Console.WriteLine(
                "  from XRDTB output folder.");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine(
                "  -bonescale <skeleton.bin>"
                + " [options]");
            Console.WriteLine();

            // Direct bone scaling
            Console.ForegroundColor =
                ConsoleColor.White;

            Console.WriteLine(
                "  Direct bone scaling"
                + " (always accurate):");
            Console.ResetColor();
            Console.WriteLine(
                "    --b<N> <v>"
                + "        Scale bone N"
                + " all axes");
            Console.WriteLine(
                "    --b<N>x <v>"
                + "       Scale bone N"
                + " X axis only");
            Console.WriteLine(
                "    --b<N>y <v>"
                + "       Scale bone N"
                + " Y axis only");
            Console.WriteLine(
                "    --b<N>z <v>"
                + "       Scale bone N"
                + " Z axis only");
            Console.WriteLine();

            // Group scaling
            Console.ForegroundColor =
                ConsoleColor.White;

            Console.WriteLine(
                "  Group scaling"
                + " (world-position based):");
            Console.ResetColor();
            Console.WriteLine(
                "    --spine <v>"
                + "       Spine / chest bones");
            Console.WriteLine(
                "    --neck <v>"
                + "        Neck bones");
            Console.WriteLine(
                "    --head <v>"
                + "        Head bones");
            Console.WriteLine(
                "    --arms <v>"
                + "        Both arms");
            Console.WriteLine(
                "    --larm <v>"
                + "        Left arm");
            Console.WriteLine(
                "    --rarm <v>"
                + "        Right arm");
            Console.WriteLine(
                "    --shoulders <v>"
                + "   Shoulder bones");
            Console.WriteLine(
                "    --lshldr <v>"
                + "      Left shoulder");
            Console.WriteLine(
                "    --rshldr <v>"
                + "      Right shoulder");
            Console.WriteLine(
                "    --hands <v>"
                + "       Both hands");
            Console.WriteLine(
                "    --lhand <v>"
                + "       Left hand");
            Console.WriteLine(
                "    --rhand <v>"
                + "       Right hand");
            Console.WriteLine(
                "    --fingers <v>"
                + "     Finger bones");
            Console.WriteLine(
                "    --legs <v>"
                + "        Both legs");
            Console.WriteLine(
                "    --lleg <v>"
                + "        Left leg");
            Console.WriteLine(
                "    --rleg <v>"
                + "        Right leg");
            Console.WriteLine(
                "    --hips <v>"
                + "        Hip bones");
            Console.WriteLine(
                "    --lhip <v>"
                + "        Left hip");
            Console.WriteLine(
                "    --rhip <v>"
                + "        Right hip");
            Console.WriteLine(
                "    --thighs <v>"
                + "      Thigh bones");
            Console.WriteLine(
                "    --lthigh <v>"
                + "      Left thigh");
            Console.WriteLine(
                "    --rthigh <v>"
                + "      Right thigh");
            Console.WriteLine(
                "    --shins <v>"
                + "       Shin bones");
            Console.WriteLine(
                "    --lshin <v>"
                + "       Left shin");
            Console.WriteLine(
                "    --rshin <v>"
                + "       Right shin");
            Console.WriteLine(
                "    --ankles <v>"
                + "      Ankle bones");
            Console.WriteLine(
                "    --lankle <v>"
                + "      Left ankle");
            Console.WriteLine(
                "    --rankle <v>"
                + "      Right ankle");
            Console.WriteLine(
                "    --feet <v>"
                + "        Foot bones");
            Console.WriteLine(
                "    --lfoot <v>"
                + "       Left foot");
            Console.WriteLine(
                "    --rfoot <v>"
                + "       Right foot");
            Console.WriteLine(
                "    --upper <v>"
                + "       Upper body");
            Console.WriteLine(
                "    --lower <v>"
                + "       Lower body");
            Console.WriteLine(
                "    --all <v>"
                + "         All bones");
            Console.WriteLine();

            // Axis suffix
            Console.ForegroundColor =
                ConsoleColor.White;

            Console.WriteLine(
                "  Add x/y/z for one axis:");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    --legsy 1.25"
                + "     → legs Y axis only");
            Console.WriteLine(
                "    --spiney 1.1"
                + "     → spine Y axis only");
            Console.WriteLine(
                "    --armsx 0.9"
                + "      → arms X axis only");
            Console.ResetColor();
            Console.WriteLine();

            // Other flags
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Other flags:");
            Console.ResetColor();

            Console.WriteLine(
                "    --info"
                + "   Show raw bone data"
                + " (WX, WY, LX, LY,"
                + " POS%)");
            Console.WriteLine();

            // Boy bone reference
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Boy skeleton quick"
                + " reference:");
            Console.ResetColor();

            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    Bone  2 = SPINE_BASE"
                + "    Bone  3 = SPINE_MID");
            Console.WriteLine(
                "    Bone  4 = SPINE_TOP"
                + "    Bone  5 = NECK");
            Console.WriteLine(
                "    Bone 15 = SHOULDER_R"
                + "   Bone 32 = SHOULDER_L");
            Console.WriteLine(
                "    Bone 17 = UPPER_ARM_R"
                + "  Bone 34 = UPPER_ARM_L");
            Console.WriteLine(
                "    Bone 18 = ELBOW_R"
                + "      Bone 35 = ELBOW_L");
            Console.WriteLine(
                "    Bone 20 = HAND_R"
                + "       Bone 37 = HAND_L");
            Console.WriteLine(
                "    Bone 50 = HIP_R"
                + "        Bone 59 = HIP_L");
            Console.WriteLine(
                "    Bone 51 = THIGH_R"
                + "     Bone 60 = THIGH_L");
            Console.WriteLine(
                "    Bone 52 = SHIN_R"
                + "      Bone 61 = SHIN_L");
            Console.WriteLine(
                "    Bone 53 = ANKLE_R"
                + "     Bone 62 = ANKLE_L");
            Console.WriteLine(
                "    Bone 54 = FOOT_R"
                + "      Bone 63 = FOOT_L");
            Console.ResetColor();
            Console.WriteLine();

            // Examples
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  bonescale Examples:");

            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin --info");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --b2y 1.20 --b3y 1.20");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --legsy 1.25");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --all 1.1");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --spine 1.1 --legs 0.9");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --lleg 1.15 --rleg 1.15");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --thighs 1.2 --shins 1.1");
            Console.WriteLine(
                "    tool.exe bonescale"
                + " 00_skeleton.bin"
                + " --b52y 1.3 --b61y 1.3");
            Console.ResetColor();
            Console.WriteLine();

            // ── BOY Presets ───────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== BOY Mod Presets ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -boymodv2 -bin" +
                " 00_skeleton.bin");
            Console.WriteLine(
                "  -boymodv2 -rdtb" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "  -boymodv3 -bin" +
                " 00_skeleton.bin");
            Console.WriteLine(
                "  -boymodv4 -bin" +
                " 00_skeleton.bin");
            Console.WriteLine(
                "  -boymodv4 -rdtb" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "  -boyoriginal" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "  Also: boyrestore," +
                " boyback, boyorig");
            Console.WriteLine();

            // ── Examples ──────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== General Examples ===");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe xhda" +
                " game.hda ./output");
            Console.WriteLine(
                "  tool.exe chda" +
                " ./folder game.hda");
            Console.WriteLine(
                "  tool.exe xrdtb" +
                " BOY_00000.rdtb ./boy_out");
            Console.WriteLine(
                "  tool.exe crdtb" +
                " ./boy_out BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe mrdtb" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe detect" +
                " FRM_MAP_00000.srdb");
            Console.WriteLine(
                "  tool.exe xsrdbrdtb" +
                " FRM_MAP_00000.srdb" +
                " ./extracted_rdtbs");
            Console.WriteLine(
                "  tool.exe isrdb" +
                " FRM_MAP_00000.srdb");
            Console.WriteLine(
                "  tool.exe xgdtb" +
                " textures.gdtb ./output");
            Console.WriteLine(
                "  tool.exe cgdtb" +
                " ./bmps textures.gdtb");
            Console.WriteLine(
                "  tool.exe xtxt" +
                " File_00001.bin" +
                " File_00000.bin hayato.txt");
            Console.WriteLine(
                "  tool.exe ctxt hayato.txt" +
                " File_00001.bin" +
                " File_00000.bin");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
