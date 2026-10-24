namespace Game.Platform.Windows.Input
{
    public sealed class ReversalDetector
    {
        public const double DefaultMinRunPx = 60;

        private readonly double _minRunPx;
        private readonly Axis _x = new Axis();
        private readonly Axis _y = new Axis();

        public ReversalDetector(double minRunPx = DefaultMinRunPx)
        {
            _minRunPx = minRunPx;
        }

        public int Add(double dx, double dy) => _x.Add(dx, _minRunPx) + _y.Add(dy, _minRunPx);

        private sealed class Axis
        {
            private int _sign;
            private double _run;
            private double _back;

            public int Add(double delta, double minRunPx)
            {
                if (delta == 0)
                    return 0;
                var sign = delta > 0 ? 1 : -1;
                var length = System.Math.Abs(delta);
                if (_sign == 0 || sign == _sign)
                {
                    _sign = sign;
                    _run += length;
                    _back = 0;
                    return 0;
                }
                _back += length;
                if (_back < minRunPx)
                    return 0;
                var reversed = _run >= minRunPx ? 1 : 0;
                _sign = sign;
                _run = _back;
                _back = 0;
                return reversed;
            }
        }
    }
}
