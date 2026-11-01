using System;
using System.Collections.Generic;

namespace Game.Core.Sim
{
    public static class HeroClassNames
    {
        public const string Novice = "novice";

        public static readonly IReadOnlyDictionary<string, HeroClass> ByName = new Dictionary<string, HeroClass>(StringComparer.Ordinal)
        {
            ["runedancer"] = HeroClass.Runedancer,
            ["duelist"] = HeroClass.Duelist,
            ["windshot"] = HeroClass.Windshot,
            ["shardcaller"] = HeroClass.Shardcaller,
            ["sandwalker"] = HeroClass.Sandwalker,
            ["minstrel"] = HeroClass.Minstrel,
            ["dreamcatcher"] = HeroClass.Dreamcatcher,
            ["lanternbearer"] = HeroClass.Lanternbearer,
            ["whisperer"] = HeroClass.Whisperer,
            ["clockwright"] = HeroClass.Clockwright,
            ["alchemist"] = HeroClass.Alchemist,
            ["dowser"] = HeroClass.Dowser,
            ["soulbinder"] = HeroClass.Soulbinder,
            ["ghostcaptain"] = HeroClass.Ghostcaptain,
            ["mothcaller"] = HeroClass.Mothcaller,
            ["cartomancer"] = HeroClass.Cartomancer,
            ["herbwitch"] = HeroClass.Herbwitch,
            ["golemancer"] = HeroClass.Golemancer
        };

        public static readonly IReadOnlyDictionary<string, HeroClass> InDevelopment = new Dictionary<string, HeroClass>(StringComparer.Ordinal)
        {
        };

        private static readonly string[] Names = NamesByIndex();

        public static string Of(HeroClass heroClass)
        {
            var index = (int)heroClass;
            if (index < 0 || index >= Names.Length || Names[index] == null)
                throw new ArgumentOutOfRangeException(nameof(heroClass), heroClass, "Class has no name.");
            return Names[index];
        }

        private static string[] NamesByIndex()
        {
            var names = new string[Enum.GetValues(typeof(HeroClass)).Length];
            foreach (var pair in ByName)
                names[(int)pair.Value] = pair.Key;
            foreach (var pair in InDevelopment)
                names[(int)pair.Value] = pair.Key;
            return names;
        }
    }
}
