namespace Game.Core.Sim
{
    public sealed class DistillState
    {
        public string Recipe { get; set; } = string.Empty;
        public bool Bloom { get; set; }
        public double Heat { get; set; }
        public double Pressure { get; set; }
        public long Done { get; set; }
        public long Bursts { get; set; }
        public long Miracles { get; set; }
    }

    public sealed class ColossusState
    {
        public double Cooldown { get; set; }
        public int SeedsWaiting { get; set; }
        public bool Planted { get; set; }
        public bool Grown { get; set; }
        public double Seconds { get; set; }
        public double Keys { get; set; }
        public double Clicks { get; set; }
        public double MouseMeters { get; set; }
        public double IdleSeconds { get; set; }
        public double SinceBloom { get; set; }
    }
}
