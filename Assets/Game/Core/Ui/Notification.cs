namespace Game.Core.Ui
{
    public readonly struct Notification
    {
        public readonly string Text;
        public readonly double ShownAt;

        public Notification(string text, double shownAt)
        {
            Text = text;
            ShownAt = shownAt;
        }

        public double Age(double now) => now - ShownAt;
    }
}
