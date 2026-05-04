using HMSTHModdingTool;
using HMSTHModdingTool.BMP;
using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.IO;
using HMSTHModdingTool.IO.Compression;
using HMSTHModdingTool.RDTB;
using HMSTHModdingTool.BoyMods;
using System;
using System.IO;

namespace HMSTHModdingTool
{
    class Program
    {
        // ─────────────────────────────────────────
        // VERSION INFO
        // ─────────────────────────────────────────
        const string TOOL_NAME =
            "HMSTHModdingTool original as" +
            " HDATextTool by gdkchan";
        const string TOOL_VERSION =
            "v1.4.3-Beta";
        const string TOOL_AUTHOR =
            "gdkchan + DarthKrayt333" +
            " & HMSTH Community";

        // ═════════════════════════════════════════
        // MAIN
        // ═════════════════════════════════════════
        static void Main(string[] args)
        {
            // ─────────────────────────────────────
            // INTERACTIVE MODE (double-click)
            // ─────────────────────────────────────
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
                    "Running in interactive mode.");
                Console.ForegroundColor =
                    ConsoleColor.Gray;
                Console.WriteLine(
                    "Working directory: " + exeDir);
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

                    if (input == null) continue;
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

        // ═════════════════════════════════════════
        // NORMALIZE COMMAND
        // ═════════════════════════════════════════
        static string NormalizeCommand(string cmd)
        {
            if (cmd.StartsWith("--"))
                return cmd.Substring(2).ToLower();
            if (cmd.StartsWith("-"))
                return cmd.Substring(1).ToLower();
            return cmd.ToLower();
        }

