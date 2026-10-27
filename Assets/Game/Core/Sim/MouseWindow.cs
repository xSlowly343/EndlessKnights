namespace Game.Core.Sim
{
    public sealed class MouseWindow
    {
        public const int Seconds = 10;
        public const int Capacity = Seconds * GameLoop.TicksPerSecond;

        private readonly double[] _px = new double[Capacity];
        private int _next;

        public double Total => Last(Capacity);

        public void Record(double px)
        {
            _px[_next] = System.Math.Max(0, px);
            _next = (_next + 1) % Capacity;
        }

        public double Last(long ticks)
        {
            var count = (int)System.Math.Min(Capacity, System.Math.Max(0, ticks));
            var sum = 0.0;
            for (var i = 1; i <= count; i++)
                sum += _px[(_next - i + Capacity) % Capacity];
            return sum;
        }

        public void Clear()
        {
            System.Array.Clear(_px, 0, Capacity);
            _next = 0;
        }
    }
}
