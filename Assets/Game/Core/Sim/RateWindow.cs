namespace Game.Core.Sim
{
    public sealed class RateWindow
    {
        private readonly double[] _samples;
        private double _sum;
        private int _next;

        public RateWindow(int ticks)
        {
            _samples = new double[ticks];
        }

        public double WindowSeconds => _samples.Length * GameLoop.TickSeconds;

        public double PushAndGetRate(double amount)
        {
            _sum += amount - _samples[_next];
            _samples[_next] = amount;
            _next = (_next + 1) % _samples.Length;
            if (_sum < 0)
                _sum = 0;
            return _sum / WindowSeconds;
        }

        public void Reset()
        {
            System.Array.Clear(_samples, 0, _samples.Length);
            _sum = 0;
            _next = 0;
        }
    }
}
