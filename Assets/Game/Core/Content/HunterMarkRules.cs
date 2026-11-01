namespace Game.Core.Content
{
    public sealed class HunterMarkRules
    {
        public static readonly HunterMarkRules None = new HunterMarkRules(0, 0);

        public HunterMarkRules(double seconds, double keyDamage)
        {
            Seconds = seconds;
            KeyDamage = keyDamage;
        }

        public double Seconds { get; }
        public double KeyDamage { get; }

        public static HunterMarkRules Read(ContentNode node) => new HunterMarkRules(
            node.Field("seconds").AsNumber(0, 3600),
            node.Field("keyDamage").AsNumber(0, 100));
    }
}
