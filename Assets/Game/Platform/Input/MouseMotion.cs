using System;

namespace Game.Platform.Windows.Input
{
    public sealed class MouseMotion
    {
        public const ushort MoveAbsolute = 0x0001;
        public const ushort VirtualDesktop = 0x0002;
        public const int DefaultPointerSpeed = 10;

        private const double AbsoluteRange = 65535.0;

        private static readonly double[] PointerSpeedFactors =
        {
            1.0 / 32, 1.0 / 16, 1.0 / 8, 2.0 / 8, 3.0 / 8, 4.0 / 8, 5.0 / 8, 6.0 / 8, 7.0 / 8, 1.0,
            1.25, 1.5, 1.75, 2.0, 2.25, 2.5, 2.75, 3.0, 3.25, 3.5
        };

        private double _pointerSpeedFactor = 1.0;
        private bool _hasAbsolutePoint;
        private double _lastAbsoluteX;
        private double _lastAbsoluteY;

        public void SetPointerSpeed(int windowsPointerSpeed)
        {
            var index = Math.Max(1, Math.Min(PointerSpeedFactors.Length, windowsPointerSpeed)) - 1;
            _pointerSpeedFactor = PointerSpeedFactors[index];
        }

        public double DistancePx(ushort flags, int lastX, int lastY, int screenWidth, int screenHeight)
        {
            var (dx, dy) = DeltaPx(flags, lastX, lastY, screenWidth, screenHeight);
            return Length(dx, dy);
        }

        public (double Dx, double Dy) DeltaPx(ushort flags, int lastX, int lastY, int screenWidth, int screenHeight)
        {
            if ((flags & MoveAbsolute) == 0)
            {
                _hasAbsolutePoint = false;
                return (lastX * _pointerSpeedFactor, lastY * _pointerSpeedFactor);
            }

            var x = lastX / AbsoluteRange * screenWidth;
            var y = lastY / AbsoluteRange * screenHeight;
            var delta = _hasAbsolutePoint ? (x - _lastAbsoluteX, y - _lastAbsoluteY) : (0.0, 0.0);
            _lastAbsoluteX = x;
            _lastAbsoluteY = y;
            _hasAbsolutePoint = true;
            return delta;
        }

        private static double Length(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);
    }
}
