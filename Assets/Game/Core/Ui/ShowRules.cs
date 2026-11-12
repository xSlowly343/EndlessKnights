namespace Game.Core.Ui
{
    public readonly struct ShowRules
    {
        public ShowRules(int capacity, double waitSeconds, double gapSeconds, double fullSeconds, double urgentSeconds)
        {
            Capacity = capacity;
            WaitSeconds = waitSeconds;
            GapSeconds = gapSeconds;
            FullSeconds = fullSeconds;
            UrgentSeconds = urgentSeconds;
        }

        public static ShowRules OwnSkills => new ShowRules(3, 0.3, 0, 0.4, 0.7);

        public static ShowRules PartySkills => new ShowRules(2, 1.5, 0.5, 0.4, 0.4);

        public int Capacity { get; }
        public double WaitSeconds { get; }
        public double GapSeconds { get; }
        public double FullSeconds { get; }
        public double UrgentSeconds { get; }
    }
}
