using System.Collections.Generic;

namespace Game.Core.Content.Gems
{
    public static class SkillBranches
    {
        public const int NoChoice = -1;
        public const int Combos = 27;
        public const int Plain = 13;

        public static int Index(int first, int second, int third) => (first + 1) * 9 + (second + 1) * 3 + (third + 1);

        public static int Choice(int combo, int tier) => tier switch
        {
            0 => combo / 9 - 1,
            1 => combo / 3 % 3 - 1,
            _ => combo % 3 - 1
        };

        public static int ComboOf(IReadOnlyList<SkillBranchTier> tiers, IReadOnlyList<int>? choices, int level)
        {
            if (tiers.Count == 0 || choices == null)
                return Plain;
            return Index(Open(tiers, choices, level, 0), Open(tiers, choices, level, 1), Open(tiers, choices, level, 2));
        }

        private static int Open(IReadOnlyList<SkillBranchTier> tiers, IReadOnlyList<int> choices, int level, int tier) =>
            tier < choices.Count && level >= tiers[tier].Level ? choices[tier] : NoChoice;
    }
}
