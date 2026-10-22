using System;

namespace Game.Core.Input
{
    public sealed class ObservedInputSource : IInputSource
    {
        private readonly IInputSource _inner;

        public ObservedInputSource(IInputSource inner)
        {
            _inner = inner;
        }

        public event Action<InputSnapshot>? Drained;

        public InputSnapshot Drain()
        {
            var snapshot = _inner.Drain();
            Drained?.Invoke(snapshot);
            return snapshot;
        }
    }
}
