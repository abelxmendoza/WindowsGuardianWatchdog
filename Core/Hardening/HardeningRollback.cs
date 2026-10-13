using System.Management.Automation;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public static class HardeningRollback
    {
        public static async Task RollbackDefenderRealtimeAsync()
        {
            await RunPowerShellAsync("Set-MpPreference -DisableRealtimeMonitoring $true");
        }

        public static async Task RollbackPUAProtectionAsync()
        {
            await RunPowerShellAsync("Set-MpPreference -PUAProtection Disabled");
        }

        public static async Task RollbackControlledFolderAccessAsync()
        {
            await RunPowerShellAsync("Set-MpPreference -EnableControlledFolderAccess Disabled");
        }

        public static async Task RollbackASRAsync()
        {
            // Remove ASR rules
            await RunPowerShellAsync("Set-MpPreference -AttackSurfaceReductionRules_Ids @()");
        }

        public static async Task RollbackFirewallAsync()
        {
            await RunPowerShellAsync("Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False");
        }

        private static Task RunPowerShellAsync(string script)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript(script);
                    ps.Invoke();
                }
                catch
                {
                    // Silently fail - rollback might not be critical
                }
            });
        }
    }
}


