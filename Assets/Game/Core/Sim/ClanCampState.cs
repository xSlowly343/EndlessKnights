namespace Game.Core.Sim
{
    public sealed class ClanCampState
    {
        public int Level { get; set; }
        public long FeastUntilUnixMs { get; set; }
        public long AnsweredUnixMs { get; set; }
        public long SeenUnixMs { get; set; }
    }
}
