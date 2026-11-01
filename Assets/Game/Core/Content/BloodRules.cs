namespace Game.Core.Content
{
    public sealed class BloodRules
    {
        public static readonly BloodRules None = new BloodRules(0, 0, 0);

        public BloodRules(double perThousandPx, double max, double hitsPerStack)
        {
            PerThousandPx = perThousandPx;
            Max = max;
            HitsPerStack = hitsPerStack;
        }

        public double PerThousandPx { get; }
        public double Max { get; }
        public double HitsPerStack { get; }

        public static BloodRules Read(ContentNode node) =>
            new BloodRules(node.Field("perThousandPx").AsNumber(0, 1000), node.Field("max").AsNumber(0, 1e6), node.Field("hitsPerStack").AsNumber(0, 1000));
    }
}
