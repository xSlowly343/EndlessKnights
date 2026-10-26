namespace Game.Core.Content
{
    public sealed class BannerRules
    {
        public BannerRules(double mousePx, double step, double max, double decay, double decayEverySeconds)
        {
            MousePx = mousePx;
            Step = step;
            Max = max;
            Decay = decay;
            DecayEverySeconds = decayEverySeconds;
        }

        public double MousePx { get; }
        public double Step { get; }
        public double Max { get; }
        public double Decay { get; }
        public double DecayEverySeconds { get; }

        public static BannerRules Read(ContentNode node) =>
            new BannerRules(
                node.Field("mousePx").AsNumber(1, 1e9),
                node.Field("step").AsNumber(0, 10),
                node.Field("max").AsNumber(0, 10),
                node.Field("decay").AsNumber(0, 10),
                node.Field("decayEverySeconds").AsNumber(0.1, 3600));
    }
}
