namespace Game.Core.Ui
{
    public readonly struct MilestoneCue
    {
        public MilestoneCue(MilestoneKind kind, int variant, long value)
        {
            Kind = kind;
            Variant = variant;
            Value = value;
        }

        public MilestoneKind Kind { get; }
        public int Variant { get; }
        public long Value { get; }
        public MilestoneSize Size => MilestoneShow.SizeOf(Kind);
        public double Seconds => MilestoneShow.SecondsOf(Kind);
    }
}
