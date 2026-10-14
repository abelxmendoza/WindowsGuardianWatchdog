using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core.Drivers
{
    public class DriverUpdateChecker
    {
        public async Task<bool> IsUpdateAvailableAsync(DriverInfo driver)
        {
            var titles = await FindDriverUpdateTitlesAsync();
            return Matches(driver, titles);
        }

        public async Task<IReadOnlyList<string>> FindDriverUpdateTitlesAsync()
        {
            const string script = """
                $ErrorActionPreference = 'Stop'
                $session = New-Object -ComObject Microsoft.Update.Session
                $searcher = $session.CreateUpdateSearcher()
                $searcher.Online = $true
                $result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'")
                if ($result.Updates.Count -eq 0) { Write-Output 'DRV|NONE'; return }
                foreach ($update in $result.Updates) {
                    $title = ([string]$update.Title) -replace '\|','/' -replace '[\r\n]',' '
                    Write-Output "DRV|$title"
                }
                """;
            var result = await ScriptRunner.RunPowerShellAsync(script, elevate: false, timeoutMs: 10 * 60 * 1000);
            if (!string.IsNullOrWhiteSpace(result.Output) && result.Output.Contains("DRV|", StringComparison.Ordinal) == false && result.ExitCode != 0)
            {
                throw new InvalidOperationException(result.Output.Trim());
            }

            return result.Output
                .Replace("\r", string.Empty)
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("DRV|", StringComparison.Ordinal) && !line.StartsWith("DRV|NONE", StringComparison.Ordinal))
                .Select(line => line[4..])
                .Where(title => title.Length > 0)
                .ToList();
        }

        public Task<ScriptResult> InstallDriverUpdatesAsync()
        {
            const string script = """
                $ErrorActionPreference = 'Stop'
                $session = New-Object -ComObject Microsoft.Update.Session
                $searcher = $session.CreateUpdateSearcher()
                $searcher.Online = $true
                $result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'")
                if ($result.Updates.Count -eq 0) { Write-Output 'DRV|NONE'; return }
                $collection = New-Object -ComObject Microsoft.Update.UpdateColl
                foreach ($update in $result.Updates) {
                    if (-not $update.EulaAccepted) { $update.AcceptEula() | Out-Null }
                    [void]$collection.Add($update)
                    Write-Output ("DRV|QUEUED|" + (($update.Title -replace '\|','/')))
                }
                $downloader = $session.CreateUpdateDownloader()
                $downloader.Updates = $collection
                $download = $downloader.Download()
                Write-Output "DRV|DOWNLOAD|$($download.ResultCode)"
                $installer = $session.CreateUpdateInstaller()
                $installer.Updates = $collection
                $installed = $installer.Install()
                Write-Output "DRV|RESULT|$($installed.ResultCode)|$($installed.RebootRequired)"
                """;
            return ScriptRunner.RunPowerShellAsync(script, elevate: true, timeoutMs: 90 * 60 * 1000);
        }

        public static bool Matches(DriverInfo driver, IEnumerable<string> titles)
        {
            if (string.IsNullOrWhiteSpace(driver.DeviceName)) return false;
            var name = driver.DeviceName.Trim();
            return titles.Any(title => title.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        public Task<string?> GetLatestVersionAsync(DriverInfo driver) => Task.FromResult<string?>(null);
    }
}
