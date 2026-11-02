namespace Game.Core.Sim
{
    public sealed class MothcallerState
    {
        public int Moths { get; set; }
        public double SwarmCarry { get; set; }
        public int DecayTicks { get; set; }
        public long MothsGiven { get; set; }
        public int PollenRelayTicks { get; set; }
        public int MothTicks { get; set; }
        public int AwakeTicks { get; set; }
        public double MothCarry { get; set; }
        public long Wakes { get; set; }
        public int ClockRelayTicks { get; set; }
        public long StreakSeconds { get; set; }
        public bool BossSeen { get; set; }

        public void OnSpawn() => BossSeen = false;
    }
}
