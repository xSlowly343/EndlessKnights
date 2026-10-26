namespace Game.Core.Content
{
    public sealed class CannonRules
    {
        public CannonRules(int max, double resourcePerShot, double broadsidePx, double broadsideBalls)
        {
            Max = max;
            ResourcePerShot = resourcePerShot;
            BroadsidePx = broadsidePx;
            BroadsideBalls = broadsideBalls;
        }

        public int Max { get; }
        public double ResourcePerShot { get; }
        public double BroadsidePx { get; }
        public double BroadsideBalls { get; }

        public static CannonRules Read(ContentNode node)
        {
            var broadside = node.Field("broadside");
            return new CannonRules(
                node.Field("max").AsInt(0, Sim.CannonSystem.Capacity),
                node.Field("resourcePerShot").AsNumber(0, 1000),
                broadside.Field("mousePx").AsNumber(1, 1e9),
                broadside.Field("balls").AsNumber(0, 1000));
        }
    }
}
