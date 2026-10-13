using System.Management.Automation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public static class HardeningRuleFactory
    {
        public static List<HardeningRule> CreateDefaultRules()
        {
            return new List<HardeningRule>
            {
                new HardeningRule
                {
                    Id = "DEFENDER_REALTIME",
                    Name = "Enable Defender Real-Time Protection",
                    Description = "Ensures Windows Defender real-time monitoring is enabled",
                    Category = "Defender",
                    Severity = HardeningSeverity.Critical,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync("Set-MpPreference -DisableRealtimeMonitoring $false"),
                    VerifyAsync = async () =>
                    {
                        using var ps = PowerShell.Create();
                        ps.AddScript("(Get-MpPreference).DisableRealtimeMonitoring");
                        var result = ps.Invoke();
                        return result != null && result.Count > 0 && result[0].ToString() == "False";
                    },
                    RollbackAsync = () => HardeningRollback.RollbackDefenderRealtimeAsync()
                },
                new HardeningRule
                {
                    Id = "DEFENDER_PUA",
                    Name = "Enable PUA Protection",
                    Description = "Enables Potentially Unwanted Application (PUA) protection",
                    Category = "Defender",
                    Severity = HardeningSeverity.High,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync("Set-MpPreference -PUAProtection Enabled"),
                    VerifyAsync = async () => await HardeningVerifier.VerifyPUAProtectionAsync(),
                    RollbackAsync = () => HardeningRollback.RollbackPUAProtectionAsync()
                },
                new HardeningRule
                {
                    Id = "DEFENDER_CONTROLLED_FOLDER",
                    Name = "Enable Controlled Folder Access",
                    Description = "Protects important folders from unauthorized changes",
                    Category = "Defender",
                    Severity = HardeningSeverity.High,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync("Set-MpPreference -EnableControlledFolderAccess Enabled"),
                    VerifyAsync = async () => await HardeningVerifier.VerifyControlledFolderAccessAsync(),
                    RollbackAsync = () => HardeningRollback.RollbackControlledFolderAccessAsync()
                },
                new HardeningRule
                {
                    Id = "DEFENDER_ASR",
                    Name = "Enable Attack Surface Reduction",
                    Description = "Enables ASR rules to prevent common attack vectors",
                    Category = "Defender",
                    Severity = HardeningSeverity.Critical,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync(@"
                        $rules = @(
                            'D4F940AB-401B-4EFC-AADC-AD5F3C50688A',  # Block executable content from email
                            'BE9BA2D9-53EA-4CDC-84E5-9B1EEEE46550',  # Block Office apps from creating child processes
                            '3B576869-A4EC-4529-8536-B80A7769E899',  # Block Office apps from injecting code
                            '75668C1F-73B5-4CF0-BB93-3ECF5CB7CC84',  # Block Office apps from creating executable content
                            'D3E037E1-3EB8-44C8-A917-57927947596D',  # Block JavaScript/VBScript from launching downloaded executable content
                            '5BEB7EFE-FD9A-4556-801D-275E5FFC04CC',  # Block execution of potentially obfuscated scripts
                            '92E97FA1-2EDF-4476-BDD6-9DD0B4DDDC7B',  # Block Win32 API calls from Office macros
                            '01443614-CD74-433A-B99E-2ECDC07BFC25',  # Block executable files from running unless they meet a prevalence, age, or trusted list criterion
                            'C1DB55AB-C21A-4637-BB3F-A12569109B35',  # Block persistence through WMI event subscription
                            '9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2'   # Block credential stealing from Windows local security authority subsystem
                        )
                        Set-MpPreference -AttackSurfaceReductionRules_Ids $rules
                        Set-MpPreference -AttackSurfaceReductionRules_Actions Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled
                    "),
                    VerifyAsync = async () => await HardeningVerifier.VerifyASREnabledAsync(),
                    RollbackAsync = () => HardeningRollback.RollbackASRAsync()
                },
                new HardeningRule
                {
                    Id = "FIREWALL_ENABLE",
                    Name = "Enable Windows Firewall",
                    Description = "Ensures Windows Firewall is enabled for all profiles",
                    Category = "Firewall",
                    Severity = HardeningSeverity.Critical,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync("Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True"),
                    VerifyAsync = async () => await HardeningVerifier.VerifyFirewallEnabledAsync(),
                    RollbackAsync = () => HardeningRollback.RollbackFirewallAsync()
                },
                new HardeningRule
                {
                    Id = "UAC_ENABLE",
                    Name = "Enable UAC",
                    Description = "Ensures User Account Control is enabled",
                    Category = "System",
                    Severity = HardeningSeverity.High,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync("Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name EnableLUA -Value 1"),
                    VerifyAsync = async () =>
                    {
                        return await Task.Run(() =>
                        {
                            try
                            {
                                var value = Microsoft.Win32.Registry.GetValue(
                                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                                    "EnableLUA",
                                    null);
                                return value != null && value.ToString() == "1";
                            }
                            catch
                            {
                                return false;
                            }
                        });
                    },
                    RollbackAsync = () => RunPowerShellAsync("Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name EnableLUA -Value 0")
                },
                new HardeningRule
                {
                    Id = "SMARTScreen_ENABLE",
                    Name = "Enable SmartScreen",
                    Description = "Enables Windows SmartScreen for app and browser protection",
                    Category = "System",
                    Severity = HardeningSeverity.Medium,
                    RequiresAdmin = true,
                    ApplyAsync = () => RunPowerShellAsync(@"
                        Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer' -Name SmartScreenEnabled -Value 'RequireAdmin'
                        Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\System' -Name EnableSmartScreen -Value 1
                    "),
                    VerifyAsync = async () =>
                    {
                        return await Task.Run(() =>
                        {
                            try
                            {
                                var value = Microsoft.Win32.Registry.GetValue(
                                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                                    "SmartScreenEnabled",
                                    null);
                                return value != null && value.ToString() != "Off";
                            }
                            catch
                            {
                                return false;
                            }
                        });
                    },
                    RollbackAsync = () => RunPowerShellAsync("Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer' -Name SmartScreenEnabled -Value 'Off'")
                }
            };
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
                    // Error handling is done at the engine level
                }
            });
        }
    }
}


