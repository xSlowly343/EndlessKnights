namespace Game.Core.Content
{
    public sealed class TutorialStep
    {
        public TutorialStep(string id, string text, TutorialShow show, int showCount, TutorialDone done, double doneCount, double maxSeconds)
        {
            Id = id;
            Text = text;
            Show = show;
            ShowCount = showCount;
            Done = done;
            DoneCount = doneCount;
            MaxSeconds = maxSeconds;
        }

        public string Id { get; }
        public string Text { get; }
        public TutorialShow Show { get; }
        public int ShowCount { get; }
        public TutorialDone Done { get; }
        public double DoneCount { get; }
        public double MaxSeconds { get; }
    }
}
