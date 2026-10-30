using System;
using System.Collections.Generic;

namespace Game.Core.Sim
{
    public static class HeroClassNames
    {
        public const string Novice = "novice";

        public static readonly IReadOnlyDictionary<string, HeroClass> ByName = new Dictionary<string, HeroClass>(StringComparer.Ordinal)
        {
            ["viking"] = HeroClass.Viking,
            ["ronin"] = HeroClass.Ronin,
            ["amazon"] = HeroClass.Amazon,
            ["stormweaver"] = HeroClass.Stormweaver,
            ["nomad"] = HeroClass.Nomad,
            ["metalhead"] = HeroClass.Metalhead,
            ["oracle"] = HeroClass.Oracle,
            ["bannerbearer"] = HeroClass.Bannerbearer,
            ["voodoo"] = HeroClass.Voodoo,
            ["clockmaker"] = HeroClass.Clockmaker,
            ["plague_doctor"] = HeroClass.PlagueDoctor,
            ["moonshiner"] = HeroClass.Moonshiner,
            ["gravedigger"] = HeroClass.Gravedigger,
            ["captain"] = HeroClass.Captain
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
            return names;
        }
    }
}
