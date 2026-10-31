namespace Game.Core.Sim
{
    public sealed class GemProgress
    {
        public int Level { get; set; } = 1;
        public double Experience { get; set; }
        public int Rebirths { get; set; }
        public int[] Branches { get; } = { Content.Gems.SkillBranches.NoChoice, Content.Gems.SkillBranches.NoChoice, Content.Gems.SkillBranches.NoChoice };
    }
}
