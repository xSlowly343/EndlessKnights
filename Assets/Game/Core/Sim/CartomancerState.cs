namespace Game.Core.Sim
{
    public sealed class CartomancerState
    {
        public const int Suits = 4;
        public const int Cups = 0;
        public const int Swords = 1;
        public const int Coins = 2;
        public const int Wands = 3;

        public int[] Hand { get; } = new int[Suits];
        public int[] ArcanaTicks { get; } = new int[Suits];
        public int[] Laid { get; } = new int[Suits];
        public int DrawHits { get; set; }
        public long Draws { get; set; }
        public long Arcana { get; set; }
        public long AllyCards { get; set; }
        public bool CoinsApplied { get; set; }
        public int ArcanaSecondTicks { get; set; }
        public int DealOfferTicks { get; set; }
        public int TermTicks { get; set; }
        public double GoldAdded { get; set; }
        public bool ExactPaid { get; set; }
        public bool Penalty { get; set; }
        public int Streak { get; set; }
        public long Wins { get; set; }
        public long Losses { get; set; }
        public bool BossSeen { get; set; }
        public int LayHits { get; set; }
        public long Lays { get; set; }
        public double RevealHits { get; set; }
        public int RevealSuit { get; set; } = -1;
        public int RevealTicks { get; set; }
        public long Reveals { get; set; }
        public double CupsCarry { get; set; }
        public int EffectResendTicks { get; set; }
        public bool Spawned { get; set; }

        public int HandCount => Hand[0] + Hand[1] + Hand[2] + Hand[3];
        public int LaidCount => Laid[0] + Laid[1] + Laid[2] + Laid[3];

        public void OnSpawn()
        {
            CoinsApplied = false;
            BossSeen = false;
            Spawned = true;
        }
    }
}
