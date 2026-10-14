using WindowsGuardianWatchdog.Core.Scripting;

namespace WindowsGuardianWatchdog.Core.Hardening
{
    public class SecurityHardeningEngine
    {
        private readonly List<HardeningRule> _rules;

        public SecurityHardeningEngine()
        {
            _rules = HardeningRuleFactory.CreateDefaultRules();
        }

        public SecurityHardeningEngine(List<HardeningRule> customRules)
        {
            _rules = customRules;
        }

        public List<HardeningRule> AvailableRules => _rules;

        public Task<List<HardeningResult>> ApplyAllAsync() => ExecuteAsync(_rules, rollback: false);

        public Task<List<HardeningResult>> ApplySelectedAsync(IEnumerable<string> ruleIds)
        {
            var selected = _rules.Where(rule => ruleIds.Contains(rule.Id)).ToList();
            return ExecuteAsync(selected, rollback: false);
        }

        public Task<List<HardeningResult>> RollbackAllAsync()
        {
            var applied = HardeningJournal.Read();
            var rules = _rules.Where(rule => applied.Contains(rule.Id, StringComparer.OrdinalIgnoreCase)).ToList();
            return ExecuteAsync(rules, rollback: true);
        }

        public async Task<List<HardeningResult>> VerifyAllAsync()
        {
            if (_rules.Count == 0) return new List<HardeningResult>();

            var script = new System.Text.StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Continue'");
            foreach (var rule in _rules)
            {
                var expression = string.IsNullOrWhiteSpace(rule.VerifyExpression) ? "$false" : rule.VerifyExpression;
                script.AppendLine("try {");
                script.AppendLine("    $ok = $(");
                script.AppendLine(expression);
                script.AppendLine("    )");
                script.AppendLine($"    if ($ok -eq $true) {{ Write-Output 'CHK|{rule.Id}|True' }} else {{ Write-Output 'CHK|{rule.Id}|False' }}");
                script.AppendLine("} catch {");
                script.AppendLine($"    Write-Output 'CHK|{rule.Id}|False'");
                script.AppendLine("}");
            }

            var run = await ScriptRunner.RunPowerShellAsync(script.ToString(), elevate: false, timeoutMs: 120000);
            var states = ParseChecks(run.Output);
            return _rules.Select(rule =>
            {
                var active = states.TryGetValue(rule.Id, out var value) && value;
                return new HardeningResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Category = rule.Category,
                    Severity = rule.Severity,
                    Timestamp = DateTime.Now,
                    Success = active,
                    StatusText = active ? "Active" : "Off"
                };
            }).ToList();
        }

        private async Task<List<HardeningResult>> ExecuteAsync(List<HardeningRule> rules, bool rollback)
        {
            if (rules.Count == 0)
            {
                return new List<HardeningResult>
                {
                    new()
                    {
                        RuleName = rollback ? "Rollback" : "Hardening",
                        Success = false,
                        StatusText = "Nothing to change",
                        Error = rollback
                            ? "No baseline from this app is recorded yet."
                            : "Select at least one hardening rule.",
                        Timestamp = DateTime.Now
                    }
                };
            }

            var script = new System.Text.StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            foreach (var rule in rules)
            {
                var body = rollback ? rule.RollbackScript : rule.ApplyScript;
                script.AppendLine("try {");
                script.AppendLine("    $global:LASTEXITCODE = 0");
                script.AppendLine(string.IsNullOrWhiteSpace(body) ? "    throw 'No action is defined for this rule'" : Indent(body));
                script.AppendLine("    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw \"Command failed with exit code $LASTEXITCODE\" }");
                script.AppendLine($"    Write-Output 'WG|{rule.Id}|OK|'");
                script.AppendLine("} catch {");
                script.AppendLine("    $msg = ($_.Exception.Message -replace '\\|','/' -replace '[\\r\\n]+',' ').Trim()");
                script.AppendLine($"    Write-Output \"WG|{rule.Id}|FAIL|$msg\"");
                script.AppendLine("}");
            }
            script.AppendLine("$global:LASTEXITCODE = 0");

            ScriptResult run;
            try
            {
                run = await ScriptRunner.RunPowerShellAsync(script.ToString(), elevate: true, timeoutMs: 8 * 60 * 1000);
            }
            catch (Exception ex)
            {
                return rules.Select(rule => Failed(rule, ex.Message)).ToList();
            }

            if (run.Cancelled)
            {
                return rules.Select(rule => Failed(rule, "Administrator approval was cancelled.")).ToList();
            }

            var markers = ParseMarkers(run.Output);
            var results = new List<HardeningResult>();
            foreach (var rule in rules)
            {
                var result = new HardeningResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Category = rule.Category,
                    Severity = rule.Severity,
                    Timestamp = DateTime.Now
                };

                if (!markers.TryGetValue(rule.Id, out var marker))
                {
                    result.Success = false;
                    result.StatusText = "Failed";
                    result.Error = string.IsNullOrWhiteSpace(run.Output) ? "No result was returned." : run.Output;
                }
                else if (!marker.Ok)
                {
                    result.Success = false;
                    result.StatusText = "Failed";
                    result.Error = marker.Message;
                }
                else
                {
                    result.Success = true;
                    result.WasApplied = !rollback;
                    result.StatusText = rollback ? "Rolled back" : "Applied";
                    if (rollback) HardeningJournal.Forget(rule.Id);
                    else HardeningJournal.Remember(rule.Id);
                }

                results.Add(result);
            }

            return results;
        }

        private static Dictionary<string, (bool Ok, string Message)> ParseMarkers(string output)
        {
            var markers = new Dictionary<string, (bool Ok, string Message)>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in output.Replace("\r", string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("WG|", StringComparison.Ordinal)) continue;
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                markers[parts[1]] = (parts[2].Equals("OK", StringComparison.OrdinalIgnoreCase), parts.Length > 3 ? parts[3] : string.Empty);
            }

            return markers;
        }

        private static Dictionary<string, bool> ParseChecks(string output)
        {
            var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in output.Replace("\r", string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                if (!line.StartsWith("CHK|", StringComparison.Ordinal)) continue;
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                states[parts[1]] = parts[2].Equals("True", StringComparison.OrdinalIgnoreCase);
            }

            return states;
        }

        private static HardeningResult Failed(HardeningRule rule, string error) => new()
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Category = rule.Category,
            Severity = rule.Severity,
            Success = false,
            StatusText = "Failed",
            Error = error,
            Timestamp = DateTime.Now
        };

        private static string Indent(string script)
        {
            var lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return string.Join(Environment.NewLine, lines.Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : "    " + line));
        }
    }
}
