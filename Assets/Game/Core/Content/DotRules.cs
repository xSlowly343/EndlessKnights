namespace Game.Core.Content
{
    public sealed class DotRules
    {
        public DotRules(int poisonMax, double poisonHitsPerStack, int burnMax, double burnHitsPerStack, double fanningMultiplier, int stirMax)
        {
            PoisonMax = poisonMax;
            PoisonHitsPerStack = poisonHitsPerStack;
            BurnMax = burnMax;
            BurnHitsPerStack = burnHitsPerStack;
            FanningMultiplier = fanningMultiplier;
            StirMax = stirMax;
        }

        public int PoisonMax { get; }
        public double PoisonHitsPerStack { get; }
        public int BurnMax { get; }
        public double BurnHitsPerStack { get; }
        public double FanningMultiplier { get; }
        public int StirMax { get; }

        public static DotRules Read(ContentNode node)
        {
            var poison = node.Field("poison");
            var burn = node.Field("burn");
            return new DotRules(
                poison.Field("max").AsInt(0, 100000),
                poison.Field("hitsPerStack").AsNumber(0, 100),
                burn.Field("max").AsInt(0, 100000),
                burn.Field("hitsPerStack").AsNumber(0, 100),
                burn.Field("fanning").AsNumber(1, 100),
                poison.Field("stirMax").AsInt(0, 1000));
        }
    }
}
