namespace Game.Core.Input
{
    public readonly struct InputSnapshot
    {
        public static readonly InputSnapshot Empty = default;

        public readonly int KeyDowns;
        public readonly int EnterDowns;
        public readonly int BackspaceDowns;
        public readonly int SpaceDowns;
        public readonly int LeftClicks;
        public readonly int RightClicks;
        public readonly int MiddleClicks;
        public readonly int WheelTicks;
        public readonly float MouseDistancePx;
        public readonly int Flicks;
        public readonly float CircleTurns;
        public readonly double SecondsSinceLastInput;
        public readonly float RhythmCv;
        public readonly bool Suppressed;
        public readonly int ComboDowns;
        public readonly int Reversals;

        public InputSnapshot(
            int keyDowns = 0,
            int enterDowns = 0,
            int backspaceDowns = 0,
            int spaceDowns = 0,
            int leftClicks = 0,
            int rightClicks = 0,
            int middleClicks = 0,
            int wheelTicks = 0,
            float mouseDistancePx = 0f,
            int flicks = 0,
            float circleTurns = 0f,
            double secondsSinceLastInput = 0d,
            float rhythmCv = 0f,
            bool suppressed = false,
            int comboDowns = 0,
            int reversals = 0)
        {
            Reversals = reversals;
            ComboDowns = comboDowns;
            KeyDowns = keyDowns;
            EnterDowns = enterDowns;
            BackspaceDowns = backspaceDowns;
            SpaceDowns = spaceDowns;
            LeftClicks = leftClicks;
            RightClicks = rightClicks;
            MiddleClicks = middleClicks;
            WheelTicks = wheelTicks;
            MouseDistancePx = mouseDistancePx;
            Flicks = flicks;
            CircleTurns = circleTurns;
            SecondsSinceLastInput = secondsSinceLastInput;
            RhythmCv = rhythmCv;
            Suppressed = suppressed;
        }

        public bool HasAnyInput =>
            KeyDowns > 0 || LeftClicks > 0 || RightClicks > 0 || MiddleClicks > 0 || WheelTicks > 0 || MouseDistancePx > 0f;
    }
}
