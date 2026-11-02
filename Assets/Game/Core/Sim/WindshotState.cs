namespace Game.Core.Sim
{
    public sealed class WindshotState
    {
        public int ShotProgress { get; set; }
        public long Shots { get; set; }
        public long LastHitTick { get; set; } = -1;
        public int AutoShotTicks { get; set; }
        public int StillTicks { get; set; }
        public int ForcedCalmTicks { get; set; }
        public bool WasCalm { get; set; }
        public bool CalmFirstPending { get; set; }
        public int StormHitsLeft { get; set; }
        public double GustPxCarry { get; set; }
        public int Gusts { get; set; }
        public int GustHitsLeft { get; set; }
        public int GustStormTicks { get; set; }
        public int GustTimerTicks { get; set; }
        public bool Calm { get; set; }
        public long GustsStarted { get; set; }
        public long Spawns { get; private set; }

        public bool GustActive => GustHitsLeft > 0 || GustStormTicks > 0;

        public void OnSpawn() => Spawns++;
    }
}
