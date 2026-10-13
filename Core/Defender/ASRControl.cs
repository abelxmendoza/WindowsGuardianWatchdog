using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace WindowsGuardianWatchdog.Core.Defender
{
    public class ASRControl
    {
        public class ASRRule
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public bool Enabled { get; set; }
            public string Action { get; set; } = "NotConfigured";
        }

        // Common ASR rule IDs
        public static readonly Dictionary<string, string> ASRRuleNames = new()
        {
            { "BE9BA2D9-53EA-4CDC-84E5-9B1EEEE46550", "Block Office apps from creating child processes" },
            { "D4F940AB-401B-4EFC-AADC-AD5F3C50688A", "Block executable content from email client and webmail" },
            { "3B576869-A4EC-4529-8536-B80A7769E899", "Block Office apps from injecting code into other processes" },
            { "75668C1F-73B5-4CF0-BB93-3ECF5CB7CC84", "Block Office apps from creating executable content" },
            { "D3E037E1-3EB8-44C8-A917-57927947596D", "Block JavaScript/VBScript from launching downloaded executable content" },
            { "5BEB7EFE-FD9A-4556-801D-275E5FFC04CC", "Block execution of potentially obfuscated scripts" },
            { "92E97FA1-2EDF-4476-BDD6-9DD0B4DDDC7B", "Block Win32 API calls from Office macros" },
            { "01443614-CD74-433A-B99E-2ECDC07BFC25", "Block executable files from running unless they meet a prevalence, age, or trusted list criterion" },
            { "C1DB55AB-C21A-4637-BB3F-A12569109B35", "Block persistence through WMI event subscription" },
            { "9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2", "Block credential stealing from the Windows local security authority subsystem" },
            { "B2B3F03D-6A65-4F7B-A9C7-1C7EF74A9CE4", "Block untrusted and unsigned processes that run from USB" },
            { "7674BA52-37EB-4A4F-A9A1-F29FADB5BEAB", "Block Adobe Reader from creating child processes" },
            { "26190899-1603-49E8-8B27-EB1D0A1CE467", "Block process creations originating from PSExec and WMI commands" },
            { "E6DB77E5-3DF2-4CF1-B95A-636979351E5B", "Block process creations originating from PSExec and WMI commands" }
        };

        public async Task<List<ASRRule>> GetASRRulesAsync()
        {
            return await Task.Run(() =>
            {
                var rules = new List<ASRRule>();

                try
                {
                    using var ps = PowerShell.Create();
                    ps.AddScript(@"
                        $prefs = Get-MpPreference
                        $ruleIds = $prefs.AttackSurfaceReductionRules_Ids
                        $ruleActions = $prefs.AttackSurfaceReductionRules_Actions
                        
                        for ($i = 0; $i -lt $ruleIds.Count; $i++) {
                            [PSCustomObject]@{
                                Id = $ruleIds[$i]
                                Action = $ruleActions[$i]
                            }
                        }
                    ");

                    var result = ps.Invoke();
                    if (result != null)
                    {
                        foreach (var item in result)
                        {
                            var ruleId = item.Properties["Id"]?.Value?.ToString() ?? "";
                            var action = item.Properties["Action"]?.Value?.ToString() ?? "NotConfigured";
                            
                            rules.Add(new ASRRule
                            {
                                Id = ruleId,
                                Name = ASRRuleNames.ContainsKey(ruleId) ? ASRRuleNames[ruleId] : "Unknown Rule",
                                Description = GetRuleDescription(ruleId),
                                Enabled = action == "Enabled" || action == "Audit",
                                Action = action
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting ASR rules: {ex.Message}");
                }

                return rules;
            });
        }

        public async Task<bool> SetASRRuleAsync(string ruleId, bool enabled, string action = "Enabled")
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var ps = PowerShell.Create();
                    
                    // Get current rules
                    ps.AddScript("$prefs = Get-MpPreference; $prefs.AttackSurfaceReductionRules_Ids");
                    var currentIds = ps.Invoke().Select(r => r.ToString()).ToList();
                    
                    ps.Commands.Clear();
                    
                    // Get current actions
                    ps.AddScript("$prefs = Get-MpPreference; $prefs.AttackSurfaceReductionRules_Actions");
                    var currentActions = ps.Invoke().Select(r => r.ToString()).ToList();
                    
                    ps.Commands.Clear();

                    // Update or add rule
                    if (currentIds.Contains(ruleId))
                    {
                        var index = currentIds.IndexOf(ruleId);
                        currentActions[index] = enabled ? action : "Disabled";
                    }
                    else if (enabled)
                    {
                        currentIds.Add(ruleId);
                        currentActions.Add(action);
                    }

                    // Apply changes
                    var idsString = string.Join(",", currentIds.Select(id => $"'{id}'"));
                    var actionsString = string.Join(",", currentActions);
                    
                    ps.AddScript($@"
                        Set-MpPreference -AttackSurfaceReductionRules_Ids @({idsString})
                        Set-MpPreference -AttackSurfaceReductionRules_Actions {actionsString}
                    ");
                    
                    ps.Invoke();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting ASR rule: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> EnableAllRecommendedASRRulesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var recommendedRules = new[]
                    {
                        "BE9BA2D9-53EA-4CDC-84E5-9B1EEEE46550",
                        "D4F940AB-401B-4EFC-AADC-AD5F3C50688A",
                        "3B576869-A4EC-4529-8536-B80A7769E899",
                        "75668C1F-73B5-4CF0-BB93-3ECF5CB7CC84",
                        "D3E037E1-3EB8-44C8-A917-57927947596D",
                        "5BEB7EFE-FD9A-4556-801D-275E5FFC04CC",
                        "92E97FA1-2EDF-4476-BDD6-9DD0B4DDDC7B",
                        "01443614-CD74-433A-B99E-2ECDC07BFC25",
                        "C1DB55AB-C21A-4637-BB3F-A12569109B35",
                        "9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2"
                    };

                    using var ps = PowerShell.Create();
                    var idsString = string.Join(",", recommendedRules.Select(id => $"'{id}'"));
                    var actionsString = string.Join(",", Enumerable.Repeat("Enabled", recommendedRules.Length));
                    
                    ps.AddScript($@"
                        Set-MpPreference -AttackSurfaceReductionRules_Ids @({idsString})
                        Set-MpPreference -AttackSurfaceReductionRules_Actions {actionsString}
                    ");
                    
                    ps.Invoke();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        private string GetRuleDescription(string ruleId)
        {
            return ruleId switch
            {
                "BE9BA2D9-53EA-4CDC-84E5-9B1EEEE46550" => "Prevents Office applications from spawning child processes, blocking common attack vectors.",
                "D4F940AB-401B-4EFC-AADC-AD5F3C50688A" => "Blocks executable files from being launched from email attachments.",
                "3B576869-A4EC-4529-8536-B80A7769E899" => "Prevents Office apps from injecting code into other processes.",
                "75668C1F-73B5-4CF0-BB93-3ECF5CB7CC84" => "Blocks Office apps from creating executable content.",
                "D3E037E1-3EB8-44C8-A917-57927947596D" => "Prevents JavaScript/VBScript from executing downloaded files.",
                "5BEB7EFE-FD9A-4556-801D-275E5FFC04CC" => "Blocks execution of obfuscated or suspicious scripts.",
                "92E97FA1-2EDF-4476-BDD6-9DD0B4DDDC7B" => "Prevents Office macros from calling Win32 APIs.",
                "01443614-CD74-433A-B99E-2ECDC07BFC25" => "Blocks untrusted executables unless they meet trust criteria.",
                "C1DB55AB-C21A-4637-BB3F-A12569109B35" => "Prevents malware from persisting via WMI event subscriptions.",
                "9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2" => "Protects against credential theft from LSASS.",
                _ => "Attack Surface Reduction rule for enhanced security."
            };
        }
    }
}

