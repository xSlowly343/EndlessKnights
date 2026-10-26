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
        private long _hitsTowardCrit;

        public CombatSystem(CombatRules rules, StatSheet hero, GameState state, EventQueue events)
        {
            _rules = rules;
            _hero = hero;
            _state = state;
            _events = events;
            _class = new ClassMechanics(rules.Classes, state, hero);
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

        public BigNumber SkillDamage(double baseDamage, double multiplier, StatTags tags) =>
            _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * multiplier;

        public void NotePause(long idleTicksBefore, bool hasInput)
        {
            var conditions = _rules.Conditions;
            if (hasInput && idleTicksBefore * GameLoop.TickSeconds >= conditions.AfterIdlePauseSeconds)
                _state.AfterIdleTicks = (int)System.Math.Round(conditions.AfterIdleSeconds * GameLoop.TicksPerSecond);
            else if (_state.AfterIdleTicks > 0)
                _state.AfterIdleTicks--;
        }

        public double ConditionMultiplier(StatTags tags)
        {
            var conditions = _rules.Conditions;
            var multiplier = 1.0;
            if (_state.FightingBoss)
            {
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageVsBoss, 1, tags));
                if (_state.BossSecondsLeft <= conditions.BossTimerEndSeconds)
                    multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageAtBossTimerEnd, 1, tags));
            }
            if (_state.HitsOnEnemy < conditions.FirstHits)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.FirstHitsBonus, 0, tags));
            if (_state.KillStreak > 0)
                multiplier *= 1 + System.Math.Max(0, _hero.Value(CombatStats.DamagePerKill, 0, tags)) * System.Math.Min(_state.KillStreak, conditions.KillStreakMax);
            if (_state.AfterIdleTicks > 0)
                multiplier *= System.Math.Max(0, _hero.Value(CombatStats.DamageAfterIdle, 1, tags));
            return multiplier;
        }

        public void DirectHit(BigNumber damage)
        {
            if (!_state.EnemyAlive || damage.Sign <= 0)
                return;
            _state.EnemyHp -= damage;
            LastTickDamage += damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, DamageSource.Skill, HitFlags.None));
            if (_state.EnemyAlive)
                return;
            _state.EnemyHp = BigNumber.Zero;
            _kills?.OnEnemyKilled();
        }

        public void Tick(in EffectiveInput input)
        {
            LastTickDamage = BigNumber.Zero;
            CountStreakTime();
            var keyHits = TakeWhole(_state.KeyCarry + input.Keys, out var keyCarry);
            _state.KeyCarry = keyCarry;
            var mouseHits = TakeWhole((_state.MouseCarryPx + input.MousePx) / _rules.MousePxPerHit, out var mouseCarry);
            _state.MouseCarryPx = mouseCarry * _rules.MousePxPerHit;

            for (var i = 0; i < keyHits; i++)
            {
                var multiplier = _class.OnKeyHit(out var flags) * HeavyStrike(ref flags);
                Hit(DamageSource.Keyboard, _rules.KeyDamage, StatTags.Keyboard, flags, _state.FocusMultiplier * multiplier);
                KeyHitLightning();
            }
            for (var i = 0; i < mouseHits; i++)
                Hit(DamageSource.Mouse, _rules.MouseDamage, StatTags.Mouse, HitFlags.None, _state.FocusMultiplier);
            _class.EndTick(keyHits);
        }

        private void Hit(DamageSource source, double baseDamage, StatTags tags, HitFlags flags, double multiplier)
        {
            if (!_state.EnemyAlive)
                return;

            var damage = _hero.Compute(CombatStats.Damage, BigNumber.FromDouble(baseDamage), tags) * (multiplier * ConditionMultiplier(tags));
            if (damage.Sign <= 0)
                return;
            _state.HitsOnEnemy++;

            if (ForcedCrit() || RollCrit(tags))
            {
                damage *= CritMultiplier(tags);
                flags |= HitFlags.Crit;
            }

            var hpBefore = _state.EnemyHp;
            _state.EnemyHp -= damage;
            LastTickDamage += damage;
            _events.Add(GameEvent.Damage(_state.Tick, damage, source, flags));
            if (source == DamageSource.Keyboard)
                _keyHits?.OnKeyHit(damage);
            if (_state.EnemyAlive && !Culled(tags))
                return;

            CarryOverkill(damage - hpBefore, tags);
            _state.EnemyHp = BigNumber.Zero;
            _kills?.OnEnemyKilled();
        }

        private bool Culled(StatTags tags)
        {
            var threshold = System.Math.Min(_rules.Conditions.CullMax, _hero.Value(CombatStats.CullThreshold, 0, tags));
            return threshold > 0 && _state.EnemyHp < _state.EnemyMaxHp * threshold;
        }

        private void CountStreakTime()
        {
            if (_state.KillStreak == 0)
                return;
            _state.TicksSinceKill++;
            if (_state.TicksSinceKill > _rules.Conditions.KillStreakResetSeconds * GameLoop.TicksPerSecond)
                _state.KillStreak = 0;
        }

        private void CarryOverkill(BigNumber overkill, StatTags tags)
        {
            var share = System.Math.Min(1, _hero.Value(CombatStats.OverkillCarry, 0, tags));
            if (share > 0 && overkill.Sign > 0)
                _state.QueueDamage[0] += overkill * share;
        }

        private void KeyHitLightning()
        {
            var share = _hero.Value(CombatStats.KeyHitLightning, 0, StatTags.Skill);
            if (share > 0)
                Hit(DamageSource.Skill, _rules.KeyDamage, StatTags.Skill, HitFlags.None, share);
        }

        private double HeavyStrike(ref HitFlags flags)
        {
            _state.KeyHits++;
            var every = (long)System.Math.Round(_hero.Value(CombatStats.KeyStrikeEvery, 0, StatTags.Keyboard));
            if (every < 1 || _state.KeyHits % every != 0)
                return 1;
            flags |= HitFlags.HeavyStrike;
            return System.Math.Max(1, _hero.Value(CombatStats.KeyStrikeMultiplier, 0, StatTags.Keyboard));
        }

        public double CritChance(StatTags tags)
        {
            var chance = _hero.Compute(CombatStats.CritChance, BigNumber.FromDouble(_rules.CritChance), tags | StatTags.Crit).ToDouble();
            return System.Math.Min(1, System.Math.Max(0, chance));
        }

        public double CritMultiplier(StatTags tags) =>
            System.Math.Max(1, _hero.Compute(CombatStats.CritMultiplier, BigNumber.FromDouble(_rules.CritMultiplier), tags | StatTags.Crit).ToDouble());

        private bool ForcedCrit()
        {
            var every = (long)System.Math.Round(_hero.Value(CombatStats.CritEvery, 0));
            if (every < 1)
            {
                _hitsTowardCrit = 0;
                return false;
            }
            return ++_hitsTowardCrit % every == 0;
        }

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
