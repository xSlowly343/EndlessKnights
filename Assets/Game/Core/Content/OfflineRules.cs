namespace Game.Core.Content
{
    public sealed class OfflineRules
    {
        public OfflineRules(double capHours, double dpsShare, double dpsWindowSeconds)
        {
            CapHours = capHours;
            DpsShare = dpsShare;
            DpsWindowSeconds = dpsWindowSeconds;
        }

        public double CapHours { get; }
        public double DpsShare { get; }
        public double DpsWindowSeconds { get; }
        public double CapSeconds => CapHours * 3600;

        public static OfflineRules Read(ContentNode node) =>
            new OfflineRules(
                node.Field("capHours").AsNumber(0, 72),
                node.Field("dpsShare").AsNumber(0, 10),
                node.Field("dpsWindowSeconds").AsNumber(1, 86400));
    }
}
