using System.Text;
using Microsoft.Win32;
using WindowsGuardianWatchdog.Core.Defender;
using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core
{
    /// <summary>
    /// "Look through this PC": a short list of things that are actually worth a person's attention.
    ///
    ///   Defender      anything Defender found and has not dealt with, real-time protection off, old virus definitions
    ///   firewall      a network profile with the firewall off
    ///   programs      a running program that is not signed by its maker AND runs from a folder any program can
    ///                 write to (Temp, Downloads, AppData…). Signed programs and programs under Program Files or
    ///                 Windows are not reported: being unsigned alone is common and not a sign of malware.
    ///   persistence   a "starts with Windows" entry that points into Temp or Downloads, and debugger hijacks
    ///
    /// Every finding is raised through <see cref="ThreatDetected"/>, so the list on screen matches the count.
    /// </summary>
    public class BlueTeamDetector
    {
        private readonly DefenderControl _defender = new();

        public event EventHandler<ThreatDetection>? ThreatDetected;

        public async Task<ThreatReport> PerformThreatHuntAsync()
        {
            var report = new ThreatReport { ScanTime = DateTime.Now };

            report.Threats.AddRange(await DetectDefenderProblemsAsync());
            report.Threats.AddRange(DetectFirewallProblems());
            report.Threats.AddRange(await DetectUnsignedProgramsAsync());
            report.Threats.AddRange(DetectPersistence());

            foreach (var threat in report.Threats) OnThreatDetected(threat);

            report.ThreatCount = report.Threats.Count;
            report.Severity = report.Threats.Count == 0 ? ThreatSeverity.Low : report.Threats.Max(t => t.Severity);
            return report;
        }

        private async Task<List<ThreatDetection>> DetectDefenderProblemsAsync()
        {
            var threats = new List<ThreatDetection>();
            var status = await _defender.GetStatusAsync();
            if (!status.Available)
            {
                threats.Add(Finding(ThreatType.Configuration, ThreatSeverity.Medium, "Could not read Microsoft Defender's status",
                    status.Problem + " If another antivirus is installed, check it instead."));
            }
            else
            {
                if (!status.AntivirusEnabled || !status.RealTimeProtectionEnabled)
                    threats.Add(Finding(ThreatType.Configuration, ThreatSeverity.Critical, "Microsoft Defender real-time protection is off",
                        "New files are not being checked. Turn it on in Windows Security > Virus & threat protection, or from \"Turn on protection\"."));
                if (status.SignaturesOutOfDate)
                    threats.Add(Finding(ThreatType.Configuration, ThreatSeverity.Medium, $"Virus definitions are {status.SignatureAgeDays} days old",
                        "Use \"Update virus definitions\" on this page."));
            }

            try
            {
                foreach (var found in (await _defender.GetThreatsAsync()).Where(t => t.NeedsAction))
                {
                    threats.Add(Finding(ThreatType.File, ThreatSeverity.Critical, $"Defender found {found.Name}",
                        $"{found.Status}. {found.Resources} (seen {found.WhenText}). Use \"Remove what Defender found\"."));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Defender threat list unavailable: {ex.Message}");
            }

            return threats;
        }

        private static List<ThreatDetection> DetectFirewallProblems()
        {
            var threats = new List<ThreatDetection>();
            const string root = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
            foreach (var (profile, label) in new[] { ("DomainProfile", "work"), ("StandardProfile", "private"), ("PublicProfile", "public") })
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(root + profile);
                    if (key?.GetValue("EnableFirewall") is int enabled && enabled == 0)
                        threats.Add(Finding(ThreatType.Configuration, ThreatSeverity.High, $"Windows Firewall is off for {label} networks",
                            "Turn it back on in Windows Security > Firewall & network protection."));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Firewall profile {profile} unreadable: {ex.Message}");
                }
            }
            return threats;
        }

        private static async Task<List<ThreatDetection>> DetectUnsignedProgramsAsync()
        {
            var running = new Dictionary<string, (string Name, int Pid)>(StringComparer.OrdinalIgnoreCase);
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path) && IsUserWritableLocation(path) && !running.ContainsKey(path))
                        running[path] = (process.ProcessName, process.Id);
                }
                catch
                {
                    // Protected and already-exited processes can't be inspected; Defender covers those.
                }
                finally
                {
                    process.Dispose();
                }
            }

            var threats = new List<ThreatDetection>();
            if (running.Count == 0) return threats;

            var signatures = await CheckSignaturesAsync(running.Keys.ToList());
            foreach (var (path, (name, pid)) in running)
            {
                if (!signatures.TryGetValue(path, out var status) || status == "Valid") continue;
                var tampered = status == "HashMismatch";
                threats.Add(Finding(ThreatType.Process, tampered ? ThreatSeverity.High : ThreatSeverity.Medium,
                    tampered ? $"{name} was changed after its maker signed it" : $"{name} is unsigned and runs from a download or temp folder",
                    $"PID {pid}. {path}. If you don't recognize it, scan it with \"Scan a file or folder\" and quarantine it."));
            }
            return threats;
        }

        /// <summary>Authenticode status per file, from one PowerShell call for all of them.</summary>
        private static async Task<Dictionary<string, string>> CheckSignaturesAsync(IReadOnlyList<string> paths)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var script = new StringBuilder("$paths = @(\n");
            script.Append(string.Join(",\n", paths.Select(p => "'" + p.Replace("'", "''") + "'")));
            script.Append(")\nforeach ($p in $paths) { try { $s = Get-AuthenticodeSignature -LiteralPath $p -ErrorAction Stop; \"$($s.Status)|$p\" } catch { \"Unknown|$p\" } }");
            var run = await ScriptRunner.RunPowerShellAsync(script.ToString(), elevate: false, timeoutMs: 180000);
            foreach (var line in run.Output.Split('\n'))
            {
                var parts = line.Trim().Split('|', 2);
                if (parts.Length == 2 && parts[0] != "Unknown") result[parts[1]] = parts[0];
            }
            return result;
        }

        private static List<ThreatDetection> DetectPersistence()
        {
            var threats = new List<ThreatDetection>();
            foreach (var (hive, path) in new[]
            {
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
            })
            {
                try
                {
                    using var key = hive.OpenSubKey(path);
                    if (key == null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var command = key.GetValue(name)?.ToString() ?? string.Empty;
                        if (IsTempOrDownloads(command))
                            threats.Add(Finding(ThreatType.Registry, ThreatSeverity.High, $"\"{name}\" starts with Windows from a temp or download folder",
                                $"{command}. Installers don't normally leave programs there. Check it in \"Startup programs\"."));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Run key {path} unreadable: {ex.Message}");
                }
            }

            try
            {
                using var ifeo = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options");
                foreach (var program in ifeo?.GetSubKeyNames() ?? Array.Empty<string>())
                {
                    using var key = ifeo!.OpenSubKey(program);
                    var debugger = key?.GetValue("Debugger")?.ToString();
                    if (string.IsNullOrWhiteSpace(debugger)) continue;
                    threats.Add(Finding(ThreatType.Registry, ThreatSeverity.High, $"Starting {program} runs something else",
                        $"Debugger is set to \"{debugger}\". Developer tools sometimes do this; otherwise it is a known way to hijack programs."));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IFEO unreadable: {ex.Message}");
            }

            return threats;
        }

        /// <summary>Folders any program the user runs can write to, so malware can drop files there.</summary>
        public static bool IsUserWritableLocation(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var full = path.Trim('"');
            string[] trusted =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            };
            if (trusted.Any(root => IsUnder(full, root))) return false;

            string[] writable =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetEnvironmentVariable("PUBLIC") ?? string.Empty,
                Path.GetTempPath(),
            };
            return writable.Any(root => IsUnder(full, root)) || full.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTempOrDownloads(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            return command.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase)
                || command.Contains(@"\Downloads\", StringComparison.OrdinalIgnoreCase)
                || command.Contains(@"\$Recycle.Bin\", StringComparison.OrdinalIgnoreCase)
                || command.Contains("%TEMP%", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnder(string path, string root) =>
            !string.IsNullOrEmpty(root) &&
            path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static ThreatDetection Finding(ThreatType type, ThreatSeverity severity, string description, string details) =>
            new() { Type = type, Severity = severity, Description = description, Details = details, Timestamp = DateTime.Now };

        protected virtual void OnThreatDetected(ThreatDetection threat)
        {
            ThreatDetected?.Invoke(this, threat);
        }
    }

    public class ThreatDetection
    {
        public ThreatType Type { get; set; }
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public enum ThreatType
    {
        Process,
        Network,
        File,
        Configuration,
        Registry
    }

    public enum ThreatSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class ThreatReport
    {
        public DateTime ScanTime { get; set; }
        public List<ThreatDetection> Threats { get; set; } = new();
        public int ThreatCount { get; set; }
        public ThreatSeverity Severity { get; set; }
    }
}
