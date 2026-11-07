using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core.Defender
{
    /// <summary>One thing Microsoft Defender found, from Get-MpThreatDetection joined with Get-MpThreat.</summary>
    public sealed class DefenderThreat
    {
        public string Name { get; init; } = string.Empty;
        public int SeverityId { get; init; }
        public DateTime? Detected { get; init; }
        public string Resources { get; init; } = string.Empty;
        public int StatusId { get; init; }

        public string Severity => SeverityId switch { 1 => "Low", 2 => "Moderate", 4 => "High", 5 => "Severe", _ => "Unknown" };

        /// <summary>Defender's ThreatStatusID, in words.</summary>
        public string Status => StatusId switch
        {
            1 => "Found, not dealt with yet",
            2 => "Cleaned",
            3 => "Quarantined by Defender",
            4 => "Removed",
            5 => "Allowed by you",
            6 => "Blocked",
            102 => "Quarantine failed",
            103 => "Removal failed",
            104 => "Allow failed",
            105 => "Abandoned",
            107 => "Block failed",
            _ => "Unknown"
        };

        /// <summary>True when the threat is still on the PC and nobody has decided about it.</summary>
        public bool NeedsAction => StatusId is 0 or 1 or 102 or 103 or 105 or 107;

        public string WhenText => Detected?.ToLocalTime().ToString("g") ?? "Unknown time";
    }

    public sealed class DefenderActionResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Microsoft Defender, through Windows PowerShell 5.1 out of process (see <see cref="ScriptRunner"/>).
    ///
    /// This used to host PowerShell in the app through the System.Management.Automation package alone. That package
    /// can't start a runspace by itself (PowerShell.Create() throws PSSnapInException for the missing
    /// Microsoft.PowerShell.Commands.Diagnostics.dll), so every status read came back "off" and every action "failed"
    /// or, worse, "succeeded" without doing anything. powershell.exe ships with Windows and has the Defender module.
    /// </summary>
    public class DefenderControl
    {
        public class DefenderStatus
        {
            /// <summary>False when Windows would not say. Every other field is then unknown, not "off".</summary>
            public bool Available { get; set; }
            public string Problem { get; set; } = string.Empty;
            public bool RealTimeProtectionEnabled { get; set; }
            public bool AntivirusEnabled { get; set; }
            public bool CloudProtectionEnabled { get; set; }
            public bool BehaviorMonitoringEnabled { get; set; }
            public bool PUAProtectionEnabled { get; set; }
            public bool ControlledFolderAccessEnabled { get; set; }
            public bool TamperProtectionEnabled { get; set; }
            public string MAPSReporting { get; set; } = "Unknown";
            /// <summary>"Normal", or "Passive Mode" when another antivirus is in charge.</summary>
            public string RunningMode { get; set; } = string.Empty;
            public DateTime? LastQuickScan { get; set; }
            public DateTime? LastFullScan { get; set; }
            public DateTime? LastUpdate { get; set; }
            public int SignatureAgeDays { get; set; } = -1;
            public string AntivirusSignatureVersion { get; set; } = "Unknown";

            public bool IsHealthy => Available && RealTimeProtectionEnabled && AntivirusEnabled && CloudProtectionEnabled;
            public bool SignaturesOutOfDate => Available && SignatureAgeDays > 3;
        }

        private const string StatusScript = """
            $ErrorActionPreference = 'Stop'
            function D($d) { if ($d) { ([datetime]$d).ToUniversalTime().ToString('o') } else { $null } }
            try {
                $s = Get-MpComputerStatus
                $p = Get-MpPreference
            } catch {
                [pscustomobject]@{ Error = $_.Exception.Message } | ConvertTo-Json -Compress
                exit 0
            }
            [pscustomobject]@{
                RealTime = [bool]$s.RealTimeProtectionEnabled
                Antivirus = [bool]$s.AntivirusEnabled
                Behavior = [bool]$s.BehaviorMonitorEnabled
                Tamper = [bool]$s.IsTamperProtected
                Maps = [int]$p.MAPSReporting
                Pua = [int]$p.PUAProtection
                Cfa = [int]$p.EnableControlledFolderAccess
                Mode = [string]$s.AMRunningMode
                QuickScan = (D $s.QuickScanEndTime)
                FullScan = (D $s.FullScanEndTime)
                SignatureUpdated = (D $s.AntivirusSignatureLastUpdated)
                SignatureAge = [int]$s.AntivirusSignatureAge
                SignatureVersion = [string]$s.AntivirusSignatureVersion
            } | ConvertTo-Json -Compress
            """;

        private const string ThreatsScript = """
            $ErrorActionPreference = 'Stop'
            try {
                $names = @{}
                foreach ($t in @(Get-MpThreat)) { $names[[string]$t.ThreatID] = $t }
                $list = @(Get-MpThreatDetection | Sort-Object InitialDetectionTime -Descending | Select-Object -First 50 | ForEach-Object {
                    $t = $names[[string]$_.ThreatID]
                    [pscustomobject]@{
                        Name = $(if ($t) { [string]$t.ThreatName } else { "Threat $($_.ThreatID)" })
                        Severity = $(if ($t) { [int]$t.SeverityID } else { 0 })
                        Detected = $(if ($_.InitialDetectionTime) { ([datetime]$_.InitialDetectionTime).ToUniversalTime().ToString('o') } else { $null })
                        Resources = (@($_.Resources) -join '; ')
                        Status = [int]$_.ThreatStatusID
                    }
                })
                ConvertTo-Json -InputObject $list -Compress -Depth 3
            } catch {
                [pscustomobject]@{ Error = $_.Exception.Message } | ConvertTo-Json -Compress
            }
            """;

        public async Task<DefenderStatus> GetStatusAsync()
        {
            var run = await ScriptRunner.RunPowerShellAsync(StatusScript, elevate: false, timeoutMs: 60000);
            return ParseStatus(run.Output);
        }

        public static DefenderStatus ParseStatus(string output)
        {
            var status = new DefenderStatus();
            var json = LastJsonLine(output);
            if (json == null)
            {
                status.Problem = string.IsNullOrWhiteSpace(output) ? "Windows did not answer." : output.Trim();
                return status;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Error", out var error))
                {
                    status.Problem = error.GetString() ?? "Defender did not answer.";
                    return status;
                }

                status.Available = true;
                status.RealTimeProtectionEnabled = Bool(root, "RealTime");
                status.AntivirusEnabled = Bool(root, "Antivirus");
                status.BehaviorMonitoringEnabled = Bool(root, "Behavior");
                status.TamperProtectionEnabled = Bool(root, "Tamper");
                var maps = Int(root, "Maps");
                status.MAPSReporting = maps switch { 0 => "Disabled", 1 => "Basic", 2 => "Advanced", _ => "Unknown" };
                status.CloudProtectionEnabled = maps > 0;
                // PUAProtection: 0 off, 1 block, 2 audit only. Audit doesn't stop anything.
                status.PUAProtectionEnabled = Int(root, "Pua") == 1;
                // EnableControlledFolderAccess: 0 off, 1 on, 2 audit, 3/4 block disk modification only.
                status.ControlledFolderAccessEnabled = Int(root, "Cfa") is 1 or 3 or 4;
                status.RunningMode = Text(root, "Mode");
                status.LastQuickScan = Date(root, "QuickScan");
                status.LastFullScan = Date(root, "FullScan");
                status.LastUpdate = Date(root, "SignatureUpdated");
                status.SignatureAgeDays = Int(root, "SignatureAge");
                var version = Text(root, "SignatureVersion");
                status.AntivirusSignatureVersion = string.IsNullOrEmpty(version) ? "Unknown" : version;
            }
            catch (JsonException ex)
            {
                status.Available = false;
                status.Problem = "Defender's answer could not be read: " + ex.Message;
            }

            return status;
        }

        public async Task<IReadOnlyList<DefenderThreat>> GetThreatsAsync()
        {
            var run = await ScriptRunner.RunPowerShellAsync(ThreatsScript, elevate: false, timeoutMs: 120000);
            if (run.Cancelled) throw new InvalidOperationException(run.Output);
            return ParseThreats(run.Output);
        }

        public static IReadOnlyList<DefenderThreat> ParseThreats(string output)
        {
            var json = LastJsonLine(output);
            if (json == null) return Array.Empty<DefenderThreat>();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Error", out var error))
                throw new InvalidOperationException("Defender would not list what it found: " + error.GetString());

            var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : new List<JsonElement> { root };
            return items
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item => new DefenderThreat
                {
                    Name = Text(item, "Name"),
                    SeverityId = Int(item, "Severity"),
                    Detected = Date(item, "Detected"),
                    // Defender writes "file:_C:\x.exe"; people only need the path.
                    Resources = Regex.Replace(Text(item, "Resources"), @"\b(file|process|regkey|service|containerfile):_", string.Empty),
                    StatusId = Int(item, "Status")
                })
                .ToList();
        }

        /// <summary>A quick scan (the places malware usually hides). Takes a few minutes; this waits for it.</summary>
        public Task<DefenderActionResult> QuickScanAsync() =>
            RunActionAsync("Start-MpScan -ScanType QuickScan", elevate: false, 60, "The quick scan finished.", "The quick scan did not finish");

        /// <summary>Scans one file or folder and waits for the answer.</summary>
        public Task<DefenderActionResult> ScanPathAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !(File.Exists(path) || Directory.Exists(path)))
                return Task.FromResult(new DefenderActionResult { Message = "That file or folder is not there." });
            return RunActionAsync($"Start-MpScan -ScanType CustomScan -ScanPath '{path.Replace("'", "''")}'", elevate: false, 120,
                $"Finished scanning {path}.", "The scan did not finish");
        }

        /// <summary>
        /// A full scan takes an hour or more, so it is handed to Defender's own command-line tool and left running:
        /// it keeps going if this app is closed, and Windows Security reports anything it finds.
        /// </summary>
        public DefenderActionResult StartFullScan()
        {
            var tool = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
            if (!File.Exists(tool)) return new DefenderActionResult { Message = "Microsoft Defender's scanner was not found on this PC." };
            try
            {
                Process.Start(new ProcessStartInfo(tool, "-Scan -ScanType 2")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                return new DefenderActionResult
                {
                    Success = true,
                    Message = "The full scan started. It runs in the background and usually takes an hour or more. Windows Security shows a notification if it finds anything."
                };
            }
            catch (Exception ex)
            {
                return new DefenderActionResult { Message = "The full scan could not start: " + ex.Message };
            }
        }

        public Task<DefenderActionResult> UpdateSignaturesAsync() =>
            RunActionAsync("Update-MpSignature; 'Virus definitions: ' + (Get-MpComputerStatus).AntivirusSignatureVersion", elevate: true, 15,
                "Virus definitions are up to date.", "Virus definitions could not be updated");

        /// <summary>Asks Defender to deal with every threat it found and has not dealt with yet.</summary>
        public Task<DefenderActionResult> RemoveActiveThreatsAsync() =>
            RunActionAsync("Remove-MpThreat", elevate: true, 30, "Defender dealt with what it found.", "Defender could not remove everything");

        private static async Task<DefenderActionResult> RunActionAsync(string command, bool elevate, int timeoutMinutes, string done, string failed)
        {
            // $ErrorActionPreference = 'Stop' turns Defender's non-terminating errors into a non-zero exit code,
            // so "it worked" means it worked.
            var script = "$ErrorActionPreference = 'Stop'\n" + command;
            var run = await ScriptRunner.RunPowerShellAsync(script, elevate, timeoutMs: timeoutMinutes * 60 * 1000);
            if (run.Cancelled) return new DefenderActionResult { Message = "Administrator approval was cancelled, so nothing ran." };
            if (run.Success)
            {
                var extra = string.IsNullOrWhiteSpace(run.Output) ? string.Empty : " " + run.Output.Trim();
                return new DefenderActionResult { Success = true, Message = done + extra };
            }

            return new DefenderActionResult { Message = $"{failed}. {FirstLine(run.Output)}".Trim() };
        }

        private static string FirstLine(string text)
        {
            var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? string.Empty;
            return line.Length > 300 ? line[..300] + "…" : line;
        }

        private static string? LastJsonLine(string output) =>
            output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith('{') || l.StartsWith('['));

        private static bool Bool(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        private static int Int(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : -1;

        private static string Text(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

        private static DateTime? Date(JsonElement e, string name) =>
            DateTime.TryParse(Text(e, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d.ToUniversalTime() : null;
    }
}
