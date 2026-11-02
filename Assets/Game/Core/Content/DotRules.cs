namespace Game.Core.Content
{
    public sealed class DotRules
    {
        public DotRules(int poisonMax, double poisonHitsPerStack, int burnMax, double burnHitsPerStack, int stirMax,
            double stirPxPerTurn, double miasmaEverySeconds = 0, double miasmaStackSeconds = 0, double vapourKeyHits = 0)
        {
            VapourKeyHits = vapourKeyHits;
            PoisonMax = poisonMax;
            PoisonHitsPerStack = poisonHitsPerStack;
            BurnMax = burnMax;
            BurnHitsPerStack = burnHitsPerStack;
            StirMax = stirMax;
            StirPxPerTurn = stirPxPerTurn;
            MiasmaEverySeconds = miasmaEverySeconds;
            MiasmaStackSeconds = miasmaStackSeconds;
        }

        public int PoisonMax { get; }
        public double PoisonHitsPerStack { get; }
        public int BurnMax { get; }
        public double BurnHitsPerStack { get; }
        public int StirMax { get; }
        public double StirPxPerTurn { get; }
        public double MiasmaEverySeconds { get; }
        public double MiasmaStackSeconds { get; }
        public double VapourKeyHits { get; }

        public static DotRules Read(ContentNode node)
        {
            var poison = node.Field("poison");
            var burn = node.Field("burn");
            var miasma = node.HasField("miasma");
            return new DotRules(
                poison.Field("max").AsInt(0, 100000),
                poison.Field("hitsPerStack").AsNumber(0, 100),
                burn.Field("max").AsInt(0, 100000),
                burn.Field("hitsPerStack").AsNumber(0, 100),
                poison.Field("stirMax").AsInt(0, 1000),
                poison.Field("stirPxPerTurn").AsNumber(1, 1e6),
                miasma ? node.Field("miasma").Field("everySeconds").AsNumber(0.1, 3600) : 0,
                miasma ? node.Field("miasma").Field("stackSeconds").AsNumber(0, 3600) : 0,
                poison.HasField("vapourKeyHits") ? poison.Field("vapourKeyHits").AsNumber(1, 100000) : 0);
        }
    }
}
