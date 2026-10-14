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
                $titles = New-Object System.Collections.Generic.List[string]
                $searched = $false
                $failure = $null
                function Search-Service($serviceId) {
                    try {
                        $searcher = $session.CreateUpdateSearcher()
                        $searcher.Online = $true
                        if ($serviceId) {
                            $searcher.ServerSelection = 3
                            $searcher.ServiceID = $serviceId
                        }
                        $result = $searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Driver'")
                        foreach ($update in $result.Updates) {
                            $title = ([string]$update.Title) -replace '\|','/' -replace '[\r\n]',' '
                            if ($title -and -not $titles.Contains($title)) { [void]$titles.Add($title) }
                        }
                        $script:searched = $true
                    } catch {
                        if (-not $script:failure) { $script:failure = $_.Exception.Message }
                    }
                }
                Search-Service $null
                Search-Service '7971f918-a847-4430-9279-4a52d1efe18d'
                if (-not $searched -and $failure) { throw $failure }
                if ($titles.Count -eq 0) { Write-Output 'DRV|NONE'; return }
                foreach ($title in $titles) { Write-Output "DRV|$title" }
                """;
            var result = await ScriptRunner.RunPowerShellAsync(script, elevate: false, timeoutMs: 10 * 60 * 1000);
            if (result.Cancelled) throw new InvalidOperationException("Driver update check was cancelled.");
            if (result.ExitCode != 0 && result.Output.Contains("DRV|", StringComparison.Ordinal) == false)
            {
                throw new InvalidOperationException(ExplainUpdateError(result.Output));
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

        public Task<ScriptResult> ScanHardwareAsync()
        {
            var pnputil = Path.Combine(Environment.SystemDirectory, "pnputil.exe");
            return ScriptRunner.RunAsync(pnputil, "/scan-devices", elevate: false, timeoutMs: 180000);
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
            var tokens = Tokens(driver.DeviceName).Concat(Tokens(driver.DriverProviderName))
                .Where(token => token.Length >= 4 && !GenericTokens.Contains(token))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tokens.Count == 0) return false;
            return titles.Any(title => tokens.Any(token => title.Contains(token, StringComparison.OrdinalIgnoreCase)));
        }

        public static string ExplainUpdateError(string? output)
        {
            var text = string.IsNullOrWhiteSpace(output) ? "Windows Update could not be searched." : output.Trim();
            if (text.Contains("8024402C", StringComparison.OrdinalIgnoreCase)) return "Windows Update could not be reached. Check the network connection and try again.";
            if (text.Contains("8024001E", StringComparison.OrdinalIgnoreCase) || text.Contains("80248007", StringComparison.OrdinalIgnoreCase))
                return "The Windows Update service is not available. Start the Windows Update service and try again.";
            if (text.Contains("80240438", StringComparison.OrdinalIgnoreCase)) return "Windows Update is managed by this PC's update server and did not return driver updates.";
            var line = text.Replace("\r", string.Empty).Split('\n').LastOrDefault(part => !string.IsNullOrWhiteSpace(part)) ?? text;
            return line.Length > 280 ? line[..280] : line;
        }

        private static IEnumerable<string> Tokens(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) yield break;
            foreach (var token in value.Split(new[] { ' ', '-', '_', ',', '(', ')', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
                yield return token.Trim();
        }

        private static readonly HashSet<string> GenericTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "adapter", "controller", "device", "family", "audio", "system", "driver", "extension",
            "provider", "component", "windows", "software", "generic", "standard", "composite",
            "high", "definition", "microsoft", "bus", "port", "root", "virtual", "miniport"
        };

        public Task<string?> GetLatestVersionAsync(DriverInfo driver) => Task.FromResult<string?>(null);
    }
}
