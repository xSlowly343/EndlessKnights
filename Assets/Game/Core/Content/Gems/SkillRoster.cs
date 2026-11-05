using System.Collections.Generic;
using Game.Core.Content.Weapons;

namespace Game.Core.Content.Gems
{
    public static class SkillRoster
    {
        public const int ActivePerClass = 12;
        public const int AurasPerClass = 6;
        public const int BranchTiers = SkillBranchTier.Count;

        public static List<string> Problems(GemCatalog catalog, IEnumerable<string> weapons)
        {
            var problems = new List<string>();
            foreach (var weapon in weapons)
            {
                var active = Count(catalog, weapon, SkillType.Active);
                var auras = Count(catalog, weapon, SkillType.Aura);
                if (active != ActivePerClass)
                    problems.Add($"{weapon}: {active} active skills, expected {ActivePerClass}");
                if (auras != AurasPerClass)
                    problems.Add($"{weapon}: {auras} auras, expected {AurasPerClass}");
            }
            foreach (var gem in catalog.All)
            {
                if (gem.Skill?.Weapon == null || gem.Ascendancy != null || !Contains(weapons, gem.Skill.Weapon))
                    continue;
                if (gem.Branches.Count != BranchTiers)
                    problems.Add($"{gem.Id}: {gem.Branches.Count} branch tiers, expected {BranchTiers}");
            }
            return problems;
        }

        public static List<string> NumbersOnlyTiers(GemCatalog catalog)
        {
            var found = new List<string>();
            foreach (var gem in catalog.All)
            {
                if (gem.Skill?.Weapon == null || gem.Ascendancy != null)
                    continue;
                for (var tier = 0; tier < gem.Branches.Count; tier++)
                {
                    var rule = false;
                    foreach (var option in gem.Branches[tier].Options)
                        rule |= option.Effect != null;
                    if (!rule)
                        found.Add(gem.Id + "#" + (tier + 1));
                }
            }
            return found;
        }

        private static bool Contains(IEnumerable<string> weapons, string weapon)
        {
            foreach (var id in weapons)
            {
                if (id == weapon)
                    return true;
            }
            return false;
        }

        public static void Require(GemCatalog catalog, IReadOnlyList<WeaponType> weapons)
        {
            var ids = new List<string>(weapons.Count);
            foreach (var weapon in weapons)
                ids.Add(weapon.Id);
            var problems = Problems(catalog, ids);
            if (problems.Count > 0)
                throw new ContentException(GemCatalog.FileName, "gems", string.Join("; ", problems));
        }

        private static int Count(GemCatalog catalog, string weapon, SkillType type)
        {
            var count = 0;
            foreach (var gem in catalog.All)
            {
                if (gem.Type == type && gem.Skill?.Weapon == weapon && gem.Ascendancy == null)
                    count++;
            }
            return count;
        }
    }
}
