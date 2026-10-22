using Game.Core.Clock;
using Game.Core.Sim;

namespace Game.Core.Save
{
    public sealed class SaveFile
    {
        public SaveFile(int storedVersion, ClockStamp clock, GameState state)
        {
            StoredVersion = storedVersion;
            Clock = clock;
            State = state;
        }

        public int StoredVersion { get; }
        public ClockStamp Clock { get; }
        public GameState State { get; }
        public long SavedAtUnixMs => Clock.Reading.WallUnixMs;

        public TrustedElapsed AwayTime(ClockReading now) => TrustedElapsed.Between(Clock, now);
    }
}
