using System;
using System.Collections.Generic;

namespace Game.Core.Sim
{
    public static class HeroClassNames
    {
        public static readonly IReadOnlyDictionary<string, HeroClass> ByName = new Dictionary<string, HeroClass>(StringComparer.Ordinal)
        {
            ["typist"] = HeroClass.Typist,
            ["bard"] = HeroClass.Bard
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
