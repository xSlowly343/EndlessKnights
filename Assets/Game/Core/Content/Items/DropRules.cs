using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;

namespace Game.Core.Content.Items
{
    public sealed class DropRules
    {
        public const string FileName = "drops.json";
        public const int SupportedVersion = 1;
        public const int RarityCount = (int)ItemRarity.Rift + 1;

        private const int MaxItemsPerSource = 10;
        private const string EmptySlot = "empty";

        private static readonly Dictionary<string, GuaranteeColor> ColorRules = new Dictionary<string, GuaranteeColor>(StringComparer.Ordinal)
        {
            ["any"] = GuaranteeColor.Any,
            ["own"] = GuaranteeColor.Own,
            ["ownColorOtherClass"] = GuaranteeColor.OwnColorOtherClass
        };

        private readonly double[] _rarityWeights;
        private readonly double[] _slotWeights;
        private readonly double[] _dust;
        private readonly Dictionary<long, DropGuarantee> _guarantees;
        private readonly Dictionary<GemKind, GemDropChance> _gems;

        private DropRules(double[] rarityWeights, ItemRarity firstCeiling, int ceilingPerBiome, double[] slotWeights, DropTable record, int specialBossEvery,
            DropTable specialBoss, DropTable guardian, DailyDropRules daily, Dictionary<long, DropGuarantee> guarantees, Dictionary<GemKind, GemDropChance> gems,
            long firstTriggerStage, int bagSize, double[] dust)
        {
            _rarityWeights = rarityWeights;
            FirstCeiling = firstCeiling;
            CeilingPerBiome = ceilingPerBiome;
            _slotWeights = slotWeights;
            Record = record;
            SpecialBossEvery = specialBossEvery;
            SpecialBoss = specialBoss;
            Guardian = guardian;
            Daily = daily;
            _guarantees = guarantees;
            _gems = gems;
            FirstTriggerStage = firstTriggerStage;
            BagSize = bagSize;
            _dust = dust;
        }

        public ItemRarity FirstCeiling { get; }
        public int CeilingPerBiome { get; }
        public DropTable Record { get; }
        public int SpecialBossEvery { get; }
        public DropTable SpecialBoss { get; }
        public DropTable Guardian { get; }
        public DailyDropRules Daily { get; }
        public long FirstTriggerStage { get; }
        public int BagSize { get; }
        public IReadOnlyList<double> SlotWeights => _slotWeights;

        public double RarityWeight(ItemRarity rarity) => _rarityWeights[(int)rarity];

        public double DustFor(ItemRarity rarity) => _dust[System.Math.Min((int)rarity, _dust.Length - 1)];

        public ItemRarity CeilingFor(int biomeNumber)
        {
            var ceiling = (long)FirstCeiling + (long)CeilingPerBiome * (System.Math.Max(1, biomeNumber) - 1);
            return (ItemRarity)System.Math.Min(ceiling, (long)ItemRarity.Rift);
        }

        public bool IsSpecialBoss(long stage) => SpecialBossEvery > 0 && stage % SpecialBossEvery == 0;

        public DropGuarantee? GuaranteeAt(long stage) => _guarantees.TryGetValue(stage, out var guarantee) ? guarantee : null;

        public GemDropChance GemChance(GemKind kind) => _gems.TryGetValue(kind, out var chance) ? chance : default;

        public static DropRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            var ceiling = root.Field("ceiling");
            var special = root.Field("specialBoss");
            var triggers = root.Field("gems").Field("trigger");
            return new DropRules(
                ReadRarities(root.Field("rarityWeights"), 0, 1e6, requirePositive: true),
                DropTable.Rarity(ceiling, "firstBiome", ItemRarity.Rare),
                ceiling.Field("perBiome").AsInt(0, RarityCount),
                ReadSlots(root.Field("slotWeights")),
                DropTable.Read(root.Field("record"), MaxItemsPerSource),
                special.Field("every").AsInt(0, 100000),
                DropTable.Read(special, MaxItemsPerSource),
                DropTable.Read(root.Field("guardian"), MaxItemsPerSource),
                DailyDropRules.Read(root.Field("daily")),
                ReadGuarantees(root.Field("guarantees")),
                ReadGems(root.Field("gems")),
                triggers.HasField("firstAtStage") ? triggers.Field("firstAtStage").AsLong(0) : 0,
                root.Field("bag").Field("size").AsInt(1, 10000),
                ReadRarities(root.Field("dust"), 0, 1e12, requirePositive: false));
        }

