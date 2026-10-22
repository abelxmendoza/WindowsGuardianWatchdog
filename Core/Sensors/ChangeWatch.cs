using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using WindowsGuardianWatchdog.Core.Incidents;
using WindowsGuardianWatchdog.Core.Storage;

namespace WindowsGuardianWatchdog.Core.Sensors
{
    /// <summary>
    /// A smoke detector for the few settings an intruder changes first, ported from the Linux app's defenses.py and
    /// tripwires.py. It reads, never changes anything, and needs no administrator rights.
    ///
    ///   defenses      Windows Firewall, Defender real-time protection, UAC: reported only when one that WAS on goes off
    ///   tripwires     administrators, local accounts, Run keys, Startup folders, Winlogon, debugger hijacks,
    ///                 the hosts file, the Sticky Keys / Utility Manager swap
    ///
    /// The first look only records what is normal. After that each change is reported once, as a notice.
    /// </summary>
    public static class ChangeWatch
    {
        public sealed record WatchGroup(string Id, string Title, string Technique, string Meaning, NoticeSeverity AddedSeverity, bool IsDefense = false);

        public sealed record WatchItem(string Key, string Group, string Label, string Value);

        public sealed class Change
        {
            public WatchGroup Group { get; init; } = null!;
            public string Label { get; init; } = string.Empty;
            public string What { get; init; } = string.Empty;
            public NoticeSeverity Severity { get; init; }
        }

        public sealed class Result
        {
            public bool FirstLook { get; init; }
            public int Watched { get; init; }
            public DateTime CheckedAt { get; init; }
            public List<Change> Changes { get; init; } = new();
        }

        private sealed class Snapshot
        {
            public DateTime LastRun { get; set; }
            public Dictionary<string, string> Values { get; set; } = new();
        }

        public static readonly IReadOnlyList<WatchGroup> Groups = new[]
        {
            new WatchGroup("firewall", "Windows Firewall", "T1562.004", "Without the firewall, programs on this PC can be reached from the network.", NoticeSeverity.Critical, true),
            new WatchGroup("defender", "Microsoft Defender", "T1562.001", "Turning off the virus scanner is one of the first things malware tries.", NoticeSeverity.Critical, true),
            new WatchGroup("uac", "Administrator prompts (UAC)", "T1548.002", "Without these prompts, any program can make system changes silently.", NoticeSeverity.Critical, true),
            new WatchGroup("admins", "Administrators", "T1098", "An account was given full control of this PC.", NoticeSeverity.Critical),
            new WatchGroup("accounts", "Accounts on this PC", "T1136.001", "A new account is a way back in for whoever made it.", NoticeSeverity.Warning),
            new WatchGroup("run", "Programs that open with Windows", "T1547.001", "Malware adds itself here to survive a restart.", NoticeSeverity.Warning),
            new WatchGroup("startup", "Startup folders", "T1547.001", "A file here runs every time someone signs in.", NoticeSeverity.Warning),
            new WatchGroup("winlogon", "Sign-in programs (Winlogon)", "T1547.004", "These run at every sign-in; they almost never change outside a Windows update.", NoticeSeverity.Critical),
            new WatchGroup("ifeo", "Program hijacks (debugger setting)", "T1546.012", "A program can be set to launch something else in its place.", NoticeSeverity.Critical),
            new WatchGroup("hosts", "hosts file", "T1565.001", "A website name may now point at a different address.", NoticeSeverity.Warning),
            new WatchGroup("accessibility", "Sticky Keys / Utility Manager", "T1546.008", "These run at the sign-in screen; swapping them for a command prompt gives anyone full control.", NoticeSeverity.Critical),
        };

        private const string On = "on";
        private const string Off = "off";
        private static readonly object Gate = new();
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
        private static string SnapshotPath => GuardianPaths.File("change-watch.json");

        /// <summary>When the watch last ran, or null if never. The App health page uses it as a dead-man's switch.</summary>
        public static DateTime? LastRun => LoadSnapshot()?.LastRun;

        public static Task<Result> RunAsync() => Task.Run(() => Run());

