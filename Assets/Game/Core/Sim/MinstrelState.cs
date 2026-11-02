using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class MinstrelState
    {
        public const int MelodyCount = 3;

        public int LineHits { get; set; }
        public int Lines { get; set; }
        public int Verses { get; set; }
        public int Row { get; set; }
        public int VerseTicks { get; set; }
        public int ChorusTicks { get; set; }
        public long Choruses { get; set; }
        public bool EncoreUsed { get; set; }
        public int SongHits { get; set; }
        public int Melody { get; set; }
        public long MelodyChanges { get; set; }
        public int SongAllTicks { get; set; }
        public int SongAllWaitTicks { get; set; }
        public double MarchCarry { get; set; }
        public int NoteHits { get; set; }
        public int Notes { get; set; }
        public int SleepTicks { get; set; }
        public long Sleeps { get; set; }
        public BigNumber Dream { get; set; } = BigNumber.Zero;
        public int HymnTicks { get; set; }
        public double HymnPower { get; set; }
        public double PartyVerse { get; set; }
        public double PartySong { get; set; }
        public double PartySleep { get; set; }
        public bool Chorus { get; set; }
        public int SoloTicks { get; set; }
        public bool SpawnPending { get; set; }

        public bool Asleep => SleepTicks > 0;

        public void OnSpawn() => SpawnPending = true;
    }
}
