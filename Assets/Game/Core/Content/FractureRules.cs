namespace Game.Core.Content
{
    public sealed class FractureRules
    {
        public double PerStack { get; private set; }
        public int BaseMax { get; private set; }
        public int MaxCap { get; private set; }
        public double DecaySeconds { get; private set; } = 2;
        public double ShareSeconds { get; private set; } = 2;
        public int PillarMembers { get; private set; } = 4;

        public int MaxStacks(double extra) =>
            System.Math.Min(MaxCap, BaseMax + (int)System.Math.Floor(System.Math.Max(0, extra) + 1e-9));

        internal static FractureRules Read(ContentNode node) => new FractureRules
        {
            PerStack = node.Field("perStack").AsNumber(0, 1),
            BaseMax = node.Field("baseMax").AsInt(0, 1000),
            MaxCap = node.Field("maxCap").AsInt(0, 1000),
            DecaySeconds = node.Field("decaySeconds").AsNumber(0.1, 600),
            ShareSeconds = node.Field("shareSeconds").AsNumber(0.1, 600),
            PillarMembers = node.Field("pillarMembers").AsInt(1, 8)
        };
    }
}
