using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class TrialDef
    {
        public TrialDef(string id, string nameRu, string nameEn, long goalStage, IReadOnlyList<Modifier> rules, bool noSkills, IReadOnlyList<Modifier> reward)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            GoalStage = goalStage;
            Rules = rules;
            NoSkills = noSkills;
            Reward = reward;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long GoalStage { get; }
        public IReadOnlyList<Modifier> Rules { get; }
        public bool NoSkills { get; }
        public IReadOnlyList<Modifier> Reward { get; }
    }

    public sealed class ChronicleCatalog
    {
        public const string FileName = "chronicle.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, TrialDef> _byId;

        private ChronicleCatalog(List<TrialDef> all, int minPrestiges)
        {
            All = all;
            MinPrestiges = minPrestiges;
            _byId = new Dictionary<string, TrialDef>(StringComparer.Ordinal);
            foreach (var trial in all)
                _byId.Add(trial.Id, trial);
        }

        public IReadOnlyList<TrialDef> All { get; }
        public int MinPrestiges { get; }

        public bool TryGet(string id, out TrialDef trial) => _byId.TryGetValue(id, out trial!);

        public static ChronicleCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var all = new List<TrialDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("trials").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"trial '{id}' is listed twice");
                var name = node.Field("name");
                var rules = node.HasField("rules") ? ModifierReader.ReadList(node.Field("rules")) : Array.Empty<Modifier>();
                var noSkills = node.HasField("noSkills") && node.Field("noSkills").AsBool();
                if (rules.Count == 0 && !noSkills)
                    throw node.Error($"trial '{id}' has no condition");
                all.Add(new TrialDef(
                    id,
                    name.Field("ru").AsString(),
                    name.Field("en").AsString(),
                    node.Field("goalStage").AsLong(2),
                    rules,
                    noSkills,
                    ModifierReader.ReadList(node.Field("reward"))));
            }
            return new ChronicleCatalog(all, root.Field("minPrestiges").AsInt(0, 1000));
        }
    }
}
