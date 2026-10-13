using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Defender
{
    public class DefenderControl
    {
        public class DefenderStatus
        {
            public bool RealTimeProtectionEnabled { get; set; }
            public bool AntivirusEnabled { get; set; }
            public bool CloudProtectionEnabled { get; set; }
            public bool BehaviorMonitoringEnabled { get; set; }
            public bool PUAProtectionEnabled { get; set; }
            public bool ControlledFolderAccessEnabled { get; set; }
            public string MAPSReporting { get; set; } = "Unknown";
            public DateTime LastQuickScan { get; set; }
            public DateTime LastFullScan { get; set; }
            public DateTime LastUpdate { get; set; }
            public string AntivirusSignatureVersion { get; set; } = "Unknown";
            public bool IsHealthy { get; set; }
        }

        public async Task<DefenderStatus> GetStatusAsync()
        {
            return await Task.Run(() =>
            {
                var status = new DefenderStatus();

                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript(@"
                        $status = Get-MpComputerStatus
                        $prefs = Get-MpPreference
                        [PSCustomObject]@{
                            RealTimeProtectionEnabled = $status.RealTimeProtectionEnabled
                            AntivirusEnabled = $status.AntivirusEnabled
                            CloudProtectionEnabled = $prefs.MAPSReporting -ne 'Disabled'
                            BehaviorMonitoringEnabled = -not $prefs.DisableBehaviorMonitoring
                            PUAProtectionEnabled = $prefs.PUAProtection -eq 1
                            ControlledFolderAccessEnabled = $prefs.EnableControlledFolderAccess -eq 1
                            MAPSReporting = $prefs.MAPSReporting
                            LastQuickScan = $status.QuickScanStartTime
                            LastFullScan = $status.FullScanStartTime
                            LastUpdate = $status.AntivirusSignatureLastUpdated
                            AntivirusSignatureVersion = $status.AntivirusSignatureVersion
                        }
                    ");

                    var result = ps.Invoke();
                    if (result != null && result.Count > 0)
                    {
                        var obj = result[0];
                        status.RealTimeProtectionEnabled = (bool)(obj.Properties["RealTimeProtectionEnabled"]?.Value ?? false);
                        status.AntivirusEnabled = (bool)(obj.Properties["AntivirusEnabled"]?.Value ?? false);
                        status.CloudProtectionEnabled = (bool)(obj.Properties["CloudProtectionEnabled"]?.Value ?? false);
                        status.BehaviorMonitoringEnabled = (bool)(obj.Properties["BehaviorMonitoringEnabled"]?.Value ?? false);
                        status.PUAProtectionEnabled = (bool)(obj.Properties["PUAProtectionEnabled"]?.Value ?? false);
                        status.ControlledFolderAccessEnabled = (bool)(obj.Properties["ControlledFolderAccessEnabled"]?.Value ?? false);
                        status.MAPSReporting = obj.Properties["MAPSReporting"]?.Value?.ToString() ?? "Unknown";
                        
                        if (obj.Properties["LastQuickScan"]?.Value != null)
                            status.LastQuickScan = (DateTime)obj.Properties["LastQuickScan"].Value;
                        if (obj.Properties["LastFullScan"]?.Value != null)
                            status.LastFullScan = (DateTime)obj.Properties["LastFullScan"].Value;
                        if (obj.Properties["LastUpdate"]?.Value != null)
                            status.LastUpdate = (DateTime)obj.Properties["LastUpdate"].Value;
                        
                        status.AntivirusSignatureVersion = obj.Properties["AntivirusSignatureVersion"]?.Value?.ToString() ?? "Unknown";
                    }

                    status.IsHealthy = status.RealTimeProtectionEnabled && 
                                     status.AntivirusEnabled && 
                                     status.CloudProtectionEnabled;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting Defender status: {ex.Message}");
                }

                return status;
            });
        }

        public async Task<bool> SetRealTimeProtectionAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript($"Set-MpPreference -DisableRealtimeMonitoring ${!enabled}");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> SetCloudProtectionAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    var level = enabled ? "Advanced" : "Disabled";
                    ps.AddScript($"Set-MpPreference -MAPSReporting {level}");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> SetBehaviorMonitoringAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript($"Set-MpPreference -DisableBehaviorMonitoring ${!enabled}");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> SetPUAProtectionAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    var value = enabled ? "Enabled" : "Disabled";
                    ps.AddScript($"Set-MpPreference -PUAProtection {value}");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> SetControlledFolderAccessAsync(bool enabled)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    var value = enabled ? "Enabled" : "Disabled";
                    ps.AddScript($"Set-MpPreference -EnableControlledFolderAccess {value}");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> StartQuickScanAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("Start-MpScan -ScanType QuickScan");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> StartFullScanAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("Start-MpScan -ScanType FullScan");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> UpdateSignaturesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("Update-MpSignature");
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}

