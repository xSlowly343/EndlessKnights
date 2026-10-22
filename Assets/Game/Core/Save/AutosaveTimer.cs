namespace Game.Core.Save
{
    public sealed class AutosaveTimer
    {
        public const double DefaultIntervalSeconds = 30;

        private readonly double _intervalSeconds;
        private double _sinceLastSave;

        public AutosaveTimer(double intervalSeconds = DefaultIntervalSeconds)
        {
            _intervalSeconds = intervalSeconds;
        }

        public bool Advance(double seconds)
        {
            _sinceLastSave += seconds;
            if (_sinceLastSave < _intervalSeconds)
                return false;
            _sinceLastSave = 0;
            return true;
        }
    }
}
