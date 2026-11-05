namespace Game.Core.Sim
{
    public sealed class SeasonPassState
    {
        public long UnsentPoints { get; set; }
        public long CountedMinutes { get; set; } = -1;
        public int Season { get; set; } = -1;
        public long Points { get; set; }
        public bool Premium { get; set; }
    }
}
