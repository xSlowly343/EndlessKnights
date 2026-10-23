namespace Game.Core.Content
{
    public sealed class BossHintRules
    {
        private BossHintRules(int afterFails, int roundPercent, int maxPercent)
        {
            AfterFails = afterFails;
            RoundPercent = roundPercent;
            MaxPercent = maxPercent;
        }

        public int AfterFails { get; }
        public int RoundPercent { get; }
        public int MaxPercent { get; }

        public static BossHintRules Read(ContentNode node)
        {
            var round = node.Field("roundPercent").AsInt(1, 100);
            var max = node.Field("maxPercent").AsInt(1, 100000);
            if (max < round)
                throw node.Field("maxPercent").Error($"must not be lower than roundPercent ({round})");
            return new BossHintRules(node.Field("afterFails").AsInt(1, 100), round, max);
        }

        public int MissingPercent(double bestFraction)
        {
            if (bestFraction <= 0)
                return MaxPercent;
            if (bestFraction >= 1)
                return RoundPercent;
            var percent = (1 / bestFraction - 1) * 100;
            var rounded = (int)System.Math.Min(MaxPercent, System.Math.Ceiling(percent / RoundPercent) * RoundPercent);
            return System.Math.Max(RoundPercent, rounded);
        }
    }
}
