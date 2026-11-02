namespace Game.Core.Sim
{
    public sealed class DowserState
    {
        public int VeinAt { get; set; }
        public int SecondVeinAt { get; set; }
        public double Hits { get; set; }
        public int VeinsStruck { get; set; }
        public long Veins { get; set; }
        public int CritTicks { get; set; }
        public double Luck { get; set; }
        public long Finds { get; set; }
        public double LuckGold { get; set; }
        public int HourTicks { get; set; }
        public int Nuggets { get; set; }
        public long Chests { get; set; }
        public bool SpawnPending { get; set; }

        public void OnSpawn() => SpawnPending = true;
    }
}
