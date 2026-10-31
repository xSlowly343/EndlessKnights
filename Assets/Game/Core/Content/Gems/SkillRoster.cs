using System.Collections.Generic;
using Game.Core.Content.Weapons;

namespace Game.Core.Content.Gems
{
    public static class SkillRoster
    {
        public const int ActivePerClass = 15;
        public const int AurasPerClass = 4;

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
            return problems;
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
