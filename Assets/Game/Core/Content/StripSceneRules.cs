namespace Game.Core.Content
{
    public sealed class StripSceneRules
    {
        public const double MaxSwitchSeconds = 5;

        public static readonly StripSceneRules Instant = new StripSceneRules(0);

        public StripSceneRules(double switchSeconds)
        {
            SwitchSeconds = switchSeconds;
        }

        public double SwitchSeconds { get; }

        public static StripSceneRules Load(ContentNode node) =>
            new StripSceneRules(node.Field("switchSeconds").AsNumber(0, MaxSwitchSeconds));
    }
}
