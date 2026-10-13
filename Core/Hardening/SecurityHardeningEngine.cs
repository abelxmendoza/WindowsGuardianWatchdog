namespace WindowsGuardianWatchdog.Core.Hardening
{
    public class SecurityHardeningEngine
    {
        private readonly List<HardeningRule> _rules;
        private readonly List<HardeningRule> _appliedRules;

        public SecurityHardeningEngine()
        {
            _rules = HardeningRuleFactory.CreateDefaultRules();
            _appliedRules = new List<HardeningRule>();
        }

        public SecurityHardeningEngine(List<HardeningRule> customRules)
        {
            _rules = customRules;
            _appliedRules = new List<HardeningRule>();
        }

        public List<HardeningRule> AvailableRules => _rules;

        public List<HardeningResult> AppliedResults { get; private set; } = new();

        public async Task<List<HardeningResult>> ApplyAllAsync()
        {
            AppliedResults = new List<HardeningResult>();

            foreach (var rule in _rules)
            {
                var result = await ApplyRuleAsync(rule);
                AppliedResults.Add(result);
            }

            return AppliedResults;
        }

        public async Task<List<HardeningResult>> ApplySelectedAsync(IEnumerable<string> ruleIds)
        {
            AppliedResults = new List<HardeningResult>();
            var selectedRules = _rules.Where(r => ruleIds.Contains(r.Id)).ToList();

            foreach (var rule in selectedRules)
            {
                var result = await ApplyRuleAsync(rule);
                AppliedResults.Add(result);
            }

            return AppliedResults;
        }

        public async Task<HardeningResult> ApplyRuleAsync(HardeningRule rule)
        {
            var result = new HardeningResult
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Category = rule.Category,
                Severity = rule.Severity,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                if (rule.ApplyAsync != null)
                {
                    await rule.ApplyAsync();
                    result.WasApplied = true;

                    // Verify the rule was applied
                    if (rule.VerifyAsync != null)
                    {
                        result.Success = await rule.VerifyAsync();
                    }
                    else
                    {
                        result.Success = true; // Assume success if no verification
                    }

                    if (result.Success)
                    {
                        _appliedRules.Add(rule);
                    }
                }
                else
                {
                    result.Success = false;
                    result.Error = "No apply action defined for this rule";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        public async Task<List<HardeningResult>> RollbackAllAsync()
        {
            var results = new List<HardeningResult>();

            foreach (var rule in _appliedRules)
            {
                var result = await RollbackRuleAsync(rule);
                results.Add(result);
            }

            _appliedRules.Clear();
            return results;
        }

        public async Task<HardeningResult> RollbackRuleAsync(HardeningRule rule)
        {
            var result = new HardeningResult
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Category = rule.Category,
                Severity = rule.Severity,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                if (rule.RollbackAsync != null)
                {
                    await rule.RollbackAsync();
                    result.Success = true;
                    result.WasApplied = false;
                }
                else
                {
                    result.Success = false;
                    result.Error = "No rollback action defined for this rule";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        }

        public async Task<List<HardeningResult>> VerifyAllAsync()
        {
            var results = new List<HardeningResult>();

            foreach (var rule in _rules)
            {
                var result = new HardeningResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Category = rule.Category,
                    Severity = rule.Severity,
                    Timestamp = DateTime.UtcNow
                };

                try
                {
                    if (rule.VerifyAsync != null)
                    {
                        result.Success = await rule.VerifyAsync();
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Error = ex.Message;
                }

                results.Add(result);
            }

            return results;
        }
    }
}


