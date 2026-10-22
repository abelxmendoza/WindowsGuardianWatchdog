using System.Text;
using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core.Updates
{
    public sealed class AppPackageUpdate
    {
        public string Name { get; init; } = string.Empty;
        public string Id { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string AvailableVersion { get; init; } = string.Empty;
        public string Source { get; init; } = string.Empty;
    }

    public sealed class WindowsUpdateItem
    {
        public string Title { get; init; } = string.Empty;
        public string KnowledgeBase { get; init; } = string.Empty;
        public string SizeMb { get; init; } = string.Empty;
        public string Detail => string.IsNullOrWhiteSpace(KnowledgeBase)
            ? $"{SizeMb} MB"
            : $"KB {KnowledgeBase} · {SizeMb} MB";
    }

    public static class AppUpdateService
    {
        // winget returns this when there is nothing to upgrade.
        private const int WingetNoApplicableUpgrade = unchecked((int)0x8A15002B);

        private static string? _winget;

        public static async Task<bool> IsWingetAvailableAsync()
        {
            var result = await RunWingetAsync("--version", timeoutMs: 20000);
            return result.Success && result.Output.Contains("v", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<IReadOnlyList<AppPackageUpdate>> FindAppUpdatesAsync()
        {
            var result = await RunWingetAsync(
                "upgrade --include-unknown --disable-interactivity --accept-source-agreements",
                timeoutMs: 180000);
            if (result.ExitCode != 0 && result.ExitCode != WingetNoApplicableUpgrade && !result.Output.Contains("---", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "winget could not list updates." : result.Output);
            }
            return ParseWingetUpgrades(result.Output);
        }

        public static async Task<ScriptResult> UpgradeAllAppsAsync()
        {
            var winget = await RunWingetAsync(
                "upgrade --all --include-unknown --silent --disable-interactivity --accept-package-agreements --accept-source-agreements",
                timeoutMs: 45 * 60 * 1000);
            var store = await UpdateStoreAppsAsync();

            var log = CleanWingetOutput(winget.Output);
            if (!string.IsNullOrWhiteSpace(store.Output)) log = (log + Environment.NewLine + Environment.NewLine + store.Output).Trim();
            var exitCode = winget.ExitCode == WingetNoApplicableUpgrade ? 0 : winget.ExitCode;
            return new ScriptResult { ExitCode = exitCode, Output = log, Cancelled = winget.Cancelled };
        }

        // Microsoft Store apps are not always visible to winget; this asks the Store to update them itself.
        // It needs administrator rights and only starts the updates, which then finish in the background.
        public static async Task<ScriptResult> UpdateStoreAppsAsync()
        {
            const string script = """
                try {
                    $scan = Get-CimInstance -Namespace 'root\cimv2\mdm\dmmap' -ClassName 'MDM_EnterpriseModernAppManagement_AppManagement01' -ErrorAction Stop |
                        Invoke-CimMethod -MethodName UpdateScanMethod -ErrorAction Stop
                    if ($scan.ReturnValue -eq 0) { 'Microsoft Store: asked the Store to update its apps. They finish in the background.' }
                    else { "Microsoft Store: the update request returned $($scan.ReturnValue). Open the Store and choose Get updates." }
                } catch {
                    "Microsoft Store: could not ask the Store for updates ($($_.Exception.Message)). Open the Store and choose Get updates."
                }
                """;
            return await ScriptRunner.RunPowerShellAsync(script, elevate: true, timeoutMs: 120000);
        }

        private static async Task<ScriptResult> RunWingetAsync(string arguments, int timeoutMs)
        {
            var winget = _winget ??= FindWinget();
            if (winget == null)
            {
                return new ScriptResult { ExitCode = -1, Output = "winget was not found. Install App Installer from the Microsoft Store." };
            }

            // winget writes UTF-8. Reading it as the console code page shifts the table columns on names with "…".
            return await ScriptRunner.RunAsync(winget, arguments, elevate: true, timeoutMs: timeoutMs, outputEncoding: new UTF8Encoding(false));
        }

        // winget.exe is an alias in the signed-in user's WindowsApps folder. When a different admin approved UAC,
        // or the alias was never registered, the real file still lives in the App Installer package.
        private static string? FindWinget()
        {
            var candidates = new List<string>();
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                candidates.Add(Path.Combine(dir.Trim(), "winget.exe"));
            }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe"));

            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) return candidate;
                }
                catch { /* Skip PATH entries that are not valid paths. */ }
            }

            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
                return Directory.EnumerateDirectories(root, "Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe")
                    .Select(dir => Path.Combine(dir, "winget.exe"))
                    .Where(File.Exists)
                    .OrderByDescending(PackageVersion)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static Version PackageVersion(string wingetPath)
        {
            var folder = Path.GetFileName(Path.GetDirectoryName(wingetPath) ?? string.Empty);
            var parts = folder.Split('_');
            return parts.Length > 1 && Version.TryParse(parts[1], out var version) ? version : new Version(0, 0);
        }

        // winget redraws a spinner and progress bars with carriage returns. Keep only what each line ended up as.
        public static string CleanWingetOutput(string output)
        {
            var lines = new List<string>();
            foreach (var raw in output.Split('\n'))
            {
                var line = FinalText(raw).TrimEnd();
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    if (lines.Count > 0 && lines[^1].Length > 0) lines.Add(string.Empty);
                    continue;
                }
                if (trimmed is "-" or "\\" or "|" or "/") continue;
                if (trimmed.Contains('█') || trimmed.Contains('▒')) continue;
                lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines).Trim();
        }

        private static string FinalText(string line)
        {
            line = line.TrimEnd('\r');
            var cut = line.LastIndexOf('\r');
            return cut >= 0 ? line[(cut + 1)..] : line;
        }

        public static async Task<IReadOnlyList<WindowsUpdateItem>> FindWindowsUpdatesAsync()
        {
            const string script = """
                $ErrorActionPreference = 'Stop'
                $session = New-Object -ComObject Microsoft.Update.Session
                $searcher = $session.CreateUpdateSearcher()
                $searcher.Online = $true
                $result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'")
                if ($result.Updates.Count -eq 0) { Write-Output 'WU|NONE'; return }
                foreach ($update in $result.Updates) {
                    $title = ([string]$update.Title) -replace '\|','/' -replace '[\r\n]',' '
                    $kb = (@($update.KBArticleIDs) -join ',')
                    $size = [math]::Round($update.MaxDownloadSize / 1MB, 1)
                    Write-Output "WU|$title|$kb|$size"
                }
                """;
            var result = await ScriptRunner.RunPowerShellAsync(script, elevate: false, timeoutMs: 10 * 60 * 1000);
            if (result.ExitCode != 0 && !result.Output.Contains("WU|", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "Windows Update search failed." : result.Output);
            }
            return ParseWindowsUpdates(result.Output);
        }

        public static async Task<ScriptResult> InstallWindowsUpdatesAsync()
        {
            const string script = """
                $ErrorActionPreference = 'Stop'
                $session = New-Object -ComObject Microsoft.Update.Session
                $searcher = $session.CreateUpdateSearcher()
                $searcher.Online = $true
                $result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'")
                if ($result.Updates.Count -eq 0) { Write-Output 'WU|RESULT|2|False'; return }
                $collection = New-Object -ComObject Microsoft.Update.UpdateColl
                foreach ($update in $result.Updates) {
                    if (-not $update.EulaAccepted) { $update.AcceptEula() | Out-Null }
                    [void]$collection.Add($update)
                    Write-Output ("WU|QUEUED|" + (($update.Title -replace '\|','/')))
                }
                $downloader = $session.CreateUpdateDownloader()
                $downloader.Updates = $collection
                $download = $downloader.Download()
                Write-Output "WU|DOWNLOAD|$($download.ResultCode)"
                $installer = $session.CreateUpdateInstaller()
                $installer.Updates = $collection
                $installed = $installer.Install()
                Write-Output "WU|RESULT|$($installed.ResultCode)|$($installed.RebootRequired)"
                """;
            return await ScriptRunner.RunPowerShellAsync(script, elevate: true, timeoutMs: 90 * 60 * 1000);
        }

        public static IReadOnlyList<AppPackageUpdate> ParseWingetUpgrades(string output)
        {
            var updates = new List<AppPackageUpdate>();
            var lines = output.Split('\n').Select(FinalText).ToArray();
            var header = lines.FirstOrDefault(line => line.Contains("Id", StringComparison.Ordinal) && line.Contains("Version", StringComparison.Ordinal) && line.Contains("Available", StringComparison.Ordinal));
            if (header == null) return updates;

            var idAt = header.IndexOf("Id", StringComparison.Ordinal);
            var versionAt = header.IndexOf("Version", StringComparison.Ordinal);
            var availableAt = header.IndexOf("Available", StringComparison.Ordinal);
            var sourceAt = header.IndexOf("Source", StringComparison.Ordinal);
            if (idAt < 0 || versionAt <= idAt || availableAt <= versionAt || sourceAt <= availableAt) return updates;

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd();
                if (line.Length <= idAt || line.StartsWith("Name", StringComparison.Ordinal) || line.TrimStart().StartsWith('-')) continue;
                if (line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase)) break;

                var name = Slice(line, 0, idAt);
                var id = Slice(line, idAt, versionAt);
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) || id.Contains(' ')) continue;

                updates.Add(new AppPackageUpdate
                {
                    Name = name,
                    Id = id,
                    Version = Slice(line, versionAt, availableAt),
                    AvailableVersion = Slice(line, availableAt, sourceAt),
                    Source = Slice(line, sourceAt, line.Length)
                });
            }

            return updates;
        }

        private static string Slice(string line, int start, int end)
        {
            if (start >= line.Length) return string.Empty;
            var length = Math.Max(0, Math.Min(end, line.Length) - start);
            return line.Substring(start, length).Trim();
        }

        public static IReadOnlyList<WindowsUpdateItem> ParseWindowsUpdates(string output)
        {
            var updates = new List<WindowsUpdateItem>();
            foreach (var raw in output.Replace("\r", string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("WU|", StringComparison.Ordinal) || line.StartsWith("WU|NONE", StringComparison.Ordinal)) continue;
                var parts = line.Split('|');
                if (parts.Length < 2 || parts[1] is "RESULT" or "DOWNLOAD" or "QUEUED") continue;
                updates.Add(new WindowsUpdateItem
                {
                    Title = parts[1],
                    KnowledgeBase = parts.Length > 2 ? parts[2] : string.Empty,
                    SizeMb = parts.Length > 3 ? parts[3] : string.Empty
                });
            }

            return updates;
        }
    }
}
