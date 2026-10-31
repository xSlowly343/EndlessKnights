using System.Collections.Generic;

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
