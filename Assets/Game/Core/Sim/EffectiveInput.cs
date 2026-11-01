namespace Game.Core.Sim
{
    public readonly struct EffectiveInput
    {
        public readonly double Hits;
        public readonly int LeftClicks;
        public readonly int RightClicks;
        public readonly int MiddleClicks;
        public readonly double MousePx;
        public readonly double RhythmCv;

        public EffectiveInput(double hits, int leftClicks, int rightClicks, int middleClicks, double mousePx, double rhythmCv = 0)
        {
            Hits = hits;
            LeftClicks = leftClicks;
            RightClicks = rightClicks;
            MiddleClicks = middleClicks;
            MousePx = mousePx;
            RhythmCv = rhythmCv;
        }

        public EffectiveInput WithMouse(double mousePx) =>
            new EffectiveInput(Hits, LeftClicks, RightClicks, MiddleClicks, mousePx, RhythmCv);

        public int ButtonPresses => LeftClicks + RightClicks + MiddleClicks;

        public bool HasInput => Hits > 0 || ButtonPresses > 0 || MousePx > 0;
    }
}
