namespace Game.Core.Sim
{
    public readonly struct EffectiveInput
    {
        public readonly double Keys;
        public readonly int EnterDowns;
        public readonly double Clicks;
        public readonly int RightClicks;
        public readonly int MiddleClicks;
        public readonly int WheelTicks;
        public readonly double MousePx;
        public readonly int Flicks;
        public readonly double CircleTurns;
        public readonly double RhythmCv;

        public EffectiveInput(double keys, int enterDowns, double clicks, int rightClicks, int middleClicks, int wheelTicks,
            double mousePx, int flicks, double circleTurns, double rhythmCv)
        {
            Keys = keys;
            EnterDowns = enterDowns;
            Clicks = clicks;
            RightClicks = rightClicks;
            MiddleClicks = middleClicks;
            WheelTicks = wheelTicks;
            MousePx = mousePx;
            Flicks = flicks;
            CircleTurns = circleTurns;
            RhythmCv = rhythmCv;
        }

        public bool HasInput =>
            Keys > 0 || Clicks > 0 || RightClicks > 0 || MiddleClicks > 0 || WheelTicks > 0 || MousePx > 0;
    }
}
