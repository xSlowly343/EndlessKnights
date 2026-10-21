namespace Game.Platform.Windows.Input
{
    public sealed class FlickDetector
    {
        public const double DefaultWindowMilliseconds = 80;
        public const double DefaultSpeedPxPerSecond = 4000;

        private readonly double _windowMilliseconds;
        private readonly double _speedPxPerSecond;
        private double _windowStart = double.NaN;
        private double _windowDistance;
        private bool _flickInProgress;

        public FlickDetector(double windowMilliseconds = DefaultWindowMilliseconds, double speedPxPerSecond = DefaultSpeedPxPerSecond)
        {
            _windowMilliseconds = windowMilliseconds;
            _speedPxPerSecond = speedPxPerSecond;
        }

        public bool Add(double distancePx, double timeMilliseconds)
        {
            if (double.IsNaN(_windowStart) || timeMilliseconds - _windowStart > _windowMilliseconds)
            {
                CloseWindow();
                _windowStart = timeMilliseconds;
                _windowDistance = 0;
            }

            _windowDistance += distancePx;
            var requiredDistance = _speedPxPerSecond * _windowMilliseconds / 1000.0;
            if (_flickInProgress || _windowDistance < requiredDistance)
                return false;

            _flickInProgress = true;
            return true;
        }

        private void CloseWindow()
        {
            var calmDistance = _speedPxPerSecond * _windowMilliseconds / 1000.0 / 2;
            if (_windowDistance < calmDistance)
                _flickInProgress = false;
        }
    }
}
