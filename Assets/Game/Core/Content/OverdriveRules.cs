namespace Game.Core.Content
{
    public sealed class OverdriveRules
    {
        public static readonly OverdriveRules None = new OverdriveRules(0, 0);

        public OverdriveRules(double seconds, double songs)
        {
            Seconds = seconds;
            Songs = songs;
        }

        public double Seconds { get; }
        public double Songs { get; }

        public static OverdriveRules Read(ContentNode node) => new OverdriveRules(
            node.Field("seconds").AsNumber(0, 3600),
            node.Field("songs").AsNumber(0, 100));
    }
}
