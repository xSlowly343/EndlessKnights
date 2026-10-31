namespace Game.Core.Content
{
    public sealed class StandingAuraRules
    {
        public static readonly StandingAuraRules None = new StandingAuraRules(0, 0);

        public StandingAuraRules(int count, double pauseSeconds)
        {
            Count = count;
            PauseSeconds = pauseSeconds;
        }

        public int Count { get; }
        public double PauseSeconds { get; }

        public static StandingAuraRules Read(ContentNode node) => new StandingAuraRules(
            node.Field("count").AsInt(0, 10),
            node.Field("pauseSeconds").AsNumber(0, 600));
    }
}
