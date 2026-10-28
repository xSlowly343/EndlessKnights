namespace Game.Core.Content
{
    public sealed class PathRules
    {
        public PathRules(double balanceWindowSeconds, double doomSeconds)
        {
            BalanceWindowSeconds = balanceWindowSeconds;
            DoomSeconds = doomSeconds;
        }

        public double BalanceWindowSeconds { get; }
        public double DoomSeconds { get; }

        public static PathRules Read(ContentNode node) =>
            new PathRules(
                node.Field("balanceWindowSeconds").AsNumber(0.1, 3600),
                node.Field("doom").Field("seconds").AsNumber(0.1, 600));
    }
}
