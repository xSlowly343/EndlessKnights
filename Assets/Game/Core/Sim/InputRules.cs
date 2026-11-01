using Game.Core.Content;
using Game.Core.Input;

namespace Game.Core.Sim
{
    public sealed class InputRules
    {
        public const int WindowTicks = GameLoop.TicksPerSecond;

        private readonly RateWindow _hits = new RateWindow(WindowTicks);
        private readonly RateWindow _mouse = new RateWindow(WindowTicks);
        private InputCaps _caps;

        public InputRules(InputCaps caps)
        {
            _caps = caps;
        }

        public void UseCaps(InputCaps caps) => _caps = caps;

        public EffectiveInput Apply(in InputSnapshot snapshot)
        {
            if (snapshot.Suppressed)
            {
                _hits.PushAndGetRate(0);
                _mouse.PushAndGetRate(0);
                return default;
            }

            var rawHits = snapshot.KeyDowns + MouseButtonHits(snapshot);
            var hits = Scale(rawHits, _hits.PushAndGetRate(rawHits), _caps.Hits);
            var mouseRate = _mouse.PushAndGetRate(snapshot.MouseDistancePx);
            var mouse = mouseRate > _caps.MaxMousePxPerSecond
                ? snapshot.MouseDistancePx * (_caps.MaxMousePxPerSecond / mouseRate)
                : snapshot.MouseDistancePx;

            return new EffectiveInput(
                hits,
                snapshot.EnterDowns,
                snapshot.LeftClicks,
                snapshot.RightClicks,
                snapshot.MiddleClicks,
                snapshot.WheelTicks,
                mouse,
                snapshot.Flicks,
                snapshot.CircleTurns,
                snapshot.RhythmCv,
                snapshot.SpaceDowns,
                snapshot.BackspaceDowns,
                snapshot.ComboDowns,
                snapshot.Reversals);
        }

        private static int MouseButtonHits(in InputSnapshot snapshot) =>
            snapshot.LeftClicks + snapshot.RightClicks + snapshot.MiddleClicks;

        private static double Scale(double amount, double ratePerSecond, in RateCap cap)
        {
            if (amount <= 0 || ratePerSecond <= 0)
                return 0;
            return amount * (cap.Effective(ratePerSecond) / ratePerSecond);
        }
    }
}