        public static Result Run()
        {
            lock (Gate)
            {
                var items = Collect(out var unreadable);
                var previous = LoadSnapshot();
                var now = items.GroupBy(i => i.Key).ToDictionary(g => g.Key, g => g.First().Value);
                if (previous != null)
                {
                    // A group that could not be read this time keeps its last values, so a hiccup is not reported as
                    // everything in it disappearing and then reappearing.
                    foreach (var (key, value) in previous.Values.Where(kv => unreadable.Contains(GroupOf(kv.Key))))
                        now[key] = value;
                }
                var changes = previous == null ? new List<Change>() : Compare(previous.Values, now, items);

                SaveSnapshot(new Snapshot { LastRun = DateTime.Now, Values = now });

                if (previous == null)
                {
                    EventTimeline.Record(NoticeKind.Information, NoticeSeverity.Info, "Change watch", "Change watch started",
                        $"Recorded {now.Count} settings as normal. From now on a change to any of them is reported here.");
                }

                foreach (var change in changes)
                {
                    var kind = change.Severity == NoticeSeverity.Info ? NoticeKind.Information : NoticeKind.Incident;
                    EventTimeline.Record(kind, change.Severity, "Change watch", $"{change.Group.Title}: {change.What}",
                        $"{change.Label}. {change.Group.Meaning} (MITRE ATT&CK {change.Group.Technique})",
                        Advice(change));
                }

                return new Result { FirstLook = previous == null, Watched = now.Count, CheckedAt = DateTime.Now, Changes = changes };
            }
        }

