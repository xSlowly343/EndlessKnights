namespace Game.Core.Content
{
    public sealed class SkeletonRules
    {
        public SkeletonRules(int max, double lantern)
        {
            Max = max;
            Lantern = lantern;
        }

        public int Max { get; }
        public double Lantern { get; }

        public static SkeletonRules Read(ContentNode node) =>
            new SkeletonRules(
                node.Field("max").AsInt(0, Sim.MinionSystem.Capacity),
                node.Field("lantern").AsNumber(1, 100));
    }
}
