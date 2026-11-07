namespace Game.Core.Sim
{
    public sealed class TournamentState
    {
        public long Index { get; set; } = -1;
        public string Format { get; set; } = string.Empty;
        public long BaseBosses { get; set; }
        public long BaseKills { get; set; }
        public long Best { get; set; }
        public bool Unsent { get; set; }
        public bool Joined => Index >= 0;
    }
}
