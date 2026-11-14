using System;
using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;

namespace Game.Core.Party
{
    public enum PartyTrialRule
    {
        Choir,
        Relay,
        Mirror,
        Silence,
        Pulse,
        Shields,
        Chain,
        Marathon
    }

    public enum PartyTrialComplication
    {
        None,
        Carapace,
        Shadow,
        SkillShield,
        Night,
        Regeneration
    }

    public sealed class PartyTrialDef
    {
        internal PartyTrialDef(int index, string id, PartyTrialRule rule, string nameRu, string nameEn, double seconds, ContentNode node)
        {
            Index = index;
            Id = id;
            Rule = rule;
            NameRu = nameRu;
            NameEn = nameEn;
            Seconds = seconds;
            PassSeconds = Optional(node, "passSeconds", 1, 600, 20);
            BankShare = Optional(node, "bankShare", 0, 10, 1);
            WindowSeconds = Optional(node, "windowSeconds", 0.1, 600, 30);
            Resist = Optional(node, "resist", 0, 1, 0.6);
            EverySeconds = Optional(node, "everySeconds", 0.1, 600, 10);
            Inside = Optional(node, "inside", 0, 100, 3);
            Outside = Optional(node, "outside", 0, 100, 0.3);
            ShieldShare = Optional(node, "shieldShare", 0, 1, 0.1);
            ChainBase = Optional(node, "base", 0, 10, 0.5);
            ChainStep = Optional(node, "step", 0, 10, 0.2);
            ChainMax = Optional(node, "max", 0, 100, 3);
            KeepSeconds = Optional(node, "keepSeconds", 0.1, 600, 6);
            StepSeconds = Optional(node, "stepSeconds", 0.1, 600, 1);
            WaveSeconds = Optional(node, "waveSeconds", 1, 600, 30);
            if (rule == PartyTrialRule.Pulse && WindowSeconds >= EverySeconds)
                throw node.Field("windowSeconds").Error("the pulse window must be shorter than its period");
        }

        public int Index { get; }
        public string Id { get; }
        public PartyTrialRule Rule { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Seconds { get; }
        public double PassSeconds { get; }
        public double BankShare { get; }
        public double WindowSeconds { get; }
        public double Resist { get; }
        public double EverySeconds { get; }
        public double Inside { get; }
        public double Outside { get; }
        public double ShieldShare { get; }
        public double ChainBase { get; }
        public double ChainStep { get; }
        public double ChainMax { get; }
        public double KeepSeconds { get; }
        public double StepSeconds { get; }
        public double WaveSeconds { get; }
        public int Waves => (int)System.Math.Floor(Seconds / WaveSeconds + 1e-9);

        private static double Optional(ContentNode node, string name, double min, double max, double fallback) =>
            node.HasField(name) ? node.Field(name).AsNumber(min, max) : fallback;
    }

    public sealed class PartyTrialComplicationDef
    {
        internal PartyTrialComplicationDef(PartyTrialComplication kind, string id, string nameRu, string nameEn, double keys, double mouse,
            double share, double day, double night, double perSecond, double afterSeconds)
        {
            Kind = kind;
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Keys = keys;
            Mouse = mouse;
            Share = share;
            Day = day;
            Night = night;
            PerSecond = perSecond;
            AfterSeconds = afterSeconds;
        }

        public PartyTrialComplication Kind { get; }
        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public double Keys { get; }
        public double Mouse { get; }
        public double Share { get; }
        public double Day { get; }
        public double Night { get; }
        public double PerSecond { get; }
        public double AfterSeconds { get; }
    }

    public sealed class PartyTrialRules
    {
        public static readonly PartyTrialRules None = new PartyTrialRules();
        private const long WeekMs = 7L * 86_400_000;
        private const long FirstMondayUnixMs = 4L * 86_400_000;

        private static readonly Dictionary<string, PartyTrialRule> Rules = new Dictionary<string, PartyTrialRule>(StringComparer.Ordinal)
        {
            ["choir"] = PartyTrialRule.Choir,
            ["relay"] = PartyTrialRule.Relay,
            ["mirror"] = PartyTrialRule.Mirror,
            ["silence"] = PartyTrialRule.Silence,
            ["pulse"] = PartyTrialRule.Pulse,
            ["shields"] = PartyTrialRule.Shields,
            ["chain"] = PartyTrialRule.Chain,
            ["marathon"] = PartyTrialRule.Marathon
        };

        private static readonly Dictionary<string, PartyTrialComplication> Complications = new Dictionary<string, PartyTrialComplication>(StringComparer.Ordinal)
        {
            ["carapace"] = PartyTrialComplication.Carapace,
            ["shadow"] = PartyTrialComplication.Shadow,
            ["skillShield"] = PartyTrialComplication.SkillShield,
            ["night"] = PartyTrialComplication.Night,
            ["regeneration"] = PartyTrialComplication.Regeneration
        };

        private readonly List<PartyTrialDef> _list = new List<PartyTrialDef>();
        private readonly List<PartyTrialComplicationDef> _complications = new List<PartyTrialComplicationDef>();

        public IReadOnlyList<PartyTrialDef> List => _list;
        public IReadOnlyList<PartyTrialComplicationDef> ComplicationList => _complications;
        public int PerWeek { get; private set; }
        public int Tries { get; private set; }
        public int MinFighters { get; private set; }
        public int MaxTier { get; private set; }
        public int CeilingBase { get; private set; }
        public int CeilingStep { get; private set; }
        public long HpStage { get; private set; } = 1;
        public double HpMultiplier { get; private set; } = 1;
        public double HpGrowth { get; private set; } = 1;
        public double ArmorPerTier { get; private set; }
        public double ArmorMax { get; private set; }
        public double Seal { get; private set; }
        public int FirstClearSigns { get; private set; }
        public double ExperiencePerTier { get; private set; }
        public double MinSeconds { get; private set; }
        public double SlackSeconds { get; private set; }
        public int ComplicationFromTier { get; private set; }

