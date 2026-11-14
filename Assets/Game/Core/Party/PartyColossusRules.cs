using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyColossusHardship
    {
        private readonly Dictionary<string, double> _numbers;

        internal PartyColossusHardship(int index, string id, string nameRu, string nameEn, Dictionary<string, double> numbers)
        {
            Index = index;
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            _numbers = numbers;
        }

        public int Index { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }

        public double Number(string name, double fallback = 0) => _numbers.TryGetValue(name, out var value) ? value : fallback;
    }

    public sealed class PartyColossusRelic
    {
        internal PartyColossusRelic(int index, string id, string nameRu, string nameEn, Modifier modifier, bool compound, string waits)
        {
            Index = index;
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Modifier = modifier;
            Compound = compound;
            Waits = waits;
        }

        public int Index { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public Modifier Modifier { get; }
        public bool Compound { get; }
        public string Waits { get; }
        public bool Works => Waits.Length == 0;

        public double ValueAt(int level) =>
            level <= 0 ? 0 : Compound ? System.Math.Pow(1 + Modifier.Value, level) - 1 : Modifier.Value * level;

        public Modifier At(int level) => Modifier.WithValue(ValueAt(level));
    }

    public sealed class PartyColossusRules
    {
        public static readonly PartyColossusRules None = new PartyColossusRules();

        public double PointsPerBoss { get; private set; } = 100;
        public double Background { get; private set; }
        public double Offline { get; private set; }
        public double HpBase { get; private set; } = 1;
        public double HpGrowth { get; private set; } = 1;
        public int CeilingBase { get; private set; }
        public int CeilingStep { get; private set; }
        public double MaxPointsPerSecond { get; private set; }
        public double MaxOfflinePointsPerSecond { get; private set; }
        public double ReportSeconds { get; private set; } = 300;
        public double ExperiencePerPercent { get; private set; }
        public int MaxHardships { get; private set; }
        public double TrophyBase { get; private set; } = 1;
        public int TrophyPerTiers { get; private set; } = 5;
        public double SignsPerTier { get; private set; }
        public int RelicEvery { get; private set; } = 2;
        public int RelicMaxLevel { get; private set; }
        public int RelicCost { get; private set; } = 1;
        public double WeeklyMinShare { get; private set; }
        public IReadOnlyList<PartyColossusRelic> Relics => _relics;
        private readonly List<PartyColossusRelic> _relics = new List<PartyColossusRelic>();

        public PartyColossusRelic? Relic(string id)
        {
            foreach (var relic in _relics)
            {
                if (relic.Id == id)
                    return relic;
            }
            return null;
        }

        public double Trophies(int tier) => System.Math.Floor(TrophyBase + (double)tier / TrophyPerTiers);

        public int RelicsOpen(int bestKilledTier) => System.Math.Min(_relics.Count, System.Math.Max(0, bestKilledTier) / RelicEvery);

        public int RelicPrice(int level) => level >= RelicMaxLevel ? 0 : (level + 1) * RelicCost;

        public int RelicTotal(int toLevel)
        {
            var total = 0;
            for (var level = 0; level < toLevel && level < RelicMaxLevel; level++)
                total += RelicPrice(level);
            return total;
        }
        private readonly List<(int UpTo, int Min, int Max)> _mandatory = new List<(int UpTo, int Min, int Max)>();
        public int OptionalMax { get; private set; }
        public double OptionalTrophyBonus { get; private set; }
        public double HardshipSoften { get; private set; }
        public IReadOnlyList<PartyColossusHardship> Hardships => _hardships;
        private readonly List<PartyColossusHardship> _hardships = new List<PartyColossusHardship>();

        public PartyColossusHardship? Hardship(string id)
        {
            foreach (var hardship in _hardships)
            {
                if (hardship.Id == id)
                    return hardship;
            }
            return null;
        }

        public (int Min, int Max) MandatoryRange(int tier)
        {
            foreach (var step in _mandatory)
            {
                if (step.UpTo == 0 || tier <= step.UpTo)
                    return (System.Math.Min(MaxHardships, step.Min), System.Math.Min(MaxHardships, step.Max));
            }
            return (0, 0);
        }

        public int OptionalSlots(int mandatory) => System.Math.Max(0, System.Math.Min(OptionalMax, MaxHardships - mandatory));

        public double Hp(int tier) => HpBase * System.Math.Pow(HpGrowth, System.Math.Max(1, tier) - 1);

        public int Ceiling(PartyGrowthRules growth, int partyLevel)
        {
            if (!growth.Opened(PartyUnlockKind.Colossus, partyLevel))
                return 0;
            return CeilingBase + CeilingStep * growth.CountOpened(PartyUnlockKind.ColossusTier, partyLevel);
        }

        public static PartyColossusRules Read(ContentNode node)
        {
            var hp = node.Field("hp");
            var rules = new PartyColossusRules
            {
                PointsPerBoss = node.Field("pointsPerBoss").AsNumber(1, 1e6),
                Background = node.Field("background").AsNumber(0, 1),
                Offline = node.Field("offline").AsNumber(0, 1),
                HpBase = hp.Field("base").AsNumber(1, 1e15),
                HpGrowth = hp.Field("growth").AsNumber(1, 100),
                CeilingBase = node.Field("ceilingBase").AsInt(1, 100_000),
                CeilingStep = node.Field("ceilingStep").AsInt(0, 100_000),
                MaxPointsPerSecond = node.Field("maxPointsPerSecond").AsNumber(0, 1e9),
                MaxOfflinePointsPerSecond = node.Field("maxOfflinePointsPerSecond").AsNumber(0, 1e9),
                ReportSeconds = node.Field("reportSeconds").AsNumber(10, 86400),
                ExperiencePerPercent = node.Field("experiencePerPercent").AsNumber(0, 1e6)
            };
            if (node.HasField("relics"))
                ReadRelics(node, rules);
            if (!node.HasField("hardships"))
                return rules;
            rules.MaxHardships = node.Field("maxHardships").AsInt(0, 12);
            foreach (var step in node.Field("mandatory").Items())
            {
                var min = step.Field("min").AsInt(0, 12);
                rules._mandatory.Add((step.HasField("upTo") ? step.Field("upTo").AsInt(1, 1_000_000) : 0, min, step.Field("max").AsInt(min, 12)));
            }
            rules.OptionalMax = node.Field("optionalMax").AsInt(0, 12);
            rules.OptionalTrophyBonus = node.Field("optionalTrophyBonus").AsNumber(0, 10);
            rules.HardshipSoften = node.Field("hardshipSoften").AsNumber(0, 1);
            foreach (var item in node.Field("hardships").Items())
                rules._hardships.Add(ReadHardship(item, rules._hardships.Count));
            return rules;
        }

        private static void ReadRelics(ContentNode node, PartyColossusRules rules)
        {
            var trophies = node.Field("trophies");
            rules.TrophyBase = trophies.Field("base").AsNumber(0, 1000);
            rules.TrophyPerTiers = trophies.Field("perTiers").AsInt(1, 1000);
            rules.SignsPerTier = trophies.Field("signsPerTier").AsNumber(0, 1e6);
            rules.RelicEvery = node.Field("relicEvery").AsInt(1, 1000);
            rules.RelicMaxLevel = node.Field("relicMaxLevel").AsInt(1, 1000);
            rules.RelicCost = node.Field("relicCost").AsInt(1, 1_000_000);
            rules.WeeklyMinShare = node.Field("weekly").Field("minShare").AsNumber(0, 1);
            foreach (var item in node.Field("relics").Items())
            {
                var name = item.Field("name");
                rules._relics.Add(new PartyColossusRelic(rules._relics.Count, item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                    ModifierReader.Read(item), item.HasField("compound") && item.Field("compound").AsBool(),
                    item.HasField("waits") ? item.Field("waits").AsString() : string.Empty));
            }
        }

        private static PartyColossusHardship ReadHardship(ContentNode item, int index)
        {
            var numbers = new Dictionary<string, double>();
            foreach (var field in item.FieldNames())
            {
                if (field != "id" && field != "name")
                    numbers[field] = item.Field(field).AsNumber(-1000, 1000);
            }
            var name = item.Field("name");
            return new PartyColossusHardship(index, item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), numbers);
        }
    }
}
