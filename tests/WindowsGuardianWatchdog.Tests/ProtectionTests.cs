using WindowsGuardianWatchdog.Core;
using WindowsGuardianWatchdog.Core.Drivers;
using WindowsGuardianWatchdog.Core.Incidents;
using WindowsGuardianWatchdog.Core.Quarantine;
using WindowsGuardianWatchdog.Core.Sensors;
using WindowsGuardianWatchdog.Core.ThreatIntel;
using WindowsGuardianWatchdog.Core.Updates;
using Xunit;

namespace WindowsGuardianWatchdog.Tests
{
    public class QuarantineTests
    {
        [Fact]
        public void Quarantine_then_restore_puts_back_the_same_bytes()
        {
            var folder = TestData.NewFolder();
            var file = Path.Combine(folder, "suspicious.exe");
            File.WriteAllBytes(file, new byte[] { 1, 2, 3, 4, 5 });

            var entry = QuarantineVault.QuarantineFile(file, "test");
            Assert.False(File.Exists(file));
            Assert.Contains(QuarantineVault.List(), e => e.Id == entry.Id);
            var stored = Directory.GetFiles(Path.Combine(QuarantineVault.VaultDirectory, entry.Id), "*.quarantined").Single();
            Assert.True(File.GetAttributes(stored).HasFlag(FileAttributes.ReadOnly));

            QuarantineVault.Restore(entry.Id);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, File.ReadAllBytes(file));
            Assert.DoesNotContain(QuarantineVault.List(), e => e.Id == entry.Id);
            Directory.Delete(folder, true);
        }

        [Fact]
        public void Restore_never_overwrites_a_new_file()
        {
            var folder = TestData.NewFolder();
            var file = Path.Combine(folder, "a.txt");
            File.WriteAllText(file, "original");
            var entry = QuarantineVault.QuarantineFile(file, "test");
            File.WriteAllText(file, "new");

            Assert.Throws<QuarantineException>(() => QuarantineVault.Restore(entry.Id));
            Assert.Equal("new", File.ReadAllText(file));
            QuarantineVault.Delete(entry.Id);
            Assert.DoesNotContain(QuarantineVault.List(), e => e.Id == entry.Id);
            Directory.Delete(folder, true);
        }

