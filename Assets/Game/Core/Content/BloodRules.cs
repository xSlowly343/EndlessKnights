namespace Game.Core.Content
{
    public sealed class BloodRules
    {
        public static readonly BloodRules None = new BloodRules(0, 0, 0);

        public BloodRules(double perFlick, double max, double hitsPerStack)
        {
            PerFlick = perFlick;
            Max = max;
            HitsPerStack = hitsPerStack;
        }

        public double PerFlick { get; }
        public double Max { get; }
        public double HitsPerStack { get; }

        public static BloodRules Read(ContentNode node) =>
            new BloodRules(node.Field("perFlick").AsNumber(0, 1000), node.Field("max").AsNumber(0, 1e6), node.Field("hitsPerStack").AsNumber(0, 1000));
    }
}
