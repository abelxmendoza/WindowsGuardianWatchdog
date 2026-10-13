using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
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
            return await Task.Run(() =>
            {
                var status = new NetworkStatus();

                try
                {
                    // Check firewall status
                    using var ps = PowerShell.Create();
                    ps.AddScript("(Get-NetFirewallProfile | Where-Object {$_.Enabled -eq $false}).Count -eq 0");
                    var firewallResult = ps.Invoke();
                    status.FirewallEnabled = firewallResult != null && 
                                            firewallResult.Count > 0 && 
                                            firewallResult[0].ToString() == "True";

                    // Check network protection (Defender)
                    ps.Commands.Clear();
                    ps.AddScript("(Get-MpPreference).EnableNetworkProtection");
                    var networkProtectionResult = ps.Invoke();
                    if (networkProtectionResult != null && networkProtectionResult.Count > 0)
                    {
                        var value = networkProtectionResult[0].ToString();
                        status.NetworkProtectionEnabled = value == "1" || value == "Enabled";
                    }

                    // Get active network interfaces
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .ToList();
                    status.ActiveInterfaces = interfaces;

                    // Get active TCP connections
                    var tcpConnections = IPGlobalProperties.GetIPGlobalProperties()
                        .GetActiveTcpConnections();
                    status.ActiveConnections = tcpConnections.Length;

                    status.IsSecure = status.FirewallEnabled && status.NetworkProtectionEnabled;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting network status: {ex.Message}");
                }

                return status;
            });
        }

        public async Task<bool> EnableNetworkProtectionAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    var value = enabled ? "Enabled" : "Disabled";
                    ps.AddScript($"Set-MpPreference -EnableNetworkProtection {value}");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
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

