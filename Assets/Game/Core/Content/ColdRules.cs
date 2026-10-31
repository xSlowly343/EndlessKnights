namespace Game.Core.Content
{
    public sealed class ColdRules
    {
        public static readonly ColdRules None = new ColdRules(0, 1, 0, 0);

        public ColdRules(double perKeyHit, double max, double fadePerSecond, double damageTaken)
        {
            PerKeyHit = perKeyHit;
            Max = max;
            FadePerSecond = fadePerSecond;
            DamageTaken = damageTaken;
        }

        public double PerKeyHit { get; }
        public double Max { get; }
        public double FadePerSecond { get; }
        public double DamageTaken { get; }

        public static ColdRules Read(ContentNode node) =>
            new ColdRules(node.Field("perKeyHit").AsNumber(0, 1000), node.Field("max").AsNumber(1, 1e6),
                node.Field("fadePerSecond").AsNumber(0, 1), node.Field("damageTaken").AsNumber(0, 10));
    }
}
