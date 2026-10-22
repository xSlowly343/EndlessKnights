using Game.Core.Input;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public sealed class BotInput : IInputSource
    {
        private const double MinJitter = 0.5;
        private const double MaxJitter = 1.5;

        private readonly BotProfile _profile;
        private readonly Rng _rng;
        private double _keyCarry;
        private double _clickCarry;

        public BotInput(BotProfile profile, ulong seed)
        {
            _profile = profile;
            _rng = new Rng(seed);
        }

        public InputSnapshot Drain()
        {
            var keys = Take(ref _keyCarry, _profile.KeysPerSecond);
            var clicks = Take(ref _clickCarry, _profile.ClicksPerSecond);
            var mouse = _profile.MousePxPerSecond * GameLoop.TickSeconds * Jitter();
            return new InputSnapshot(
                keyDowns: keys,
                leftClicks: clicks,
                mouseDistancePx: (float)mouse,
                rhythmCv: (float)_profile.RhythmCv);
        }

        private int Take(ref double carry, double perSecond)
        {
            if (perSecond <= 0)
                return 0;
            carry += perSecond * GameLoop.TickSeconds * Jitter();
            var whole = (int)System.Math.Floor(carry);
            carry -= whole;
            return whole;
        }

        private double Jitter() => MinJitter + (MaxJitter - MinJitter) * _rng.NextDouble();
    }
}
