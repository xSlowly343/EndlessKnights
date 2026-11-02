namespace Game.Core.Sim
{
    public sealed class GolemancerState
    {
        public const int Capacity = 12;

        public int[] MinorTicks { get; } = new int[Capacity];
        public int SealHits { get; set; }
        public long Seals { get; set; }
        public int GolemTicks { get; set; }
        public int SecondGolemTicks { get; set; }
        public double GolemPower { get; set; }
        public double GolemCarry { get; set; }
        public int Runes { get; set; }
        public int RuneHits { get; set; }
        public double EternalCarry { get; set; }
        public int Brand { get; set; }
        public double BrandHits { get; set; }
        public long BrandPops { get; set; }
        public int BrandResendTicks { get; set; }
        public int MinorHits { get; set; }
        public int Minors { get; set; }
        public double MinorCarry { get; set; }
        public bool Spawned { get; set; }

        public void OnSpawn() => Spawned = true;
    }
}
