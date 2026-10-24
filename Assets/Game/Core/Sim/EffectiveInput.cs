namespace Game.Core.Sim
{
    public readonly struct EffectiveInput
    {
        public readonly double Keys;
        public readonly int EnterDowns;
        public readonly double Clicks;
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

        public EffectiveInput(double keys, int enterDowns, double clicks, int leftClicks, int rightClicks, int middleClicks, int wheelTicks,
            double mousePx, int flicks, double circleTurns, double rhythmCv, int spaceDowns = 0, int backspaceDowns = 0, int comboDowns = 0, int reversals = 0)
        {
            Reversals = reversals;
            SpaceDowns = spaceDowns;
            BackspaceDowns = backspaceDowns;
            ComboDowns = comboDowns;
            Keys = keys;
            EnterDowns = enterDowns;
            Clicks = clicks;
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
            new EffectiveInput(Keys, EnterDowns, Clicks, LeftClicks, RightClicks, MiddleClicks, WheelTicks, mousePx, Flicks, CircleTurns, RhythmCv, SpaceDowns, BackspaceDowns, ComboDowns, Reversals);

        public int HeavyClicks => RightClicks + MiddleClicks;

        public bool HasInput =>
            Keys > 0 || Clicks > 0 || RightClicks > 0 || MiddleClicks > 0 || WheelTicks > 0 || MousePx > 0;
    }
}
