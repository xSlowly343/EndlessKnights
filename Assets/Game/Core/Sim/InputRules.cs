using Game.Core.Content;
using Game.Core.Input;

namespace Game.Core.Sim
{
    public sealed class InputRules
    {
        public const int WindowTicks = GameLoop.TicksPerSecond;

        private readonly RateWindow _keys = new RateWindow(WindowTicks);
        private readonly RateWindow _clicks = new RateWindow(WindowTicks);
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
                _keys.PushAndGetRate(0);
                _clicks.PushAndGetRate(0);
                _mouse.PushAndGetRate(0);
                return default;
            }

            var keys = Scale(snapshot.KeyDowns, _keys.PushAndGetRate(snapshot.KeyDowns), _caps.Keys);
            var clicks = Scale(snapshot.LeftClicks, _clicks.PushAndGetRate(snapshot.LeftClicks), _caps.Clicks);
            var mouseRate = _mouse.PushAndGetRate(snapshot.MouseDistancePx);
            var mouse = mouseRate > _caps.MaxMousePxPerSecond
                ? snapshot.MouseDistancePx * (_caps.MaxMousePxPerSecond / mouseRate)
                : snapshot.MouseDistancePx;

            return new EffectiveInput(
                keys,
                snapshot.EnterDowns,
                clicks,
                snapshot.RightClicks,
                snapshot.MiddleClicks,
                snapshot.WheelTicks,
                mouse,
                snapshot.Flicks,
                snapshot.CircleTurns,
                snapshot.RhythmCv);
        }

        private static double Scale(double amount, double ratePerSecond, in RateCap cap)
        {
            if (amount <= 0 || ratePerSecond <= 0)
                return 0;
            return amount * (cap.Effective(ratePerSecond) / ratePerSecond);
        }
    }
}
