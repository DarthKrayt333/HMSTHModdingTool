using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace HMSTHModdingTool.IO
{
    /// <summary>
    /// Batch-extracts 3D models and exports OBJ/GLB into a new output tree.
    /// Pairing is based on binary signatures, filename identity and proximity;
    /// ambiguous pairs are skipped rather than guessed.
    /// </summary>
    public static class Batch3DExporter
    {
        private sealed class SourceFile
        {
            public string FullPath;
            public string RelativePath;
            public string Stem;
            public string Group;
            public int? Index;
            public string DirectoryPath;
        }

        private sealed class BatchItem
        {
            public string SourceModel;
            public string SourceModelSha256;
            public string SourceGdtb;
            public string SourceGdtbSha256;
            public string Status;
            public string Message;
            public string ObjOutput;
            public readonly List<string> GlbFiles = new List<string>();
            public int ObjCount;
            public int VertexCount;
            public int TriangleCount;
            public int TextureCount;
        }

        private sealed class Manifest
        {
            public string Input;
            public string Output;
            public bool SplitMode;
            public DateTime StartedUtc;
            public DateTime FinishedUtc;
            public readonly List<BatchItem> Items = new List<BatchItem>();
        }

        /// <summary>
        /// Runs a batch export. The extraction callback reuses Program's established
        /// RDTB/SRDB extraction and post-processing path rather than duplicating parsers.
        /// </summary>
        public static void Run(
            string inputPath,
            string outputPath,
            bool overwrite,
            bool splitMode,
            string explicitGdtbPath,
            Action<string[], bool> extractModel)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
                throw new ArgumentException("Input directory or HDA path is required.", "inputPath");
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output directory is required.", "outputPath");
            if (extractModel == null)
                throw new ArgumentNullException("extractModel");

            string fullInput = Path.GetFullPath(inputPath);
            string fullOutput = Path.GetFullPath(outputPath);
            string fullExplicitGdtb = string.IsNullOrWhiteSpace(explicitGdtbPath)
                ? null : Path.GetFullPath(explicitGdtbPath);

            if (Directory.Exists(fullInput) && IsSameOrChildPath(fullOutput, fullInput))
                throw new InvalidOperationException(
                    "The output directory must be outside the input directory to avoid scanning generated files or deleting source data.");
            if (File.Exists(fullInput) && IsSameOrChildPath(fullInput, fullOutput))
                throw new InvalidOperationException(
                    "The input archive cannot be located inside the output directory.");
            if (fullExplicitGdtb != null)
            {
                if (!File.Exists(fullExplicitGdtb))
                    throw new FileNotFoundException("The specified GDTB file does not exist.", fullExplicitGdtb);
                if (ReadMagic(fullExplicitGdtb) != "GDTB")
                    throw new InvalidDataException("The specified --gdtb file does not have a GDTB signature.");
                if (IsSameOrChildPath(fullExplicitGdtb, fullOutput))
                    throw new InvalidOperationException("The explicit GDTB input cannot be inside the output directory.");
            }

            if (!File.Exists(fullInput) && !Directory.Exists(fullInput))
                throw new FileNotFoundException("Input directory or HDA archive was not found.", fullInput);
            if (File.Exists(fullInput) && !string.Equals(Path.GetExtension(fullInput), ".hda", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A file input must be an HDA archive. Otherwise provide an extracted directory.");

            PrepareOutputDirectory(fullOutput, overwrite);

            string temporaryRoot = Path.Combine(Path.GetTempPath(), "HMSTHModdingTool_Batch3D_" + Guid.NewGuid().ToString("N"));
            string scanRoot = fullInput;
            bool deleteScanRoot = false;
            var manifest = new Manifest
            {
                Input = fullInput,
                Output = fullOutput,
                SplitMode = splitMode,
                StartedUtc = DateTime.UtcNow
            };

            try
            {
                if (File.Exists(fullInput))
                {
                    if (!string.Equals(Path.GetExtension(fullInput), ".hda", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("A file input must be an HDA archive. Otherwise provide an extracted directory.");

                    Directory.CreateDirectory(temporaryRoot);
                    scanRoot = Path.Combine(temporaryRoot, "hda_extracted");
                    HarvestDataArchive.Unpack(fullInput, scanRoot);
                    deleteScanRoot = true;
                }
                else if (!Directory.Exists(fullInput))
                {
                    throw new DirectoryNotFoundException("Input directory or HDA archive not found: " + fullInput);
                }

                string scanRootFull = Path.GetFullPath(scanRoot);
                List<SourceFile> models = ScanFiles(scanRootFull, fullExplicitGdtb)
                    .Where(f => ReadMagic(f.FullPath) == "RDTB" || ReadMagic(f.FullPath) == "SRDB")
                    .OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                List<SourceFile> gdtbs = fullExplicitGdtb == null
                    ? ScanFiles(scanRootFull, null).Where(f => ReadMagic(f.FullPath) == "GDTB").ToList()
                    : new List<SourceFile> { CreateSourceFile(fullExplicitGdtb, scanRootFull) };

                if (models.Count == 0)
                    throw new InvalidDataException("No RDTB or SRDB model files were found in the input.");
                if (gdtbs.Count == 0)
                    Console.WriteLine("[WARN] No GDTB files found. Models without a unique texture pair will be skipped.");

                Console.WriteLine("[BATCH3D] Models found: " + models.Count.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("[BATCH3D] GDTB files found: " + gdtbs.Count.ToString(CultureInfo.InvariantCulture));
                Console.WriteLine("[BATCH3D] Output: " + fullOutput);

                foreach (SourceFile model in models)
                {
                    BatchItem item = new BatchItem
                    {
                        SourceModel = model.RelativePath,
                        Status = "skipped"
                    };
                    manifest.Items.Add(item);

                    try
                    {
                        item.SourceModelSha256 = ComputeSha256(model.FullPath);
                        SourceFile gdtb;
                        string pairReason;
                        if (fullExplicitGdtb != null)
                        {
                            gdtb = gdtbs[0];
                            pairReason = null;
                        }
                        else
                        {
                            gdtb = SelectGdtb(model, gdtbs, out pairReason);
                        }

                        if (gdtb == null)
                        {
                            item.Message = pairReason ?? "No unambiguous GDTB pair was found.";
                            Console.WriteLine("[SKIP] " + model.RelativePath + " — " + item.Message);
                            continue;
                        }

                        item.SourceGdtb = gdtb.RelativePath;
                        item.SourceGdtbSha256 = ComputeSha256(gdtb.FullPath);

                        string modelKey = MakeOutputKey(model.RelativePath);
                        string safeBase = MakeSafeName(Path.GetFileNameWithoutExtension(model.FullPath));
                        string modelWork = Path.Combine(temporaryRoot, "model_" + Guid.NewGuid().ToString("N"));
                        string inputWork = Path.Combine(modelWork, "input");
                        Directory.CreateDirectory(inputWork);
                        string modelCopy = Path.Combine(inputWork, "source_model" + Path.GetExtension(model.FullPath));
                        string gdtbCopy = Path.Combine(inputWork, "source_textures" + Path.GetExtension(gdtb.FullPath));
                        File.Copy(model.FullPath, modelCopy, true);
                        if (!string.Equals(model.FullPath, gdtb.FullPath, StringComparison.OrdinalIgnoreCase))
                            File.Copy(gdtb.FullPath, gdtbCopy, true);
                        else
                            throw new InvalidDataException("Model and GDTB resolved to the same file.");

                        extractModel(new string[] { modelCopy, gdtbCopy, safeBase }, splitMode);

                        List<string> exportableObjs = FindExportableObjFiles(inputWork, safeBase);
                        if (exportableObjs.Count == 0)
                            throw new InvalidDataException("Extraction produced no complete OBJ models.");

                        var successfulGlbs = new List<string>();
                        var conversionErrors = new List<string>();
                        foreach (string objPath in exportableObjs)
                        {
                            try
                            {
                                string glbPath = Path.ChangeExtension(objPath, ".glb");
                                if (File.Exists(glbPath))
                                    File.Delete(glbPath); // work directory is unique; never reuse old output.
                                ObjGltfExporter.ConvertObjToGlb(objPath, glbPath);
                                if (!File.Exists(glbPath) || new FileInfo(glbPath).Length < 20)
                                    throw new InvalidDataException("Exporter did not produce a non-empty GLB.");
                                successfulGlbs.Add(glbPath);
                            }
                            catch (Exception ex)
                            {
                                conversionErrors.Add(Path.GetFileName(objPath) + ": " + ex.Message);
                            }
                        }

                        if (successfulGlbs.Count == 0)
                            throw new InvalidDataException("No GLB could be exported. " + string.Join(" | ", conversionErrors.ToArray()));

                        string objDestination = Path.Combine(fullOutput, "obj", modelKey);
                        string glbDestination = Path.Combine(fullOutput, "glb", modelKey);
                        Directory.CreateDirectory(objDestination);
                        Directory.CreateDirectory(glbDestination);

                        List<string> candidateFolders = FindExportFolders(inputWork, safeBase);
                        foreach (string folder in candidateFolders)
                        {
                            string relativeFolder = MakeRelativePath(inputWork, folder);
                            string destinationFolder = Path.Combine(objDestination, relativeFolder);
                            CopyDirectoryWithoutGlb(folder, destinationFolder);
                        }

                        foreach (string glbPath in successfulGlbs)
                        {
                            string relativeGlb = MakeRelativePath(inputWork, glbPath);
                            string finalGlbPath = Path.Combine(glbDestination, relativeGlb);
                            string finalGlbDir = Path.GetDirectoryName(finalGlbPath);
                            if (!Directory.Exists(finalGlbDir)) Directory.CreateDirectory(finalGlbDir);
                            File.Copy(glbPath, finalGlbPath, true);
                            item.GlbFiles.Add(MakeRelativePath(fullOutput, finalGlbPath));
                        }

                        item.ObjOutput = MakeRelativePath(fullOutput, objDestination);
                        item.ObjCount = exportableObjs.Count;
                        item.TextureCount = CountTextures(candidateFolders);
                        foreach (string obj in exportableObjs)
                            CountGeometry(obj, item);

                        item.Status = conversionErrors.Count == 0 ? "success" : "partial";
                        item.Message = conversionErrors.Count == 0
                            ? "Export completed."
                            : "Some OBJ files could not be converted: " + string.Join(" | ", conversionErrors.ToArray());
                        Console.WriteLine("[" + item.Status.ToUpperInvariant() + "] " + model.RelativePath +
                            " -> " + item.GlbFiles.Count.ToString(CultureInfo.InvariantCulture) + " GLB file(s)");
                    }
                    catch (Exception ex)
                    {
                        item.Status = "failed";
                        item.Message = ex.GetType().Name + ": " + ex.Message;
                        Console.WriteLine("[FAIL] " + model.RelativePath + " — " + item.Message);
                    }
                }

                manifest.FinishedUtc = DateTime.UtcNow;
                WriteManifestAtomic(Path.Combine(fullOutput, "manifest.json"), manifest);

                int success = manifest.Items.Count(i => i.Status == "success");
                int partial = manifest.Items.Count(i => i.Status == "partial");
                int failed = manifest.Items.Count(i => i.Status == "failed");
                int skipped = manifest.Items.Count(i => i.Status == "skipped");
                Console.WriteLine();
                Console.WriteLine("[BATCH3D] Summary: " + success + " success, " + partial + " partial, " +
                    failed + " failed, " + skipped + " skipped.");
                Console.WriteLine("[BATCH3D] Manifest: " + Path.Combine(fullOutput, "manifest.json"));
                if (success + partial == 0)
                    Console.WriteLine("[BATCH3D] No model was exported. Review manifest.json for pairing and extraction diagnostics.");
            }
            finally
            {
                if (deleteScanRoot && Directory.Exists(temporaryRoot))
                {
                    try { Directory.Delete(temporaryRoot, true); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            "[WARN] Could not remove temporary scan directory '" +
                            temporaryRoot + "': " + ex.Message);
                    }
                }
                else if (Directory.Exists(temporaryRoot))
                {
                    try { Directory.Delete(temporaryRoot, true); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(
                            "[WARN] Could not remove temporary scan directory '" +
                            temporaryRoot + "': " + ex.Message);
                    }
                }
            }
        }

        private static void PrepareOutputDirectory(string outputPath, bool overwrite)
        {
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                return;
            }

            bool hasContent = Directory.GetFileSystemEntries(outputPath).Length > 0;
            if (hasContent && !overwrite)
                throw new IOException("Output directory already exists and is not empty. Use --overwrite to replace this tool's obj/, glb/, and manifest.json outputs: " + outputPath);

            if (overwrite)
            {
                string[] owned = { "obj", "glb" };
                foreach (string name in owned)
                {
                    string path = Path.Combine(outputPath, name);
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    else if (File.Exists(path)) File.Delete(path);
                }
                string manifestPath = Path.Combine(outputPath, "manifest.json");
                if (File.Exists(manifestPath)) File.Delete(manifestPath);
            }
        }

        private static List<SourceFile> ScanFiles(string root, string explicitGdtb)
        {
            var result = new List<SourceFile>();
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (explicitGdtb != null && string.Equals(Path.GetFullPath(file), explicitGdtb, StringComparison.OrdinalIgnoreCase))
                    continue;
                string magic = ReadMagic(file);
                if (magic != "RDTB" && magic != "SRDB" && magic != "GDTB") continue;
                result.Add(CreateSourceFile(file, root));
            }
            return result;
        }

        private static SourceFile CreateSourceFile(string path, string root)
        {
            string full = Path.GetFullPath(path);
            string stem = Path.GetFileNameWithoutExtension(full);
            Match match = Regex.Match(stem, @"^(.*?)(?:[_-](\d+)|(\d{3,}))$");
            string group = match.Success ? match.Groups[1].Value : stem;
            string number = match.Success
                ? (match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value)
                : null;
            int parsed;
            int? index = number != null && int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? (int?)parsed : null;
            string relative = IsSameOrChildPath(full, root)
                ? MakeRelativePath(root, full) : full;
            return new SourceFile
            {
                FullPath = full,
                RelativePath = relative,
                Stem = stem,
                Group = group,
                Index = index,
                DirectoryPath = Path.GetDirectoryName(full) ?? string.Empty
            };
        }

        private static SourceFile SelectGdtb(SourceFile model, List<SourceFile> gdtbs, out string reason)
        {
            reason = null;
            if (gdtbs == null || gdtbs.Count == 0)
            {
                reason = "No GDTB file was found.";
                return null;
            }
            if (gdtbs.Count == 1) return gdtbs[0];

            var ranked = new List<Tuple<SourceFile, int>>();
            foreach (SourceFile candidate in gdtbs)
            {
                int score = 0;
                if (string.Equals(model.Stem, candidate.Stem, StringComparison.OrdinalIgnoreCase)) score += 20000;
                if (string.Equals(model.DirectoryPath, candidate.DirectoryPath, StringComparison.OrdinalIgnoreCase)) score += 10000;
                if (string.Equals(model.Group, candidate.Group, StringComparison.OrdinalIgnoreCase)) score += 5000;
                if (model.Index.HasValue && candidate.Index.HasValue)
                {
                    long distance = Math.Abs((long)model.Index.Value - candidate.Index.Value);
                    score += (int)Math.Max(0L, 500L - Math.Min(500L, distance));
                }
                if (score > 0) ranked.Add(Tuple.Create(candidate, score));
            }

            if (ranked.Count == 0)
            {
                reason = "Several GDTB files exist, but none has a matching filename family or directory. Pass --gdtb <file.gdtb> to select one explicitly.";
                return null;
            }
            int maxScore = ranked.Max(x => x.Item2);
            List<SourceFile> best = ranked.Where(x => x.Item2 == maxScore)
                .Select(x => x.Item1).ToList();
            if (best.Count != 1)
            {
                reason = "GDTB pairing is ambiguous between: " +
                    string.Join(", ", best.Select(x => x.RelativePath).ToArray()) +
                    ". Pass --gdtb <file.gdtb> to select one explicitly.";
                return null;
            }
            return best[0];
        }

        private static List<string> FindExportFolders(string outputRoot, string baseName)
        {
            string[] suffixes = { "_all_obj", "_obj", "_embedded_rdtbs_obj", "_3d_batches_obj" };
            return suffixes.Select(s => Path.Combine(outputRoot, baseName + s))
                .Where(Directory.Exists)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> FindExportableObjFiles(string outputRoot, string baseName)
        {
            var candidates = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in FindExportFolders(outputRoot, baseName))
            {
                foreach (string objPath in Directory.GetFiles(folder, "*.obj", SearchOption.AllDirectories))
                {
                    string stem = Path.GetFileNameWithoutExtension(objPath);
                    if (Regex.IsMatch(stem, @"^batch[_-]?\d+$", RegexOptions.IgnoreCase)) continue;
                    if (stem.StartsWith("_source", StringComparison.OrdinalIgnoreCase)) continue;
                    candidates.Add(Path.GetFullPath(objPath));
                }
            }
            return candidates.ToList();
        }

        private static void CopyDirectoryWithoutGlb(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetExtension(file), ".glb", StringComparison.OrdinalIgnoreCase)) continue;
                string relative = MakeRelativePath(source, file);
                string destFile = Path.Combine(destination, relative);
                string destDir = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                File.Copy(file, destFile, true);
            }
        }

        private static int CountTextures(List<string> folders)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] extensions = { ".png", ".bmp", ".jpg", ".jpeg", ".tga", ".dds", ".webp" };
            foreach (string folder in folders)
            {
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    if (extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) files.Add(file);
            }
            return files.Count;
        }

        private static void CountGeometry(string objPath, BatchItem item)
        {
            using (var reader = new StreamReader(objPath, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("v ", StringComparison.Ordinal) || trimmed.StartsWith("v\t", StringComparison.Ordinal))
                        item.VertexCount++;
                    else if (trimmed.StartsWith("f ", StringComparison.Ordinal) || trimmed.StartsWith("f\t", StringComparison.Ordinal))
                    {
                        string[] fields = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length >= 4) item.TriangleCount += fields.Length - 3;
                    }
                }
            }
        }

        private static string ReadMagic(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 4) return string.Empty;
                    byte[] bytes = new byte[4];
                    if (stream.Read(bytes, 0, 4) != 4) return string.Empty;
                    return Encoding.ASCII.GetString(bytes);
                }
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "[WARN] Could not read file magic from '" +
                    path + "': " + ex.Message);
                return string.Empty;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static string MakeOutputKey(string relativeModelPath)
        {
            string value = Path.ChangeExtension(relativeModelPath, null) ?? relativeModelPath;
            value = value.Replace('/', '_').Replace('\\', '_');
            return MakeSafeName(value);
        }

        private static string MakeSafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "model";
            char[] invalid = Path.GetInvalidFileNameChars();
            var result = new StringBuilder(value.Length);
            foreach (char c in value)
                result.Append(invalid.Contains(c) || c == '/' || c == '\\' ? '_' : c);
            string safe = result.ToString().Trim();
            return safe.Length == 0 ? "model" : safe;
        }

        private static bool IsSameOrChildPath(string path, string parent)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullPath, fullParent, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = fullParent + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar &&
                 fullPath.StartsWith(fullParent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }

        private static string MakeRelativePath(string baseDirectory, string fullPath)
        {
            string baseFull = Path.GetFullPath(baseDirectory);
            if (!baseFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                baseFull += Path.DirectorySeparatorChar;
            var baseUri = new Uri(baseFull, UriKind.Absolute);
            var targetUri = new Uri(Path.GetFullPath(fullPath), UriKind.Absolute);
            if (!string.Equals(baseUri.Scheme, targetUri.Scheme, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(fullPath);
            string relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(targetUri).ToString());
            return relative.Replace('/', Path.DirectorySeparatorChar);
        }

        private static void WriteManifestAtomic(string path, Manifest manifest)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, SerializeManifest(manifest), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try { File.Replace(tempPath, path, null); }
                catch (Exception replaceEx)
                {
                    Console.Error.WriteLine(
                        "[WARN] Atomic replacement failed for '" + path +
                        "'; attempting delete/move fallback: " + replaceEx.Message);
                    File.Delete(path);
                    File.Move(tempPath, path);
                }
            }
            else File.Move(tempPath, path);
        }

        private static string SerializeManifest(Manifest manifest)
        {
            int success = manifest.Items.Count(i => i.Status == "success");
            int partial = manifest.Items.Count(i => i.Status == "partial");
            int failed = manifest.Items.Count(i => i.Status == "failed");
            int skipped = manifest.Items.Count(i => i.Status == "skipped");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"schemaVersion\": 1,");
            AppendJsonString(sb, "tool", "HMSTHModdingTool", true, 1, true);
            AppendJsonString(sb, "input", manifest.Input, true, 1, true);
            AppendJsonString(sb, "output", manifest.Output, true, 1, true);
            AppendJsonString(sb, "startedUtc", manifest.StartedUtc.ToString("o", CultureInfo.InvariantCulture), true, 1, true);
            AppendJsonString(sb, "finishedUtc", manifest.FinishedUtc.ToString("o", CultureInfo.InvariantCulture), true, 1, true);
            sb.Append("  \"splitMode\": ").Append(manifest.SplitMode ? "true" : "false").AppendLine(",");
            sb.Append("  \"summary\": {\"models\": ").Append(manifest.Items.Count)
                .Append(", \"success\": ").Append(success)
                .Append(", \"partial\": ").Append(partial)
                .Append(", \"failed\": ").Append(failed)
                .Append(", \"skipped\": ").Append(skipped).AppendLine("},");
            sb.AppendLine("  \"items\": [");
            for (int i = 0; i < manifest.Items.Count; i++)
            {
                BatchItem item = manifest.Items[i];
                sb.AppendLine("    {");
                AppendJsonString(sb, "sourceModel", item.SourceModel, true, 3, true);
                AppendJsonString(sb, "sourceModelSha256", item.SourceModelSha256, true, 3, true);
                AppendJsonString(sb, "sourceGdtb", item.SourceGdtb, true, 3, true);
                AppendJsonString(sb, "sourceGdtbSha256", item.SourceGdtbSha256, true, 3, true);
                AppendJsonString(sb, "status", item.Status, true, 3, true);
                AppendJsonString(sb, "message", item.Message, true, 3, true);
                AppendJsonString(sb, "objOutput", item.ObjOutput, true, 3, true);
                sb.Append("      \"objCount\": ").Append(item.ObjCount).AppendLine(",");
                sb.Append("      \"vertexCount\": ").Append(item.VertexCount).AppendLine(",");
                sb.Append("      \"triangleCount\": ").Append(item.TriangleCount).AppendLine(",");
                sb.Append("      \"textureCount\": ").Append(item.TextureCount).AppendLine(",");
                sb.AppendLine("      \"glbFiles\": [");
                for (int g = 0; g < item.GlbFiles.Count; g++)
                {
                    sb.Append("        \"").Append(JsonEscape(item.GlbFiles[g])).Append("\"");
                    sb.AppendLine(g + 1 < item.GlbFiles.Count ? "," : string.Empty);
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                sb.AppendLine(i + 1 < manifest.Items.Count ? "," : string.Empty);
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendJsonString(StringBuilder sb, string name, string value, bool comma, int indent, bool allowNull)
        {
            sb.Append(' ', indent * 2).Append('"').Append(JsonEscape(name)).Append("\": ");
            if (value == null && allowNull) sb.Append("null");
            else sb.Append('"').Append(JsonEscape(value ?? string.Empty)).Append('"');
            sb.AppendLine(comma ? "," : string.Empty);
        }

        private static string JsonEscape(string value)
        {
            if (value == null) return string.Empty;
            var sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
