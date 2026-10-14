namespace WindowsGuardianWatchdog.Core.Hardening
{
    public static class HardeningRuleFactory
    {
        public static List<HardeningRule> CreateDefaultRules()
        {
            return new List<HardeningRule>
            {
                Rule("DEFENDER_REALTIME", "Defender real-time protection",
                    "Keeps Microsoft Defender watching files as they are opened.",
                    "Defender", HardeningSeverity.Critical, recommended: true,
                    "Set-MpPreference -DisableRealtimeMonitoring $false",
                    "Set-MpPreference -DisableRealtimeMonitoring $true",
                    "(Get-MpPreference).DisableRealtimeMonitoring -eq $false"),

                Rule("DEFENDER_PUA", "Potentially unwanted apps",
                    "Blocks adware and other unwanted programs Defender already knows about.",
                    "Defender", HardeningSeverity.High, recommended: true,
                    "Set-MpPreference -PUAProtection Enabled",
                    "Set-MpPreference -PUAProtection Disabled",
                    "$v = [string](Get-MpPreference).PUAProtection; $v -eq '1' -or $v -eq 'Enabled'"),

                Rule("DEFENDER_CLOUD", "Defender cloud protection",
                    "Lets Defender check suspicious files against Microsoft's cloud reputation. Sample upload stays off.",
                    "Defender", HardeningSeverity.High, recommended: true,
                    "Set-MpPreference -MAPSReporting Advanced; Set-MpPreference -SubmitSamplesConsent NeverSend",
                    "Set-MpPreference -MAPSReporting Disabled",
                    "$v = [string](Get-MpPreference).MAPSReporting; $v -ne '0' -and $v -ne 'Disabled' -and $v -ne ''"),

                Rule("DEFENDER_ASR", "Attack surface reduction",
                    "Blocks common script, Office, and credential-theft tricks. Some older macros and installers may stop working.",
                    "Defender", HardeningSeverity.Critical, recommended: false,
                    """
                    $rules = @(
                        'D4F940AB-401B-4EFC-AADC-AD5F3C50688A',
                        'BE9BA2D9-53EA-4CDC-84E5-9B1EEEE46550',
                        '3B576869-A4EC-4529-8536-B80A7769E899',
                        '75668C1F-73B5-4CF0-BB93-3ECF5CB7CC84',
                        'D3E037E1-3EB8-44C8-A917-57927947596D',
                        '5BEB7EFE-FD9A-4556-801D-275E5FFC04CC',
                        '92E97FA1-2EDF-4476-BDD6-9DD0B4DDDC7B',
                        'C1DB55AB-C21A-4637-BB3F-A12569109B35',
                        '9E6C4E1F-7D60-472F-BA1A-A39EF669E4B2'
                    )
                    Set-MpPreference -AttackSurfaceReductionRules_Ids $rules
                    Set-MpPreference -AttackSurfaceReductionRules_Actions Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled,Enabled
                    """,
                    "Set-MpPreference -AttackSurfaceReductionRules_Ids @(); Set-MpPreference -AttackSurfaceReductionRules_Actions @()",
                    """
                    $p = Get-MpPreference
                    $actions = @($p.AttackSurfaceReductionRules_Actions)
                    @($actions | Where-Object { $_ -eq 1 -or $_ -eq 'Enabled' -or $_ -eq 'Block' }).Count -gt 0
                    """),

                Rule("DEFENDER_CONTROLLED_FOLDER", "Controlled folder access",
                    "Stops untrusted programs from changing Documents, Pictures, and the other protected folders. Apps you trust may need an allow rule.",
                    "Defender", HardeningSeverity.High, recommended: false,
                    "Set-MpPreference -EnableControlledFolderAccess Enabled",
                    "Set-MpPreference -EnableControlledFolderAccess Disabled",
                    "$v = [string](Get-MpPreference).EnableControlledFolderAccess; $v -eq '1' -or $v -eq 'Enabled'"),

                Rule("FIREWALL_ENABLE", "Windows Firewall",
                    "Turns the firewall on for domain, private, and public networks.",
                    "Firewall", HardeningSeverity.Critical, recommended: true,
                    "Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True",
                    "Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled False",
                    "@(Get-NetFirewallProfile -Profile Domain,Public,Private | Where-Object { $_.Enabled -ne $true }).Count -eq 0"),

                Rule("UAC_ENABLE", "User Account Control",
                    "Asks before a program changes Windows. A restart is needed before a change to this setting takes effect.",
                    "System", HardeningSeverity.High, recommended: true,
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name EnableLUA -Value 1",
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name EnableLUA -Value 0",
                    "(Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System' -Name EnableLUA).EnableLUA -eq 1"),

                Rule("SMARTSCREEN", "SmartScreen",
                    "Warns before Windows runs an unrecognized app.",
                    "System", HardeningSeverity.Medium, recommended: true,
                    """
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' -Name SmartScreenEnabled -Value 'RequireAdmin'
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name EnableSmartScreen -Value 1
                    """,
                    "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer' -Name SmartScreenEnabled -Value 'Off'",
                    "$s = (Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer' -Name SmartScreenEnabled -ErrorAction SilentlyContinue).SmartScreenEnabled; $s -and $s -ne 'Off'"),

                Rule("AUTORUN_DISABLE", "Block AutoRun",
                    "Stops removable drives from launching programs on their own.",
                    "System", HardeningSeverity.High, recommended: true,
                    """
                    New-Item -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer' -Name NoDriveTypeAutoRun -Value 255
                    """,
                    "Remove-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer' -Name NoDriveTypeAutoRun -ErrorAction SilentlyContinue",
                    "(Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer' -Name NoDriveTypeAutoRun -ErrorAction SilentlyContinue).NoDriveTypeAutoRun -eq 255"),

                Rule("SMB1_DISABLE", "Disable SMBv1",
                    "Turns off the old SMB1 file-sharing protocol.",
                    "Network", HardeningSeverity.High, recommended: true,
                    """
                    Set-SmbServerConfiguration -EnableSMB1Protocol $false -Force
                    New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters' -Name SMB1 -Value 0 -PropertyType DWord -Force | Out-Null
                    """,
                    """
                    Set-SmbServerConfiguration -EnableSMB1Protocol $true -Force
                    New-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters' -Name SMB1 -Value 1 -PropertyType DWord -Force | Out-Null
                    """,
                    "(Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters' -Name SMB1 -ErrorAction SilentlyContinue).SMB1 -eq 0"),

                Rule("WINDOWS_UPDATE_AUTO", "Automatic Windows Update",
                    "Tells Windows to download and install updates on its own.",
                    "Updates", HardeningSeverity.High, recommended: true,
                    """
                    New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Force | Out-Null
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name NoAutoUpdate -Value 0
                    Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU' -Name AUOptions -Value 4
                    """,
                    "Remove-Item -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU' -Recurse -Force -ErrorAction SilentlyContinue",
                    "$au = Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU' -ErrorAction SilentlyContinue; $au.NoAutoUpdate -eq 0 -and $au.AUOptions -eq 4"),

                Rule("GUEST_DISABLE", "Disable the Guest account",
                    "Turns the built-in Guest account off.",
                    "Accounts", HardeningSeverity.Medium, recommended: true,
                    "net.exe user guest /active:no",
                    "net.exe user guest /active:yes",
                    "((net.exe user guest) -join \"`n\") -match 'Account active\\s+No'"),

                Rule("REMOTE_ASSISTANCE_DISABLE", "Disable Remote Assistance",
                    "Stops other people from requesting to control this PC through Remote Assistance.",
                    "Network", HardeningSeverity.Medium, recommended: true,
                    "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance' -Name fAllowToGetHelp -Value 0",
                    "Set-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance' -Name fAllowToGetHelp -Value 1",
                    "(Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Remote Assistance' -Name fAllowToGetHelp -ErrorAction SilentlyContinue).fAllowToGetHelp -eq 0")
            };
        }

        private static HardeningRule Rule(
            string id,
            string name,
            string description,
            string category,
            HardeningSeverity severity,
            bool recommended,
            string applyScript,
            string rollbackScript,
            string verifyExpression)
        {
            return new HardeningRule
            {
                Id = id,
                Name = name,
                Description = description,
                Category = category,
                Severity = severity,
                Recommended = recommended,
                RequiresAdmin = true,
                ApplyScript = applyScript,
                RollbackScript = rollbackScript,
                VerifyExpression = verifyExpression
            };
        }
    }
}
