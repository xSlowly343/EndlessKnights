namespace Game.Core.Content
{
    public sealed class DotRules
    {
        public DotRules(int poisonMax, double poisonHitsPerStack, int burnMax, double burnHitsPerStack, int stirMax,
            double stirPxPerTurn, double vapourKeyHits = 0)
        {
            VapourKeyHits = vapourKeyHits;
            PoisonMax = poisonMax;
            PoisonHitsPerStack = poisonHitsPerStack;
            BurnMax = burnMax;
            BurnHitsPerStack = burnHitsPerStack;
            StirMax = stirMax;
            StirPxPerTurn = stirPxPerTurn;
        }

        public int PoisonMax { get; }
        public double PoisonHitsPerStack { get; }
        public int BurnMax { get; }
        public double BurnHitsPerStack { get; }
        public int StirMax { get; }
        public double StirPxPerTurn { get; }
        public double VapourKeyHits { get; }

        public static DotRules Read(ContentNode node)
        {
            var poison = node.Field("poison");
            var burn = node.Field("burn");
            return new DotRules(
                poison.Field("max").AsInt(0, 100000),
                poison.Field("hitsPerStack").AsNumber(0, 100),
                burn.Field("max").AsInt(0, 100000),
                burn.Field("hitsPerStack").AsNumber(0, 100),
                poison.Field("stirMax").AsInt(0, 1000),
                poison.Field("stirPxPerTurn").AsNumber(1, 1e6),
                poison.HasField("vapourKeyHits") ? poison.Field("vapourKeyHits").AsNumber(1, 100000) : 0);
        }
    }
}
