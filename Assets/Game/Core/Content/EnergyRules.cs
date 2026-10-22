namespace Game.Core.Content
{
    public sealed class EnergyRules
    {
        public EnergyRules(double perKey, double perClick, double mousePxPerPoint, double max)
        {
            PerKey = perKey;
            PerClick = perClick;
            MousePxPerPoint = mousePxPerPoint;
            Max = max;
        }

        public double PerKey { get; }
        public double PerClick { get; }
        public double MousePxPerPoint { get; }
        public double Max { get; }

        public static EnergyRules Read(ContentNode node) =>
            new EnergyRules(
                node.Field("perKey").AsNumber(0, 1000),
                node.Field("perClick").AsNumber(0, 1000),
                node.Field("mousePxPerPoint").AsNumber(1, 1e6),
                node.Field("max").AsNumber(1, 1e6));
    }
}
