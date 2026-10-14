using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public static class HardeningVerifier
    {
        public static Task<bool> VerifyDefenderBaselineAsync() =>
            CheckAsync("if ((Get-MpPreference).DisableRealtimeMonitoring -eq $false) { 'True' } else { 'False' }");

        public static Task<bool> VerifyPUAProtectionAsync() =>
            CheckAsync("$v = [string](Get-MpPreference).PUAProtection; if ($v -eq '1' -or $v -eq 'Enabled') { 'True' } else { 'False' }");

        public static Task<bool> VerifyControlledFolderAccessAsync() =>
            CheckAsync("$v = [string](Get-MpPreference).EnableControlledFolderAccess; if ($v -eq '1' -or $v -eq 'Enabled') { 'True' } else { 'False' }");

        public static Task<bool> VerifyASREnabledAsync() =>
            CheckAsync("$actions = @((Get-MpPreference).AttackSurfaceReductionRules_Actions); if (@($actions | Where-Object { $_ -eq 1 -or $_ -eq 'Enabled' -or $_ -eq 'Block' }).Count -gt 0) { 'True' } else { 'False' }");

        public static Task<bool> VerifyFirewallEnabledAsync() =>
            CheckAsync("if (@(Get-NetFirewallProfile -Profile Domain,Public,Private | Where-Object { $_.Enabled -ne $true }).Count -eq 0) { 'True' } else { 'False' }");

        public static async Task<string> GetDefenderStatusAsync()
        {
            var result = await ScriptRunner.RunPowerShellAsync(
                """
                $status = Get-MpComputerStatus
                "Antivirus: $($status.AntivirusEnabled) · Real-time: $($status.RealTimeProtectionEnabled) · Cloud: $($status.IoavProtectionEnabled) · Signatures: $($status.AntivirusSignatureLastUpdated)"
                """,
                elevate: false,
                timeoutMs: 60000);
            return string.IsNullOrWhiteSpace(result.Output) ? "Defender status is unavailable." : result.Output.Trim();
        }

        private static async Task<bool> CheckAsync(string script)
        {
            var result = await ScriptRunner.RunPowerShellAsync(script, elevate: false, timeoutMs: 60000);
            return result.Output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase)
                || result.Output.Contains("\nTrue", StringComparison.OrdinalIgnoreCase);
        }
    }
}
