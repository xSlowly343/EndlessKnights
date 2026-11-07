using System;
using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Content
{
    public sealed class TalentDef
    {
        public TalentDef(HeroClass heroClass, string nameRu, string nameEn, string aboutRu, string aboutEn, IReadOnlyList<PerkValue> perks, string waits)
        {
            Class = heroClass;
            NameRu = nameRu;
            NameEn = nameEn;
            AboutRu = aboutRu;
            AboutEn = aboutEn;
            Perks = perks;
            Waits = waits;
        }

        public HeroClass Class { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string AboutRu { get; }
        public string AboutEn { get; }
        public IReadOnlyList<PerkValue> Perks { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0;
    }

    public sealed class TalentRules
    {
        public const string FileName = "talents.json";
        public const int SupportedVersion = 1;
        public const double MaxFlag = 5;

        private readonly Dictionary<HeroClass, TalentDef> _byClass;

        private TalentRules(Dictionary<HeroClass, TalentDef> byClass)
        {
            _byClass = byClass;
        }

        public int Count => _byClass.Count;

        public TalentDef? Of(HeroClass heroClass) => _byClass.TryGetValue(heroClass, out var talent) ? talent : null;

        public static bool IsFlag(MuseumPerk perk) => perk == MuseumPerk.ForecastSight || perk == MuseumPerk.FreeRedraw || perk == MuseumPerk.ExtraHerb;

        public static TalentRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var byClass = new Dictionary<HeroClass, TalentDef>();
            foreach (var node in root.Field("talents").Items())
            {
                var name = node.Field("class").AsString();
                if (!HeroClassNames.ByName.TryGetValue(name, out var heroClass))
                    throw node.Field("class").Error($"unknown class '{name}'");
                if (byClass.ContainsKey(heroClass))
                    throw node.Field("class").Error($"class '{name}' has two talents");
                var perks = new List<PerkValue>();
                if (node.HasField("perks"))
                {
                    foreach (var item in node.Field("perks").Items())
                    {
                        var perkName = item.Field("perk").AsString();
                        if (!MuseumRules.TryPerk(perkName, out var perk))
                            throw item.Field("perk").Error($"unknown perk '{perkName}'");
                        var max = IsFlag(perk) ? MaxFlag : MuseumRules.MaxOf(perk);
                        perks.Add(new PerkValue(perk, item.Field("value").AsNumber(0.001, max)));
                    }
                }
                var waits = node.HasField("waits") ? node.Field("waits").AsString() : string.Empty;
                if ((perks.Count > 0) == (waits.Length > 0))
                    throw node.Error($"talent of '{name}' either gives perks or waits for a mechanic");
                var title = node.Field("name");
                var about = node.Field("about");
                byClass[heroClass] = new TalentDef(heroClass, title.Field("ru").AsString(), title.Field("en").AsString(),
                    about.Field("ru").AsString(), about.Field("en").AsString(), perks, waits);
            }
            foreach (var pair in HeroClassNames.ByName)
            {
                if (!byClass.ContainsKey(pair.Value))
                    throw root.Field("talents").Error($"class '{pair.Key}' has no peaceful talent");
            }
            return new TalentRules(byClass);
        }
    }
}
