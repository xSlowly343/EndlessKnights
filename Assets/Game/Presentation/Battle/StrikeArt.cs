namespace Game.Presentation.Battle
{
    public sealed class StrikeArt
    {
        public StrikeArt(int look, HeroClip slash, HeroClip flight, HeroClip burst, bool runsOnGround, float arcPx, bool burstsOnGround)
        {
            Look = look;
            Slash = slash;
            Flight = flight;
            Burst = burst;
            RunsOnGround = runsOnGround;
            ArcPx = arcPx;
            BurstsOnGround = burstsOnGround;
        }

        public int Look { get; }
        public HeroClip Slash { get; }
        public HeroClip Flight { get; }
        public HeroClip Burst { get; }
        public bool RunsOnGround { get; }
        public float ArcPx { get; }
        public bool BurstsOnGround { get; }
    }
}
