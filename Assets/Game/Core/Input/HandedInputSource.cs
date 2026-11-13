namespace Game.Core.Input
{
    public sealed class HandedInputSource : IInputSource
    {
        private InputSnapshot _next;

        public void Hand(InputSnapshot snapshot) => _next = snapshot;

        public InputSnapshot Drain()
        {
            var snapshot = _next;
            _next = InputSnapshot.Empty;
            return snapshot;
        }
    }
}
