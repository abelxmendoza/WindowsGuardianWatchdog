using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using WindowsGuardianWatchdog.Core.Sensors;
using WindowsGuardianWatchdog.Core.Storage;

namespace WindowsGuardianWatchdog.Core.SelfCheck
{
    public enum CheckStatus { Ok, Warning, Problem }

    public sealed class SelfCheckItem
    {
        public string Name { get; init; } = string.Empty;
        public CheckStatus Status { get; init; }
        public string Detail { get; init; } = string.Empty;
        public string Advice { get; init; } = string.Empty;
        /// <summary>A repair the page can offer, or null. See <see cref="AppSelfCheck.Repair"/>.</summary>
        public string? Repair { get; init; }
        public string? RepairLabel { get; init; }

        public string StatusText => Status switch { CheckStatus.Ok => "Fine", CheckStatus.Warning => "Worth a look", _ => "Needs fixing" };
    }

    /// <summary>
    /// Is the app itself healthy, and has anyone tampered with it? Ported from the Mac Self-Check and the Linux
    /// self_check.py. A security tool is a target, so it checks its own foundations:
    ///
    ///   app files     still match the hashes recorded when this version was first run
    ///   permissions   ordinary accounts can't write to the app's folder
    ///   saved state   every settings/state file still parses; a damaged one can be restored from a daily backup
    ///   change watch  has actually run recently (a dead-man's switch for the background watch)
    ///   crashes       the crash log is quiet
    ///   space         the data folder isn't filling the disk
    ///
    /// Honest limit: someone who already controls this account can edit the app AND its recorded hashes. This catches
    /// accidents, half-finished updates and unsophisticated tampering; it can't promise more.
    /// </summary>
    public static class AppSelfCheck
    {
        private const int BackupDays = 7;
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
        private static string BaselinePath => GuardianPaths.File("app-files.json");
        private static string BackupRoot => Path.Combine(GuardianPaths.DataDirectory, "backups");

        private sealed class AppBaseline
        {
            public string Version { get; set; } = string.Empty;
            public Dictionary<string, string> Hashes { get; set; } = new();
        }

        public static Task<List<SelfCheckItem>> RunAsync() => Task.Run(() => Run());

        public static List<SelfCheckItem> Run()
        {
            var checks = new List<SelfCheckItem>();
            void Add(string name, Func<SelfCheckItem> check)
            {
                try { checks.Add(check()); }
                catch (Exception ex) { checks.Add(new SelfCheckItem { Name = name, Status = CheckStatus.Warning, Detail = $"Could not check: {ex.Message}" }); }
            }

            Add("Data folder", CheckDataFolder);
            Add("Saved settings", CheckStateFiles);
            Add("App files", CheckAppFiles);
            Add("App folder permissions", CheckAppFolderPermissions);
            Add("Change watch", CheckChangeWatch);
            Add("Crashes", CheckCrashLog);
            Add("Disk space", CheckSpace);
            return checks;
        }

