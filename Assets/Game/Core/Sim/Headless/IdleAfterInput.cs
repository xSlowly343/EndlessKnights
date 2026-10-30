using Game.Core.Input;

namespace Game.Core.Sim.Headless
{
    public sealed class IdleAfterInput : IInputSource
    {
        private readonly IInputSource _typing;
        private readonly long _idleAfterTicks;
        private long _ticks;

        public IdleAfterInput(IInputSource typing, double idleAfterSeconds)
        {
            _typing = typing;
            _idleAfterTicks = (long)System.Math.Round(idleAfterSeconds * GameLoop.TicksPerSecond);
        }

        public bool Idle => _ticks >= _idleAfterTicks;

        public InputSnapshot Drain()
        {
            var snapshot = _typing.Drain();
            return _ticks++ < _idleAfterTicks ? snapshot : default;
        }
    }
}
