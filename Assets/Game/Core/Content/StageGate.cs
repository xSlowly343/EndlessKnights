namespace Game.Core.Content
{
    public sealed class StageGate
    {
        public StageGate(long stage, double hpMultiplier, double timerSeconds, string nameRu, string nameEn)
        {
            Stage = stage;
            HpMultiplier = hpMultiplier;
            TimerSeconds = timerSeconds;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public long Stage { get; }
        public double HpMultiplier { get; }
        public double TimerSeconds { get; }
        public string NameRu { get; }
        public string NameEn { get; }

        public static StageGate Read(ContentNode node)
        {
            var name = node.Field("name");
            return new StageGate(node.Field("stage").AsLong(1), node.Field("hpMultiplier").AsNumber(1, 1e12), node.Field("timerSeconds").AsNumber(1, 3600),
                name.Field("ru").AsString(), name.Field("en").AsString());
        }
    }
}
