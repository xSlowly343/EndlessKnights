namespace Game.Core.Sim
{
    public sealed class ApprenticeState
    {
        public string Building { get; set; } = string.Empty;
        public double Worked { get; set; }
        public double Progress { get; set; }
        public string Trait { get; set; } = string.Empty;
        public long Brought { get; set; }
    }
}
