using HMSTHModdingTool;
using HMSTHModdingTool.BMP;
using HMSTHModdingTool.GDTB;
using HMSTHModdingTool.IO;
using HMSTHModdingTool.IO.Compression;
using HMSTHModdingTool.RDTB;
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
            "HMSTHModdingTool original as HDATextTool by gdkchan";
        const string TOOL_VERSION =
            "v1.4.0-Beta";
        const string TOOL_AUTHOR =
            "gdkchan + DarthKrayt333 & HMSTH Community";

        // ═════════════════════════════════════════
        // MAIN
        // ═════════════════════════════════════════
        static void Main(string[] args)
        {
            // ─────────────────────────────────────────
            // INTERACTIVE MODE (double-click support)
            // ─────────────────────────────────────────
            if (args.Length == 0)
            {
                // Set working directory to where the EXE is located
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                Directory.SetCurrentDirectory(exeDir);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Running in interactive mode.");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("Working directory: " + exeDir);
                Console.ResetColor();
                Console.WriteLine();

                PrintUsage();

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("HMSTHModdingTool> ");
                    Console.ResetColor();

                    string input = Console.ReadLine();

                    if (input == null) continue;
                    input = input.Trim();
                    if (input == string.Empty) continue;

                    if (input.ToLower() == "exit" ||
                        input.ToLower() == "quit" ||
                        input.ToLower() == "q")
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Goodbye!");
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
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(TOOL_NAME);
                        Console.WriteLine("Version " + TOOL_VERSION);
                        Console.WriteLine("By " + TOOL_AUTHOR);
                        Console.ResetColor();
                        Console.WriteLine();
                        continue;
                    }

                    // Parse the input line into args array
                    string[] parsedArgs = ParseInput(input);
                    if (parsedArgs.Length == 0) continue;

                    // Run the command
                    RunCommand(parsedArgs);

                    Console.WriteLine();
                }

                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(TOOL_NAME);
            Console.WriteLine("Version " + TOOL_VERSION);
            Console.WriteLine("By " + TOOL_AUTHOR);
            Console.ResetColor();
            Console.WriteLine();

            // ── Normal CMD mode (args passed) ────────────────────
            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            RunCommand(args);
        }

        // ═════════════════════════════════════════
        // NORMALIZE COMMAND
        // Strips leading "-" or "--" so both
        // "-xhda" and "xhda" work the same.
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
        // RUN COMMAND (shared by both modes)
        // ═════════════════════════════════════════
        static void RunCommand(string[] args)
        {
            try
            {
                // Normalize: strip "-" or "--" prefix
                string cmd = NormalizeCommand(args[0]);

                // Flag: if the case prints its own Finished + extras,
                // set this to true to skip the generic one at the bottom.
                bool customFinish = false;

                switch (cmd)
                {
                    // ════════════════════════════
                    // ORIGINAL HDA COMMANDS
                    // ════════════════════════════
                    case "xhda":
                        RequireArgs(args, 3,
                            "-xhda <file.hda> <out_folder>");
                        {
                            // Uppercase only the last folder name segment
                            string xhdaOut = args[2];
                            string xhdaDir = Path.GetDirectoryName(xhdaOut);
                            string xhdaName = Path.GetFileName(xhdaOut).ToUpper();
                            xhdaOut = string.IsNullOrEmpty(xhdaDir)
                                ? xhdaName
                                : Path.Combine(xhdaDir, xhdaName);
                            HarvestDataArchive.Unpack(args[1], xhdaOut);
                        }
                        break;

                    case "chda":
                        // ── chda <folder> <out.hda>              (compressed, default)
                        // ── chda uncomp <folder> <out.hda>       (uncompressed)
                        if (args.Length >= 2 &&
                            args[1].ToLower() == "uncomp")
                        {
                            // -chda uncomp <in_folder> <file.hda>
                            RequireArgs(args, 4,
                                "-chda uncomp <in_folder> <file.hda>");

                            string chdaOut = args[3];
                            string chdaDir =
                                Path.GetDirectoryName(chdaOut);
                            string chdaName =
                                Path.GetFileName(chdaOut).ToUpper();
                            chdaOut =
                                string.IsNullOrEmpty(chdaDir)
                                    ? chdaName
                                    : Path.Combine(chdaDir, chdaName);

                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Packing uncompressed HDA...");
                            Console.ResetColor();

                            HarvestDataArchive.Pack(chdaOut, args[2]);
                        }
                        else
                        {
                            // -chda <in_folder> <file.hda>
                            // Default = compressed
                            RequireArgs(args, 3,
                                "-chda <in_folder> <file.hda>");

                            string chdaOut = args[2];
                            string chdaDir =
                                Path.GetDirectoryName(chdaOut);
                            string chdaName =
                                Path.GetFileName(chdaOut).ToUpper();
                            chdaOut =
                                string.IsNullOrEmpty(chdaDir)
                                    ? chdaName
                                    : Path.Combine(chdaDir, chdaName);

                            HarvestDataArchive.PackCompressed(
                                chdaOut, args[1]);
                        }
                        break;

                    // ════════════════════════════
                    // SINGLE FILE COMPRESS
                    // ════════════════════════════
                    case "compress":
                        RequireArgs(args, 3,
                            "-compress <input_file> <output_file>");

                        {
                            string inPath = args[1];
                            string outPath = args[2];

                            if (!File.Exists(inPath))
                            {
                                TextOut.PrintError(
                                    "Input file not found: " + inPath);
                                return;
                            }

                            byte[] raw = File.ReadAllBytes(inPath);

                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Compressing single file...");
                            Console.ResetColor();

                            var sw = System.Diagnostics.Stopwatch.StartNew();

                            byte[] comp =
                                HarvestCompression.Compress(
                                    raw,
                                    (cur, total) =>
                                    {
                                        // Optional progress display
                                        double pct =
                                            total == 0
                                            ? 100
                                            : (double)cur * 100.0 / total;

                                        Console.Error.Write(
                                            "\r  {0:F1}%  ({1:N0}/{2:N0})   ",
                                            pct, cur, total);
                                    });

                            sw.Stop();
                            Console.Error.Write(
                                "\r" + new string(' ', 50) + "\r");

                            File.WriteAllBytes(outPath, comp);

                            double ratio =
                                raw.Length == 0
                                ? 0
                                : (double)comp.Length / raw.Length * 100.0;

                            Console.ForegroundColor =
                                ConsoleColor.Green;
                            Console.WriteLine(
                                "Done!  {0:N0} → {1:N0} bytes  ({2:F1}%)  in {3:F2}s",
                                raw.Length,
                                comp.Length,
                                ratio,
                                sw.Elapsed.TotalSeconds);
                            Console.ResetColor();
                        }
                        break;


                    // ════════════════════════════
                    // SINGLE FILE UNCOMPRESS
                    // ════════════════════════════
                    case "uncompress":
                        RequireArgs(args, 3,
                            "-uncompress <input_file> <output_file>");

                        {
                            string inPath = args[1];
                            string outPath = args[2];

                            if (!File.Exists(inPath))
                            {
                                TextOut.PrintError(
                                    "Input file not found: " + inPath);
                                return;
                            }

                            byte[] comp = File.ReadAllBytes(inPath);

                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Decompressing single file...");
                            Console.ResetColor();

                            byte[] raw =
                                HarvestCompression.Decompress(comp);

                            File.WriteAllBytes(outPath, raw);

                            Console.ForegroundColor =
                                ConsoleColor.Green;
                            Console.WriteLine(
                                "Done!  {0:N0} → {1:N0} bytes",
                                comp.Length,
                                raw.Length);
                            Console.ResetColor();
                        }
                        break;

                    // ════════════════════════════
                    // TEXT COMMANDS
                    // ════════════════════════════

                    // ── xtxt: export text + dat, then report both ────────
                    case "xtxt":
                        RequireArgs(args, 4,
                            "-xtxt <text.bin> <ptr.bin> <out.txt>");
                        {
                            // Check that the two input files are not the same file
                            string xtxtData = Path.GetFullPath(args[1]);
                            string xtxtPtrs = Path.GetFullPath(args[2]);

                            if (string.Equals(xtxtData, xtxtPtrs,
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(
                                    "  ERROR: This is the same file." +
                                    " These must be two different files.");
                                Console.ResetColor();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine(
                                    "  text.bin : " + xtxtData);
                                Console.WriteLine(
                                    "  ptr.bin  : " + xtxtPtrs);
                                Console.WriteLine(
                                    "  These must be two different files.");
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine(
                                    "  Example: -xtxt <text.bin> <ptr.bin> <out.txt>");
                                Console.ResetColor();
                                Console.WriteLine();
                                customFinish = true;
                                break;
                            }

                            string datPath = HarvestText.DecodeToFile(
                                args[1], args[2], args[3]);

                            TextOut.PrintSuccess("Finished!");
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(
                                "  " + Path.GetFileName(args[3]) + " Exported");
                            Console.WriteLine(
                                "  " + Path.GetFileName(datPath) + " Exported");
                            Console.ResetColor();
                            customFinish = true;
                        }
                        break;

                    // ── ctxt: check .dat exists before importing ─────────
                    case "ctxt":
                        RequireArgs(args, 4,
                            "-ctxt <in.txt> <text.bin> <ptr.bin>");
                        {
                            // Build the expected .dat path the same way
                            // HarvestText does, so we can warn early.
                            string txtFull = Path.GetFullPath(args[1]);
                            string datCheck = Path.Combine(
                                Path.GetDirectoryName(txtFull) ?? ".",
                                Path.GetFileNameWithoutExtension(txtFull) + ".dat");

                            if (!File.Exists(datCheck))
                            {
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(
                                    "  ERROR: companion .dat file not found!");
                                Console.ResetColor();
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine(
                                    "  Expected: " + Path.GetFileName(datCheck));
                                Console.WriteLine(
                                    "  The .dat file is created when you run -xtxt.");
                                Console.WriteLine(
                                    "  It must stay in the same folder as the .txt");
                                Console.WriteLine(
                                    "  and have the same base name.");
                                Console.ResetColor();
                                Console.WriteLine();
                                return; // stop here, do not attempt encode
                            }

                            HarvestText.EncodeFromFile(args[1], args[2], args[3]);

                            TextOut.PrintSuccess("Finished!");
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(
                                "  " + Path.GetFileName(args[1]) + " and " +
                                Path.GetFileNameWithoutExtension(args[1]) + ".dat Being Combined and Completed!");
                            Console.ResetColor();
                            customFinish = true;
                        }
                        break;

                    case "fixelf":
                        RequireArgs(args, 4,
                            "-fixelf <SLUS> <lba> <size>");
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
                        RDTBArchive.InfoNoBones(args[1]);
                        break;

                    case "xrdtb":
                        RequireArgs(args, 3,
                            "-xrdtb <file.rdtb> <out_folder>");
                        RDTBArchive.Extract(args[1], args[2]);
                        break;

                    case "crdtb":
                        RequireArgs(args, 3,
                            "-crdtb <in_folder> <file.rdtb>");
                        RDTBArchive.Create(args[1], args[2]);
                        break;

                    case "srdtb":
                        RequireArgs(args, 2,
                            "-srdtb <file.rdtb>");
                        RDTBArchive.Skeleton(args[1]);
                        break;

                    case "rrdtb":
                        RequireArgs(args, 3,
                            "-rrdtb <file_a.rdtb> <file_b.rdtb>");
                        RDTBArchive.Compare(args[1], args[2]);
                        break;

                    case "vrdtb":
                        RequireArgs(args, 3,
                            "-vrdtb <original.rdtb> <rebuilt.rdtb>");
                        RDTBArchive.Verify(args[1], args[2]);
                        break;

                    case "rcrdtb":
                        RequireArgs(args, 4,
                            "-rcrdtb <file.rdtb> <index> <chunk.bin>");
                        {
                            int rcIdx;
                            if (!int.TryParse(args[2], out rcIdx))
                            {
                                TextOut.PrintError(
                                    "Invalid index: " + args[2]);
                                return;
                            }
                            RDTBArchive.ReplaceChunk(
                                args[1], rcIdx, args[3]);
                        }
                        break;

                    case "scanrdtb":
                        RequireArgs(args, 2,
                            "-scanrdtb <folder>");
                        RDTBArchive.ScanFolder(args[1]);
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
                            "-xgdtb <file.gdtb> <out_folder>");
                        GDTBArchive.Extract(args[1], args[2]);
                        break;

                    case "cgdtb":
                        RequireArgs(args, 3,
                            "-cgdtb <in_folder> <file.gdtb>");
                        GDTBArchive.Create(args[1], args[2]);
                        break;

                    case "rgdtb":
                        RequireArgs(args, 4,
                            "-rgdtb <index> <texture.bmp> <file.gdtb>");
                        int rIdx;
                        if (!int.TryParse(args[1], out rIdx))
                        {
                            TextOut.PrintError(
                                "Invalid index: " + args[1]);
                            return;
                        }
                        GDTBArchive.Replace(args[3], rIdx, args[2]);
                        break;

                    case "rfgdtb":
                        int startIdx = 0;
                        if (args.Length >= 4 &&
                            int.TryParse(args[2], out startIdx))
                        {
                            RequireArgs(args, 4,
                                "-rfgdtb <folder> <start> <file.gdtb>");
                            GDTBArchive.ReplaceFolder(
                                args[3], args[1], startIdx);
                        }
                        else
                        {
                            RequireArgs(args, 3,
                                "-rfgdtb <folder> <file.gdtb>");
                            GDTBArchive.ReplaceFolder(
                                args[2], args[1], 0);
                        }
                        break;

                    case "cngdtb":
                        RequireArgs(args, 3,
                            "-cngdtb <number> <file.gdtb>");
                        int newCnt;
                        if (!int.TryParse(args[1], out newCnt))
                        {
                            TextOut.PrintError(
                                "Invalid number: " + args[1]);
                            return;
                        }
                        GDTBArchive.ChangeCount(args[2], newCnt);
                        break;

                    // ════════════════════════════
                    // BMP CONVERTER COMMANDS
                    // ════════════════════════════
                    case "tops2bmp":
                        RequireArgs(args, 2,
                            "-tops2bmp <image.bmp>");
                        PS2BMPConverter.ToPS2(args[1]);
                        break;

                    case "towinbmp":
                        RequireArgs(args, 2,
                            "-towinbmp <image.bmp>");
                        PS2BMPConverter.ToWindows(args[1]);
                        break;

                    // ════════════════════════════
                    // BMP PALETTE COMMANDS
                    // ════════════════════════════
                    case "xbmppal":
                        RequireArgs(args, 3,
                            "-xbmppal <image.bmp> <palette_name>");
                        BMPPalette.Extract(args[1], args[2]);
                        break;

                    case "rbmppal":
                        RequireArgs(args, 3,
                            "-rbmppal <palette_file> <image.bmp>");
                        BMPPalette.Import(args[1], args[2]);
                        break;

                    // ════════════════════════════
                    // AUDIO COMMANDS
                    // ════════════════════════════
                    case "cmusic":
                        RequireArgs(args, 2,
                            "-cmusic <input.vag>");
                        {
                            string vagPath = args[1];
                            if (!Path.IsPathRooted(vagPath))
                                vagPath = Path.Combine(
                                    Directory.GetCurrentDirectory(),
                                    vagPath);

                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine(
                                "Converting VAG to BD/HD/SQ...");
                            Console.ResetColor();

                            AudioConverter.ConvertVagToMusic(vagPath);
                        }
                        break;

                    case "xvag":
                        // Check for "all" mode first
                        if (args.Length >= 2 && args[1].ToLower() == "all")
                        {
                            // -xvag all <bd_file> <hd_file> <out_folder>
                            RequireArgs(args, 5,
                                "-xvag all <bd_file> <hd_file> <out_folder>");

                            string hdPathAllX = args[3];
                            string bdPathAllX = args[2];
                            string outFolder = args[4];

                            if (!Path.IsPathRooted(hdPathAllX))
                                hdPathAllX = Path.Combine(
                                    Directory.GetCurrentDirectory(), hdPathAllX);
                            if (!Path.IsPathRooted(bdPathAllX))
                                bdPathAllX = Path.Combine(
                                    Directory.GetCurrentDirectory(), bdPathAllX);
                            if (!Path.IsPathRooted(outFolder))
                                outFolder = Path.Combine(
                                    Directory.GetCurrentDirectory(), outFolder);

                            AudioBank.ExtractAllVags(hdPathAllX, bdPathAllX, outFolder);
                        }
                        else
                        {
                            // -xvag <bd_file> <hd_file> <index> [output.vag]
                            // output.vag is OPTIONAL — if omitted, auto-named by index
                            RequireArgs(args, 4,
                                "-xvag <bd_file> <hd_file> <index> [output.vag]");

                            string bdPathX = args[1];
                            string hdPathX = args[2];
                            int idxX = int.Parse(args[3]);

                            // Auto-generate output filename if not provided
                            // e.g. index 9 → "009.vag", index 73 → "073.vag"
                            string outVag;
                            if (args.Length >= 5 &&
                                !string.IsNullOrEmpty(args[4]))
                            {
                                outVag = args[4];
                            }
                            else
                            {
                                outVag = string.Format("{0:000}.vag", idxX);
                            }

                            if (!Path.IsPathRooted(hdPathX))
                                hdPathX = Path.Combine(
                                    Directory.GetCurrentDirectory(), hdPathX);
                            if (!Path.IsPathRooted(bdPathX))
                                bdPathX = Path.Combine(
                                    Directory.GetCurrentDirectory(), bdPathX);
                            if (!Path.IsPathRooted(outVag))
                                outVag = Path.Combine(
                                    Directory.GetCurrentDirectory(), outVag);

                            AudioBank.ExtractVag(hdPathX, bdPathX, idxX, outVag);
                        }
                        break;

                    case "rvag":
                        // Check for "all" mode first
                        if (args.Length >= 2 && args[1].ToLower() == "all")
                        {
                            // -rvag all <folder_with_vags> <bd_file> <hd_file>
                            RequireArgs(args, 5,
                                "-rvag all <folder_with_vags> <bd_file> <hd_file>");

                            string folderWithVags = args[2];
                            string bdPathAllI = args[3];
                            string hdPathAllI = args[4];

                            if (!Path.IsPathRooted(folderWithVags))
                                folderWithVags = Path.Combine(
                                    Directory.GetCurrentDirectory(), folderWithVags);
                            if (!Path.IsPathRooted(bdPathAllI))
                                bdPathAllI = Path.Combine(
                                    Directory.GetCurrentDirectory(), bdPathAllI);
                            if (!Path.IsPathRooted(hdPathAllI))
                                hdPathAllI = Path.Combine(
                                    Directory.GetCurrentDirectory(), hdPathAllI);

                            AudioBank.ReplaceAllVags(hdPathAllI, bdPathAllI, folderWithVags);
                        }
                        else
                        {
                            // -rvag <index> <input.vag> <bd_file> <hd_file>
                            RequireArgs(args, 5,
                                "-rvag <index> <input.vag> <bd_file> <hd_file>");

                            int idxI = int.Parse(args[1]);
                            string inVag = args[2];
                            string bdPathI = args[3];
                            string hdPathI = args[4];

                            if (!Path.IsPathRooted(hdPathI))
                                hdPathI = Path.Combine(
                                    Directory.GetCurrentDirectory(), hdPathI);
                            if (!Path.IsPathRooted(bdPathI))
                                bdPathI = Path.Combine(
                                    Directory.GetCurrentDirectory(), bdPathI);
                            if (!Path.IsPathRooted(inVag))
                                inVag = Path.Combine(
                                    Directory.GetCurrentDirectory(), inVag);

                            AudioBank.ImportVag(hdPathI, bdPathI, idxI, inVag);
                        }
                        break;

                    // ════════════════════════════
                    // UNKNOWN COMMAND
                    // ════════════════════════════
                    default:
                        TextOut.PrintError(
                            "Invalid command \"" +
                            args[0] + "\" used!");
                        Console.WriteLine();
                        PrintUsage();
                        return;
                }

                // Generic finish — skipped if the case handled it itself
                if (!customFinish)
                    TextOut.PrintSuccess("Finished!");
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine();
                TextOut.PrintError("File not found: " + e.FileName);
            }
            catch (DirectoryNotFoundException e)
            {
                Console.WriteLine();
                TextOut.PrintError(
                    "Directory not found: " + e.Message);
            }
            catch (ArgumentException e)
            {
                Console.WriteLine();
                TextOut.PrintError(e.Message);
            }
            catch (InvalidDataException e)
            {
                Console.WriteLine();
                TextOut.PrintError("Invalid data: " + e.Message);
            }
            catch (FormatException)
            {
                Console.WriteLine();
                TextOut.PrintError("Invalid number format!");
            }
            catch (Exception e)
            {
                Console.WriteLine();
                TextOut.PrintError("Unexpected error: " + e.Message);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(e.StackTrace);
                Console.ResetColor();
            }
        }

        // ═════════════════════════════════════════
        // PARSE INPUT LINE (interactive mode)
        // Splits a command line string into args
        // respecting quoted paths with spaces.
        // Accepts any tool name prefix or none.
        // ═════════════════════════════════════════
        static string[] ParseInput(string input)
        {
            input = input.Trim();

            // ── Split input into tokens first ─────────────────────
            var tokens =
                new System.Collections.Generic.List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < input.Length; i++)
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
                        tokens.Add(current.ToString());
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

            // ── If the first token looks like an .exe or tool name
            // (does NOT start with "-" and is NOT a known command),
            // skip it.
            // ──────────────────────────────────────────────────────
            string first = tokens[0].ToLower();

            // Known commands (without "-" prefix)
            var knownCommands =
                new System.Collections.Generic.HashSet<string>
            {
                "xhda",     "chda",
                "compress", "uncompress",
                "xtxt",     "ctxt",
                "fixelf",
                "irdtb",    "irdtbnb",
                "xrdtb",    "crdtb",
                "srdtb",    "rrdtb",
                "vrdtb",    "rcrdtb",
                "scanrdtb",
                "igdtb",    "xgdtb",   "cgdtb",
                "rgdtb",    "rfgdtb",  "cngdtb",
                "tops2bmp", "towinbmp",
                "xbmppal",  "rbmppal",
                "cmusic",
                "xvag",     "rvag"
            };

            bool firstIsCommand =
                first.StartsWith("-") ||
                knownCommands.Contains(first);

            // ── Extra guard: if second token is "all", the first
            // token MUST be the command — never strip it.
            // This prevents "XVAGS ALL ..." from stripping "XVAGS"
            // and treating "ALL" as the command.
            // ──────────────────────────────────────────────────────
            bool secondIsAll = tokens.Count >= 2 &&
                               tokens[1].ToLower() == "all";

            if (!firstIsCommand && !secondIsAll && tokens.Count > 1)
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
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== HDA Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xhda          / xhda          <file.hda> <out_folder>");
            Console.WriteLine(
                "  -chda          / chda          <in_folder> <file.hda>");
            Console.WriteLine(
                "    Default: creates compressed HDA (recommended)");
            Console.WriteLine(
                "  -chda uncomp   / chda uncomp   <in_folder> <file.hda>");
            Console.WriteLine(
                "    Creates uncompressed HDA (larger, use only if needed)");
            Console.WriteLine();

            // ── Single File Compression ─────────────────────
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== Single File Compression ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -compress   / compress   <input_file> <output_file>");
            Console.WriteLine(
                "  -uncompress / uncompress <input_file> <output_file>");
            Console.WriteLine();

            // ── Text Commands ─────────────────────
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== Text Commands ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xtxt   / xtxt   <text.bin> <ptr.bin> <out.txt>");
            Console.WriteLine(
                "  -ctxt   / ctxt   <in.txt> <text.bin> <ptr.bin>");
            Console.WriteLine(
                "  NOTE: -xtxt also creates a <out.dat> companion file.");
            Console.WriteLine(
                "        Keep it next to your .txt — needed for -ctxt!");
            Console.WriteLine();

            // ── ELF Commands ──────────────────────
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== ELF Commands ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -fixelf / fixelf <SLUS> <lba> <size>");
            Console.WriteLine();

            // ── RDTB Commands ─────────────────────
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("=== RDTB Model Archive ===");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Show full info including bone list");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -irdtb   / irdtb   <file.rdtb>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Show info without bone list");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -irdtbnb / irdtbnb <file.rdtb>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Extract all chunks + skeleton.csv + manifest");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -xrdtb   / xrdtb   <file.rdtb> <out_folder>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Rebuild RDTB from extracted folder");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -crdtb   / crdtb   <in_folder> <file.rdtb>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Show ASCII skeleton bone tree");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -srdtb   / srdtb   <file.rdtb>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Compare two RDTB files structurally");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -rrdtb   / rrdtb   <file_a.rdtb> <file_b.rdtb>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Byte-for-byte verify rebuild vs original");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -vrdtb   / vrdtb   <original.rdtb> <rebuilt.rdtb>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Replace a single chunk by index");
            Console.WriteLine(
                "  Safe warnings for skeleton/mesh/UV chunks");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -rcrdtb  / rcrdtb  <file.rdtb> <index> <chunk.bin>");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  Scan folder for all .rdtb files");
            Console.WriteLine(
                "  Classifies each as player/NPC/prop/tool");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(
                "    -scanrdtb / scanrdtb <folder>");
            Console.WriteLine();

            // ── RDTB Examples ─────────────────────
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("=== RDTB Examples ===");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine(
                "  tool.exe -irdtb   BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -irdtbnb HAYATO_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -xrdtb   BOY_00000.rdtb ./boy_out");
            Console.WriteLine(
                "  tool.exe -crdtb   ./boy_out BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -srdtb   BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -rrdtb   BOY_00000.rdtb HAYATO_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -vrdtb   BOY_00000.rdtb BOY_rebuilt.rdtb");
            Console.WriteLine(
                "  tool.exe -rcrdtb  BOY_00000.rdtb 2 kurt_mesh.bin");
            Console.WriteLine(
                "  tool.exe -rcrdtb  BOY_00000.rdtb 11 kurt_uv.bin");
            Console.WriteLine(
                "  tool.exe -scanrdtb ./extracted_game");
            Console.WriteLine(
                "  tool.exe -scanrdtb ./hayato_out");
            Console.ResetColor();
            Console.WriteLine();

            // ── GDTB Commands ─────────────────────
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("=== GDTB Texture Archive ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -igdtb  / igdtb  <file.gdtb>");
            Console.WriteLine(
                "  -xgdtb  / xgdtb  <file.gdtb> <out_folder>");
            Console.WriteLine(
                "  -cgdtb  / cgdtb  <in_folder> <file.gdtb>");
            Console.WriteLine(
                "  -rgdtb  / rgdtb  <index> <tex.bmp> <file.gdtb>");
            Console.WriteLine(
                "  -rfgdtb / rfgdtb <folder> <file.gdtb>");
            Console.WriteLine(
                "  -rfgdtb / rfgdtb <folder> <start> <file.gdtb>");
            Console.WriteLine(
                "  -cngdtb / cngdtb <number> <file.gdtb>");
            Console.WriteLine();

            // ── BMP Converter Commands ────────────
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== PS2 BMP Converter ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -tops2bmp / tops2bmp <image.bmp>");
            Console.WriteLine(
                "  -towinbmp / towinbmp <image.bmp>");
            Console.WriteLine();

            // ── BMP Palette Commands ──────────────
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("=== BMP Palette ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xbmppal / xbmppal <image.bmp> <palette_name>");
            Console.WriteLine(
                "  -rbmppal / rbmppal <palette_file> <image.bmp>");
            Console.WriteLine();

            // ── Audio / Music Commands ────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== Audio / Music ===");
            Console.ResetColor();
            Console.WriteLine(
                "  -xvag    / xvag    <bd_file> <hd_file> <index> [output.vag]");
            Console.WriteLine(
                "    Extracts a single VAG by index from BD/HD.");
            Console.WriteLine(
                "    Output filename is optional — auto-named by index if omitted.");
            Console.WriteLine(
                "    (e.g. index 9 -> 009.vag, index 73 -> 073.vag)");
            Console.WriteLine();

            Console.WriteLine(
                "  -rvag    / rvag    <index> <input.vag> <bd_file> <hd_file>");
            Console.WriteLine(
                "    Replaces a single VAG by index in BD/HD.");
            Console.WriteLine();

            Console.WriteLine(
                "  -xvag all / xvag all <bd_file> <hd_file> <out_folder>");
            Console.WriteLine(
                "    Extracts all VAGs from BD/HD into the specified folder.");
            Console.WriteLine(
                "    (files named 000.VAG, 001.VAG, ...)");
            Console.WriteLine();

            Console.WriteLine(
                "  -rvag all / rvag all <folder_with_vags> <bd_file> <hd_file>");
            Console.WriteLine(
                "    Replaces all VAGs from folder in BD/HD.");
            Console.WriteLine(
                "    - stops at max index if folder has more");
            Console.WriteLine(
                "    - replaces only up to folder count if fewer");
            Console.WriteLine();

            // ── Examples ──────────────────────────
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("=== Examples ===");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;

            // ── Single File Compression ─────────────────────

            Console.WriteLine(
                "  tool.exe -compress BOY_00001.bin BOY_00001_comp.bin");
            Console.WriteLine(
                "  tool.exe -uncompress BOY_00001_comp.bin BOY_00001.bin");
            Console.WriteLine();

            // Text examples
            Console.WriteLine(
                "  tool.exe -xtxt File_00001.bin File_00000.bin hayato.txt");
            Console.WriteLine(
                "  → Creates: hayato.txt");
            Console.WriteLine(
                "             hayato.dat  (companion — keep next to .txt!)");
            Console.WriteLine();
            Console.WriteLine(
                "  tool.exe -ctxt hayato.txt File_00001.bin File_00000.bin");
            Console.WriteLine(
                "  → Requires: hayato.txt + hayato.dat in the same folder");
            Console.WriteLine();

            // RDTB examples
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(
                "=== Experimental - RDTB Examples ===");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Info ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -irdtb   BOY_00000.rdtb          # full info + bones");
            Console.WriteLine(
                "  tool.exe -irdtbnb BOY_00000.rdtb          # info without bones");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Extract ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -xrdtb BOY_00000.rdtb ./boy_extracted");
            Console.WriteLine(
                "  tool.exe -xrdtb HAYATO_00000.rdtb ./kurt_extracted");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Rebuild ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -crdtb ./boy_extracted BOY_00000_new.rdtb");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Skeleton Tree ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -srdtb BOY_00000.rdtb");
            Console.WriteLine(
                "  tool.exe -srdtb HAYATO_00000.rdtb");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Compare two models ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -rrdtb BOY_00000.rdtb HAYATO_00000.rdtb");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Verify byte-perfect rebuild ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -vrdtb BOY_00000.rdtb BOY_00000_new.rdtb");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
                "  === Replace ===");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  -rcrdtb  / rcrdtb  " +
                "<file.rdtb> <index> <chunk.bin>");
            Console.WriteLine(
                "    Replace single chunk by index");
            Console.WriteLine(
                "    Example:");
            Console.WriteLine(
                "    tool.exe -rcrdtb BOY_00000.rdtb " +
                "2 kurt_02_mesh_main.bin");

            Console.ResetColor();
            Console.WriteLine();

            // GDTB examples
            Console.WriteLine(
                "  tool.exe -igdtb textures.gdtb");
            Console.WriteLine(
                "  tool.exe igdtb textures.gdtb");
            Console.WriteLine(
                "  tool.exe -xgdtb textures.gdtb ./output");
            Console.WriteLine(
                "  tool.exe xgdtb textures.gdtb ./output");
            Console.WriteLine(
                "  tool.exe -cgdtb ./bmps textures.gdtb");
            Console.WriteLine(
                "  tool.exe cgdtb ./bmps textures.gdtb");
            Console.WriteLine(
                "  tool.exe -rgdtb 3 new.bmp textures.gdtb");
            Console.WriteLine(
                "  tool.exe rgdtb 3 new.bmp textures.gdtb");
            Console.WriteLine(
                "  tool.exe -rfgdtb ./bmps textures.gdtb");
            Console.WriteLine(
                "  tool.exe rfgdtb ./bmps textures.gdtb");
            Console.WriteLine(
                "  tool.exe -rfgdtb ./bmps 5 textures.gdtb");
            Console.WriteLine(
                "  tool.exe rfgdtb ./bmps 5 textures.gdtb");
            Console.WriteLine(
                "  tool.exe -cngdtb 9 textures.gdtb");
            Console.WriteLine(
                "  tool.exe cngdtb 9 textures.gdtb");
            Console.WriteLine();

            // BMP converter examples
            Console.WriteLine(
                "  tool.exe -tops2bmp texture.bmp");
            Console.WriteLine(
                "  tool.exe tops2bmp texture.bmp");
            Console.WriteLine(
                "  tool.exe -towinbmp texture_ps2.bmp");
            Console.WriteLine(
                "  tool.exe towinbmp texture_ps2.bmp");
            Console.WriteLine();

            // BMP palette examples
            Console.WriteLine(
                "  tool.exe -xbmppal texture.bmp my_palette");
            Console.WriteLine(
                "  tool.exe xbmppal texture.bmp my_palette");
            Console.WriteLine(
                "  tool.exe -rbmppal my_palette texture.bmp");
            Console.WriteLine(
                "  tool.exe rbmppal my_palette texture.bmp");
            Console.WriteLine();

            // Audio / Music examples
            Console.WriteLine(
                "  tool.exe -cmusic mysong.vag");
            Console.WriteLine(
                "  tool.exe cmusic mysong.vag");
            Console.WriteLine(
                "  → Creates: MYSONG\\MYSONG.BD");
            Console.WriteLine(
                "             MYSONG\\MYSONG.HD");
            Console.WriteLine(
                "             MYSONG\\MYSONG.SQ");
            Console.WriteLine();

            Console.WriteLine(
                "  tool.exe -xvag MUSIC.BD MUSIC.HD 9");
            Console.WriteLine(
                "  → Extracts VAG index 9 as 009.vag");
            Console.WriteLine();

            Console.WriteLine(
                "  tool.exe -rvag 9 new.vag SE.BD SE.HD");
            Console.WriteLine(
                "  → Replaces VAG index 9 in SE.BD/SE.HD with new.vag");
            Console.WriteLine();

            Console.WriteLine(
                "  tool.exe -xvag all MUSIC.BD MUSIC.HD ./all_vags");
            Console.WriteLine(
                "  → Extracts all VAGs into ./all_vags (000.VAG, 001.VAG, ...)");
            Console.WriteLine();

            Console.WriteLine(
                "  tool.exe -rvag all ./all_vags SE.BD SE.HD");
            Console.WriteLine(
                "  → Replaces all VAGs from ./all_vags into SE.BD/SE.HD");
            Console.WriteLine(
                "    (stops at max index if folder has more,");
            Console.WriteLine(
                "     replaces only up to folder count if fewer)");            
            Console.WriteLine();

            // HDA examples
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(
                "=== HDA Examples ===");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "  tool.exe -xhda game.hda ./output");
            Console.WriteLine(
                "  tool.exe xhda game.hda ./output");
            Console.WriteLine();
            Console.WriteLine(
                "  tool.exe -chda uncomp HAYATO HAYATONEW.HDA");
            Console.WriteLine(
                "    → Packs HAYATO folder into HAYATONEW.HDA (uncompressed)");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(
                "  === Highly Recommended Default: Make Compressed Files in HDA ===");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                "    tool.exe -chda ./folder game.hda");
            Console.WriteLine(
                "    tool.exe chda ./folder game.hda");
            Console.WriteLine();
            Console.WriteLine(
                "    tool.exe -chda HAYATO HAYATONEW.HDA");
            Console.WriteLine(
                "      → Packs HAYATO folder into HAYATONEW.HDA (compressed)");
            Console.WriteLine();
        }
    }
}
