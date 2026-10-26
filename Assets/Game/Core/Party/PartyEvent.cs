namespace Game.Core.Party
{
    public readonly struct PartyEvent
    {
        public readonly PartyEventKind Kind;
        public readonly long Tick;
        public readonly uint EnemyId;
        public readonly PartyEnemyKind EnemyKind;
        public readonly double Share0;
        public readonly double Share1;
        public readonly double Share2;
        public readonly double Share3;

        public PartyEvent(PartyEventKind kind, long tick, uint enemyId, PartyEnemyKind enemyKind, double share0 = 0, double share1 = 0, double share2 = 0, double share3 = 0)
        {
            Kind = kind;
            Tick = tick;
            EnemyId = enemyId;
            EnemyKind = enemyKind;
            Share0 = share0;
            Share1 = share1;
            Share2 = share2;
            Share3 = share3;
        }

        public double Contribution(int slot) => slot switch
        {
            0 => Share0,
            1 => Share1,
            2 => Share2,
            3 => Share3,
            _ => 0
        };
    }
}
