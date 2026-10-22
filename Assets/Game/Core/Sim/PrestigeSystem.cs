using Game.Core.Content;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class PrestigeSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly StageSystem _stages;
        private PrestigeRules _rules;

        public PrestigeSystem(PrestigeRules rules, GameState state, EventQueue events, StageSystem stages)
        {
            _rules = rules;
            _state = state;
            _events = events;
            _stages = stages;
        }

        public void UseRules(PrestigeRules rules) => _rules = rules;

        public bool Available => _state.HighestStage >= _rules.MinStage;

        public BigNumber EchoGain => _rules.EchoFor(_state.RunGoldEarned);

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
            _state.Stage = 1;
            _state.HighestStage = 1;
            _state.KillsOnStage = 0;
            _state.Farming = false;
            _state.FarmKills = 0;
            _state.BossFailsInARow = 0;
            _state.Gold = BigNumber.Zero;
            _state.RunGoldEarned = BigNumber.Zero;
            _state.ChainDamage = BigNumber.Zero;
            _state.SpawnEnemy(null, Content.EnemyType.Normal, BigNumber.Zero);
            _stages.EnsureEnemy();
        }
    }
}
