using WindowsGuardianWatchdog.Core.Defender;
using WindowsGuardianWatchdog.Core.Network;
using Xunit;

namespace WindowsGuardianWatchdog.Tests
{
    public class DefenderParsingTests
    {
        [Fact]
        public void Status_is_read_from_powershell_json()
        {
            const string json = """{"RealTime":true,"Antivirus":true,"Behavior":true,"Tamper":true,"Maps":2,"Pua":1,"Cfa":0,"Mode":"Normal","QuickScan":"2026-10-14T18:55:47.0000000Z","FullScan":null,"SignatureUpdated":"2026-11-06T22:49:36.0000000Z","SignatureAge":0,"SignatureVersion":"1.441.12.0"}""";
            var status = DefenderControl.ParseStatus("some warning on stderr\n" + json);

            Assert.True(status.Available);
            Assert.True(status.RealTimeProtectionEnabled);
            Assert.True(status.CloudProtectionEnabled);
            Assert.Equal("Advanced", status.MAPSReporting);
            Assert.True(status.PUAProtectionEnabled);
            Assert.False(status.ControlledFolderAccessEnabled);
            Assert.True(status.IsHealthy);
            Assert.False(status.SignaturesOutOfDate);
            Assert.Equal(new DateTime(2026, 10, 14, 18, 55, 47, DateTimeKind.Utc), status.LastQuickScan);
            Assert.Null(status.LastFullScan);
            Assert.Equal("1.441.12.0", status.AntivirusSignatureVersion);
        }

        [Fact]
        public void Audit_modes_do_not_count_as_protection()
        {
            var status = DefenderControl.ParseStatus("""{"RealTime":true,"Antivirus":true,"Maps":0,"Pua":2,"Cfa":2,"SignatureAge":9}""");
            Assert.False(status.CloudProtectionEnabled);
            Assert.False(status.PUAProtectionEnabled);
            Assert.False(status.ControlledFolderAccessEnabled);
            Assert.True(status.SignaturesOutOfDate);
            Assert.False(status.IsHealthy);
        }

        [Theory]
        [InlineData("""{"Error":"Get-MpComputerStatus : The service is not running"}""", "The service is not running")]
        [InlineData("", "Windows did not answer.")]
        [InlineData("powershell.exe could not be started", "powershell.exe could not be started")]
        public void Unreadable_status_is_unknown_not_off(string output, string problem)
        {
            var status = DefenderControl.ParseStatus(output);
            Assert.False(status.Available);
            Assert.Contains(problem, status.Problem);
            Assert.False(status.IsHealthy);
            Assert.False(status.SignaturesOutOfDate);
        }

        [Fact]
        public void Threats_are_read_from_an_array()
        {
            const string json = """[{"Name":"Virus:DOS/EICAR_Test_File","Severity":5,"Detected":"2026-11-07T08:30:00.0000000Z","Resources":"file:_C:\\Users\\me\\eicar.txt","Status":3},{"Name":"Trojan:Win32/Wacatac","Severity":4,"Detected":null,"Resources":"","Status":1}]""";
            var threats = DefenderControl.ParseThreats(json);

            Assert.Equal(2, threats.Count);
            Assert.Equal("Severe", threats[0].Severity);
            Assert.Equal("Quarantined by Defender", threats[0].Status);
            Assert.False(threats[0].NeedsAction);
            Assert.True(threats[1].NeedsAction);
            Assert.Equal("Unknown time", threats[1].WhenText);
        }

        [Fact]
        public void A_single_threat_object_is_a_list_of_one()
        {
            var threats = DefenderControl.ParseThreats("""{"Name":"X","Severity":1,"Status":2}""");
            Assert.Single(threats);
            Assert.Equal("Cleaned", threats[0].Status);
        }

        [Fact]
        public void No_threats_is_an_empty_list()
        {
            Assert.Empty(DefenderControl.ParseThreats("[]"));
            Assert.Empty(DefenderControl.ParseThreats(""));
        }

        [Fact]
        public void A_threat_list_error_is_reported()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => DefenderControl.ParseThreats("""{"Error":"Access denied"}"""));
            Assert.Contains("Access denied", ex.Message);
        }

        [Theory]
        [InlineData("firewall=True\r\nnetwork=1", true, true)]
        [InlineData("firewall=False\nnetwork=2", false, false)]
        [InlineData("", false, false)]
        public void Network_status_lines(string output, bool firewall, bool network)
        {
            var (fw, np) = NetworkMonitor.ParseStatus(output);
            Assert.Equal(firewall, fw);
            Assert.Equal(network, np);
        }
    }

    [Trait("Category", "Device")]
    public class DefenderDeviceTests
    {
        [Fact]
        public async Task Defender_status_comes_back_from_this_pc()
        {
            var status = await new DefenderControl().GetStatusAsync();
            Assert.True(status.Available, "Defender did not answer: " + status.Problem);
            Assert.NotEqual("Unknown", status.AntivirusSignatureVersion);
        }

        [Fact]
        public async Task Defender_threat_history_can_be_listed()
        {
            var threats = await new DefenderControl().GetThreatsAsync();
            Assert.NotNull(threats);
        }

        [Fact]
        public async Task A_clean_file_scans_clean()
        {
            var folder = TestData.NewFolder();
            var file = Path.Combine(folder, "clean.txt");
            File.WriteAllText(file, "nothing to see here");
            var result = await new DefenderControl().ScanPathAsync(file);
            Assert.True(result.Success, result.Message);
            Directory.Delete(folder, true);
        }

        [Fact]
        public async Task A_missing_path_is_refused_without_running_a_scan()
        {
            var result = await new DefenderControl().ScanPathAsync(@"C:\does\not\exist\" + Guid.NewGuid());
            Assert.False(result.Success);
        }

        [Fact]
        public async Task Network_status_reads_the_firewall()
        {
            var status = await new NetworkMonitor().GetNetworkStatusAsync();
            Assert.True(status.ActiveConnections >= 0);
        }
    }
}
