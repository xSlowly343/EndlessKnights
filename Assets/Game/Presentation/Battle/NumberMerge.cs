namespace Game.Presentation.Battle
{
    public sealed class NumberMerge
    {
        public const float DefaultWindowSeconds = 0.3f;

        private readonly float _windowSeconds;
        private float _lastHitTime = float.NegativeInfinity;
        private bool _open;

        public NumberMerge(float windowSeconds = DefaultWindowSeconds)
        {
            _windowSeconds = windowSeconds;
        }

        public bool ShouldMerge(float now, bool crit)
        {
            var merge = _open && !crit && now - _lastHitTime <= _windowSeconds;
            if (!crit)
            {
                _open = true;
                _lastHitTime = now;
            }
            return merge;
        }

        public void Break() => _open = false;
    }
}
