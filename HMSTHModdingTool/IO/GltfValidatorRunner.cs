using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HMSTHModdingTool.IO
{
    internal enum GltfValidationStatus
    {
        Valid,
        Invalid,
        NotConfigured,
        TimedOut,
        Failed
    }

    internal sealed class GltfValidationResult
    {
        public GltfValidationStatus Status { get; set; }
        public string ValidatorPath { get; set; }
        public string ValidatorVersion { get; set; }
        public string ReportPath { get; set; }
        public string Message { get; set; }
        public int? ExitCode { get; set; }
        public bool ReportWritten { get; set; }
    }

    /// <summary>
    /// Optional integration with the official Khronos glTF-Validator executable.
    /// No validator binary is bundled. Configure it through HMSTH_GLTF_VALIDATOR,
    /// place gltf_validator.exe beside the application or under tools/, or pass
    /// an explicit executable path to the glbvalidate command.
    /// </summary>
    internal static class GltfValidatorRunner
    {
        private const int TimeoutMilliseconds = 120000;

        public static GltfValidationResult Validate(string glbPath, string explicitValidatorPath)
        {
            if (string.IsNullOrWhiteSpace(glbPath))
                throw new ArgumentException("GLB input path cannot be empty.", "glbPath");
            if (!File.Exists(glbPath))
                throw new FileNotFoundException("GLB input file was not found.", glbPath);

            string fullGlbPath = Path.GetFullPath(glbPath);
            if (!string.IsNullOrWhiteSpace(explicitValidatorPath) && !File.Exists(explicitValidatorPath))
            {
                return new GltfValidationResult
                {
                    Status = GltfValidationStatus.Failed,
                    ValidatorPath = Path.GetFullPath(explicitValidatorPath),
                    ReportPath = fullGlbPath + ".report.json",
                    Message = "Validator executable was not found: " + explicitValidatorPath
                };
            }
            string validatorPath = ResolveValidatorPath(explicitValidatorPath);
            if (validatorPath == null)
            {
                return new GltfValidationResult
                {
                    Status = GltfValidationStatus.NotConfigured,
                    ReportPath = fullGlbPath + ".report.json",
                    Message = "Khronos glTF-Validator was not found. Set HMSTH_GLTF_VALIDATOR, " +
                              "place gltf_validator.exe beside the application or in tools/, " +
                              "or provide the executable path."
                };
            }

            string reportPath = fullGlbPath + ".report.json";
            string validatorVersion = ReadValidatorVersion(validatorPath);
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = validatorPath,
                    Arguments = "--stdout --messages " + QuoteArgument(fullGlbPath),
                    WorkingDirectory = Path.GetDirectoryName(validatorPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = new Process())
                {
                    process.StartInfo = startInfo;
                    if (!process.Start())
                    {
                        return new GltfValidationResult
                        {
                            Status = GltfValidationStatus.Failed,
                            ValidatorPath = validatorPath,
                            ValidatorVersion = validatorVersion,
                            ReportPath = reportPath,
                            Message = "The validator process did not start."
                        };
                    }

                    Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(TimeoutMilliseconds))
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                        catch (Exception killException)
                        {
                            return new GltfValidationResult
                            {
                                Status = GltfValidationStatus.TimedOut,
                                ValidatorPath = validatorPath,
                                ValidatorVersion = validatorVersion,
                                ReportPath = reportPath,
                                Message = "Validation exceeded " + TimeoutMilliseconds +
                                          " ms; stopping the process also failed: " + killException.Message
                            };
                        }

                        return new GltfValidationResult
                        {
                            Status = GltfValidationStatus.TimedOut,
                            ValidatorPath = validatorPath,
                            ValidatorVersion = validatorVersion,
                            ReportPath = reportPath,
                            Message = "Validation exceeded " + TimeoutMilliseconds + " ms and was stopped."
                        };
                    }

                    // WaitForExit() after the timed overload ensures process completion;
                    // the asynchronous readers prevent redirected-pipe deadlocks.
                    process.WaitForExit();
                    string stdout = stdoutTask.GetAwaiter().GetResult();
                    string stderr = stderrTask.GetAwaiter().GetResult();
                    bool reportWritten = false;
                    if (!string.IsNullOrWhiteSpace(stdout))
                    {
                        WriteReportAtomically(reportPath, stdout);
                        reportWritten = true;
                    }

                    bool succeeded = process.ExitCode == 0;
                    string diagnostic = string.IsNullOrWhiteSpace(stderr)
                        ? (succeeded ? "Validator reported no errors." : "Validator exited with a non-zero status.")
                        : stderr.Trim();
                    return new GltfValidationResult
                    {
                        Status = succeeded ? GltfValidationStatus.Valid : GltfValidationStatus.Invalid,
                        ValidatorPath = validatorPath,
                        ValidatorVersion = validatorVersion,
                        ReportPath = reportPath,
                        ExitCode = process.ExitCode,
                        ReportWritten = reportWritten,
                        Message = diagnostic
                    };
                }
            }
            catch (Exception ex)
            {
                return new GltfValidationResult
                {
                    Status = GltfValidationStatus.Failed,
                    ValidatorPath = validatorPath,
                    ValidatorVersion = validatorVersion,
                    ReportPath = reportPath,
                    Message = ex.GetType().Name + ": " + ex.Message
                };
            }
        }

        public static GltfValidationResult ValidateAfterExportIfConfigured(string glbPath)
        {
            string validatorPath = ResolveValidatorPath(null);
            if (validatorPath == null)
            {
                var notConfigured = new GltfValidationResult
                {
                    Status = GltfValidationStatus.NotConfigured,
                    ReportPath = Path.GetFullPath(glbPath) + ".report.json",
                    Message = "Validator executable is not configured. Use glbvalidate or set HMSTH_GLTF_VALIDATOR."
                };
                PrintResult(notConfigured, true);
                return notConfigured;
            }

            GltfValidationResult result = Validate(glbPath, validatorPath);
            PrintResult(result, true);
            return result;
        }

        public static void PrintResult(GltfValidationResult result, bool automatic)
        {
            if (result == null) throw new ArgumentNullException("result");
            string prefix = automatic ? "[GLB-AUTO-VALIDATOR] " : "[GLB-VALIDATOR] ";
            switch (result.Status)
            {
                case GltfValidationStatus.Valid:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(prefix + "PASS: " + result.Message);
                    Console.ResetColor();
                    break;
                case GltfValidationStatus.Invalid:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(prefix + "FAIL (exit " + result.ExitCode + "): " + result.Message);
                    Console.ResetColor();
                    break;
                case GltfValidationStatus.NotConfigured:
                    Console.WriteLine(prefix + "NOT RUN: " + result.Message);
                    break;
                case GltfValidationStatus.TimedOut:
                case GltfValidationStatus.Failed:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(prefix + result.Status.ToString().ToUpperInvariant() + ": " + result.Message);
                    Console.ResetColor();
                    break;
            }

            if (!string.IsNullOrWhiteSpace(result.ValidatorPath))
            {
                string version = string.IsNullOrWhiteSpace(result.ValidatorVersion)
                    ? "file version metadata unavailable; record the downloaded release/tag"
                    : result.ValidatorVersion;
                Console.WriteLine("  Validator: " + result.ValidatorPath + " (" + version + ")");
            }
            if (result.ReportWritten && !string.IsNullOrWhiteSpace(result.ReportPath) && File.Exists(result.ReportPath))
                Console.WriteLine("  JSON report: " + result.ReportPath);
        }

        private static string ReadValidatorVersion(string path)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion)) return info.ProductVersion;
                if (!string.IsNullOrWhiteSpace(info.FileVersion)) return info.FileVersion;
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GLB-VALIDATOR] Could not read executable version metadata: " + ex.Message);
                return null;
            }
        }

        private static string ResolveValidatorPath(string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string path = Path.GetFullPath(explicitPath);
                return File.Exists(path) ? path : null;
            }

            string fromEnvironment = Environment.GetEnvironmentVariable("HMSTH_GLTF_VALIDATOR");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                try
                {
                    string path = Path.GetFullPath(fromEnvironment);
                    if (File.Exists(path)) return path;
                    Console.WriteLine("[GLB-VALIDATOR] HMSTH_GLTF_VALIDATOR does not point to an existing file: " + path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GLB-VALIDATOR] Could not resolve HMSTH_GLTF_VALIDATOR: " + ex.Message);
                }
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[]
            {
                Path.Combine(baseDirectory, "gltf_validator.exe"),
                Path.Combine(baseDirectory, "tools", "gltf_validator.exe"),
                Path.Combine(baseDirectory, "gltf_validator")
            };
            foreach (string candidate in candidates)
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);

            return null;
        }

        private static void WriteReportAtomically(string reportPath, string contents)
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (string.IsNullOrEmpty(directory)) directory = Directory.GetCurrentDirectory();
            string temporaryPath = Path.Combine(directory,
                Path.GetFileName(reportPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
                if (File.Exists(reportPath))
                {
                    string backupPath = reportPath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Replace(temporaryPath, reportPath, backupPath, true);
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, reportPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static string QuoteArgument(string value)
        {
            if (value == null) return "\"\"";
            var result = new StringBuilder();
            result.Append('"');
            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }
                if (backslashes > 0)
                {
                    result.Append('\\', backslashes);
                    backslashes = 0;
                }
                result.Append(c);
            }
            if (backslashes > 0) result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }
}
