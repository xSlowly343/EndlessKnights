using System;

namespace Game.Platform.Windows.Input
{
    public sealed class JigglerFilter
    {
        public const double DefaultWindowMilliseconds = 250;
        public const double DefaultMinDisplacementPx = 8;

        private readonly double _windowMilliseconds;
        private readonly double _minDisplacementPx;
        private double _windowStart = double.NaN;
        private double _pathPx;
        private double _netX;
        private double _netY;

        public JigglerFilter(double windowMilliseconds = DefaultWindowMilliseconds, double minDisplacementPx = DefaultMinDisplacementPx)
        {
            _windowMilliseconds = windowMilliseconds;
            _minDisplacementPx = minDisplacementPx;
        }

        public double Add(double dx, double dy, double timeMilliseconds)
        {
            var accepted = 0.0;
            if (!double.IsNaN(_windowStart) && timeMilliseconds - _windowStart >= _windowMilliseconds)
                accepted = Flush();
            if (double.IsNaN(_windowStart))
                _windowStart = timeMilliseconds;

            _pathPx += Math.Sqrt(dx * dx + dy * dy);
            _netX += dx;
            _netY += dy;
            return accepted;
        }

        public double Flush()
        {
            var displacement = Math.Sqrt(_netX * _netX + _netY * _netY);
            var accepted = displacement >= _minDisplacementPx ? _pathPx : 0.0;
            _windowStart = double.NaN;
            _pathPx = 0;
            _netX = 0;
            _netY = 0;
            return accepted;
        }
    }
}
