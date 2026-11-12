namespace Game.Presentation.Battle
{
    public sealed class StrikeArt
    {
        public StrikeArt(int look, HeroClip slash, HeroClip flight, HeroClip burst, bool runsOnGround, float arcPx, bool burstsOnGround, HeroClip[]? tiers = null)
        {
            Look = look;
            Slash = slash;
            Flight = flight;
            Burst = burst;
            RunsOnGround = runsOnGround;
            ArcPx = arcPx;
            BurstsOnGround = burstsOnGround;
            _tiers = tiers != null && tiers.Length == TierCount ? tiers : null;
        }

        public const int TierCount = 3;

        private readonly HeroClip[]? _tiers;

        public int Look { get; }
        public HeroClip Slash { get; }
        public HeroClip Flight { get; }
        public HeroClip Burst { get; }
        public bool RunsOnGround { get; }
        public float ArcPx { get; }
        public bool BurstsOnGround { get; }
        public bool HasTiers => _tiers != null;

        public HeroClip BurstOf(int tier)
        {
            if (_tiers == null)
                return Burst;
            return _tiers[System.Math.Min(TierCount, System.Math.Max(1, tier)) - 1];
        }
    }
}
