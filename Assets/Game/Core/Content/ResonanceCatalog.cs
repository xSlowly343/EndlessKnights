using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class ResonanceMilestone
    {
        public ResonanceMilestone(int level, IReadOnlyList<Modifier> modifiers)
        {
            Level = level;
            Modifiers = modifiers;
        }

        public int Level { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class ResonanceBranch
    {
        public ResonanceBranch(string id, string nameRu, string nameEn, int maxLevel, ExponentialCurve cost, IReadOnlyList<Modifier> perLevel, IReadOnlyList<ResonanceMilestone> milestones)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            MaxLevel = maxLevel;
            Cost = cost;
            PerLevel = perLevel;
            Milestones = milestones;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int MaxLevel { get; }
        public ExponentialCurve Cost { get; }
        public IReadOnlyList<Modifier> PerLevel { get; }
        public IReadOnlyList<ResonanceMilestone> Milestones { get; }
    }

    public sealed class ResonanceCatalog
    {
        public const string FileName = "resonance.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, ResonanceBranch> _byId;

        private ResonanceCatalog(List<ResonanceBranch> all)
        {
            All = all;
            _byId = new Dictionary<string, ResonanceBranch>(StringComparer.Ordinal);
            foreach (var branch in all)
                _byId.Add(branch.Id, branch);
        }

        public IReadOnlyList<ResonanceBranch> All { get; }

        public bool TryGet(string id, out ResonanceBranch branch) => _byId.TryGetValue(id, out branch!);

        public static ResonanceCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var all = new List<ResonanceBranch>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("branches").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"resonance branch '{id}' is listed twice");
                var maxLevel = item.Field("maxLevel").AsInt(1, 1000);
                var milestones = new List<ResonanceMilestone>();
                foreach (var step in item.Field("milestones").Items())
                    milestones.Add(new ResonanceMilestone(step.Field("level").AsInt(1, maxLevel), ModifierReader.ReadList(step.Field("modifiers"))));
                var name = item.Field("name");
                all.Add(new ResonanceBranch(id, name.Field("ru").AsString(), name.Field("en").AsString(), maxLevel, ExponentialCurve.Read(item.Field("cost")),
                    ModifierReader.ReadList(item.Field("perLevel")), milestones));
            }
            return new ResonanceCatalog(all);
        }
    }
}
