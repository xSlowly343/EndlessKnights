namespace Game.Core.Content
{
    public sealed class BlindRules
    {
        public static readonly BlindRules None = new BlindRules(0, 0);

        public BlindRules(double seconds, double damageTaken)
        {
            Seconds = seconds;
            DamageTaken = damageTaken;
        }

        public double Seconds { get; }
        public double DamageTaken { get; }

        public static BlindRules Read(ContentNode node) => new BlindRules(
            node.Field("seconds").AsNumber(0, 3600),
            node.Field("damageTaken").AsNumber(0, 10));
    }
}
