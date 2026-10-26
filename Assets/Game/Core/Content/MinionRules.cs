namespace Game.Core.Content
{
    public sealed class MinionRules
    {
        public MinionRules(int max, double mouseSpeed)
        {
            Max = max;
            MouseSpeed = mouseSpeed;
        }

        public int Max { get; }
        public double MouseSpeed { get; }

        public static MinionRules Read(ContentNode node) =>
            new MinionRules(
                node.Field("max").AsInt(0, Sim.MinionSystem.Capacity),
                node.Field("mouseSpeed").AsNumber(1, 100));
    }
}
