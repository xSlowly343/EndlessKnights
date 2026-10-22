using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class ArtifactDef
    {
        public ArtifactDef(string id, string nameRu, string nameEn, IReadOnlyList<Modifier> perLevel)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            PerLevel = perLevel;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> PerLevel { get; }
    }

    public sealed class ArtifactCatalog
    {
        public const string FileName = "artifacts.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, ArtifactDef> _byId;

        private ArtifactCatalog(List<ArtifactDef> all, ExponentialCurve unlockCost, ExponentialCurve levelCost, double allDamagePerLevel)
        {
            All = all;
            UnlockCost = unlockCost;
            LevelCost = levelCost;
            AllDamagePerLevel = allDamagePerLevel;
            _byId = new Dictionary<string, ArtifactDef>(StringComparer.Ordinal);
            foreach (var artifact in all)
                _byId.Add(artifact.Id, artifact);
        }

        public IReadOnlyList<ArtifactDef> All { get; }
        public ExponentialCurve UnlockCost { get; }
        public ExponentialCurve LevelCost { get; }
        public double AllDamagePerLevel { get; }

        public bool TryGet(string id, out ArtifactDef artifact) => _byId.TryGetValue(id, out artifact!);

        public static ArtifactCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var all = new List<ArtifactDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("artifacts").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"artifact id '{id}' is used twice");
                var name = node.Field("name");
                all.Add(new ArtifactDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), ModifierReader.ReadList(node.Field("perLevel"))));
            }
            if (all.Count == 0)
                throw root.Field("artifacts").Error("at least one artifact is required");

            return new ArtifactCatalog(all,
                ExponentialCurve.Read(root.Field("unlockCost")),
                ExponentialCurve.Read(root.Field("levelCost")),
                root.Field("allDamagePerLevel").AsNumber(0, 10));
        }
    }
}
