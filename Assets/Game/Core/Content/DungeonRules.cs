using System;
using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class DungeonTier
    {
        public DungeonTier(int tier, long stage, DropTable loot, double shards, double uniqueChance, bool needsLimit)
        {
            Tier = tier;
            Stage = stage;
            Loot = loot;
            Shards = shards;
            UniqueChance = uniqueChance;
            NeedsLimit = needsLimit;
        }

        public int Tier { get; }
        public long Stage { get; }
        public DropTable Loot { get; }
        public double Shards { get; }
        public double UniqueChance { get; }
        public bool NeedsLimit { get; }
    }

    public sealed class DungeonModifier
    {
        public DungeonModifier(string id, string nameRu, string nameEn, IReadOnlyList<Modifier> hero, double enemyHp, double bossHp, double bossTimer, double reward)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Hero = hero;
            EnemyHp = enemyHp;
            BossHp = bossHp;
            BossTimer = bossTimer;
            Reward = reward;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Hero { get; }
        public double EnemyHp { get; }
        public double BossHp { get; }
        public double BossTimer { get; }
        public double Reward { get; }
    }

    public sealed class DungeonRules
    {
        public const string FileName = "dungeons.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, DungeonModifier> _byId;

        private DungeonRules(int stages, double bossTimerSeconds, int maxModifiers, long keysFromStage, double bossKeyChance, double specialBossKeyChance,
            double guardianKeyChance, int keyBag, List<DungeonTier> tiers, List<DungeonModifier> modifiers)
        {
            Stages = stages;
            BossTimerSeconds = bossTimerSeconds;
            MaxModifiers = maxModifiers;
            KeysFromStage = keysFromStage;
            BossKeyChance = bossKeyChance;
            SpecialBossKeyChance = specialBossKeyChance;
            GuardianKeyChance = guardianKeyChance;
            KeyBag = keyBag;
            Tiers = tiers;
            Modifiers = modifiers;
            _byId = new Dictionary<string, DungeonModifier>(StringComparer.Ordinal);
            foreach (var modifier in modifiers)
                _byId.Add(modifier.Id, modifier);
        }

        public int Stages { get; }
        public double BossTimerSeconds { get; }
        public int MaxModifiers { get; }
        public long KeysFromStage { get; }
        public double BossKeyChance { get; }
        public double SpecialBossKeyChance { get; }
        public double GuardianKeyChance { get; }
        public int KeyBag { get; }
        public IReadOnlyList<DungeonTier> Tiers { get; }
        public IReadOnlyList<DungeonModifier> Modifiers { get; }

        public bool TryGetModifier(string id, out DungeonModifier modifier) => _byId.TryGetValue(id, out modifier!);

        public DungeonTier? TierAt(int tier) => tier >= 1 && tier <= Tiers.Count ? Tiers[tier - 1] : null;

        public int HighestTierFor(long stage, bool limitCleared)
        {
            var best = 0;
            foreach (var tier in Tiers)
            {
                if (tier.Stage <= stage && (limitCleared || !tier.NeedsLimit))
                    best = tier.Tier;
            }
            return best;
        }

        public static DungeonRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var tiers = new List<DungeonTier>();
            foreach (var item in root.Field("tiers").Items())
            {
                var stage = item.Field("stage").AsLong(1);
                if (tiers.Count > 0 && stage <= tiers[tiers.Count - 1].Stage)
                    throw item.Field("stage").Error("tiers must go up by stage");
                tiers.Add(new DungeonTier(tiers.Count + 1, stage, DropTable.Read(item, 100), item.Field("shards").AsNumber(0, 1e9),
                    item.HasField("uniqueChance") ? item.Field("uniqueChance").AsNumber(0, 1) : 0,
                    item.HasField("needsLimit") && item.Field("needsLimit").AsBool()));
            }
            if (tiers.Count == 0)
                throw root.Field("tiers").Error("at least one tier is required");
            var modifiers = new List<DungeonModifier>();
            foreach (var item in root.Field("modifiers").Items())
            {
                var name = item.Field("name");
                modifiers.Add(new DungeonModifier(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(),
                    item.HasField("hero") ? ModifierReader.ReadList(item.Field("hero")) : Array.Empty<Modifier>(),
                    item.HasField("enemyHp") ? item.Field("enemyHp").AsNumber(0.01, 1000) : 1,
                    item.HasField("bossHp") ? item.Field("bossHp").AsNumber(0.01, 1000) : 1,
                    item.HasField("bossTimer") ? item.Field("bossTimer").AsNumber(0.1, 10) : 1,
                    item.Field("reward").AsNumber(0, 10)));
            }
            var keys = root.Field("keys");
            return new DungeonRules(root.Field("stages").AsInt(1, 100), root.Field("bossTimerSeconds").AsNumber(1, 3600), root.Field("maxModifiers").AsInt(0, 10),
                keys.Field("fromStage").AsLong(1), keys.Field("boss").AsNumber(0, 1), keys.Field("specialBoss").AsNumber(0, 1), keys.Field("guardian").AsNumber(0, 1),
                keys.Field("bag").AsInt(1, 10000), tiers, modifiers);
        }
    }
}
