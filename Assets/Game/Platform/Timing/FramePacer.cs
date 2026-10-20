using System;

namespace Game.Platform.Windows.Timing
{
    public sealed class FramePacer
    {
        private double _interval;
        private double _nextFrameStart = double.NaN;

        public FramePacer(int framesPerSecond)
        {
            SetFrameRate(framesPerSecond);
        }

        public void SetFrameRate(int framesPerSecond)
        {
            if (framesPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond), framesPerSecond, "Frame rate must be positive.");
            _interval = 1.0 / framesPerSecond;
            _nextFrameStart = double.NaN;
        }

        public double SecondsToWait(double now)
        {
            if (double.IsNaN(_nextFrameStart) || now - _nextFrameStart > _interval)
                _nextFrameStart = now;

            _nextFrameStart += _interval;
            var wait = _nextFrameStart - now;
            return wait > 0 ? wait : 0;
        }
    }
}