        // ═════════════════════════════════════════
        // RUN COMMAND
        // ═════════════════════════════════════════
        static void RunCommand(string[] args)
        {
            try
            {
                string cmd =
                    NormalizeCommand(args[0]);

                bool customFinish = false;

                switch (cmd)
                {
                    // ════════════════════════════
                    // HDA COMMANDS
                    // ════════════════════════════
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

                    // ════════════════════════════
                    // SHORTCUT: raw / uncomp
                    // ════════════════════════════
                    case "raw":
                    case "uncomp":
                        RequireArgs(args, 3,
                            "-raw <in_folder>" +
                            " <file.hda>");
                        {
                            string rawOut = args[2];
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
                                    rawDir, rawName);

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

                    // ════════════════════════════
                    // SHORTCUT: comp
                    // ════════════════════════════
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

                    // ════════════════════════════
                    // SINGLE FILE COMPRESS
                    // ════════════════════════════
                    case "compress":
                        RequireArgs(args, 3,
                            "-compress" +
                            " <input_file>" +
                            " <output_file>");
                        {
                            string inPath = args[1];
                            string outPath = args[2];

                            if (!File.Exists(inPath))
                            {
                                TextOut.PrintError(
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
                                "Compressing" +
                                " single file...");
                            Console.ResetColor();

                            var sw = System
                                .Diagnostics
                                .Stopwatch
                                .StartNew();

                            byte[] comp =
                                HarvestCompression
                                    .Compress(
                                        raw,
                                        (cur, total) =>
                                        {
                                            double pct =
                                                total == 0
                                                ? 100
                                                : (double)
                                                  cur *
                                                  100.0 /
                                                  total;
                                            Console
                                                .Error
                                                .Write(
                                                "\r  " +
                                                "{0:F1}%" +
                                                "  ({1:N0}" +
                                                "/{2:N0})" +
                                                "   ",
                                                pct,
                                                cur,
                                                total);
                                        });

                            Console.Error.Write(
                                "\r" +
                                new string(' ', 50) +
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
                                    .ForegroundColor =
                                    ConsoleColor
                                        .Yellow;
                                Console.WriteLine(
                                    "  Using single" +
                                    " literal" +
                                    " stream...");
                                Console.ResetColor();
                                comp =
                                    HarvestCompression
                                        .CompressAsLiterals(
                                            raw);
                                ok =
                                    HarvestCompression
                                        .VerifyRoundTrip(
                                            raw,
                                            comp);
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
                                ? ConsoleColor.Green
                                : ConsoleColor
                                    .Yellow;

                            Console.WriteLine(
                                "Done!  {0:N0}" +
                                " → {1:N0} bytes" +
                                "  ({2:F1}%)" +
                                "  in {3:F2}s",
                                raw.Length,
                                comp.Length,
                                ratio,
                                sw.Elapsed
                                    .TotalSeconds);
                            Console.ResetColor();

                            if (!ok)
                            {
                                Console
                                    .ForegroundColor =
                                    ConsoleColor.Red;
                                Console.WriteLine(
                                    "Verify" +
                                    " failed!");
                                Console.ResetColor();
                            }
                        }
                        break;

                    // ════════════════════════════
                    // SINGLE FILE UNCOMPRESS
                    // ════════════════════════════
                    case "uncompress":
                        RequireArgs(args, 3,
                            "-uncompress" +
                            " <input_file>" +
                            " <output_file>");
                        {
                            string inPath = args[1];
                            string outPath = args[2];

                            if (!File.Exists(inPath))
                            {
                                TextOut.PrintError(
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
                                "Decompressing" +
                                " single file...");
                            Console.ResetColor();

                            byte[] raw =
                                HarvestCompression
                                    .Decompress(comp);

                            File.WriteAllBytes(
                                outPath, raw);

                            Console.ForegroundColor =
                                ConsoleColor.Green;
                            Console.WriteLine(
                                "Done!  {0:N0}" +
                                " → {1:N0} bytes",
                                comp.Length,
                                raw.Length);
                            Console.ResetColor();
                        }
                        break;

                    // ════════════════════════════
                    // TEXT COMMANDS
                    // ════════════════════════════
                    case "xtxt":
                        RequireArgs(args, 4,
                            "-xtxt <text.bin>" +
                            " <ptr.bin> <out.txt>");
                        {
                            string xtxtData =
                                Path.GetFullPath(
                                    args[1]);
                            string xtxtPtrs =
                                Path.GetFullPath(
                                    args[2]);

                            if (string.Equals(
                                    xtxtData,
                                    xtxtPtrs,
                                    StringComparison
                                        .OrdinalIgnoreCase))
                            {
                                Console.WriteLine();
                                Console
                                    .ForegroundColor =
                                    ConsoleColor
                                        .Yellow;
                                Console.WriteLine(
                                    "  This is the" +
                                    " same file." +
                                    " These must be" +
                                    " two different" +
                                    " files.");
                                Console.WriteLine(
                                    "  text.bin : " +
                                    xtxtData);
                                Console.WriteLine(
                                    "  ptr.bin  : " +
                                    xtxtPtrs);
                                Console
                                    .ForegroundColor =
                                    ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Example:" +
                                    " -xtxt" +
                                    " <text.bin>" +
                                    " <ptr.bin>" +
                                    " <out.txt>");
                                Console.ResetColor();
                                Console.WriteLine();
                                customFinish = true;
                                break;
                            }

                            string datPath =
                                HarvestText
                                    .DecodeToFile(
                                        args[1],
                                        args[2],
                                        args[3]);

                            TextOut.PrintSuccess(
                                "Finished!");
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "  " +
                                Path.GetFileName(
                                    args[3]) +
                                " Exported");
                            Console.WriteLine(
                                "  " +
                                Path.GetFileName(
                                    datPath) +
                                " Exported");
                            Console.ResetColor();
                            customFinish = true;
                        }
                        break;

                    case "ctxt":
                        RequireArgs(args, 4,
                            "-ctxt <in.txt>" +
                            " <text.bin>" +
                            " <ptr.bin>");
                        {
                            string txtFull =
                                Path.GetFullPath(
                                    args[1]);
                            string datCheck =
                                Path.Combine(
                                    Path
                                        .GetDirectoryName(
                                            txtFull)
                                        ?? ".",
                                    Path
                                        .GetFileNameWithoutExtension(
                                            txtFull) +
                                    ".dat");

                            if (!File.Exists(
                                    datCheck))
                            {
                                Console.WriteLine();
                                Console
                                    .ForegroundColor =
                                    ConsoleColor
                                        .Yellow;
                                Console.WriteLine(
                                    "  Companion" +
                                    " .dat file" +
                                    " not found!");
                                Console.WriteLine(
                                    "  Expected: " +
                                    Path.GetFileName(
                                        datCheck));
                                Console.WriteLine(
                                    "  The .dat" +
                                    " file is" +
                                    " created when" +
                                    " you run" +
                                    " -xtxt.");
                                Console.WriteLine(
                                    "  It must stay" +
                                    " in the same" +
                                    " folder as" +
                                    " the .txt");
                                Console.WriteLine(
                                    "  and have the" +
                                    " same base" +
                                    " name.");
                                Console.ResetColor();
                                Console.WriteLine();
                                return;
                            }

                            HarvestText
                                .EncodeFromFile(
                                    args[1],
                                    args[2],
                                    args[3]);

                            TextOut.PrintSuccess(
                                "Finished!");
                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "  " +
                                Path.GetFileName(
                                    args[1]) +
                                " and " +
                                Path
                                    .GetFileNameWithoutExtension(
                                        args[1]) +
                                ".dat Being" +
                                " Combined and" +
                                " Completed!");
                            Console.ResetColor();
                            customFinish = true;
                        }
                        break;

                    case "fixelf":
                        RequireArgs(args, 4,
                            "-fixelf <SLUS>" +
                            " <lba> <size>");
                        HarvestElf.Fix(
                            args[1],
                            uint.Parse(args[2]),
                            uint.Parse(args[3]));
                        break;

                    // ════════════════════════════
                    // RDTB COMMANDS
                    // ════════════════════════════
                    case "irdtb":
                        RequireArgs(args, 2,
                            "-irdtb <file.rdtb>");
                        RDTBArchive.Info(args[1]);
                        break;

                    case "irdtbnb":
                        RequireArgs(args, 2,
                            "-irdtbnb <file.rdtb>");
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
                            "-rrdtb <file_a.rdtb>" +
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
                            "-rcrdtb <file.rdtb>" +
                            " <index> <chunk.bin>");
                        {
                            int rcIdx;
                            if (!int.TryParse(
                                    args[2],
                                    out rcIdx))
                            {
                                TextOut.PrintError(
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
                            "-scanrdtb <folder>");
                        RDTBArchive.ScanFolder(
                            args[1]);
                        break;

                    // ════════════════════════════
                    // GDTB COMMANDS
                    // ════════════════════════════
                    case "igdtb":
                        RequireArgs(args, 2,
                            "-igdtb <file.gdtb>");
                        GDTBArchive.Info(args[1]);
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
                                "Invalid index: " +
                                args[1]);
                            return;
                        }
                        GDTBArchive.Replace(
                            args[3], rIdx, args[2]);
                        break;

                    case "rfgdtb":
                        int startIdx = 0;
                        if (args.Length >= 4 &&
                            int.TryParse(
                                args[2],
                                out startIdx))
                        {
                            RequireArgs(args, 4,
                                "-rfgdtb <folder>" +
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
                                "-rfgdtb <folder>" +
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
                                "Invalid number: " +
                                args[1]);
                            return;
                        }
                        GDTBArchive.ChangeCount(
                            args[2], newCnt);
                        break;

                    // ════════════════════════════
                    // BMP CONVERTER COMMANDS
                    // ════════════════════════════
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
                        PS2BMPConverter.ToWindows(
                            args[1]);
                        break;

                    // ════════════════════════════
                    // BMP PALETTE COMMANDS
                    // ════════════════════════════
                    case "xbmppal":
                        RequireArgs(args, 3,
                            "-xbmppal <image.bmp>" +
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

                    // ════════════════════════════
                    // AUDIO COMMANDS
                    // ════════════════════════════
                    case "cmusic":
                        RequireArgs(args, 2,
                            "-cmusic" +
                            " <input.vag>");
                        {
                            string vagPath =
                                args[1];
                            if (!Path.IsPathRooted(
                                    vagPath))
                                vagPath =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        vagPath);

                            Console.ForegroundColor =
                                ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Converting VAG" +
                                " to BD/HD/SQ...");
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

                            string hdPathAllX =
                                args[3];
                            string bdPathAllX =
                                args[2];
                            string outFolder =
                                args[4];

                            if (!Path.IsPathRooted(
                                    hdPathAllX))
                                hdPathAllX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdPathAllX);
                            if (!Path.IsPathRooted(
                                    bdPathAllX))
                                bdPathAllX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdPathAllX);
                            if (!Path.IsPathRooted(
                                    outFolder))
                                outFolder =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        outFolder);