        /// <summary>Daily upkeep: keep a copy of each state file so a damaged one can be put back.</summary>
        public static void Maintain()
        {
            try
            {
                var today = Path.Combine(BackupRoot, DateTime.Today.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(today))
                {
                    Directory.CreateDirectory(today);
                    foreach (var file in StateFiles())
                    {
                        if (TryParse(file)) System.IO.File.Copy(file, Path.Combine(today, BackupName(file)), overwrite: true);
                    }
                }
                foreach (var old in Directory.EnumerateDirectories(BackupRoot).OrderByDescending(d => d).Skip(BackupDays))
                    Directory.Delete(old, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Self-check upkeep failed: {ex.Message}");
            }
        }

        /// <summary>Runs a repair offered by a check. Returns what was done, in plain words.</summary>
        public static string Repair(string repair)
        {
            if (repair == "accept-app-files")
            {
                SaveBaseline(CurrentAppBaseline());
                return "The app files as they are now are recorded as trusted.";
            }
            if (repair.StartsWith("restore:", StringComparison.Ordinal))
            {
                var target = repair["restore:".Length..];
                if (!StateFiles().Contains(target, StringComparer.OrdinalIgnoreCase)) return "That file is not one the app keeps.";
                var backup = Directory.Exists(BackupRoot)
                    ? Directory.EnumerateDirectories(BackupRoot).OrderByDescending(d => d)
                        .Select(d => Path.Combine(d, BackupName(target))).FirstOrDefault(System.IO.File.Exists)
                    : null;
                if (backup == null)
                {
                    var aside = target + ".damaged";
                    System.IO.File.Move(target, aside, overwrite: true);
                    return $"No backup existed, so the damaged file was set aside as {Path.GetFileName(aside)} and the app will start that part fresh.";
                }
                System.IO.File.Copy(target, target + ".damaged", overwrite: true);
                System.IO.File.Copy(backup, target, overwrite: true);
                return $"Restored {Path.GetFileName(target)} from the backup of {Directory.GetParent(backup)!.Name}.";
            }
            return "Unknown repair.";
        }

        private static SelfCheckItem CheckDataFolder()
        {
            Directory.CreateDirectory(GuardianPaths.DataDirectory);
            var probe = Path.Combine(GuardianPaths.DataDirectory, $".write-test-{Environment.ProcessId}");
            System.IO.File.WriteAllText(probe, "ok");
            System.IO.File.Delete(probe);
            return new SelfCheckItem { Name = "Data folder", Status = CheckStatus.Ok, Detail = $"{GuardianPaths.DataDirectory} can be written to." };
        }

        private static SelfCheckItem CheckStateFiles()
        {
            var files = StateFiles().ToList();
            var damaged = files.Where(f => !TryParse(f)).ToList();
            if (damaged.Count == 0)
                return new SelfCheckItem { Name = "Saved settings", Status = CheckStatus.Ok, Detail = $"All {files.Count} saved files read correctly." };

            var first = damaged[0];
            return new SelfCheckItem
            {
                Name = "Saved settings",
                Status = CheckStatus.Problem,
                Detail = $"Damaged: {string.Join(", ", damaged.Select(Path.GetFileName))}.",
                Advice = "A damaged file makes that part of the app forget what it knew. Restore it from the latest daily backup.",
                Repair = "restore:" + first,
                RepairLabel = $"Restore {Path.GetFileName(first)}"
            };
        }

        private static SelfCheckItem CheckAppFiles()
        {
            var current = CurrentAppBaseline();
            if (System.IO.File.Exists(BaselinePath) && !TryParse(BaselinePath))
            {
                // Don't overwrite it: that would trust whatever is installed now and destroy the evidence.
                return new SelfCheckItem
                {
                    Name = "App files",
                    Status = CheckStatus.Problem,
                    Detail = "The record of trusted app files is damaged, so the app files could not be compared.",
                    Advice = "Restore it from the daily backup (see Saved settings), or reinstall the app and then trust the files.",
                    Repair = "accept-app-files",
                    RepairLabel = "Trust the files as they are now"
                };
            }
            var saved = LoadBaseline();
            if (saved == null || saved.Version != current.Version)
            {
                // A new version is a legitimate change: trust it as installed.
                SaveBaseline(current);
                return new SelfCheckItem { Name = "App files", Status = CheckStatus.Ok, Detail = $"Recorded {current.Hashes.Count} files of version {current.Version} as trusted." };
            }

            var changed = current.Hashes.Where(h => !saved.Hashes.TryGetValue(h.Key, out var old) || old != h.Value).Select(h => h.Key)
                .Concat(saved.Hashes.Keys.Except(current.Hashes.Keys)).ToList();
            if (changed.Count == 0)
                return new SelfCheckItem { Name = "App files", Status = CheckStatus.Ok, Detail = $"All {current.Hashes.Count} files match version {current.Version} as installed." };

            return new SelfCheckItem
            {
                Name = "App files",
                Status = CheckStatus.Problem,
                Detail = $"Changed without a version update: {string.Join(", ", changed.Take(5))}{(changed.Count > 5 ? "…" : string.Empty)}.",
                Advice = "If you did not rebuild or repair the app, reinstall it from the official release. Only accept the change if you made it.",
                Repair = "accept-app-files",
                RepairLabel = "I made this change, trust it"
            };
        }

        private static SelfCheckItem CheckAppFolderPermissions()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            var security = folder.GetAccessControl();
            var broad = new[]
            {
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                // A copy unpacked into your own profile is writable by every program you run.
                WindowsIdentity.GetCurrent().User!
            };
            // Modify and FullControl include these bits; checking them directly avoids matching rules that only allow reading.
            const FileSystemRights writeRights = FileSystemRights.WriteData | FileSystemRights.AppendData;
            var writable = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .OfType<FileSystemAccessRule>()
                .Where(r => r.AccessControlType == AccessControlType.Allow && (r.FileSystemRights & writeRights) != 0)
                .Select(r => (SecurityIdentifier)r.IdentityReference)
                .Where(sid => broad.Contains(sid))
                .Select(sid => sid.Translate(typeof(NTAccount)).Value)
                .Distinct()
                .ToList();

            if (writable.Count == 0)
                return new SelfCheckItem { Name = "App folder permissions", Status = CheckStatus.Ok, Detail = $"Ordinary programs can't change {folder.FullName}." };

            return new SelfCheckItem
            {
                Name = "App folder permissions",
                Status = CheckStatus.Warning,
                Detail = $"{string.Join(", ", writable)} can change files in {folder.FullName}.",
                Advice = "Any program running as you could swap the app's files. Install it under Program Files (the installer does this) instead of a personal folder."
            };
        }

