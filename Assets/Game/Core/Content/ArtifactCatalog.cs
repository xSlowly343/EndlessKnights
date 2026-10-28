using System;
using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public enum ArtifactType
    {
        Class,
        Damage,
        Economy,
        Tempo,
        Meta,
        Late
    }

    public sealed class ArtifactDef
    {
        public static readonly IReadOnlyList<Modifier> NoModifiers = Array.Empty<Modifier>();
        public static readonly IReadOnlyList<ArtifactType> NoTypes = Array.Empty<ArtifactType>();

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
        public int Group { get; set; } = 1;
        public ArtifactType Type { get; set; } = ArtifactType.Damage;
        public HeroClass? Class { get; set; }
        public int MaxLevel { get; set; }
        public IReadOnlyList<Modifier> PerArtifact { get; set; } = NoModifiers;
        public IReadOnlyList<ArtifactType> BoostTypes { get; set; } = NoTypes;
        public double Boost { get; set; }
        public double BoostAllDamage { get; set; }

        public bool Capped => MaxLevel > 0;
        public bool WorksFor(HeroClass heroClass) => Class == null || Class == heroClass;
        public int EffectiveLevel(int level) => Capped ? System.Math.Min(level, MaxLevel) : level;
    }

    public sealed class ArtifactCatalog
    {
        public const string FileName = "artifacts.json";
        public const int SupportedVersion = 2;

        private static readonly IReadOnlyDictionary<string, ArtifactType> TypeNames = new Dictionary<string, ArtifactType>(StringComparer.Ordinal)
        {
            ["class"] = ArtifactType.Class,
            ["damage"] = ArtifactType.Damage,
            ["economy"] = ArtifactType.Economy,
            ["tempo"] = ArtifactType.Tempo,
            ["meta"] = ArtifactType.Meta,
            ["late"] = ArtifactType.Late
        };

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
            foreach (var artifact in all)
                Groups = System.Math.Max(Groups, artifact.Group);
        }

        public IReadOnlyList<ArtifactDef> All { get; }
        public ExponentialCurve UnlockCost { get; }
        public ExponentialCurve LevelCost { get; }
        public double AllDamagePerLevel { get; }
        public int Groups { get; }
        public double JackpotMultiplier { get; private set; } = 1;
        public bool AllDamageCompounds { get; private set; }

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
                all.Add(Read(node, id));
            }
            if (all.Count == 0)
                throw root.Field("artifacts").Error("at least one artifact is required");
            CheckGroups(all, root.Field("artifacts"));

            return new ArtifactCatalog(all,
                ExponentialCurve.Read(root.Field("unlockCost")),
                ExponentialCurve.Read(root.Field("levelCost")),
                root.Field("allDamagePerLevel").AsNumber(0, 10))
            {
                JackpotMultiplier = root.Field("jackpotMultiplier").AsNumber(1, 1000),
                AllDamageCompounds = root.HasField("allDamageCompounds") && root.Field("allDamageCompounds").AsBool()
            };
        }

        private static ArtifactDef Read(ContentNode node, string id)
        {
            var name = node.Field("name");
            return new ArtifactDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), ModifierReader.ReadList(node.Field("perLevel")))
            {
                Group = node.HasField("group") ? node.Field("group").AsInt(1, 100) : 1,
                Type = node.HasField("type") ? TypeOf(node.Field("type")) : ArtifactType.Damage,
                Class = node.HasField("class") ? ClassOf(node.Field("class")) : (HeroClass?)null,
                MaxLevel = node.HasField("maxLevel") ? node.Field("maxLevel").AsInt(1, 100000) : 0,
                PerArtifact = node.HasField("perArtifact") ? ModifierReader.ReadList(node.Field("perArtifact")) : ArtifactDef.NoModifiers,
                BoostTypes = node.HasField("boost") ? TypesOf(node.Field("boost").Field("types")) : ArtifactDef.NoTypes,
                Boost = node.HasField("boost") ? node.Field("boost").Field("value").AsNumber(0, 10) : 0,
                BoostAllDamage = node.HasField("boostAllDamage") ? node.Field("boostAllDamage").AsNumber(0, 10) : 0
            };
        }

        private static void CheckGroups(List<ArtifactDef> all, ContentNode node)
        {
            var max = 0;
            foreach (var artifact in all)
                max = System.Math.Max(max, artifact.Group);
            for (var group = 1; group <= max; group++)
            {
                if (!all.Exists(a => a.Group == group))
                    throw node.Error($"artifact group {group} is empty");
            }
        }

        private static ArtifactType TypeOf(ContentNode node)
        {
            var text = node.AsString();
            if (!TypeNames.TryGetValue(text, out var type))
                throw node.Error($"unknown artifact type '{text}'");
            return type;
        }

        private static IReadOnlyList<ArtifactType> TypesOf(ContentNode node)
        {
            var types = new List<ArtifactType>();
            foreach (var item in node.Items())
                types.Add(TypeOf(item));
            return types;
        }

        private static HeroClass ClassOf(ContentNode node)
        {
            var text = node.AsString();
            if (!HeroClassNames.ByName.TryGetValue(text, out var heroClass))
                throw node.Error($"unknown hero class '{text}'");
            return heroClass;
        }
    }
}
