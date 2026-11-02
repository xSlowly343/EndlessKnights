namespace Game.Core.Sim
{
    public sealed class HerbwitchState
    {
        public const int Capacity = 8;

        public int[] VineStage { get; } = new int[Capacity];
        public int[] VineGrowTicks { get; } = new int[Capacity];
        public int Vines { get; set; }
        public int SeedHits { get; set; }
        public long Seeds { get; set; }
        public long Flowers { get; set; }
        public double VineCarry { get; set; }
        public int GardenResendTicks { get; set; }
        public double Venom { get; set; }
        public double VenomCarryOver { get; set; }
        public double VenomTickCarry { get; set; }
        public double Brew { get; set; }
        public int DrinkHits { get; set; }
        public long Drinks { get; set; }
        public int PartyPoisonTicks { get; set; }
        public double PoisonCarry { get; set; }
        public bool Spawned { get; set; }
        public int FlowerCarry { get; set; }

        public void OnSpawn() => Spawned = true;
    }
}
