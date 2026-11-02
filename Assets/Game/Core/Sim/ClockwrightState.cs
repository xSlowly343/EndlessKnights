namespace Game.Core.Sim
{
    public sealed class ClockwrightState
    {
        public const int Slots = 8;

        public double SentWind { get; set; } = -1;
        public int SpringRelayTicks { get; set; }
        public int HoldTicks { get; set; }
        public int BurstTicks { get; set; }
        public long Bursts { get; set; }
        public int LoopTicks { get; set; }
        public long Loops { get; set; }
        public int GearHits { get; set; }
        public long GearTicks { get; set; }
        public long Meshes { get; set; }
        public int CallTicks { get; set; }
        public int IdleTicks { get; set; }
        public long OwnCastTick { get; set; } = long.MinValue / 2;
        public long PreviousOwnCastTick { get; set; } = long.MinValue / 2;
        public long LastMeshTick { get; set; } = long.MinValue / 2;
        public long[] CogTicks { get; } = NewCogTicks();

        private static long[] NewCogTicks()
        {
            var ticks = new long[Slots];
            for (var i = 0; i < ticks.Length; i++)
                ticks[i] = long.MinValue / 2;
            return ticks;
        }
    }
}
