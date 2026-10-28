using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class FocusSystem
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private FocusRules _rules;

        public FocusSystem(FocusRules rules, GameState state, StatSheet hero)
        {
            _rules = rules;
            _state = state;
            _hero = hero;
            _state.Focus = System.Math.Max(_state.Focus, rules.Min);
            _state.FocusMultiplier = _state.Focus;
        }

        public void UseRules(FocusRules rules) => _rules = rules;

        public bool MacroDetected => _state.MacroRhythmTicks >= SecondsToTicks(_rules.MacroAfterSeconds);

        public void Tick(in EffectiveInput input)
        {
            TrackIdle(input);
            TrackRhythm(input.RhythmCv);

            if (_state.IdleTicks > SecondsToTicks(ResetAfterIdleSeconds) && _hero.Value(CombatStats.FocusHold, 0) <= 0)
                _state.Focus = _rules.Min;
            else
                _state.Focus = Clamp(_state.Focus + _rules.PerEvent * _hero.Value(CombatStats.FocusGain, 1) * Events(input));

            if (_state.FocusLockTicks > 0)
                _state.FocusLockTicks--;
            _state.FocusMultiplier = MacroDetected || _state.FocusLockTicks > 0
                ? _rules.Min
                : _state.Focus * System.Math.Max(0, _hero.Value(CombatStats.FocusMultiplier, 1));
        }

        private void TrackIdle(in EffectiveInput input)
        {
            if (input.HasInput)
                _state.IdleTicks = 0;
            else
                _state.IdleTicks++;
        }

        private void TrackRhythm(double rhythmCv)
        {
            if (rhythmCv > 0 && rhythmCv < _rules.MacroCvBelow)
                _state.MacroRhythmTicks++;
            else
                _state.MacroRhythmTicks = 0;
        }

        private double Events(in EffectiveInput input) =>
            input.Keys + input.Clicks + input.MousePx / _rules.MousePxPerEvent;

        public double Max => System.Math.Max(_rules.Min, _rules.Max + _hero.Value(CombatStats.FocusMax, 0));

        public double ResetAfterIdleSeconds => System.Math.Max(0, _rules.ResetAfterIdleSeconds + _hero.Value(CombatStats.FocusResetSeconds, 0));

        private double Clamp(double focus) => System.Math.Min(Max, System.Math.Max(_rules.Min, focus));

        private static long SecondsToTicks(double seconds) => (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
