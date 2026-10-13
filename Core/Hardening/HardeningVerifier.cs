using System;
using System.Threading.Tasks;
using System.Management.Automation;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public static class HardeningVerifier
    {
        public static async Task<bool> VerifyDefenderBaselineAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("(Get-MpPreference).DisableRealtimeMonitoring");
                    var result = ps.Invoke();
                    return result != null && result.Count > 0 && result[0].ToString() == "False";
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> VerifyPUAProtectionAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("(Get-MpPreference).PUAProtection -eq 'Enabled'");
                    var result = ps.Invoke();
                    return result != null && result.Count > 0 && result[0].ToString().Contains("Enabled");
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> VerifyControlledFolderAccessAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("(Get-MpPreference).EnableControlledFolderAccess -eq 'Enabled'");
                    var result = ps.Invoke();
                    return result != null && result.Count > 0 && result[0].ToString().Contains("Enabled");
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> VerifyASREnabledAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("(Get-MpPreference).AttackSurfaceReductionRules_Ids");
                    var result = ps.Invoke();
                    return result != null && result.Count > 0;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> VerifyFirewallEnabledAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("(Get-NetFirewallProfile -Profile Domain,Public,Private).Enabled -contains 'True'");
                    var result = ps.Invoke();
                    return result != null && result.Count > 0;
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<string> GetDefenderStatusAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript("Get-MpComputerStatus | Select-Object -Property AntivirusEnabled,AntispywareEnabled,AMServiceEnabled");
                    var result = ps.Invoke();
                    return result != null && result.Count > 0 ? string.Join(",", result.Select(r => r.ToString())) : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            });
        }
    }
}


