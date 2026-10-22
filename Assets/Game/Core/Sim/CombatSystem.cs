using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class CombatSystem
    {
        private readonly StatSheet _hero;
        private readonly GameState _state;
        private readonly EventQueue _events;
        private CombatRules _rules;
        private IKillHandler? _kills;

        public CombatSystem(CombatRules rules, StatSheet hero, GameState state, EventQueue events)
        {
            _rules = rules;
            _hero = hero;
            _state = state;
            _events = events;
        }

        public void UseRules(CombatRules rules) => _rules = rules;

        public void OnKill(IKillHandler kills) => _kills = kills;

        public void Tick(in EffectiveInput input)
        {
            var keyHits = TakeWhole(_state.KeyCarry + input.Keys, out var keyCarry);
            _state.KeyCarry = keyCarry;
            var clickHits = TakeWhole(_state.ClickCarry + input.Clicks, out var clickCarry);
            _state.ClickCarry = clickCarry;
            var mouseHits = TakeWhole((_state.MouseCarryPx + input.MousePx) / _rules.MousePxPerHit, out var mouseCarry);
            _state.MouseCarryPx = mouseCarry * _rules.MousePxPerHit;

            for (var i = 0; i < keyHits; i++)
                Hit(DamageSource.Keyboard, _rules.KeyDamage, StatTags.Keyboard);
            for (var i = 0; i < clickHits; i++)
                Hit(DamageSource.Click, _rules.ClickDamage, StatTags.Click);
            for (var i = 0; i < mouseHits; i++)
                Hit(DamageSource.Mouse, _rules.MouseDamage, StatTags.Mouse);
        }

        private void Hit(DamageSource source, double baseDamage, StatTags tags)
        {
            if (!_state.EnemyAlive)
                return;

            var damage = _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * _state.FocusMultiplier;
            if (damage.Sign <= 0)
                return;

            var flags = HitFlags.None;
            if (RollCrit(tags))
            {
                damage *= CritMultiplier(tags);
                flags |= HitFlags.Crit;
            }

            _state.EnemyHp -= damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, source, flags));
            if (_state.EnemyAlive)
                return;

            _state.EnemyHp = BigNumber.Zero;
            _kills?.OnEnemyKilled();
        }

        public double CritChance(StatTags tags)
        {
            var chance = _hero.Compute(CombatStats.CritChance, BigNumber.FromDouble(_rules.CritChance), tags | StatTags.Crit).ToDouble();
            return System.Math.Min(1, System.Math.Max(0, chance));
        }

        public double CritMultiplier(StatTags tags) =>
            System.Math.Max(1, _hero.Compute(CombatStats.CritMultiplier, BigNumber.FromDouble(_rules.CritMultiplier), tags | StatTags.Crit).ToDouble());

        private bool RollCrit(StatTags tags)
        {
            var chance = CritChance(tags);
            return chance > 0 && _state.Rng.NextDouble() < chance;
        }

        private static int TakeWhole(double amount, out double remainder)
        {
            var whole = System.Math.Floor(amount + 1e-9);
            remainder = System.Math.Max(0, amount - whole);
            return (int)whole;
        }
    }
}
