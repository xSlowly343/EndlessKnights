namespace Game.Platform.Windows.Input
{
    public sealed class InputSuppression
    {
        public const long DefaultCheckIntervalMilliseconds = 500;

        private readonly InputBlacklist _blacklist;
        private readonly long _checkIntervalMilliseconds;
        private long _nextCheck = long.MinValue;
        private bool _blockedByForeground;

        public InputSuppression(InputBlacklist blacklist, long checkIntervalMilliseconds = DefaultCheckIntervalMilliseconds)
        {
            _blacklist = blacklist;
            _checkIntervalMilliseconds = checkIntervalMilliseconds;
        }

        public bool Paused { get; set; }

        public bool IsSuppressed(long nowMilliseconds, System.Func<string?> foregroundExecutable)
        {
            if (nowMilliseconds >= _nextCheck)
            {
                _nextCheck = nowMilliseconds + _checkIntervalMilliseconds;
                _blockedByForeground = _blacklist.IsBlocked(foregroundExecutable());
            }
            return Paused || _blockedByForeground;
        }
    }
}
