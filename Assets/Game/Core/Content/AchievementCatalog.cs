using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum AchievementCounter
    {
        Kills,
        Bosses,
        SkillCasts,
        RecordStage,
        Prestiges,
        Keys,
        Clicks,
        MouseKm,
        PlayHours,
        Trials,
        Artifacts,
        Dungeons,
        Salvage
    }

    public sealed class AchievementDef
    {
        public AchievementDef(string id, string nameRu, string nameEn, AchievementCounter counter, double target)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Counter = counter;
            Target = target;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public AchievementCounter Counter { get; }
        public double Target { get; }
    }

    public sealed class AchievementCatalog
    {
        public const string FileName = "achievements.json";
        public const int SupportedVersion = 1;
        public static readonly int CounterCount = Enum.GetValues(typeof(AchievementCounter)).Length;

        private static readonly Dictionary<string, AchievementCounter> Names = new Dictionary<string, AchievementCounter>(StringComparer.Ordinal)
        {
            ["kills"] = AchievementCounter.Kills,
            ["bosses"] = AchievementCounter.Bosses,
            ["skillCasts"] = AchievementCounter.SkillCasts,
            ["recordStage"] = AchievementCounter.RecordStage,
            ["prestiges"] = AchievementCounter.Prestiges,
            ["keys"] = AchievementCounter.Keys,
            ["clicks"] = AchievementCounter.Clicks,
            ["mouseKm"] = AchievementCounter.MouseKm,
            ["playHours"] = AchievementCounter.PlayHours,
            ["trials"] = AchievementCounter.Trials,
            ["artifacts"] = AchievementCounter.Artifacts,
            ["dungeons"] = AchievementCounter.Dungeons,
            ["salvage"] = AchievementCounter.Salvage
        };

        private AchievementCatalog(List<AchievementDef> all)
        {
            All = all;
        }

        public IReadOnlyList<AchievementDef> All { get; }

        public static string IdOf(AchievementCounter counter)
        {
            foreach (var pair in Names)
            {
                if (pair.Value == counter)
                    return pair.Key;
            }
            return counter.ToString();
        }

        public static bool TryParse(string id, out AchievementCounter counter) => Names.TryGetValue(id, out counter);

        public static AchievementCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var all = new List<AchievementDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("achievements").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"achievement '{id}' is listed twice");
                var counterId = node.Field("counter").AsString();
                if (!Names.TryGetValue(counterId, out var counter))
                    throw node.Field("counter").Error($"unknown counter '{counterId}'");
                var name = node.Field("name");
                all.Add(new AchievementDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), counter, node.Field("target").AsNumber(1, 1e15)));
            }
            return new AchievementCatalog(all);
        }
    }
}
