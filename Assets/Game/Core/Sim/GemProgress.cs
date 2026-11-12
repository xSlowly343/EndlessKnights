namespace Game.Core.Sim
{
    public sealed class GemProgress
    {
        public int Level { get; set; } = 1;
        public double Experience { get; set; }
        public int Rebirths { get; set; }
        public bool AheadOf(GemProgress other) =>
            Rebirths != other.Rebirths ? Rebirths > other.Rebirths : Level != other.Level ? Level > other.Level : Experience > other.Experience;

        public GemProgress Copy()
        {
            var copy = new GemProgress { Level = Level, Experience = Experience, Rebirths = Rebirths };
            Branches.CopyTo(copy.Branches, 0);
            return copy;
        }

        public int[] Branches { get; } = { Content.Gems.SkillBranches.NoChoice, Content.Gems.SkillBranches.NoChoice, Content.Gems.SkillBranches.NoChoice };
    }
}
