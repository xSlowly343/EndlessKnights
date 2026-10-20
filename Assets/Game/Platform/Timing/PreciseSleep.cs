using System.Runtime.InteropServices;
using System.Threading;

namespace Game.Platform.Windows.Timing
{
    public static class PreciseSleep
    {
        private const uint TimerResolutionMilliseconds = 1;
        private static bool _resolutionRaised;

        public static void For(double seconds)
        {
            if (!_resolutionRaised)
            {
                timeBeginPeriod(TimerResolutionMilliseconds);
                _resolutionRaised = true;
            }

            var milliseconds = (int)(seconds * 1000.0);
            if (milliseconds > 0)
                Thread.Sleep(milliseconds);
        }

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);
    }
}
