using System;
using System.Collections.Generic;

namespace Game.Core.Sim
{
    public static class HeroClassNames
    {
        public static readonly IReadOnlyDictionary<string, HeroClass> ByName = new Dictionary<string, HeroClass>(StringComparer.Ordinal)
        {
            ["swordsman"] = HeroClass.Swordsman,
            ["bard"] = HeroClass.Bard,
            ["barbarian"] = HeroClass.Barbarian,
            ["ranger"] = HeroClass.Ranger,
            ["sorcerer"] = HeroClass.Sorcerer,
            ["witch"] = HeroClass.Witch,
            ["priest"] = HeroClass.Priest,
            ["alchemist"] = HeroClass.Alchemist,
            ["wanderer"] = HeroClass.Wanderer
        };

        public static string Of(HeroClass heroClass)
        {
            foreach (var pair in ByName)
            {
                if (pair.Value == heroClass)
                    return pair.Key;
            }
            throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, "Class has no name.");
        }
    }
}
