namespace Game.Core.Content.Items
{
    public readonly struct TierRange
    {
        public TierRange(double low, double high)
        {
            Low = low;
            High = high;
        }

        public double Low { get; }
        public double High { get; }
        public bool Spreads => System.Math.Abs(High - Low) > 1e-9;
    }
}
