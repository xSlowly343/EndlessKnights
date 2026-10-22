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
        private readonly ClassMechanics _class;
        private CombatRules _rules;
        private IKillHandler? _kills;
        private IKeyHitListener? _keyHits;

        public CombatSystem(CombatRules rules, StatSheet hero, GameState state, EventQueue events)
        {
            _rules = rules;
            _hero = hero;
            _state = state;
            _events = events;
            _class = new ClassMechanics(rules.Classes, state, events);
        }

        public ClassMechanics Class => _class;
        public BigNumber LastTickDamage { get; private set; }

        public void UseRules(CombatRules rules)
        {
            _rules = rules;
            _class.UseRules(rules.Classes);
        }

        public void OnKill(IKillHandler kills) => _kills = kills;

        public void OnKeyHit(IKeyHitListener keyHits) => _keyHits = keyHits;

        public double BaseKeyDamage => _rules.KeyDamage;

        public void SkillHit(double baseDamage, double multiplier, StatTags tags) =>
            Hit(DamageSource.Skill, baseDamage, tags, HitFlags.None, multiplier);

        public void Tick(in EffectiveInput input)
        {
            LastTickDamage = BigNumber.Zero;
            var keyHits = TakeWhole(_state.KeyCarry + input.Keys, out var keyCarry);
            _state.KeyCarry = keyCarry;
            var clickHits = TakeWhole(_state.ClickCarry + input.Clicks, out var clickCarry);
            _state.ClickCarry = clickCarry;
            var mouseHits = TakeWhole((_state.MouseCarryPx + input.MousePx) / _rules.MousePxPerHit, out var mouseCarry);
            _state.MouseCarryPx = mouseCarry * _rules.MousePxPerHit;

            for (var i = 0; i < keyHits; i++)
            {
                var multiplier = _class.OnKeyHit(out var flags);
                Hit(DamageSource.Keyboard, _rules.KeyDamage, StatTags.Keyboard, flags, _state.FocusMultiplier * multiplier);
            }
            for (var i = 0; i < clickHits; i++)
            {
                _class.OnClickHit();
                Hit(DamageSource.Click, _rules.ClickDamage, StatTags.Click, HitFlags.None, _state.FocusMultiplier);
            }
            for (var i = 0; i < mouseHits; i++)
                Hit(DamageSource.Mouse, _rules.MouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier);
            _class.EndTick(keyHits);
        }

        private void Hit(DamageSource source, double baseDamage, StatTags tags, HitFlags flags, double multiplier)
        {
            if (!_state.EnemyAlive)
                return;

            var damage = _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * multiplier;
            if (damage.Sign <= 0)
                return;

            if (RollCrit(tags))
            {
                damage *= CritMultiplier(tags);
                flags |= HitFlags.Crit;
            }

            _state.EnemyHp -= damage;
            LastTickDamage += damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, source, flags));
            if (source == DamageSource.Keyboard)
                _keyHits?.OnKeyHit(damage);
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