        /// <summary>Forget the recorded settings, so the next look treats what is there now as normal.</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                try { System.IO.File.Delete(SnapshotPath); } catch (IOException) { }
            }
        }

        private static List<Change> Compare(Dictionary<string, string> before, Dictionary<string, string> now, List<WatchItem> items)
        {
            var changes = new List<Change>();
            var labels = items.GroupBy(i => i.Key).ToDictionary(g => g.Key, g => g.First());

            foreach (var (key, value) in now)
            {
                if (!labels.TryGetValue(key, out var item)) continue; // carried over from a group that could not be read
                var group = Groups.First(g => g.Id == item.Group);
                if (!before.TryGetValue(key, out var old))
                {
                    // A defense that appears for the first time is not news; a new entry in a tripwire group is.
                    if (!group.IsDefense) changes.Add(new Change { Group = group, Label = item.Label, What = "something was added", Severity = group.AddedSeverity });
                    continue;
                }
                if (old == value) continue;

                if (group.IsDefense)
                {
                    // Only on -> off is an alarm. One that was never on is the Hardening page's business.
                    if (old == On && value == Off)
                        changes.Add(new Change { Group = group, Label = item.Label, What = "switched off", Severity = NoticeSeverity.Critical });
                    else if (old == Off && value == On)
                        changes.Add(new Change { Group = group, Label = item.Label, What = "switched back on", Severity = NoticeSeverity.Info });
                }
                else
                {
                    changes.Add(new Change { Group = group, Label = item.Label, What = "something changed", Severity = group.AddedSeverity });
                }
            }

            foreach (var key in before.Keys.Except(now.Keys))
            {
                var groupId = GroupOf(key);
                var group = Groups.FirstOrDefault(g => g.Id == groupId);
                if (group == null || group.IsDefense) continue;
                changes.Add(new Change { Group = group, Label = key[(groupId.Length + 1)..], What = "something was removed", Severity = NoticeSeverity.Info });
            }

            return changes;
        }

        private static string Advice(Change change) => change.Group.Id switch
        {
            _ when change.Severity == NoticeSeverity.Info => "Nothing to do if you expected this.",
            "firewall" or "defender" or "uac" => "If you did not switch it off, turn it back on from \"Turn on protection\", then run a full Defender scan.",
            "admins" or "accounts" => "If you did not make this change, open Settings > Accounts > Other users, remove the account, and change your password.",
            "run" or "startup" => "If you do not recognize it, open \"Startup programs\" and turn it off, then check the file with Defender.",
            "hosts" => @"Open C:\Windows\System32\drivers\etc\hosts in Notepad and look for lines you did not add.",
            _ => "This is rarely legitimate. If no Windows update or setup just ran, treat the PC as compromised: disconnect it and run a Defender offline scan."
        };

        public static List<WatchItem> Collect(out HashSet<string> unreadable)
        {
            var items = new List<WatchItem>();
            var failed = new HashSet<string>();
            void Safe(string group, Action<List<WatchItem>> read)
            {
                var found = new List<WatchItem>();
                try
                {
                    read(found);
                    items.AddRange(found);
                }
                catch (Exception ex)
                {
                    failed.Add(group);
                    Debug.WriteLine($"Change watch could not read {group}: {ex.Message}");
                }
            }

            Safe("firewall", CollectFirewall);
            Safe("defender", CollectDefender);
            Safe("uac", CollectUac);
            Safe("admins", CollectAdministrators);
            Safe("accounts", CollectAccounts);
            Safe("run", CollectRunKeys);
            Safe("startup", CollectStartupFolders);
            Safe("winlogon", CollectWinlogon);
            Safe("ifeo", CollectDebuggerHijacks);
            Safe("hosts", CollectHosts);
            Safe("accessibility", CollectAccessibility);
            unreadable = failed;
            return items;
        }

        private static string GroupOf(string key) => key.Split(':')[0];

        private static void CollectFirewall(List<WatchItem> items)
        {
            const string root = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\";
            foreach (var (profile, label) in new[] { ("DomainProfile", "Work network"), ("StandardProfile", "Private network"), ("PublicProfile", "Public network") })
            {
                using var key = Registry.LocalMachine.OpenSubKey(root + profile);
                if (key?.GetValue("EnableFirewall") is int enabled)
                    items.Add(new WatchItem($"firewall:{profile}", "firewall", $"Firewall for the {label.ToLowerInvariant()} profile", enabled == 1 ? On : Off));
            }
        }

        private static void CollectDefender(List<WatchItem> items)
        {
            using (var policy = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender"))
            {
                var disabled = policy?.GetValue("DisableAntiSpyware") is int value && value == 1;
                items.Add(new WatchItem("defender:policy", "defender", "Defender allowed by policy", disabled ? Off : On));
            }
            using var realtime = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
            if (realtime != null)
            {
                var off = realtime.GetValue("DisableRealtimeMonitoring") is int value && value == 1;
                items.Add(new WatchItem("defender:realtime", "defender", "Defender real-time protection", off ? Off : On));
            }
        }

        private static void CollectUac(List<WatchItem> items)
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            if (key?.GetValue("EnableLUA") is int lua)
                items.Add(new WatchItem("uac:EnableLUA", "uac", "Administrator prompts", lua == 1 ? On : Off));
        }

        private static void CollectAdministrators(List<WatchItem> items)
        {
            // Look the group up by its well-known SID: its name is translated on non-English Windows.
            using var groups = new ManagementObjectSearcher("SELECT Name, Domain FROM Win32_Group WHERE LocalAccount = True AND SID = 'S-1-5-32-544'");
            foreach (ManagementObject group in groups.Get())
            {
                var name = group["Name"]?.ToString();
                var domain = group["Domain"]?.ToString();
                if (name == null || domain == null) continue;
                var query = $"SELECT PartComponent FROM Win32_GroupUser WHERE GroupComponent = \"Win32_Group.Domain='{domain}',Name='{name}'\"";
                using var members = new ManagementObjectSearcher(query);
                foreach (ManagementObject member in members.Get())
                {
                    var account = DescribeWmiAccount(member["PartComponent"]?.ToString() ?? string.Empty);
                    items.Add(new WatchItem($"admins:{account}", "admins", $"{account} is an administrator", "member"));
                }
            }
        }

        private static void CollectAccounts(List<WatchItem> items)
        {
            using var users = new ManagementObjectSearcher("SELECT Name, Disabled FROM Win32_UserAccount WHERE LocalAccount = True");
            foreach (ManagementObject user in users.Get())
            {
                var name = user["Name"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                var disabled = user["Disabled"] is bool d && d;
                items.Add(new WatchItem($"accounts:{name}", "accounts", $"Account \"{name}\"", disabled ? "disabled" : "enabled"));
            }
        }

        private static void CollectRunKeys(List<WatchItem> items)
        {
            var keys = new (RegistryKey Hive, string Path, string Label)[]
            {
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "your account"),
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "your account, once"),
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "every account"),
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "every account, once"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "every account (32-bit)"),
            };
            foreach (var (hive, path, label) in keys)
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;
                foreach (var name in key.GetValueNames())
                {
                    var command = key.GetValue(name)?.ToString() ?? string.Empty;
                    items.Add(new WatchItem($"run:{hive.Name}\\{path}\\{name}", "run", $"\"{name}\" opens with Windows for {label}: {command}", Fingerprint(command)));
                }
            }
        }

        private static void CollectStartupFolders(List<WatchItem> items)
        {
            foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
            {
                var path = Environment.GetFolderPath(folder);
                if (!Directory.Exists(path)) continue;
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (Path.GetFileName(file).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    var info = new FileInfo(file);
                    items.Add(new WatchItem($"startup:{file}", "startup", $"\"{info.Name}\" in {path}", $"{info.Length}:{info.LastWriteTimeUtc.Ticks}"));
                }
            }
        }

        private static void CollectWinlogon(List<WatchItem> items)
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon");
            if (key == null) return;
            foreach (var name in new[] { "Shell", "Userinit" })
            {
                var value = key.GetValue(name)?.ToString() ?? string.Empty;
                items.Add(new WatchItem($"winlogon:{name}", "winlogon", $"Winlogon {name} is \"{value}\"", Fingerprint(value)));
            }
        }

        private static void CollectDebuggerHijacks(List<WatchItem> items)
        {
            using var root = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options");
            if (root == null) return;
            foreach (var program in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(program);
                var debugger = key?.GetValue("Debugger")?.ToString();
                if (string.IsNullOrWhiteSpace(debugger)) continue;
                items.Add(new WatchItem($"ifeo:{program}", "ifeo", $"Starting {program} runs \"{debugger}\" instead", Fingerprint(debugger)));
            }
        }

        private static void CollectHosts(List<WatchItem> items)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            if (!System.IO.File.Exists(path)) return;
            // Comments and blank lines are ignored, so reformatting the file is not a change.
            var entries = System.IO.File.ReadAllLines(path)
                .Select(l => l.Split('#')[0].Trim())
                .Where(l => l.Length > 0)
                .Select(l => string.Join(' ', l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
            items.Add(new WatchItem("hosts:entries", "hosts", path, Fingerprint(string.Join('\n', entries))));
        }

        private static void CollectAccessibility(List<WatchItem> items)
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var shells = new[] { "cmd.exe", "powershell.exe", "WindowsPowerShell\\v1.0\\powershell.exe", "explorer.exe" }
                .Select(n => Path.Combine(system, n))
                .Concat(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") })
                .Where(System.IO.File.Exists)
                .Select(HashFile)
                .ToHashSet();

            foreach (var tool in new[] { "sethc.exe", "utilman.exe", "osk.exe", "Magnify.exe", "Narrator.exe" })
            {
                var path = Path.Combine(system, tool);
                if (!System.IO.File.Exists(path)) continue;
                // Comparing against the shells' own hashes catches the swap without flagging every Windows update.
                var swapped = shells.Contains(HashFile(path));
                items.Add(new WatchItem($"accessibility:{tool}", "accessibility", swapped ? $"{tool} is now a copy of a command shell" : $"{tool} is the real tool", swapped ? "swapped" : "original"));
            }
        }

        private static string DescribeWmiAccount(string path)
        {
            // Win32_UserAccount.Domain="PC",Name="alice" -> PC\alice
            string Part(string name)
            {
                var start = path.IndexOf(name + "=\"", StringComparison.OrdinalIgnoreCase);
                if (start < 0) return string.Empty;
                start += name.Length + 2;
                var end = path.IndexOf('"', start);
                return end > start ? path[start..end] : string.Empty;
            }
            var domain = Part("Domain");
            var account = Part("Name");
            return string.IsNullOrEmpty(account) ? path : $"{domain}\\{account}";
        }

        private static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

        private static string HashFile(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static Snapshot? LoadSnapshot()
        {
            try
            {
                return System.IO.File.Exists(SnapshotPath)
                    ? JsonSerializer.Deserialize<Snapshot>(System.IO.File.ReadAllText(SnapshotPath))
                    : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Change watch snapshot unreadable: {ex.Message}");
                return null;
            }
        }

        private static void SaveSnapshot(Snapshot snapshot)
        {
            try { GuardianPaths.WriteAtomically(SnapshotPath, JsonSerializer.Serialize(snapshot, Json)); }
            catch (Exception ex) { Debug.WriteLine($"Change watch snapshot not saved: {ex.Message}"); }
        }
    }
}
