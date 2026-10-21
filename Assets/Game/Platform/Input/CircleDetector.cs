using System;

namespace Game.Platform.Windows.Input
{
    public sealed class CircleDetector
    {
        public const double DefaultMinStepPx = 2;
        public const double DefaultPauseMilliseconds = 300;

        private const double FullTurn = 2 * Math.PI;
        private const double MaxTurnPerStep = 2 * Math.PI / 3;

        private readonly double _minStepPx;
        private readonly double _pauseMilliseconds;
        private double _lastX;
        private double _lastY;
        private bool _hasDirection;
        private double _lastTime = double.NaN;
        private double _angle;

        public CircleDetector(double minStepPx = DefaultMinStepPx, double pauseMilliseconds = DefaultPauseMilliseconds)
        {
            _minStepPx = minStepPx;
            _pauseMilliseconds = pauseMilliseconds;
        }

        public int Add(double dx, double dy, double timeMilliseconds)
        {
            if (!double.IsNaN(_lastTime) && timeMilliseconds - _lastTime > _pauseMilliseconds)
                Reset();
            _lastTime = timeMilliseconds;

            if (Math.Sqrt(dx * dx + dy * dy) < _minStepPx)
                return 0;

            if (!_hasDirection)
            {
                Remember(dx, dy);
                return 0;
            }

            var turn = Math.Atan2(_lastX * dy - _lastY * dx, _lastX * dx + _lastY * dy);
            if (Math.Abs(turn) > MaxTurnPerStep || (turn != 0 && _angle != 0 && Math.Sign(turn) != Math.Sign(_angle)))
                _angle = 0;
            if (Math.Abs(turn) > MaxTurnPerStep)
                turn = 0;
            _angle += turn;
            Remember(dx, dy);

            var turns = (int)(Math.Abs(_angle) / FullTurn);
            if (turns > 0)
                _angle -= Math.Sign(_angle) * turns * FullTurn;
            return turns;
        }

        private void Remember(double dx, double dy)
        {
            _lastX = dx;
            _lastY = dy;
            _hasDirection = true;
        }

        private void Reset()
        {
            _hasDirection = false;
            _angle = 0;
        }
    }
}
