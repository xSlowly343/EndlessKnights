using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class FocusSystem
    {
        private readonly GameState _state;
        private FocusRules _rules;

        public FocusSystem(FocusRules rules, GameState state)
        {
            _rules = rules;
            _state = state;
            _state.Focus = System.Math.Max(_state.Focus, rules.Min);
            _state.FocusMultiplier = _state.Focus;
        }

        public void UseRules(FocusRules rules) => _rules = rules;

        public bool MacroDetected => _state.MacroRhythmTicks >= SecondsToTicks(_rules.MacroAfterSeconds);

        public void Tick(in EffectiveInput input)
        {
            TrackIdle(input);
            TrackRhythm(input.RhythmCv);

            if (_state.IdleTicks > SecondsToTicks(_rules.ResetAfterIdleSeconds))
                _state.Focus = _rules.Min;
            else
                _state.Focus = Clamp(_state.Focus + _rules.PerEvent * Events(input));

            _state.FocusMultiplier = MacroDetected ? _rules.Min : _state.Focus;
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

        private double Clamp(double focus) => System.Math.Min(_rules.Max, System.Math.Max(_rules.Min, focus));

        private static long SecondsToTicks(double seconds) => (long)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
