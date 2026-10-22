using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class EnemyCatalog
    {
        public const string FileName = "enemies.json";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, EnemyType> Types = new Dictionary<string, EnemyType>(StringComparer.Ordinal)
        {
            ["normal"] = EnemyType.Normal,
            ["boss"] = EnemyType.Boss,
            ["guardian"] = EnemyType.Guardian
        };

        private static readonly Dictionary<string, ModifierKind> Kinds = new Dictionary<string, ModifierKind>(StringComparer.Ordinal)
        {
            ["flat"] = ModifierKind.Flat,
            ["increased"] = ModifierKind.Increased,
            ["more"] = ModifierKind.More
        };

        private readonly Dictionary<string, EnemyDef> _byId;
        private readonly Dictionary<string, List<EnemyDef>> _normalsByBiome;

        private EnemyCatalog(List<EnemyDef> enemies)
        {
            All = enemies;
            _byId = enemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
            _normalsByBiome = enemies.Where(enemy => enemy.Type == EnemyType.Normal)
                .GroupBy(enemy => enemy.Biome)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        }

        public IReadOnlyList<EnemyDef> All { get; }

        public EnemyDef this[string id] => _byId[id];

        public bool TryGet(string id, out EnemyDef enemy) => _byId.TryGetValue(id, out enemy!);

        public IReadOnlyList<EnemyDef> Normals(string biome) =>
            _normalsByBiome.TryGetValue(biome, out var normals) ? normals : (IReadOnlyList<EnemyDef>)Array.Empty<EnemyDef>();

        public EnemyDef? Boss(string biome) => All.FirstOrDefault(enemy => enemy.Biome == biome && enemy.Type == EnemyType.Boss);

        public EnemyDef? Guardian(string biome) => All.FirstOrDefault(enemy => enemy.Biome == biome && enemy.Type == EnemyType.Guardian);

        public static EnemyCatalog Load(string json, IReadOnlyCollection<string> knownBiomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var enemies = new List<EnemyDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("enemies").Items())
            {
                var enemy = ReadEnemy(node, knownBiomes);
                if (!ids.Add(enemy.Id))
                    throw node.Field("id").Error($"enemy id '{enemy.Id}' is used twice");
                enemies.Add(enemy);
            }

            var catalog = new EnemyCatalog(enemies);
            foreach (var biome in enemies.Select(enemy => enemy.Biome).Distinct())
            {
                if (catalog.Normals(biome).Count == 0)
                    throw root.Field("enemies").Error($"biome '{biome}' has no normal enemies");
                if (enemies.Count(enemy => enemy.Biome == biome && enemy.Type == EnemyType.Boss) > 1)
                    throw root.Field("enemies").Error($"biome '{biome}' has more than one boss");
                if (enemies.Count(enemy => enemy.Biome == biome && enemy.Type == EnemyType.Guardian) > 1)
                    throw root.Field("enemies").Error($"biome '{biome}' has more than one guardian");
            }
            return catalog;
        }

        private static EnemyDef ReadEnemy(ContentNode node, IReadOnlyCollection<string> knownBiomes)
        {
            var biomeNode = node.Field("biome");
            var biome = biomeNode.AsString();
            if (!knownBiomes.Contains(biome))
                throw biomeNode.Error($"unknown biome '{biome}', it must be listed in {StageTable.FileName}");

            var typeNode = node.Field("type");
            var typeName = typeNode.AsString();
            if (!Types.TryGetValue(typeName, out var type))
                throw typeNode.Error($"unknown type '{typeName}', expected normal, boss or guardian");

            var modifiers = new List<Modifier>();
            if (node.HasField("modifiers"))
            {
                foreach (var modifier in node.Field("modifiers").Items())
                    modifiers.Add(ReadModifier(modifier));
            }

            return new EnemyDef(
                node.Field("id").AsString(),
                biome,
                type,
                node.Field("hpMultiplier").AsNumber(0.01, 1e6),
                node.Field("goldMultiplier").AsNumber(0.0, 1e6),
                node.Field("sprite").AsString(),
                modifiers);
        }

        private static Modifier ReadModifier(ContentNode node)
        {
            var kindNode = node.Field("kind");
            var kindName = kindNode.AsString();
            if (!Kinds.TryGetValue(kindName, out var kind))
                throw kindNode.Error($"unknown modifier kind '{kindName}', expected flat, increased or more");

            var tags = StatTags.None;
            if (node.HasField("tags"))
            {
                foreach (var tagNode in node.Field("tags").Items())
                {
                    var tagName = tagNode.AsString();
                    if (!Enum.TryParse<StatTags>(tagName, false, out var tag) || tag == StatTags.None)
                        throw tagNode.Error($"unknown tag '{tagName}'");
                    tags |= tag;
                }
            }

            return new Modifier(new StatId(node.Field("stat").AsString()), kind, node.Field("value").AsNumber(-1e6, 1e6), tags);
        }
    }
}
