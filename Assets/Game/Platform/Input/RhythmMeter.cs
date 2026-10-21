using System;

namespace Game.Platform.Windows.Input
{
    public sealed class RhythmMeter
    {
        public const int DefaultCapacity = 200;
        public const int MinIntervalsForEstimate = 10;
        public const double DefaultMaxIntervalMilliseconds = 2000;

        private readonly double[] _intervals;
        private readonly double _maxIntervalMilliseconds;
        private double _lastEventTime = double.NaN;
        private int _next;
        private int _count;
        private double _sum;
        private double _sumOfSquares;

        public RhythmMeter(int capacity = DefaultCapacity, double maxIntervalMilliseconds = DefaultMaxIntervalMilliseconds)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            _intervals = new double[capacity];
            _maxIntervalMilliseconds = maxIntervalMilliseconds;
        }

        public void Record(double timeMilliseconds)
        {
            var previous = _lastEventTime;
            _lastEventTime = timeMilliseconds;
            if (double.IsNaN(previous))
                return;

            var interval = timeMilliseconds - previous;
            if (interval <= 0 || interval > _maxIntervalMilliseconds)
                return;

            if (_count == _intervals.Length)
            {
                var removed = _intervals[_next];
                _sum -= removed;
                _sumOfSquares -= removed * removed;
            }
            else
            {
                _count++;
            }

            _intervals[_next] = interval;
            _sum += interval;
            _sumOfSquares += interval * interval;
            _next = (_next + 1) % _intervals.Length;
        }

        public float CoefficientOfVariation()
        {
            if (_count < MinIntervalsForEstimate)
                return 0f;
            var mean = _sum / _count;
            var variance = Math.Max(0.0, _sumOfSquares / _count - mean * mean);
            return mean <= 0 ? 0f : (float)(Math.Sqrt(variance) / mean);
        }

        public bool HasEstimate => _count >= MinIntervalsForEstimate;
    }
}
