namespace Game.Core.Sim
{
    public readonly struct EffectiveInput
    {
        public readonly double Hits;
        public readonly int EnterDowns;
        public readonly int LeftClicks;
        public readonly int RightClicks;
        public readonly int MiddleClicks;
        public readonly int WheelTicks;
        public readonly double MousePx;
        public readonly int Flicks;
        public readonly double CircleTurns;
        public readonly double RhythmCv;
        public readonly int SpaceDowns;
        public readonly int BackspaceDowns;
        public readonly int ComboDowns;
        public readonly int Reversals;

        public EffectiveInput(double hits, int enterDowns, int leftClicks, int rightClicks, int middleClicks, int wheelTicks,
            double mousePx, int flicks, double circleTurns, double rhythmCv, int spaceDowns = 0, int backspaceDowns = 0, int comboDowns = 0, int reversals = 0)
        {
            Reversals = reversals;
            SpaceDowns = spaceDowns;
            BackspaceDowns = backspaceDowns;
            ComboDowns = comboDowns;
            Hits = hits;
            EnterDowns = enterDowns;
            LeftClicks = leftClicks;
            RightClicks = rightClicks;
            MiddleClicks = middleClicks;
            WheelTicks = wheelTicks;
            MousePx = mousePx;
            Flicks = flicks;
            CircleTurns = circleTurns;
            RhythmCv = rhythmCv;
        }

        public EffectiveInput WithMouse(double mousePx) =>
            new EffectiveInput(Hits, EnterDowns, LeftClicks, RightClicks, MiddleClicks, WheelTicks, mousePx, Flicks, CircleTurns, RhythmCv, SpaceDowns, BackspaceDowns, ComboDowns, Reversals);

        public int HeavyClicks => RightClicks + MiddleClicks;

        public int ButtonPresses => LeftClicks + RightClicks + MiddleClicks;

        public bool HasInput =>
            Hits > 0 || ButtonPresses > 0 || WheelTicks > 0 || MousePx > 0;
    }
}