        private static double[] ReadRarities(ContentNode node, double min, double max, bool requirePositive)
        {
            var weights = new double[RarityCount];
            var total = 0.0;
            for (var rarity = 0; rarity < RarityCount; rarity++)
            {
                var name = ItemRarityNames.Of((ItemRarity)rarity);
                weights[rarity] = node.Field(name).AsNumber(min, max);
                total += weights[rarity];
            }
            RejectUnknown(node, ItemRarityNames.ByName, RarityCount);
            if (requirePositive && total <= 0)
                throw node.Error("at least one rarity needs a positive weight");
            return weights;
        }

        private static double[] ReadSlots(ContentNode node)
        {
            var weights = new double[ItemSlotNames.ByName.Count];
            var total = 0.0;
            foreach (var pair in ItemSlotNames.ByName)
            {
                weights[(int)pair.Value] = node.Field(pair.Key).AsNumber(0, 1e6);
                total += weights[(int)pair.Value];
            }
            RejectUnknown(node, ItemSlotNames.ByName, int.MaxValue);
            if (total <= 0)
                throw node.Error("at least one slot needs a positive weight");
            return weights;
        }

        private static void RejectUnknown<T>(ContentNode node, IReadOnlyDictionary<string, T> known, int limit) where T : struct, Enum
        {
            foreach (var name in node.FieldNames())
            {
                if (!known.TryGetValue(name, out var value) || Convert.ToInt32(value) >= limit)
                    throw node.Field(name).Error("unknown key");
            }
        }

        private static Dictionary<long, DropGuarantee> ReadGuarantees(ContentNode node)
        {
            var guarantees = new Dictionary<long, DropGuarantee>();
            foreach (var item in node.Items())
            {
                var stage = item.Field("stage").AsLong(1);
                if (guarantees.ContainsKey(stage))
                    throw item.Field("stage").Error($"stage {stage} already has a guarantee");
                var slotName = item.Field("slot").AsString();
                ItemSlot? slot = null;
                if (slotName != EmptySlot)
                {
                    if (!ItemSlotNames.ByName.TryGetValue(slotName, out var found))
                        throw item.Field("slot").Error($"unknown slot '{slotName}', expected '{EmptySlot}' or one of {string.Join(", ", ItemSlotNames.ByName.Keys)}");
                    slot = found;
                }
                var colorName = item.HasField("color") ? item.Field("color").AsString() : "any";
                if (!ColorRules.TryGetValue(colorName, out var color))
                    throw item.Field("color").Error($"unknown color rule '{colorName}', expected one of {string.Join(", ", ColorRules.Keys)}");
                if (color == GuaranteeColor.OwnColorOtherClass && slot != ItemSlot.Weapon)
                    throw item.Field("color").Error("only a weapon can belong to another class");
                guarantees[stage] = new DropGuarantee(stage, slot, DropTable.Rarity(item, "rarity", ItemRarity.Simple), color);
            }
            return guarantees;
        }

        private static Dictionary<GemKind, GemDropChance> ReadGems(ContentNode node) =>
            new Dictionary<GemKind, GemDropChance>
            {
                [GemKind.Skill] = GemDropChance.Read(node.Field("skill")),
                [GemKind.Support] = GemDropChance.Read(node.Field("support")),
                [GemKind.Trigger] = GemDropChance.Read(node.Field("trigger"))
            };
    }
}
