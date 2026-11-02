using Game.Core.Stats;

namespace Game.Core.Sim
{
    public static class CartomancerStats
    {
        public static readonly StatId SpreadPower = new StatId("spreadPower");
        public static readonly StatId SpreadHits = new StatId("spreadHits");
        public static readonly StatId SpreadLong = new StatId("spreadLong");
        public static readonly StatId SpreadPair = new StatId("spreadPair");
        public static readonly StatId SpreadSeconds = new StatId("spreadSeconds");
        public static readonly StatId SpreadSwords = new StatId("spreadSwords");
        public static readonly StatId SpreadHand = new StatId("spreadHand");

        public static readonly StatId ForecastPower = new StatId("forecastPower");
        public static readonly StatId ForecastStreak = new StatId("forecastStreak");
        public static readonly StatId ForecastLong = new StatId("forecastLong");
        public static readonly StatId ForecastShort = new StatId("forecastShort");
        public static readonly StatId ForecastExact = new StatId("forecastExact");
        public static readonly StatId ForecastRush = new StatId("forecastRush");
        public static readonly StatId ForecastSafe = new StatId("forecastSafe");
        public static readonly StatId ForecastDouble = new StatId("forecastDouble");

        public static readonly StatId ReversedPower = new StatId("reversedPower");
        public static readonly StatId ReversedHits = new StatId("reversedHits");
        public static readonly StatId ReversedMax = new StatId("reversedMax");
        public static readonly StatId ReversedKeep = new StatId("reversedKeep");
        public static readonly StatId ReversedFifth = new StatId("reversedFifth");
        public static readonly StatId ReversedStrong = new StatId("reversedStrong");
        public static readonly StatId ReversedRevealHits = new StatId("reversedRevealHits");
        public static readonly StatId ReversedParty = new StatId("reversedParty");
    }
}
