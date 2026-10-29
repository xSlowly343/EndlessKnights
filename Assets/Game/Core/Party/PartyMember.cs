namespace Game.Core.Party
{
    public sealed class PartyMember
    {
        public ulong PlayerId { get; internal set; }
        public bool Joined { get; internal set; }
        public bool Peaceful { get; internal set; }
        public bool Connected { get; internal set; }
        public long LastInputTick { get; internal set; }
        public long DisconnectedAtTick { get; internal set; }
        public int Weight { get; internal set; }
        public double EnemyShare { get; internal set; }
        public long LastContributionTick { get; internal set; } = PartyEncounter.Never;
        public long RestBankTicks { get; internal set; }
        public byte HeroClass { get; internal set; }
        public int GuestTicksLeft { get; internal set; }
        public bool Guest => GuestTicksLeft > 0;

        internal void Reset()
        {
            PlayerId = 0;
            Joined = false;
            Peaceful = false;
            Connected = false;
            LastInputTick = 0;
            DisconnectedAtTick = 0;
            Weight = 0;
            EnemyShare = 0;
            LastContributionTick = PartyEncounter.Never;
            RestBankTicks = 0;
            HeroClass = 0;
            GuestTicksLeft = 0;
        }
    }
}
