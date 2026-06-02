using HMSTHModdingTool;
using HMSTHModdingTool.BMP;
using HMSTHModdingTool.RDTB;
using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.SRDB;
using HMSTHModdingTool.IO;
using HMSTHModdingTool.IO.Compression;
using HMSTHModdingTool.BoyMods;
using System;
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
            "v1.4.5-Beta";
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
                        RequireArgs(args, 4,
                            "-fixelf <SLUS>" +
                            " <lba> <size>");
                        HarvestElf.Fix(
                            args[1],
                            uint.Parse(args[2]),
                            uint.Parse(args[3]));
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
                            "-xsrdb <file.srdb>" +
                            " <out_folder>");
                        SRDBArchive.Extract(
                            args[1], args[2]);
                        break;

                    // ════════════════════════
                    // CSRDB - Repack embedded
                    // RDTBs into SRDB (new)
                    // ════════════════════════
                    case "csrdb":
                        RequireArgs(args, 3,
                            "-csrdb <in_folder>" +
                            " <file.srdb>");
                        SRDBArchive.Create(
                            args[1], args[2]);
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

                    case "c3d":
                        {
                            float scaleC3d = 1.0f;
                            var cleanC3d =
                                new System.Collections.Generic
                                    .List<string>();
                            int ic3d = 1;
                            while (ic3d < args.Length)
                            {
                                string a = args[ic3d];
                                if (a.ToLower() == "--scale"
                                    || a.ToLower() == "-scale"
                                    || a.ToLower() == "-s")
                                {
                                    if (ic3d + 1 < args.Length)
                                    {
                                        float.TryParse(
                                            args[ic3d + 1],
                                            out scaleC3d);
                                        if (scaleC3d <= 0)
                                            scaleC3d = 1.0f;
                                        ic3d += 2;
                                        continue;
                                    }
                                }
                                cleanC3d.Add(a);
                                ic3d++;
                            }
                            if (cleanC3d.Count == 2)
                            {
                                // Auto-detect by checking for
                                // rebuild_manifest.json content
                                // to determine if this is SRDB
                                // or RDTB output folder
                                string mfp = Path.Combine(
                                    cleanC3d[0],
                                    "rebuild_manifest.json");
                                bool isSrdbFolder = false;
                                if (File.Exists(mfp))
                                {
                                    string mfc =
                                        File.ReadAllText(mfp);
                                    // SRDB manifests have
                                    // embedded_rdtbs key
                                    isSrdbFolder = mfc.Contains(
                                        "\"embedded_rdtbs\"");
                                }

                                if (isSrdbFolder)
                                {
                                    Console.ForegroundColor =
                                        ConsoleColor.Cyan;
                                    Console.WriteLine(
                                        "[Auto-detect] SRDB" +
                                        " folder detected" +
                                        " -> csrdb3d");
                                    Console.ResetColor();
                                    SRDBArchive.Create3D(
                                        cleanC3d[0],
                                        cleanC3d[1],
                                        scaleC3d);
                                }
                                else
                                {
                                    Model3D.Create(
                                        cleanC3d[0],
                                        cleanC3d[1],
                                        scaleC3d);
                                }
                            }
                            else
                            {
                                Console.WriteLine(
                                    "Usage: c3d <folder>" +
                                    " <output> [--scale N]");
                            }
                        }
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

                    case "boyoriginal":
                    case "boyrestore":
                    case "boyback":
                    case "boyorig":
                        BoyModPresets
                            .ApplyOriginal(args);
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
                    "fixelf",
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
                    "boyscale",
                    "boymodv2",   "boymodv3",
                    "boyoriginal","boyrestore",
                    "boyback",    "boyorig",
                    "x3d",        "c3d",
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

            // ── 3D Model ──────────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== 3D Model Tools ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -x3d <file.rdtb>" +
                " <file.gdtb> <base>");
            Console.WriteLine(
                "    Extract 3D models with" +
                " textures. Creates:");
            Console.WriteLine(
                "      <base>_obj/");
            Console.WriteLine(
                "      <base>_dae/");
            Console.WriteLine(
                "      <base>_all_obj/");
            Console.WriteLine(
                "      <base>_all_dae/");
            Console.WriteLine(
                "  -x3d split <rdtb> <gdtb>" +
                " <base>  (per-batch split)");
            Console.WriteLine(
                "  -c3d <models_folder>" +
                " <output_folder>" +
                " [--scale N]");
            Console.WriteLine(
                "    Rebuild RDTB+GDTB from" +
                " edited OBJ/DAE files");
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
            Console.WriteLine(
                "    tool.exe c3d" +
                " BOY_obj BOY_NEW");
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
            Console.WriteLine(
                "  -boyscale <skeleton.bin>" +
                " [options]");
            Console.WriteLine(
                "    --b<N> <v>    all axes");
            Console.WriteLine(
                "    --b<N>x/y/z <v>  one axis");
            Console.WriteLine(
                "    --spine --neck" +
                " --arms --legs ...");
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe boyscale" +
                " 00_skeleton.bin" +
                " --b2y 1.20 --b3y 1.20");
            Console.WriteLine(
                "    tool.exe boyscale" +
                " 00_skeleton.bin" +
                " --legsy 1.25");
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
