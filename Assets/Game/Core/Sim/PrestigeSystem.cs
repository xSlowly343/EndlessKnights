using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class PrestigeSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly StageSystem _stages;
        private readonly StrikeUpgradeSystem _upgrades;
        private readonly StatSheet _hero;
        private PrestigeRules _rules;

        public PrestigeSystem(PrestigeRules rules, GameState state, EventQueue events, StageSystem stages, StrikeUpgradeSystem upgrades, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _events = events;
            _stages = stages;
            _upgrades = upgrades;
            _hero = hero;
        }

        public void UseRules(PrestigeRules rules) => _rules = rules;

        public bool Available => !_state.Rift.Inside && _state.HighestStage >= _rules.MinStage;

        public BigNumber EchoGain
        {
            get
            {
                var echo = _rules.EchoFor(_state.RunGoldEarned);
                var bonus = System.Math.Max(0, _hero.Value(CombatStats.EchoGain, 1));
                if (bonus == 1 || echo.Sign <= 0)
                    return echo;
                var boosted = echo * bonus;
                return boosted.Log10() < 15 ? BigNumber.FromDouble(System.Math.Floor(boosted.ToDouble())) : boosted;
            }
        }

        public long StartStage => System.Math.Max(1, System.Math.Min(_state.RecordStage, 1 + (long)System.Math.Floor(System.Math.Max(0, _hero.Value(CombatStats.PrestigeStartStage, 0)) + 1e-9)));

        public bool Highlighted
        {
            get
            {
                if (!Available || EchoGain.Sign <= 0)
                    return false;
                return _state.EchoEarned.IsZero || EchoGain >= _state.EchoEarned * _rules.HighlightGain;
            }
        }

        public bool TryPrestige()
        {
            if (!Available)
                return false;
            var gain = EchoGain;
            _state.Echo += gain;
            _state.EchoEarned += gain;
            _state.Prestiges++;
            _state.Ascendancy.Complete(ActivityIds.FirstPrestige);
            _events.Add(GameEvent.WithAmount(GameEventKind.Prestige, _state.Tick, gain, _state.HighestStage));
            ResetRun();
            return true;
        }

        private void ResetRun()
        {
            var start = StartStage;
            _state.Stage = start;
            _state.HighestStage = start;
            _state.KillsOnStage = 0;
            _state.Farming = false;
            _state.FarmKills = 0;
            _state.BossFailsInARow = 0;
            _state.BossBestFraction = 0;
            _state.Gold = BigNumber.Zero;
            _state.RunGoldEarned = BigNumber.Zero;
            _state.ClearQueue();
            _upgrades.Reset();
            _state.SpawnEnemy(null, Content.EnemyType.Normal, BigNumber.Zero);
            _stages.EnsureEnemy();
        }
    }
}
