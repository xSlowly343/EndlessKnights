using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ClassMechanics
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private ClassRules _rules;

        public ClassMechanics(ClassRules rules, GameState state, StatSheet hero)
        {
            _hero = hero;
            _rules = rules;
            _state = state;
        }

        public void UseRules(ClassRules rules) => _rules = rules;

        public double OnKeyHit(out HitFlags flags)
        {
            flags = HitFlags.None;
            if (_state.Is(HeroClass.Viking))
                return AncestorShield(ref flags);
            if (!_state.Is(HeroClass.Ronin))
                return 1;

            _state.Combo++;
            _state.ComboIdleTicks = 0;
            if (_state.Combo % _rules.BreathEvery == 0)
                _state.PendingResource += _rules.WhirlResource * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Keyboard);
            if (++_state.ComboTowardStrike < StrikeEvery)
                return 1;
            _state.ComboTowardStrike = 0;
            flags = HitFlags.ComboStrike;
            _state.LastComboStrikeTick = _state.Tick;
            _state.PendingResource += _hero.Value(CombatStats.ComboStrikeResource, 0, StatTags.Keyboard) * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Keyboard);
            return System.Math.Max(1, _rules.ComboStrikeMultiplier + _hero.Value(CombatStats.ComboStrikeMultiplier, 0, StatTags.Keyboard));
        }

        private double AncestorShield(ref HitFlags flags)
        {
            if (_rules.ShieldEvery <= 0 || ++_state.KeysTowardShield < ShieldEvery)
                return 1;
            _state.KeysTowardShield = 0;
            flags |= HitFlags.ShieldStrike;
            _state.StunTicks = System.Math.Max(_state.StunTicks, (int)System.Math.Round((_rules.ShieldStunSeconds + _hero.Value(CombatStats.StunSeconds, 0)) * GameLoop.TicksPerSecond));
            _state.PendingResource += System.Math.Max(0, _hero.Value(CombatStats.ShieldResource, 0, StatTags.Keyboard));
            return _rules.ShieldMultiplier * System.Math.Max(0, _hero.Value(CombatStats.ShieldDamage, 1, StatTags.Keyboard));
        }

        public int ShieldEvery => System.Math.Max(_rules.ShieldEveryMin, (int)System.Math.Round(_rules.ShieldEvery + _hero.Value(CombatStats.ShieldEvery, 0, StatTags.Keyboard)));

        public int StrikeEvery
        {
            get
            {
                var every = _rules.ComboStrikeEvery + _hero.Value(CombatStats.ComboStrikeEvery, 0, StatTags.Keyboard);
                if (_state.ResourceFill >= 1)
                    every += _hero.Value(CombatStats.ComboStrikeEveryAtFullResource, 0, StatTags.Keyboard);
                return System.Math.Max(_rules.ComboStrikeEveryMin, (int)System.Math.Round(every));
            }
        }

        public int StrikeTargets => System.Math.Max(0, _rules.ComboStrikeTargets + (int)System.Math.Round(_hero.Value(CombatStats.ComboStrikeTargets, 0, StatTags.Keyboard)));

        public double StrikeQueueShare => System.Math.Max(0, _hero.Value(CombatStats.ComboStrikeQueueShare, 1, StatTags.Keyboard));

        public void EndTick(int keyHits, double mousePx)
        {
            if (_state.StunTicks > 0)
                _state.StunTicks = _state.EnemyAlive ? _state.StunTicks - 1 : 0;
            Clockwork();
            EndCombo(keyHits, mousePx);
        }

        private void Clockwork()
        {
            var running = _state.Is(HeroClass.Clockmaker) && _rules.ClockworkSlow > 0
                && _state.Resource > _rules.ClockworkAbove + _hero.Value(CombatStats.ClockworkAbove, 0);
            _state.ClockworkSlow = running ? System.Math.Min(1, _rules.ClockworkSlow + System.Math.Max(0, _hero.Value(CombatStats.ClockworkSlow, 0))) : 0;
            _state.ClockworkFloor = _rules.ClockworkFloor;
        }

        private void EndCombo(int keyHits, double mousePx)
        {
            if (keyHits > 0 || _state.Combo == 0)
                return;
            if (mousePx > 0 && _state.Is(HeroClass.Ronin))
                return;
            if (_hero.Value(CombatStats.ComboHold, 0) > 0)
                return;
            _state.ComboIdleTicks++;
            var timeout = _rules.ComboTimeoutSeconds + _hero.Value(CombatStats.ComboTimeoutSeconds, 0, StatTags.Keyboard);
            if (_state.ComboIdleTicks > System.Math.Round(timeout * GameLoop.TicksPerSecond))
            {
                var keep = System.Math.Min(1, System.Math.Max(0, _hero.Value(CombatStats.ComboKeepShare, 0, StatTags.Keyboard)));
                _state.Combo = (int)System.Math.Floor(_state.Combo * keep);
                _state.ComboTowardStrike = (int)System.Math.Floor(_state.ComboTowardStrike * keep);
                _state.ComboIdleTicks = 0;
            }
        }
    }
}
