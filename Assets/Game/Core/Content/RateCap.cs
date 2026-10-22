namespace Game.Core.Content
{
    public readonly struct RateCap
    {
        public readonly double FullRatePerSecond;
        public readonly double DiminishingUntilPerSecond;

        public RateCap(double fullRatePerSecond, double diminishingUntilPerSecond)
        {
            FullRatePerSecond = fullRatePerSecond;
            DiminishingUntilPerSecond = diminishingUntilPerSecond;
        }

        public double MaxEffectivePerSecond => System.Math.Sqrt(FullRatePerSecond * DiminishingUntilPerSecond);

        public double Effective(double ratePerSecond)
        {
            if (ratePerSecond <= FullRatePerSecond)
                return System.Math.Max(0, ratePerSecond);
            return System.Math.Min(System.Math.Sqrt(FullRatePerSecond * ratePerSecond), MaxEffectivePerSecond);
        }

        public static RateCap Read(ContentNode node)
        {
            var full = node.Field("fullRatePerSecond").AsNumber(0.1, 1000);
            var until = node.Field("diminishingUntilPerSecond").AsNumber(0.1, 1000);
            if (until < full)
                throw node.Field("diminishingUntilPerSecond").Error($"must not be lower than fullRatePerSecond ({full})");
            return new RateCap(full, until);
        }
    }
}
