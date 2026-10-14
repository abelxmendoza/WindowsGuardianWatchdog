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
        public static async Task<bool> IsWingetAvailableAsync()
        {
            var result = await ScriptRunner.RunAsync("winget.exe", "--version", elevate: false, timeoutMs: 20000);
            return result.Success && result.Output.Contains("v", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<IReadOnlyList<AppPackageUpdate>> FindAppUpdatesAsync()
        {
            var result = await ScriptRunner.RunAsync(
                "winget.exe",
                "upgrade --include-unknown --disable-interactivity --accept-source-agreements",
                elevate: false,
                timeoutMs: 180000);
            if (result.ExitCode != 0 && !result.Output.Contains("---", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Output) ? "winget could not list updates." : result.Output);
            }
            return ParseWingetUpgrades(result.Output);
        }

        public static async Task<ScriptResult> UpgradeAllAppsAsync()
        {
            const string script = """
                function Find-Winget {
                    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
                    if ($cmd) { return $cmd.Source }
                    $root = Join-Path $env:ProgramFiles 'WindowsApps'
                    $package = Get-ChildItem -Path $root -Directory -Filter 'Microsoft.DesktopAppInstaller_*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
                    if ($package) {
                        $exe = Join-Path $package.FullName 'winget.exe'
                        if (Test-Path -LiteralPath $exe) { return $exe }
                    }
                    throw 'winget was not found. Install App Installer from the Microsoft Store.'
                }
                $winget = Find-Winget
                & $winget upgrade --all --include-unknown --disable-interactivity --accept-package-agreements --accept-source-agreements
                """;
            return await ScriptRunner.RunPowerShellAsync(script, elevate: true, timeoutMs: 45 * 60 * 1000);
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
            var lines = output.Replace("\r", string.Empty).Split('\n');
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
