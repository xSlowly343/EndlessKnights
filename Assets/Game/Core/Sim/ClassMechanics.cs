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
            if (!_state.Is(HeroClass.Ronin))
                return 1;

            _state.Combo++;
            _state.ComboIdleTicks = 0;
            if (_state.Combo % _rules.ComboStrikeEvery != 0)
                return 1;
            flags = HitFlags.ComboStrike;
            _state.LastComboStrikeTick = _state.Tick;
            var whirl = _rules.WhirlResource + _hero.Value(CombatStats.ComboStrikeResource, 0, StatTags.Keyboard);
            _state.PendingResource += whirl * _hero.Value(CombatStats.ResourceGain, 1, StatTags.Keyboard);
            return _rules.ComboStrikeMultiplier + _hero.Value(CombatStats.ComboStrikeMultiplier, 0, StatTags.Keyboard);
        }

        public void EndTick(int keyHits, double mousePx)
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
                _state.ComboIdleTicks = 0;
            }
        }
    }
}
