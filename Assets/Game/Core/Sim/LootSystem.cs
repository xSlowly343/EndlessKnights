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
            var baseGold = type == EnemyType.Normal ? _stages.EnemyGold(stage) : _stages.BossGold(stage) * System.Math.Max(0, _hero.Value(CombatStats.BossGold, 1, StatTags.Gold));
            var fill = 1 + System.Math.Max(0, _hero.Value(CombatStats.GoldPerResource, 0, StatTags.Gold)) * _state.ResourceFill;
            return _hero.Compute(CombatStats.Gold, baseGold * (enemy?.GoldMultiplier ?? 1.0), StatTags.Gold) * fill;
        }

        public void Golden(long stage, double share)
        {
            var gold = _hero.Compute(CombatStats.Gold, _stages.EnemyGold(stage) * share * System.Math.Max(0, _hero.Value(CombatStats.EncounterGold, 1, StatTags.Gold)), StatTags.Gold);
            if (gold.Sign <= 0)
                return;
            _state.Gold += gold;
            _state.RunGoldEarned += gold;
            _events.Add(GameEvent.Gold(_state.Tick, gold, stage));
        }

        public void Chest(long stage, double share)
        {
            var gold = _hero.Compute(CombatStats.Gold, _stages.BossGold(stage) * share * System.Math.Max(0, _hero.Value(CombatStats.ChestGold, 1, StatTags.Gold)), StatTags.Gold);
            if (gold.Sign <= 0)
                return;
            _state.Gold += gold;
            _state.RunGoldEarned += gold;
            _events.Add(GameEvent.Gold(_state.Tick, gold, stage));
        }

        public double JackpotMultiplier { get; set; } = 1;

        public void OnKill(long stage, EnemyType type, EnemyDef? enemy, double multiplier = 1)
        {
            var gold = GoldFor(stage, type, enemy) * (multiplier * Jackpot(type));
            if (gold.Sign <= 0)
                return;
            _state.Gold += gold;
            _state.RunGoldEarned += gold;
            _events.Add(GameEvent.Gold(_state.Tick, gold, stage));
        }

        private double Jackpot(EnemyType type)
        {
            if (type != EnemyType.Normal)
                return 1;
            var chance = _hero.Value(CombatStats.GoldJackpotChance, 0, StatTags.Gold);
            return chance > 0 && _state.Rng.NextDouble() < chance ? JackpotMultiplier : 1;
        }
    }
}
