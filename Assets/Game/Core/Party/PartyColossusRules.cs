using System.Collections.Generic;
using Game.Core.Content;

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
        public int MandatoryEvery { get; private set; }
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

        public PartyColossusHardship? MandatoryOf(int tier)
        {
            if (MandatoryEvery <= 0 || _hardships.Count == 0 || tier % MandatoryEvery != 0)
                return null;
            return _hardships[(tier / MandatoryEvery - 1) % _hardships.Count];
        }

        public bool OptionalAllowed(int tier) => MandatoryOf(tier) == null;

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
            if (!node.HasField("hardships"))
                return rules;
            rules.MandatoryEvery = node.Field("mandatoryEvery").AsInt(1, 1000);
            rules.OptionalMax = node.Field("optionalMax").AsInt(0, 12);
            rules.OptionalTrophyBonus = node.Field("optionalTrophyBonus").AsNumber(0, 10);
            rules.HardshipSoften = node.Field("hardshipSoften").AsNumber(0, 1);
            foreach (var item in node.Field("hardships").Items())
                rules._hardships.Add(ReadHardship(item, rules._hardships.Count));
            return rules;
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