        [Fact]
        public void A_file_in_use_stays_where_it_is()
        {
            var folder = TestData.NewFolder();
            var file = Path.Combine(folder, "busy.exe");
            File.WriteAllText(file, "x");
            var before = QuarantineVault.List().Count;
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<QuarantineException>(() => QuarantineVault.QuarantineFile(file, "test"));
            }
            Assert.True(File.Exists(file));
            Assert.Equal(before, QuarantineVault.List().Count);
            Directory.Delete(folder, true);
        }

        [Fact]
        public void Windows_files_are_refused()
        {
            var notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "notepad.exe");
            if (!File.Exists(notepad)) return;
            Assert.Throws<QuarantineException>(() => QuarantineVault.QuarantineFile(notepad, "test"));
        }

        [Theory]
        [InlineData("../../etc")]
        [InlineData("abc")]
        public void Bad_ids_are_rejected(string id)
        {
            Assert.Throws<QuarantineException>(() => QuarantineVault.Delete(id));
        }
    }

    public class ThreatCheckTests
    {
        [Theory]
        [InlineData(@"""C:\Users\me\AppData\Local\Temp\x\run.exe"" /q", true)]
        [InlineData(@"C:\Users\me\Downloads\setup.exe", true)]
        [InlineData(@"%TEMP%\a.exe", true)]
        [InlineData(@"""C:\Program Files\App\app.exe"" --minimized", false)]
        [InlineData("", false)]
        public void Startup_entries_from_temp_or_downloads_are_flagged(string command, bool flagged)
        {
            Assert.Equal(flagged, BlueTeamDetector.IsTempOrDownloads(command));
        }

        [Fact]
        public void Program_files_and_windows_are_not_user_writable()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Assert.False(BlueTeamDetector.IsUserWritableLocation(Path.Combine(programFiles, "App", "app.exe")));
            Assert.False(BlueTeamDetector.IsUserWritableLocation(Path.Combine(windows, "System32", "svchost.exe")));
            Assert.True(BlueTeamDetector.IsUserWritableLocation(Path.Combine(profile, "Downloads", "thing.exe")));
            Assert.True(BlueTeamDetector.IsUserWritableLocation(Path.Combine(Path.GetTempPath(), "x.exe")));
        }

        [Fact]
        [Trait("Category", "Device")]
        public async Task The_threat_check_finishes_and_lists_everything_it_counts()
        {
            var detector = new BlueTeamDetector();
            var raised = new List<ThreatDetection>();
            detector.ThreatDetected += (_, t) => raised.Add(t);
            var report = await detector.PerformThreatHuntAsync().WaitAsync(TimeSpan.FromMinutes(4));
            Assert.Equal(report.ThreatCount, raised.Count);
            Assert.DoesNotContain(report.Threats, t => t.Description.StartsWith("Could not read Microsoft Defender"));
        }
    }

    public class VulnerabilityCatalogTests
    {
        [Theory]
        [InlineData("VLC Media Player 2.2.1 - Buffer Overflow", "3.0.21", false)]
        [InlineData("VLC Media Player 3.0.11 - Heap Overflow", "3.0.11", true)]
        [InlineData("VLC Media Player 3.0 - Crash", "3.0.21", true)]
        [InlineData("7-Zip < 24.07 - Remote Code Execution", "23.01", true)]
        [InlineData("7-Zip < 24.07 - Remote Code Execution", "24.08", false)]
        [InlineData("Notepad++ 8.4.6 - DLL Hijack", "", null)]
        [InlineData("Some App - Denial of Service", "1.0", null)]
        [InlineData("Foo 1.0 / 5.0 - XSS", "2.0", null)]
        public void Version_check(string title, string installed, bool? affected)
        {
            Assert.Equal(affected, ExploitDbCatalog.IsProbablyAffected(title, installed));
        }

        [Fact]
        [Trait("Category", "Device")]
        public async Task The_public_catalog_downloads_and_matches_this_pc()
        {
            var count = await ExploitDbCatalog.RefreshAsync(force: false);
            Assert.True(count > 40000, $"Only {count} entries");
            var matches = ExploitDbCatalog.MatchInstalledSoftware();
            Assert.All(matches, m => Assert.False(string.IsNullOrEmpty(m.MatchedProduct)));
            Assert.NotEmpty(ExploitDbCatalog.Search("CVE-2021-44228"));
        }
    }

    public class ChangeWatchTests
    {
        [Fact]
        [Trait("Category", "Device")]
        public void Change_watch_reads_every_group_on_this_pc()
        {
            var items = ChangeWatch.Collect(out var unreadable);
            Assert.True(unreadable.Count == 0, "Could not read: " + string.Join("; ", unreadable.Select(g => $"{g}: {(ChangeWatch.LastErrors.TryGetValue(g, out var e) ? e : "?")}")));
            foreach (var group in new[] { "firewall", "defender", "uac", "admins", "accounts", "hosts", "accessibility" })
                Assert.Contains(items, i => i.Group == group);
            Assert.DoesNotContain(items, i => i.Group == "accessibility" && i.Value == "swapped");
        }

        [Fact]
        [Trait("Category", "Device")]
        public void A_second_look_with_nothing_changed_reports_nothing()
        {
            ChangeWatch.Reset();
            var first = ChangeWatch.Run();
            Assert.True(first.FirstLook);
            var second = ChangeWatch.Run();
            Assert.False(second.FirstLook);
            Assert.DoesNotContain(second.Changes, c => c.Severity == NoticeSeverity.Critical);
        }
    }

    public class RemediationTests
    {
        [Fact]
        public async Task A_preview_is_shown_and_changes_nothing()
        {
            var engine = new RemediationEngine();
            RemediationResult? shown = null;
            engine.RemediationCompleted += (_, r) => shown = r;
            var result = await engine.RemediateAsync(new RemediationAction { Type = RemediationType.EnableFirewall }, dryRun: true);
            Assert.Same(result, shown);
            Assert.Contains("Nothing was changed", result.Message);
        }

        [Fact]
        public async Task A_failure_is_shown_too()
        {
            var engine = new RemediationEngine();
            RemediationResult? shown = null;
            engine.RemediationCompleted += (_, r) => shown = r;
            await engine.RemediateAsync(new RemediationAction { Type = RemediationType.QuarantineFile, Target = @"C:\no\such\file.exe" }, dryRun: false);
            Assert.NotNull(shown);
            Assert.False(shown!.Success);
        }
    }

    public class UpdatesAndDriversTests
    {
        [Fact]
        public void Driver_problem_codes_are_explained()
        {
            Assert.Equal("Working", DriverScanner.Describe(0, "OK", isSigned: true));
            Assert.Equal("Driver missing", DriverScanner.Describe(28, "Error", isSigned: true));
            Assert.Equal("Unsigned", DriverScanner.Describe(0, "OK", isSigned: false));
        }

        [Fact]
        public void Windows_update_errors_are_explained()
        {
            Assert.Contains("could not be reached", DriverUpdateChecker.ExplainUpdateError("0x8024402C"));
        }

        [Fact]
        public void Driver_updates_match_by_maker_and_model()
        {
            var driver = new DriverInfo { DeviceName = "Intel(R) Wi-Fi 6E AX211 160MHz", DriverProviderName = "Intel" };
            Assert.True(DriverUpdateChecker.Matches(driver, new[] { "Intel Corporation - Net - 23.60.1.2" }));
            Assert.False(DriverUpdateChecker.Matches(driver, new[] { "Realtek Semiconductor Corp. - Extension - 10.0.1" }));
        }

        [Fact]
        public void Winget_upgrade_table_is_parsed()
        {
            const string output = """
                Name                       Id                         Version      Available    Source
                -----------------------------------------------------------------------------------------
                Mozilla Firefox (x64 en-US) Mozilla.Firefox           131.0        132.0.1      winget
                7-Zip 23.01 (x64)          7zip.7zip                  23.01        24.08        winget
                2 upgrades available.
                """;
            var updates = AppUpdateService.ParseWingetUpgrades(output);
            Assert.Contains(updates, u => u.Id == "7zip.7zip" && u.AvailableVersion == "24.08");
        }

        [Fact]
        [Trait("Category", "Device")]
        public void Hardware_scan_finds_this_pcs_devices()
        {
            var drivers = new DriverScanner().Scan();
            Assert.NotEmpty(drivers);
        }
    }
}
