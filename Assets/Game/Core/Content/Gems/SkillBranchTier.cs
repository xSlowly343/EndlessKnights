using System.Collections.Generic;

namespace Game.Core.Content.Gems
{
    public sealed class SkillBranchTier
    {
        public const int Count = 3;
        public const int OptionCount = 2;

        public SkillBranchTier(int level, IReadOnlyList<SkillBranch> options)
        {
            Level = level;
            Options = options;
        }

        public int Level { get; }
        public IReadOnlyList<SkillBranch> Options { get; }
    }
}