        private static SelfCheckItem CheckChangeWatch()
        {
            var last = ChangeWatch.LastRun;
            if (last == null)
                return new SelfCheckItem { Name = "Change watch", Status = CheckStatus.Warning, Detail = "Has not run yet.", Advice = "Open Notices and choose \"Check now\"." };
            var age = DateTime.Now - last.Value;
            if (age > TimeSpan.FromHours(24))
                return new SelfCheckItem
                {
                    Name = "Change watch",
                    Status = CheckStatus.Warning,
                    Detail = $"Last ran {last.Value:g}, more than a day ago.",
                    Advice = "The watch runs while the app is open. Leave the app open (it can stay minimized), or open Notices and choose \"Check now\"."
                };
            return new SelfCheckItem { Name = "Change watch", Status = CheckStatus.Ok, Detail = $"Last ran {last.Value:g}." };
        }

        private static SelfCheckItem CheckCrashLog()
        {
            var log = App.CrashLogPath;
            if (!System.IO.File.Exists(log) || System.IO.File.GetLastWriteTime(log) < DateTime.Now.AddDays(-7))
                return new SelfCheckItem { Name = "Crashes", Status = CheckStatus.Ok, Detail = "No crashes in the last week." };
            return new SelfCheckItem
            {
                Name = "Crashes",
                Status = CheckStatus.Warning,
                Detail = $"The app recorded a crash on {System.IO.File.GetLastWriteTime(log):g}.",
                Advice = $"The details are in {log}. Send that file along when reporting the problem."
            };
        }

        private static SelfCheckItem CheckSpace()
        {
            long used = 0;
            foreach (var root in new[] { GuardianPaths.DataDirectory, GuardianPaths.RoamingDirectory }.Where(Directory.Exists))
                used += Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            var drive = new DriveInfo(Path.GetPathRoot(GuardianPaths.DataDirectory)!);
            var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var usedMb = used / (1024.0 * 1024);
            var status = usedMb > 1024 || freeGb < 2 ? CheckStatus.Warning : CheckStatus.Ok;
            return new SelfCheckItem
            {
                Name = "Disk space",
                Status = status,
                Detail = $"The app's data uses {usedMb:0.#} MB; {freeGb:0.#} GB is free on {drive.Name}.",
                Advice = status == CheckStatus.Ok ? string.Empty : "Clear old reports and quarantined files you no longer need, or use \"Free up space\"."
            };
        }

        private static IEnumerable<string> StateFiles() =>
            new[] { GuardianPaths.DataDirectory, GuardianPaths.RoamingDirectory }
                .Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d, "*.json", SearchOption.TopDirectoryOnly));

        private static string BackupName(string file) =>
            (file.StartsWith(GuardianPaths.RoamingDirectory, StringComparison.OrdinalIgnoreCase) ? "roaming-" : "local-") + Path.GetFileName(file);

        private static bool TryParse(string file)
        {
            try
            {
                using var _ = JsonDocument.Parse(System.IO.File.ReadAllText(file));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static AppBaseline CurrentAppBaseline()
        {
            var root = AppContext.BaseDirectory;
            var files = new[] { "WindowsGuardianWatchdog.dll", "WindowsGuardianWatchdog.exe" }
                .Select(f => Path.Combine(root, f))
                .Concat(Directory.Exists(Path.Combine(root, "Resources")) ? Directory.EnumerateFiles(Path.Combine(root, "Resources")) : Enumerable.Empty<string>())
                .Where(System.IO.File.Exists);
            var hashes = new Dictionary<string, string>();
            foreach (var file in files)
            {
                using var stream = System.IO.File.OpenRead(file);
                hashes[Path.GetRelativePath(root, file)] = Convert.ToHexString(SHA256.HashData(stream));
            }
            // The informational version carries the commit a build came from, so a rebuild from new code is an update,
            // not tampering.
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? assembly.GetName().Version?.ToString() ?? "unknown";
            return new AppBaseline { Version = version, Hashes = hashes };
        }

        private static AppBaseline? LoadBaseline()
        {
            try { return System.IO.File.Exists(BaselinePath) ? JsonSerializer.Deserialize<AppBaseline>(System.IO.File.ReadAllText(BaselinePath)) : null; }
            catch { return null; }
        }

        private static void SaveBaseline(AppBaseline baseline) =>
            GuardianPaths.WriteAtomically(BaselinePath, JsonSerializer.Serialize(baseline, Json));
    }
}
