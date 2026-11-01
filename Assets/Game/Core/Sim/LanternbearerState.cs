namespace Game.Core.Sim
{
    public sealed class LanternbearerState
    {
        public double Light { get; set; }
        public int ShadowTicks { get; set; }
        public bool LitFull { get; set; }
        public bool LitOilGiven { get; set; }
        public int FlareTicks { get; set; }
        public long FullLights { get; set; }
        public double AllyHits { get; set; }
        public int FireflyHits { get; set; }
        public long FirefliesSent { get; set; }
        public int NextFireflySlot { get; set; }
        public int BeaconTicks { get; set; }
        public long Flashes { get; set; }
        public int StrikeTicks { get; set; }
        public int GlowTicks { get; set; }
        public double GlowPower { get; set; }
        public bool SpawnPending { get; set; }

        public void OnSpawn() => SpawnPending = true;
    }
}