                            AudioBank.ExtractAllVags(
                                hdPathAllX,
                                bdPathAllX,
                                outFolder);
                        }
                        else
                        {
                            RequireArgs(args, 4,
                                "-xvag <bd_file>" +
                                " <hd_file>" +
                                " <index>" +
                                " [output.vag]");

                            string bdPathX =
                                args[1];
                            string hdPathX =
                                args[2];
                            int idxX =
                                int.Parse(args[3]);

                            string outVag;
                            if (args.Length >= 5 &&
                                !string
                                    .IsNullOrEmpty(
                                        args[4]))
                            {
                                outVag = args[4];
                            }
                            else
                            {
                                outVag =
                                    string.Format(
                                        "{0:000}.vag",
                                        idxX);
                            }

                            if (!Path.IsPathRooted(
                                    hdPathX))
                                hdPathX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdPathX);
                            if (!Path.IsPathRooted(
                                    bdPathX))
                                bdPathX =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdPathX);
                            if (!Path.IsPathRooted(
                                    outVag))
                                outVag =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        outVag);

                            AudioBank.ExtractVag(
                                hdPathX, bdPathX,
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
                                " <folder_with_vags>" +
                                " <bd_file>" +
                                " <hd_file>");

                            string folderWithVags =
                                args[2];
                            string bdPathAllI =
                                args[3];
                            string hdPathAllI =
                                args[4];

                            if (!Path.IsPathRooted(
                                    folderWithVags))
                                folderWithVags =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        folderWithVags);
                            if (!Path.IsPathRooted(
                                    bdPathAllI))
                                bdPathAllI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdPathAllI);
                            if (!Path.IsPathRooted(
                                    hdPathAllI))
                                hdPathAllI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdPathAllI);

                            AudioBank.ReplaceAllVags(
                                hdPathAllI,
                                bdPathAllI,
                                folderWithVags);
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
                            string inVag = args[2];
                            string bdPathI = args[3];
                            string hdPathI = args[4];

                            if (!Path.IsPathRooted(
                                    hdPathI))
                                hdPathI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        hdPathI);
                            if (!Path.IsPathRooted(
                                    bdPathI))
                                bdPathI =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        bdPathI);
                            if (!Path.IsPathRooted(
                                    inVag))
                                inVag =
                                    Path.Combine(
                                        Directory
                                            .GetCurrentDirectory(),
                                        inVag);

                            AudioBank.ImportVag(
                                hdPathI, bdPathI,
                                idxI, inVag);
                        }
                        break;

                    // ════════════════════════════
                    // BOY ADVANCED BONE SCALER
                    // ════════════════════════════
                    case "boyscale":
                        BoyScaler.Run(args);
                        customFinish = true;
                        break;

                    // ════════════════════════════
                    // BOY MOD PRESETS
                    // ════════════════════════════
                    case "boymodv2":
                        BoyModPresets.ApplyModV2(args);
                        break;

                    case "boymodv3":
                        BoyModPresets.ApplyModV3(args);
                        break;

                    case "boyoriginal":
                    case "boyrestore":
                    case "boyback":
                    case "boyorig":
                        BoyModPresets.ApplyOriginal(args);
                        break;

                    // ════════════════════════════
                    // UNKNOWN COMMAND
                    // ════════════════════════════
                    default:
                        Console.WriteLine();
                        Console.ForegroundColor =
                            ConsoleColor.Yellow;
                        Console.WriteLine(
                            "  Unknown command: " +
                            args[0]);
                        Console.WriteLine();
                        Console.WriteLine(
                            "  Type 'help' to" +
                            " see all available" +
                            " commands.");
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
                Console.WriteLine();
                Console.WriteLine(
                    "  Did you type the filename" +
                    " correctly?");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  === Quick Help ===");
                Console.WriteLine(
                    "  Extract:  -xhda <file.hda>" +
                    " <out_folder>");
                Console.WriteLine(
                    "  Pack:     -chda <in_folder>" +
                    " <file.hda>");
                Console.WriteLine();
                Console.WriteLine(
                    "  Example: -xhda START.HDA" +
                    " START");
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
                Console.WriteLine();
                Console.WriteLine(
                    "  Did you type the folder" +
                    " name correctly?");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  === Quick Help ===");
                Console.WriteLine(
                    "  Extract:  -xhda <file.hda>" +
                    " <out_folder>");
                Console.WriteLine(
                    "  Pack:     -chda <in_folder>" +
                    " <file.hda>");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Blue;
                Console.WriteLine(
                    "  You typed something" +
                    " wrong!");
                Console.WriteLine();
                Console.WriteLine(
                    "  Maybe you typed a FOLDER" +
                    " where a FILE");
                Console.WriteLine(
                    "  was expected, or the" +
                    " other way around.");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  === HDA Commands ===");
                Console.WriteLine(
                    "  Extract:  -xhda <file.hda>" +
                    " <out_folder>");
                Console.WriteLine(
                    "  Pack:     -chda <in_folder>" +
                    " <file.hda>");
                Console.WriteLine();
                Console.WriteLine(
                    "  === Examples ===");
                Console.WriteLine(
                    "  -xhda START.HDA START");
                Console.WriteLine(
                    "    → Extracts START.HDA" +
                    " into START folder");
                Console.WriteLine();
                Console.WriteLine(
                    "  -chda START STARTNEW.HDA");
                Console.WriteLine(
                    "    → Packs START folder" +
                    " into STARTNEW.HDA");
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
                        "  " + parts[0].Trim());

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
                    "  You typed an invalid" +
                    " number!");
                Console.WriteLine();
                Console.WriteLine(
                    "  Check the command and" +
                    " try again.");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (IOException)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  You typed something" +
                    " wrong!");
                Console.WriteLine();
                Console.WriteLine(
                    "  Check your file and" +
                    " folder names.");
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Cyan;
                Console.WriteLine(
                    "  === Quick Help ===");
                Console.WriteLine(
                    "  Extract:  -xhda <file.hda>" +
                    " <out_folder>");
                Console.WriteLine(
                    "  Pack:     -chda <in_folder>" +
                    " <file.hda>");
                Console.WriteLine(
                    "  Help:     help");
                Console.ResetColor();
                Console.WriteLine();
            }
            catch (Exception)
            {
                Console.WriteLine();
                Console.ForegroundColor =
                    ConsoleColor.Yellow;
                Console.WriteLine(
                    "  Something went wrong!");
                Console.WriteLine();
                Console.WriteLine(
                    "  Check your command and" +
                    " try again.");
                Console.WriteLine(
                    "  Type 'help' to see all" +
                    " commands.");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        // ═════════════════════════════════════════
        // PARSE INPUT LINE
        // ═════════════════════════════════════════
        static string[] ParseInput(string input)
        {
            input = input.Trim();

            var tokens =
                new System.Collections.Generic
                    .List<string>();
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
                new System.Collections.Generic
                    .HashSet<string>
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
                "igdtb",      "xgdtb",
                "cgdtb",      "rgdtb",
                "rfgdtb",     "cngdtb",
                "tops2bmp",   "towinbmp",
                "xbmppal",    "rbmppal",
                "cmusic",
                "xvag",       "rvag",
                "boyscale",
                "boymodv2",   "boymodv3",
                "boyoriginal","boyrestore",
                "boyback", "boyorig"
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

        // ═════════════════════════════════════════
        // REQUIRE ARGS
        // ═════════════════════════════════════════
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

        // ═════════════════════════════════════════
        // PRINT USAGE
        // ═════════════════════════════════════════
        static void PrintUsage()
        {
            // ── HDA Commands ──────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== HDA Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xhda        / xhda" +
                "        <file.hda> <out_folder>");
            Console.WriteLine(
                "  -chda        / chda" +
                "        <in_folder> <file.hda>");
            Console.WriteLine(
                "    Smart compressed HDA" +
                " (recommended):");
            Console.WriteLine(
                "      files <= 64 bytes" +
                "    → RAW (flag=0)");
            Console.WriteLine(
                "      compressible files" +
                "   → compressed (flag=1)");
            Console.WriteLine(
                "  -chda raw    / chda raw" +
                "    <in_folder> <file.hda>");
            Console.WriteLine(
                "  -chda uncomp / chda uncomp" +
                " <in_folder> <file.hda>");
            Console.WriteLine();

            // ── Compression ───────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== Single File Compression ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -compress   / compress" +
                "   <input_file> <output_file>");
            Console.WriteLine(
                "  -uncompress / uncompress" +
                " <input_file> <output_file>");
            Console.WriteLine();

            // ── Text ──────────────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== Text Commands ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xtxt / xtxt" +
                "  <text.bin> <ptr.bin>" +
                " <out.txt>");
            Console.WriteLine(
                "  -ctxt / ctxt" +
                "  <in.txt> <text.bin>" +
                " <ptr.bin>");
            Console.WriteLine();

            // ── ELF ───────────────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== ELF Commands ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -fixelf / fixelf" +
                " <SLUS> <lba> <size>");
            Console.WriteLine();

            // ── RDTB ──────────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== RDTB Model Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -irdtb   / irdtb" +
                "   <file.rdtb>");
            Console.WriteLine(
                "  -irdtbnb / irdtbnb" +
                " <file.rdtb>");
            Console.WriteLine(
                "  -xrdtb   / xrdtb" +
                "   <file.rdtb> <out_folder>");
            Console.WriteLine(
                "  -crdtb   / crdtb" +
                "   <in_folder> <file.rdtb>");
            Console.WriteLine(
                "  -srdtb   / srdtb" +
                "   <file.rdtb>");
            Console.WriteLine(
                "  -rrdtb   / rrdtb" +
                "   <file_a.rdtb> <file_b.rdtb>");
            Console.WriteLine(
                "  -vrdtb   / vrdtb" +
                "   <original.rdtb>" +
                " <rebuilt.rdtb>");
            Console.WriteLine(
                "  -rcrdtb  / rcrdtb" +
                "  <file.rdtb> <index>" +
                " <chunk.bin>");
            Console.WriteLine(
                "  -scanrdtb / scanrdtb" +
                " <folder>");
            Console.WriteLine();

            // ── GDTB ──────────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "=== GDTB Texture Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -igdtb  / igdtb" +
                "  <file.gdtb>");
            Console.WriteLine(
                "  -xgdtb  / xgdtb" +
                "  <file.gdtb> <out_folder>");
            Console.WriteLine(
                "  -cgdtb  / cgdtb" +
                "  <in_folder> <file.gdtb>");
            Console.WriteLine(
                "  -rgdtb  / rgdtb" +
                "  <index> <tex.bmp>" +
                " <file.gdtb>");
            Console.WriteLine(
                "  -rfgdtb / rfgdtb" +
                " <folder> <file.gdtb>");
            Console.WriteLine(
                "  -cngdtb / cngdtb" +
                " <number> <file.gdtb>");
            Console.WriteLine();

            // ── BMP Converter ─────────────────────
            Console.ForegroundColor =
                ConsoleColor.Yellow;
            Console.WriteLine(
                "=== PS2 BMP Converter ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -tops2bmp / tops2bmp" +
                " <image.bmp>");
            Console.WriteLine(
                "  -towinbmp / towinbmp" +
                " <image.bmp>");
            Console.WriteLine();

            // ── BMP Palette ───────────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== BMP Palette ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xbmppal / xbmppal" +
                " <image.bmp> <palette_name>");
            Console.WriteLine(
                "  -rbmppal / rbmppal" +
                " <palette_file> <image.bmp>");
            Console.WriteLine();

            // ── Audio ─────────────────────────────
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "=== Audio / Music ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xvag / xvag" +
                " <bd_file> <hd_file>" +
                " <index> [output.vag]");
            Console.WriteLine(
                "  -rvag / rvag" +
                " <index> <input.vag>" +
                " <bd_file> <hd_file>");
            Console.WriteLine(
                "  -xvag all / xvag all" +
                " <bd_file> <hd_file>" +
                " <out_folder>");
            Console.WriteLine(
                "  -rvag all / rvag all" +
                " <folder_with_vags>" +
                " <bd_file> <hd_file>");
            Console.WriteLine();

            // ── BOY Bone Scaler ───────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== BOY Advanced Bone Scaler" +
                " & Height Tool ===");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  BOY 3D Tools by" +
                " DarthKrayt333");
            Console.ResetColor();
            Console.WriteLine(
                "  -boyscale / boyscale" +
                " <00_skeleton.bin> [options]");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Individual bone:");
            Console.ResetColor();
            Console.WriteLine(
                "    --b<N>   <v>  all axes");
            Console.WriteLine(
                "    --b<N>x  <v>  X only");
            Console.WriteLine(
                "    --b<N>y  <v>  Y only");
            Console.WriteLine(
                "    --b<N>z  <v>  Z only");
            Console.WriteLine(
                "    N = 0 to 67");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "  Groups:");
            Console.ResetColor();
            Console.WriteLine(
                "    --spine --neck --arms" +
                " --legs --ankles --feet ...");
            Console.WriteLine(
                "    (add x/y/z suffix" +
                " for single axis)");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.Green;
            Console.WriteLine(
                "  ✓ Safe bones (won't" +
                " move hair):");
            Console.ResetColor();
            Console.WriteLine(
                "    Spine: --b2 --b3 --b4");
            Console.WriteLine(
                "    Neck:  --b5");
            Console.WriteLine(
                "    Arms:  --b16-20 --b33-37");
            Console.WriteLine(
                "    Legs:  --b50 to --b67");
            Console.ForegroundColor =
                ConsoleColor.Red;
            Console.WriteLine(
                "  ✗ DANGER (moves hair):");
            Console.ResetColor();
            Console.WriteLine(
                "    --b12 --b13 --b14");
            Console.WriteLine();
            Console.ForegroundColor =
                ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  BOY Scaler Examples:");
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --b2y 1.20 --b3y 1.20" +
                " --b4y 1.20");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --b53y 1.30 --b62y 1.30");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --legsy 1.25 --armsy 2.00");
            Console.WriteLine(
                "    tool.exe -boyscale" +
                " 00_skeleton.bin" +
                " --b5y 0.80 --b5x 1.40" +
                " --b5z 1.40");
            Console.ResetColor();
            Console.WriteLine();

            // ── BOY Mod Presets ───────────────────
            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "=== BOY Mod Presets ===");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "  BOY 3D Tools by" +
                " DarthKrayt333");
            Console.ResetColor();
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "  BoyModV2 - Taller Player" +
                " Mod - Default Farmer Version");
            Console.ResetColor();
            Console.WriteLine(
                "    tool.exe -boymodv2" +
                " -bin 00_skeleton.bin");
            Console.WriteLine(
                "    tool.exe boymodv2" +
                " bin 00_skeleton.bin");
            Console.WriteLine(
                "    tool.exe -boymodv2" +
                " -rdtb BOY_00000.rdtb");
            Console.WriteLine(
                "    tool.exe boymodv2" +
                " rdtb BOY_00000.rdtb");
            Console.WriteLine();

            Console.ForegroundColor =
                ConsoleColor.Magenta;
            Console.WriteLine(
                "  BoyModV3 - Taller Player" +
                " Mod - Uptight Farmer Version");
            Console.ResetColor();
            Console.WriteLine(
                "    tool.exe -boymodv3" +
                " -bin 00_skeleton.bin");
            Console.WriteLine(
                "    tool.exe boymodv3" +
                " bin 00_skeleton.bin");
            Console.WriteLine(
                "    tool.exe -boymodv3" +
                " -rdtb BOY_00000.rdtb");
            Console.WriteLine(
                "    tool.exe boymodv3" +
                " rdtb BOY_00000.rdtb");
            Console.WriteLine();

            Console.ForegroundColor =
            ConsoleColor.Green;
            Console.WriteLine(
                "  BoyOriginal - Restore BOY" +
                " to Original Vanilla Skeleton");
            Console.ResetColor();
            Console.WriteLine(
                "    tool.exe boyoriginal" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "    tool.exe boyoriginal" +
                " 00_skeleton.bin");
            Console.WriteLine(
                "  Also works as:");
            Console.ForegroundColor =
                ConsoleColor.Cyan;
            Console.WriteLine(
                "    tool.exe boyrestore" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "    tool.exe boyback" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "    tool.exe boyorig" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "    tool.exe boyorig" +
                " 00_skeleton.bin");
            Console.ResetColor();
            Console.WriteLine();

            // ── Examples ──────────────────────────
            Console.ForegroundColor =
                ConsoleColor.White;
            Console.WriteLine(
                "=== General Examples ===");
            Console.ResetColor();
            Console.ForegroundColor =
                ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -xhda game.hda" +
                " ./output");
            Console.WriteLine(
                "  tool.exe -chda ./folder" +
                " game.hda");
            Console.WriteLine(
                "  tool.exe -xrdtb" +
                " BOY_00000.rdtb ./boy_out");
            Console.WriteLine(
                "  tool.exe -crdtb ./boy_out" +
                " BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -xgdtb" +
                " textures.gdtb ./output");
            Console.WriteLine(
                "  tool.exe -cgdtb ./bmps" +
                " textures.gdtb");
            Console.WriteLine(
                "  tool.exe -xtxt" +
                " File_00001.bin" +
                " File_00000.bin hayato.txt");
            Console.WriteLine(
                "  tool.exe -ctxt hayato.txt" +
                " File_00001.bin" +
                " File_00000.bin");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
