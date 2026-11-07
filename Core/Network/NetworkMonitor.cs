using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WindowsGuardianWatchdog.Core.Scripting;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Network
{
    public class NetworkMonitor
    {
        public class NetworkStatus
        {
            public bool FirewallEnabled { get; set; }
            public bool NetworkProtectionEnabled { get; set; }
            public List<NetworkInterface> ActiveInterfaces { get; set; } = new();
            public int ActiveConnections { get; set; }
            public bool IsSecure { get; set; }
        }

        public async Task<NetworkStatus> GetNetworkStatusAsync()
        {
            var status = new NetworkStatus();

            // Out of process: the app can't host PowerShell itself (see DefenderControl).
            const string script = """
                $fw = @(Get-NetFirewallProfile -ErrorAction SilentlyContinue)
                $np = (Get-MpPreference -ErrorAction SilentlyContinue).EnableNetworkProtection
                "firewall=" + (($fw.Count -gt 0) -and (@($fw | Where-Object { -not $_.Enabled }).Count -eq 0))
                "network=" + [int]$np
                """;
            var run = await ScriptRunner.RunPowerShellAsync(script, elevate: false, timeoutMs: 60000);
            var (firewall, networkProtection) = ParseStatus(run.Output);
            status.FirewallEnabled = firewall;
            status.NetworkProtectionEnabled = networkProtection;

            await Task.Run(() =>
            {
                try
                {
                    status.ActiveInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .ToList();
                    status.ActiveConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Length;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting network status: {ex.Message}");
                }
            });

            status.IsSecure = status.FirewallEnabled && status.NetworkProtectionEnabled;
            return status;
        }

        /// <summary>Reads "firewall=True" and "network=1" lines. Network protection 1 is block; 2 is audit only.</summary>
        public static (bool Firewall, bool NetworkProtection) ParseStatus(string output)
        {
            bool firewall = false, network = false;
            foreach (var raw in output.Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("firewall=", StringComparison.OrdinalIgnoreCase))
                    firewall = line["firewall=".Length..].Equals("True", StringComparison.OrdinalIgnoreCase);
                else if (line.StartsWith("network=", StringComparison.OrdinalIgnoreCase))
                    network = line["network=".Length..] == "1";
            }
            return (firewall, network);
        }

        public async Task<List<string>> GetActiveConnectionsAsync()
        {
            return await Task.Run(() =>
            {
                var connections = new List<string>();

                try
                {
                    var tcpConnections = IPGlobalProperties.GetIPGlobalProperties()
                        .GetActiveTcpConnections();

                    foreach (var conn in tcpConnections.Take(50)) // Limit to 50 for performance
                    {
                        connections.Add($"{conn.LocalEndPoint} -> {conn.RemoteEndPoint} ({conn.State})");
                    }
                }
                catch
                {
                }

                return connections;
            });
        }
    }
}

