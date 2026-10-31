namespace Game.Core.Content
{
    public sealed class BlindRules
    {
        public static readonly BlindRules None = new BlindRules(0, 1, 0);

        public BlindRules(int every, int everyMin, double seconds)
        {
            Every = every;
            EveryMin = everyMin;
            Seconds = seconds;
        }

        public int Every { get; }
        public int EveryMin { get; }
        public double Seconds { get; }

        public static BlindRules Read(ContentNode node) => new BlindRules(
            node.Field("every").AsInt(1, 10000),
            node.Field("everyMin").AsInt(1, 10000),
            node.Field("seconds").AsNumber(0, 3600));
    }
}
