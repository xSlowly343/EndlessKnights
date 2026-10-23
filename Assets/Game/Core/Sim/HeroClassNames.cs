using System;
using System.Collections.Generic;

namespace Game.Core.Sim
{
    public static class HeroClassNames
    {
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
