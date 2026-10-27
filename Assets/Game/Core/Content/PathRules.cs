namespace Game.Core.Content
{
    public sealed class PathRules
    {
        public PathRules(double balanceWindowSeconds)
        {
            BalanceWindowSeconds = balanceWindowSeconds;
        }

        public double BalanceWindowSeconds { get; }

        public static PathRules Read(ContentNode node) =>
            new PathRules(node.Field("balanceWindowSeconds").AsNumber(0.1, 3600));
    }
}
