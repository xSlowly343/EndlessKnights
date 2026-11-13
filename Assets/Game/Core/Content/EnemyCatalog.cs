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

        private readonly Dictionary<string, EnemyDef> _byId;
        private readonly Dictionary<string, List<EnemyDef>> _normalsByBiome;

        private EnemyCatalog(List<EnemyDef> enemies)
            : this(enemies, enemies.Where(enemy => enemy.Type == EnemyType.Normal)
                .GroupBy(enemy => enemy.Biome)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal))
        {
        }

        private EnemyCatalog(List<EnemyDef> enemies, Dictionary<string, List<EnemyDef>> normalsByBiome)
        {
            All = enemies;
            _byId = enemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
            _normalsByBiome = normalsByBiome;
        }

        public EnemyCatalog Shuffled(ulong seed)
        {
            var pool = All.Where(enemy => enemy.Type == EnemyType.Normal).ToList();
            var rng = new Math.Rng(seed == 0 ? 1 : seed);
            var shuffled = new Dictionary<string, List<EnemyDef>>(StringComparer.Ordinal);
            foreach (var biome in _normalsByBiome.Keys.OrderBy(key => key, StringComparer.Ordinal))
            {
                var left = new List<EnemyDef>(pool);
                var picked = new List<EnemyDef>();
                for (var i = 0; i < _normalsByBiome[biome].Count && left.Count > 0; i++)
                {
                    var index = rng.NextInt(0, left.Count);
                    picked.Add(left[index]);
                    left.RemoveAt(index);
                }
                shuffled[biome] = picked;
            }
            return new EnemyCatalog(new List<EnemyDef>(All), shuffled);
        }

        public IReadOnlyList<EnemyDef> All { get; }

        public EnemyDef this[string id] => _byId[id];

        public bool TryGet(string id, out EnemyDef enemy) => _byId.TryGetValue(id, out enemy!);

        public IReadOnlyList<EnemyDef> Normals(string biome) =>
            _normalsByBiome.TryGetValue(biome, out var normals) ? normals : (IReadOnlyList<EnemyDef>)Array.Empty<EnemyDef>();

        public EnemyDef? Boss(string biome) => BossAt(biome, 0);

        public EnemyDef? BossAt(string biome, int slot)
        {
            EnemyDef? first = null;
            EnemyDef? fallback = null;
            foreach (var enemy in All)
            {
                if (enemy.Biome != biome || enemy.Type != EnemyType.Boss)
                    continue;
                if (enemy.Slot == slot)
                    return enemy;
                first ??= enemy;
                if (enemy.Slot == 0)
                    fallback = enemy;
            }
            return fallback ?? first;
        }

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
                var bosses = enemies.Where(enemy => enemy.Biome == biome && enemy.Type == EnemyType.Boss).ToList();
                if (bosses.Count == 0)
                    throw root.Field("enemies").Error($"biome '{biome}' has no boss");
                if (bosses.Select(enemy => enemy.Slot).Distinct().Count() != bosses.Count)
                    throw root.Field("enemies").Error($"biome '{biome}' has two bosses in one slot");
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
                    modifiers.Add(ModifierReader.Read(modifier));
            }

            return new EnemyDef(
                node.Field("id").AsString(),
                biome,
                type,
                node.Field("hpMultiplier").AsNumber(0.01, 1e6),
                node.Field("goldMultiplier").AsNumber(0.0, 1e6),
                node.Field("sprite").AsString(),
                modifiers,
                node.HasField("slot") ? node.Field("slot").AsInt(0, 100) : 0,
                node.HasField("mechanic") ? GuardianMechanic.Read(node.Field("mechanic")) : null);
        }
    }
}
