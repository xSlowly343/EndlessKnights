using System.Diagnostics;
using Game.Platform.Windows.Timing;
using Game.Presentation.Strip;
using UnityEngine;

namespace Game.Bootstrap
{
    [DefaultExecutionOrder(int.MaxValue)]
    public sealed class FramePacingDriver : MonoBehaviour
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private FramePacer _pacer = null!;

        public static FramePacingDriver Attach(GameObject host, int framesPerSecond)
        {
            FrameRateLimit.DisableEngineLimit();
            var driver = host.AddComponent<FramePacingDriver>();
            driver._pacer = new FramePacer(framesPerSecond);
            return driver;
        }

        public void SetFrameRate(int framesPerSecond)
        {
            _pacer.SetFrameRate(framesPerSecond);
        }

        private void LateUpdate()
        {
            PreciseSleep.For(_pacer.SecondsToWait(_clock.Elapsed.TotalSeconds));
        }
    }
}
