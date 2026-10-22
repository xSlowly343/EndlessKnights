using Game.Core.Clock;
using Game.Core.Sim;

namespace Game.Core.Save
{
    public sealed class SaveFile
    {
        public SaveFile(int version, ClockStamp clock, GameState state)
        {
            Version = version;
            Clock = clock;
            State = state;
        }

        public int Version { get; }
        public ClockStamp Clock { get; }
        public GameState State { get; }
        public long SavedAtUnixMs => Clock.Reading.WallUnixMs;

        public TrustedElapsed AwayTime(ClockReading now) => TrustedElapsed.Between(Clock, now);
    }
}
