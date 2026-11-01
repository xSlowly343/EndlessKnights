namespace Game.Core.Sim
{
    public sealed class ShardcallerState
    {
        public int Crystals { get; set; }
        public int Size { get; set; }
        public int StrikeProgress { get; set; }
        public int GrowTicks { get; set; }
        public int LostCrystals { get; set; }
        public int LostSize { get; set; }
        public bool SpawnPending { get; set; }
        public long Detonations { get; set; }
        public long Pulses { get; set; }
        public long Blooms { get; set; }
        public int ShellTicks { get; set; }
        public int CrackTicks { get; set; }
        public long Cracks { get; set; }
        public bool Shatter { get; set; }
        public int PrisonTicks { get; set; }
        public int HeartTicks { get; set; }

        public bool Cracked => CrackTicks > 0;

        public void OnSpawn()
        {
            LostCrystals = Crystals;
            LostSize = Size;
            Crystals = 0;
            Size = 0;
            StrikeProgress = 0;
            PrisonTicks = 0;
            SpawnPending = true;
        }
    }
}
