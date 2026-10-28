using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyGhostRules
    {
        private PartyGhostRules(double power, double fullHours, double minActivity, double uploadMinutes, double freshHours, int pool)
        {
            Power = power;
            FullHours = fullHours;
            MinActivity = minActivity;
            UploadMinutes = uploadMinutes;
            FreshHours = freshHours;
            Pool = pool;
        }

        public double Power { get; }
        public double FullHours { get; }
        public double MinActivity { get; }
        public double UploadMinutes { get; }
        public double FreshHours { get; }
        public int Pool { get; }

        public static PartyGhostRules Read(ContentNode node) => new PartyGhostRules(
            node.Field("power").AsNumber(0, 1),
            node.Field("fullHours").AsNumber(0.5, 24),
            node.Field("minActivity").AsNumber(0, 1),
            node.Field("uploadMinutes").AsNumber(1, 120),
            node.Field("freshHours").AsNumber(1, 720),
            node.Field("pool").AsInt(1, 10000));
    }
}
