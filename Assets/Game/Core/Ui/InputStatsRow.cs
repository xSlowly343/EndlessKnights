namespace Game.Core.Ui
{
    public readonly struct InputStatsRow
    {
        public readonly string Label;
        public readonly string Session;
        public readonly string AllTime;
        public readonly string SessionHint;
        public readonly string AllTimeHint;

        public InputStatsRow(string label, string session, string allTime, string sessionHint = "", string allTimeHint = "")
        {
            Label = label;
            Session = session;
            AllTime = allTime;
            SessionHint = sessionHint;
            AllTimeHint = allTimeHint;
        }

        public bool HasHint => SessionHint.Length > 0;
    }
}
