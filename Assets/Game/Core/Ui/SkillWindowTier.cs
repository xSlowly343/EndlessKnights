using Game.Core.Content.Gems;
using Game.Core.Math;

namespace Game.Core.Ui
{
    public struct SkillWindowTier
    {
        public SkillBranchTier Def;
        public bool Open;
        public int Chosen;
        public BigNumber ChangeCost;

        public bool HasChoice => Chosen != SkillBranches.NoChoice;
    }
}
