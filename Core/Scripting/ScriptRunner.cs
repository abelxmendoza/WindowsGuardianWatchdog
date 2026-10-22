using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Text;

namespace WindowsGuardianWatchdog.Core.Scripting
{
    public sealed class ScriptResult
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public bool Cancelled { get; init; }
        public bool Success => ExitCode == 0 && !Cancelled;
    }

    public static class ScriptRunner
    {
        // app.manifest asks for administrator rights, so the app is normally elevated already. Going through
        // "runas" again from inside the MSIX package is what broke updates: the script and log are written to
        // %TEMP%, which the package redirects, so the elevated PowerShell outside the package never found them.
        public static bool IsElevated { get; } = CheckElevated();

        public static Task<ScriptResult> RunPowerShellAsync(string script, bool elevate, int timeoutMs = 120000)
        {
            return ExecutePowerShellAsync(script, elevate && !IsElevated, timeoutMs);
        }

        public static Task<ScriptResult> RunAsync(string fileName, string arguments, bool elevate, int timeoutMs = 120000, Encoding? outputEncoding = null)
        {
            if (!elevate || IsElevated)
            {
                return ExecuteProcessAsync(fileName, arguments, timeoutMs, outputEncoding);
            }

            var script = $"& '{Escape(fileName)}' {arguments}";
            return ExecutePowerShellAsync(script, elevate: true, timeoutMs);
        }

        private static async Task<ScriptResult> ExecutePowerShellAsync(string script, bool elevate, int timeoutMs)
        {
            var directory = Path.Combine(Path.GetTempPath(), "WindowsGuardianWatchdog");
            Directory.CreateDirectory(directory);
            var id = Guid.NewGuid().ToString("N");
            var scriptPath = Path.Combine(directory, id + ".ps1");
            var logPath = Path.Combine(directory, id + ".log");
            string? wrapperPath = null;

            try
            {
                await File.WriteAllTextAsync(scriptPath, script, Utf8);

                ProcessStartInfo start;
                if (elevate)
                {
                    wrapperPath = Path.Combine(directory, id + "-run.ps1");
                    await File.WriteAllTextAsync(wrapperPath, BuildWrapper(scriptPath, logPath), Utf8);
                    start = new ProcessStartInfo
                    {
                        FileName = ResolvePowerShell(),
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{wrapperPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                }
                else
                {
                    start = new ProcessStartInfo
                    {
                        FileName = ResolvePowerShell(),
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                }

                using var process = new Process { StartInfo = start };
                try
                {
                    if (!process.Start())
                    {
                        return new ScriptResult { ExitCode = -1, Output = "The command could not be started." };
                    }
                }
                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    return new ScriptResult { ExitCode = 1223, Cancelled = true, Output = "Administrator approval was cancelled." };
                }

                if (!elevate)
                {
                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    if (!await WaitAsync(process, timeoutMs))
                    {
                        TryKill(process);
                        return new ScriptResult { ExitCode = -1, Output = "The command timed out." };
                    }

                    var stdout = await stdoutTask;
                    var stderr = await stderrTask;
                    var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
                    return new ScriptResult { ExitCode = process.ExitCode, Output = output.Trim() };
                }

                if (!await WaitAsync(process, timeoutMs))
                {
                    TryKill(process);
                    return new ScriptResult { ExitCode = -1, Output = "The command timed out." };
                }

                return new ScriptResult { ExitCode = process.ExitCode, Output = (await ReadLogAsync(logPath)).Trim() };
            }
            finally
            {
                TryDelete(scriptPath);
                TryDelete(wrapperPath);
                TryDelete(logPath);
            }
        }

        private static async Task<ScriptResult> ExecuteProcessAsync(string fileName, string arguments, int timeoutMs, Encoding? outputEncoding)
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (outputEncoding != null)
            {
                start.StandardOutputEncoding = outputEncoding;
                start.StandardErrorEncoding = outputEncoding;
            }

            using var process = new Process { StartInfo = start };
            try
            {
                if (!process.Start())
                {
                    return new ScriptResult { ExitCode = -1, Output = "The command could not be started." };
                }
            }
            catch (Win32Exception ex)
            {
                return new ScriptResult { ExitCode = -1, Output = $"{Path.GetFileName(fileName)} could not be started: {ex.Message}" };
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!await WaitAsync(process, timeoutMs))
            {
                TryKill(process);
                return new ScriptResult { ExitCode = -1, Output = "The command timed out." };
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
            return new ScriptResult { ExitCode = process.ExitCode, Output = output.Trim() };
        }

        private static string BuildWrapper(string scriptPath, string logPath)
        {
            return $$"""
                $wgLog = '{{Escape(logPath)}}'
                $wgCode = 0
                try {
                    & '{{Escape(scriptPath)}}' *>&1 | Out-File -FilePath $wgLog -Encoding utf8
                    if ($null -ne $LASTEXITCODE) { $wgCode = $LASTEXITCODE }
                } catch {
                    $wgCode = 1
                    $_ | Out-File -FilePath $wgLog -Append -Encoding utf8
                }
                exit $wgCode
                """;
        }

        private static bool CheckElevated()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private static string Escape(string value) => value.Replace("'", "''");

        private static string ResolvePowerShell()
        {
            var windowsPowerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            return File.Exists(windowsPowerShell) ? windowsPowerShell : "powershell.exe";
        }

        private static async Task<bool> WaitAsync(Process process, int timeoutMs)
        {
            return await Task.Run(() => process.WaitForExit(timeoutMs));
        }

        private static async Task<string> ReadLogAsync(string logPath)
        {
            if (!File.Exists(logPath)) return string.Empty;
            await using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return await reader.ReadToEndAsync();
        }

        private static void TryKill(Process process)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* The process may already have exited. */ }
        }

        private static void TryDelete(string? path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch { /* Leave the temp file if it is still in use. */ }
        }

        private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: true);
    }
}
