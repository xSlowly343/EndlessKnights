namespace Game.Core.Content
{
    public sealed class BondRules
    {
        public static readonly BondRules None = new BondRules(0, 0);

        public BondRules(double resourceAbove, double auraDuration)
        {
            ResourceAbove = resourceAbove;
            AuraDuration = auraDuration;
        }

        public double ResourceAbove { get; }
        public double AuraDuration { get; }

        public static BondRules Read(ContentNode node) =>
            new BondRules(node.Field("resourceAbove").AsNumber(0, 1e6), node.Field("auraDuration").AsNumber(0, 100));
    }
}
