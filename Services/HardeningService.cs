using WindowsGuardianWatchdog.Core.Hardening;
using System.Collections.ObjectModel;

namespace WindowsGuardianWatchdog.Services
{
    public class HardeningService
    {
        private readonly SecurityHardeningEngine _engine;

        public HardeningService()
        {
            _engine = new SecurityHardeningEngine();
        }

        public List<HardeningRule> GetAvailableRules()
        {
            return _engine.AvailableRules;
        }

        public async Task<List<HardeningResult>> ApplyAllHardeningAsync()
        {
            return await _engine.ApplyAllAsync();
        }

        public async Task<List<HardeningResult>> ApplySelectedHardeningAsync(IEnumerable<string> ruleIds)
        {
            return await _engine.ApplySelectedAsync(ruleIds);
        }

        public async Task<List<HardeningResult>> RollbackAllAsync()
        {
            return await _engine.RollbackAllAsync();
        }

        public async Task<List<HardeningResult>> VerifyAllAsync()
        {
            return await _engine.VerifyAllAsync();
        }

        public async Task<string> GetDefenderStatusAsync()
        {
            return await HardeningVerifier.GetDefenderStatusAsync();
        }
    }
}