        public PartyTrialDef? Find(string id)
        {
            foreach (var trial in _list)
            {
                if (trial.Id == id)
                    return trial;
            }
            return null;
        }

        public int Ceiling(PartyGrowthRules growth, int partyLevel)
        {
            if (!growth.Opened(PartyUnlockKind.Trials, partyLevel))
                return 0;
            return System.Math.Min(MaxTier, CeilingBase + CeilingStep * growth.CountOpened(PartyUnlockKind.TrialTier, partyLevel));
        }

        public int NextTier(int best, int ceiling) => System.Math.Min(ceiling, System.Math.Max(0, best) + 1);

        public static long Week(long unixMs) => System.Math.Max(0, unixMs - FirstMondayUnixMs) / WeekMs;

        public static long WeekEndsAt(long unixMs) => FirstMondayUnixMs + (Week(unixMs) + 1) * WeekMs;

        public bool OpenIn(PartyTrialDef trial, long week)
        {
            var first = (int)(week * PerWeek % _list.Count);
            for (var i = 0; i < PerWeek; i++)
            {
                if ((first + i) % _list.Count == trial.Index)
                    return true;
            }
            return false;
        }

        public PartyTrialComplicationDef? ComplicationOf(PartyTrialDef trial, int tier)
        {
            if (_complications.Count == 0 || tier < ComplicationFromTier || trial.Rule == PartyTrialRule.Marathon)
                return null;
            return _complications[(tier - ComplicationFromTier + trial.Index) % _complications.Count];
        }

        public BigNumber Hp(StageTable stages, int tier, double fightersMultiplier) =>
            stages.BossHp(HpStage) * (HpMultiplier * System.Math.Pow(HpGrowth, System.Math.Max(1, tier) - 1) * fightersMultiplier);

        public double ArmorShare(int tier) => System.Math.Min(ArmorMax, ArmorPerTier * (System.Math.Max(1, tier) - 1));

        public double WaveHp(int wave) => System.Math.Pow(HpGrowth, System.Math.Max(1, wave) - 1);

        public double SealIncrease(IReadOnlyList<int> best)
        {
            var tiers = 0;
            for (var i = 0; i < best.Count; i++)
                tiers += System.Math.Min(MaxTier, System.Math.Max(0, best[i]));
            return Seal * tiers;
        }

        public static PartyTrialRules Read(ContentNode node)
        {
            var rules = new PartyTrialRules
            {
                Tries = node.Field("tries").AsInt(1, 100),
                MinFighters = node.Field("minFighters").AsInt(1, 4),
                MaxTier = node.Field("maxTier").AsInt(1, 1000),
                CeilingBase = node.Field("ceilingBase").AsInt(1, 1000),
                CeilingStep = node.Field("ceilingStep").AsInt(0, 1000),
                Seal = node.Field("seal").AsNumber(0, 1),
                FirstClearSigns = node.Field("firstClearSigns").AsInt(0, 1_000_000),
                ExperiencePerTier = node.Field("experiencePerTier").AsNumber(0, 1e6),
                MinSeconds = node.Field("minSeconds").AsNumber(0, 3600),
                SlackSeconds = node.Field("slackSeconds").AsNumber(0, 3600),
                ComplicationFromTier = node.Field("complicationFromTier").AsInt(1, 1000)
            };
            var hp = node.Field("hp");
            rules.HpStage = hp.Field("stage").AsInt(1, 10_000_000);
            rules.HpMultiplier = hp.Field("multiplier").AsNumber(1e-6, 1e9);
            rules.HpGrowth = hp.Field("growth").AsNumber(1, 100);
            var armor = node.Field("armor");
            rules.ArmorPerTier = armor.Field("perTier").AsNumber(0, 100);
            rules.ArmorMax = armor.Field("max").AsNumber(0, 100);
            foreach (var item in node.Field("list").Items())
                rules._list.Add(ReadTrial(item, rules._list.Count));
            if (rules._list.Count == 0)
                throw node.Field("list").Error("at least one trial is needed");
            rules.PerWeek = node.Field("perWeek").AsInt(1, rules._list.Count);
            foreach (var item in node.Field("complications").Items())
                rules._complications.Add(ReadComplication(item));
            return rules;
        }

        private static PartyTrialDef ReadTrial(ContentNode item, int index)
        {
            var ruleName = item.Field("rule").AsString();
            if (!Rules.TryGetValue(ruleName, out var rule))
                throw item.Field("rule").Error($"unknown trial rule '{ruleName}'");
            var name = item.Field("name");
            return new PartyTrialDef(index, item.Field("id").AsString(), rule, name.Field("ru").AsString(), name.Field("en").AsString(),
                item.Field("seconds").AsNumber(10, 3600), item);
        }

        private static PartyTrialComplicationDef ReadComplication(ContentNode item)
        {
            var id = item.Field("id").AsString();
            if (!Complications.TryGetValue(id, out var kind))
                throw item.Field("id").Error($"unknown trial complication '{id}'");
            var name = item.Field("name");
            return new PartyTrialComplicationDef(kind, id, name.Field("ru").AsString(), name.Field("en").AsString(),
                Number(item, "keys", 1), Number(item, "mouse", 1), Number(item, "share", 0), Number(item, "day", 1), Number(item, "night", 1),
                Number(item, "perSecond", 0), Number(item, "afterSeconds", 0));
        }

        private static double Number(ContentNode item, string name, double fallback) =>
            item.HasField(name) ? item.Field(name).AsNumber(0, 100) : fallback;
    }
}
