using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum QuestKind
    {
        Kills,
        Bosses,
        Guardians,
        Keys,
        Clicks,
        MouseMeters,
        SkillCasts,
        RecordStages,
        Prestiges,
        Salvage,
        ActiveMinutes,
        PartyBosses
    }

    public readonly struct QuestReward
    {
        public QuestReward(long shards, double dust, long ingots, long friendship = 0)
        {
            Shards = shards;
            Dust = dust;
            Ingots = ingots;
            Friendship = friendship;
        }

        public long Shards { get; }
        public double Dust { get; }
        public long Ingots { get; }
        public long Friendship { get; }
    }

    public sealed class QuestKindRule
    {
        public QuestKindRule(QuestKind kind, string id, double daily, double weekly, bool party, bool grows = false, long fromStage = 0)
        {
            Kind = kind;
            Id = id;
            Daily = daily;
            Weekly = weekly;
            Party = party;
            Grows = grows;
            FromStage = fromStage;
        }

        public QuestKind Kind { get; }
        public string Id { get; }
        public double Daily { get; }
        public double Weekly { get; }
        public bool Party { get; }
        public bool Grows { get; }
        public long FromStage { get; }
    }

    public sealed class QuestRules
    {
        public const string FileName = "quests.json";
        public const int SupportedVersion = 1;
        public static readonly int KindCount = Enum.GetValues(typeof(QuestKind)).Length;

        private static readonly Dictionary<string, QuestKind> Names = new Dictionary<string, QuestKind>(StringComparer.Ordinal)
        {
            ["kills"] = QuestKind.Kills,
            ["bosses"] = QuestKind.Bosses,
            ["guardians"] = QuestKind.Guardians,
            ["keys"] = QuestKind.Keys,
            ["clicks"] = QuestKind.Clicks,
            ["mouseMeters"] = QuestKind.MouseMeters,
            ["skillCasts"] = QuestKind.SkillCasts,
            ["recordStages"] = QuestKind.RecordStages,
            ["prestiges"] = QuestKind.Prestiges,
            ["salvage"] = QuestKind.Salvage,
            ["activeMinutes"] = QuestKind.ActiveMinutes,
            ["partyBosses"] = QuestKind.PartyBosses
        };

        private QuestRules()
        {
        }

        public int PerDay { get; private set; }
        public int KeepDays { get; private set; }
        public int SwapsPerDay { get; private set; }
        public QuestReward DailyReward { get; private set; }
        public QuestReward PartyReward { get; private set; }
        public int PerWeek { get; private set; }
        public QuestReward WeeklyReward { get; private set; }
        public double StreakBonusPerDay { get; private set; }
        public int StreakMaxDays { get; private set; }
        public double StreakMissKeep { get; private set; }
        public double SizeStart { get; private set; }
        public long SizeFullAtRecord { get; private set; }
        public IReadOnlyList<QuestKindRule> Kinds { get; private set; } = Array.Empty<QuestKindRule>();

        public QuestKindRule Rule(QuestKind kind)
        {
            foreach (var rule in Kinds)
            {
                if (rule.Kind == kind)
                    return rule;
            }
            throw new ArgumentException("no rule for " + kind);
        }

        public double SizeAt(long record) => SizeFullAtRecord <= 0 || record >= SizeFullAtRecord
            ? 1
            : SizeStart + (1 - SizeStart) * record / SizeFullAtRecord;

        public double Target(QuestKind kind, bool daily, long record)
        {
            var rule = Rule(kind);
            var full = daily ? rule.Daily : rule.Weekly;
            return rule.Grows ? System.Math.Max(1, System.Math.Ceiling(full * SizeAt(record))) : full;
        }

        public static string IdOf(QuestKind kind)
        {
            foreach (var pair in Names)
            {
                if (pair.Value == kind)
                    return pair.Key;
            }
            return kind.ToString();
        }

        public static bool TryParse(string id, out QuestKind kind) => Names.TryGetValue(id, out kind);

        public static QuestRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var daily = root.Field("daily");
            var weekly = root.Field("weekly");
            var streak = root.Field("streak");
            var size = root.Field("size");
            var kinds = new List<QuestKindRule>();
            foreach (var item in root.Field("kinds").Items())
            {
                var id = item.Field("id").AsString();
                if (!Names.TryGetValue(id, out var kind))
                    throw item.Field("id").Error($"unknown quest kind '{id}'");
                foreach (var known in kinds)
                {
                    if (known.Kind == kind)
                        throw item.Field("id").Error($"quest kind '{id}' is listed twice");
                }
                kinds.Add(new QuestKindRule(kind, id, item.Field("daily").AsNumber(0, 1e9), item.Field("weekly").AsNumber(0, 1e9),
                    item.HasField("party") && item.Field("party").AsBool(),
                    item.HasField("grows") && item.Field("grows").AsBool(),
                    item.HasField("fromStage") ? item.Field("fromStage").AsLong(0) : 0));
            }
            var rules = new QuestRules
            {
                PerDay = daily.Field("perDay").AsInt(1, 10),
                KeepDays = daily.Field("keepDays").AsInt(1, 30),
                SwapsPerDay = daily.Field("swapsPerDay").AsInt(0, 10),
                DailyReward = Reward(daily.Field("reward")),
                PartyReward = Reward(daily.Field("partyReward")),
                PerWeek = weekly.Field("perWeek").AsInt(1, 10),
                WeeklyReward = Reward(weekly.Field("reward")),
                StreakBonusPerDay = streak.Field("bonusPerDay").AsNumber(0, 1),
                StreakMaxDays = streak.Field("maxDays").AsInt(0, 365),
                StreakMissKeep = streak.Field("missKeep").AsNumber(0, 1),
                SizeStart = size.Field("start").AsNumber(0.01, 1),
                SizeFullAtRecord = size.Field("fullAtRecord").AsLong(0),
                Kinds = kinds
            };
            if (Count(kinds, true, 0) < rules.PerDay + 1)
                throw root.Field("kinds").Error($"need at least {rules.PerDay + 1} daily kinds so that a swap has a choice");
            if (Count(kinds, false, 0) < rules.PerWeek)
                throw root.Field("kinds").Error($"need at least {rules.PerWeek} weekly kinds");
            return rules;
        }

        private static int Count(List<QuestKindRule> kinds, bool daily, long record)
        {
            var count = 0;
            foreach (var kind in kinds)
            {
                if ((daily ? kind.Daily : kind.Weekly) > 0 && kind.FromStage <= record)
                    count++;
            }
            return count;
        }

        private static QuestReward Reward(ContentNode node) => new QuestReward(
            node.HasField("shards") ? node.Field("shards").AsLong(0) : 0,
            node.HasField("dust") ? node.Field("dust").AsNumber(0, 1e9) : 0,
            node.HasField("ingots") ? node.Field("ingots").AsLong(0) : 0,
            node.HasField("friendship") ? node.Field("friendship").AsLong(0) : 0);
    }
}
