namespace Game.Core.Sim.Headless
{
    public readonly struct BotProfile
    {
        public readonly double KeysPerSecond;
        public readonly double ClicksPerSecond;
        public readonly double MousePxPerSecond;
        public readonly double RhythmCv;
        public readonly double CircleTurnsPerSecond;

        public BotProfile(double keysPerSecond, double clicksPerSecond = 0, double mousePxPerSecond = 0, double rhythmCv = 0.4, double circleTurnsPerSecond = 0)
        {
            CircleTurnsPerSecond = circleTurnsPerSecond;
            KeysPerSecond = keysPerSecond;
            ClicksPerSecond = clicksPerSecond;
            MousePxPerSecond = mousePxPerSecond;
            RhythmCv = rhythmCv;
        }
    }
}
