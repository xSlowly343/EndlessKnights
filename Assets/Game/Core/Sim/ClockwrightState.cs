namespace Game.Core.Sim
{
    public sealed class ClockwrightState
    {
        public int HoldTicks { get; set; }
        public int BurstTicks { get; set; }
        public long Bursts { get; set; }
        public int LoopTicks { get; set; }
        public long Loops { get; set; }
        public int GearHits { get; set; }
        public long GearTicks { get; set; }
        public long Meshes { get; set; }
        public int IdleTicks { get; set; }
        public long OwnCastTick { get; set; } = long.MinValue / 2;
        public long PreviousOwnCastTick { get; set; } = long.MinValue / 2;
        public long LastMeshTick { get; set; } = long.MinValue / 2;
        public double PartySlow { get; set; }
    }
}
