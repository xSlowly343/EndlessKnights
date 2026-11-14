namespace Game.Core.Ui
{
    public enum DayPhase
    {
        Morning,
        Day,
        Evening,
        Night
    }

    public readonly struct DayLight
    {
        public DayLight(DayPhase from, DayPhase to, double blend)
        {
            From = from;
            To = to;
            Blend = blend;
        }

        public DayPhase From { get; }
        public DayPhase To { get; }
        public double Blend { get; }
        public bool IsNight => To == DayPhase.Night;

        public static DayLight Steady(DayPhase phase) => new DayLight(phase, phase, 1);
    }
}
