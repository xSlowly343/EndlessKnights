namespace Game.Core.Sim
{
    public sealed class CaravanState
    {
        public double SeenMouseMeters { get; set; } = -1;
        public double Traveled { get; set; }
        public int Next { get; set; }
        public int Laps { get; set; }
        public int Waiting { get; set; }
        public long Arrivals { get; set; }
        public string LastStop { get; set; } = string.Empty;
    }
}
