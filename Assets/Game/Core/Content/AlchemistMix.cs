namespace Game.Core.Content
{
    public enum AlchemistMixKind : byte
    {
        Blast,
        Corrode,
        Resource,
        Stop,
        Crit,
        Gold,
        Cut
    }

    public readonly struct AlchemistMixEffect
    {
        public AlchemistMixEffect(AlchemistMixKind kind, double value, double seconds)
        {
            Kind = kind;
            Value = value;
            Seconds = seconds;
        }

        public AlchemistMixKind Kind { get; }
        public double Value { get; }
        public double Seconds { get; }
    }
}
