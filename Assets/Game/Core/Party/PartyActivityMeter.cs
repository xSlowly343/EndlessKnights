using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyActivityMeter
    {
        private long _activeTicks;

        public long PendingSeconds => _activeTicks / GameLoop.TicksPerSecond;

        public void NoteInput(bool hasInput)
        {
            if (hasInput)
                _activeTicks++;
        }

        public long TakeActiveSeconds()
        {
            var seconds = _activeTicks / GameLoop.TicksPerSecond;
            _activeTicks -= seconds * GameLoop.TicksPerSecond;
            return seconds;
        }
    }
}
