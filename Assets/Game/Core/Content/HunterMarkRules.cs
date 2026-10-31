namespace Game.Core.Content
{
    public sealed class HunterMarkRules
    {
        public static readonly HunterMarkRules None = new HunterMarkRules(0, 1, 0, 0);

        public HunterMarkRules(int every, int everyMin, double seconds, double keyDamage)
        {
            Every = every;
            EveryMin = everyMin;
            Seconds = seconds;
            KeyDamage = keyDamage;
        }

        public int Every { get; }
        public int EveryMin { get; }
        public double Seconds { get; }
        public double KeyDamage { get; }

        public static HunterMarkRules Read(ContentNode node) => new HunterMarkRules(
            node.Field("every").AsInt(1, 10000),
            node.Field("everyMin").AsInt(1, 10000),
            node.Field("seconds").AsNumber(0, 3600),
            node.Field("keyDamage").AsNumber(0, 100));
    }
}
