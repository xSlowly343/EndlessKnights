using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class LootSystem
    {
        private readonly StatSheet _hero;
        private readonly GameState _state;
        private readonly EventQueue _events;
        private StageTable _stages;

        public LootSystem(StageTable stages, StatSheet hero, GameState state, EventQueue events)
        {
            _stages = stages;
            _hero = hero;
            _state = state;
            _events = events;
        }

        public void UseData(StageTable stages) => _stages = stages;

        public BigNumber GoldFor(long stage, EnemyType type, EnemyDef? enemy)
        {
            var baseGold = type == EnemyType.Normal ? _stages.EnemyGold(stage) : _stages.BossGold(stage);
            return _hero.Compute(CombatStats.Gold, baseGold * (enemy?.GoldMultiplier ?? 1.0), StatTags.Gold);
        }

        public void OnKill(long stage, EnemyType type, EnemyDef? enemy, double multiplier = 1)
        {
            var gold = GoldFor(stage, type, enemy) * multiplier;
            if (gold.Sign <= 0)
                return;
            _state.Gold += gold;
            _state.RunGoldEarned += gold;
            _events.Add(GameEvent.Gold(_state.Tick, gold, stage));
        }
    }
}
