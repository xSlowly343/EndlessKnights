using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public enum CompanionGearSlot
    {
        Collar,
        Bag,
        Amulet
    }

    public enum CompanionGearStat
    {
        Duration,
        Loot,
        Rare,
        HomeTraits,
        Experience,
        Bond
    }

    public enum CompanionSetPower
    {
        None,
        InstantTrip,
        HomeSlot
    }

    public sealed class GearSlotRule
    {
        public GearSlotRule(CompanionGearSlot slot, int stage, int stars)
        {
            Slot = slot;
            Stage = stage;
            Stars = stars;
        }

        public CompanionGearSlot Slot { get; }
        public int Stage { get; }
        public int Stars { get; }
    }

    public sealed class GearAffixRule
    {
        public GearAffixRule(CompanionGearStat stat, double min, double max)
        {
            Stat = stat;
            Min = min;
            Max = max;
        }

        public CompanionGearStat Stat { get; }
        public double Min { get; }
        public double Max { get; }
    }

    public sealed class GearSetBonus
    {
        public GearSetBonus(int pieces, CompanionGearStat stat, double value, CompanionSetPower power)
        {
            Pieces = pieces;
            Stat = stat;
            Value = value;
            Power = power;
        }

        public int Pieces { get; }
        public CompanionGearStat Stat { get; }
        public double Value { get; }
        public CompanionSetPower Power { get; }
    }

    public sealed class GearSet
    {
        public GearSet(string id, IReadOnlyList<GearSetBonus> bonuses)
        {
            Id = id;
            Bonuses = bonuses;
        }

        public string Id { get; }
        public IReadOnlyList<GearSetBonus> Bonuses { get; }
    }

    public sealed class CompanionGearRules
    {
        public string Building { get; private set; } = string.Empty;
        public IReadOnlyList<GearSlotRule> Slots { get; private set; } = Array.Empty<GearSlotRule>();
        public int MaxRarity { get; private set; }
        public long ShardsPerRarity { get; private set; }
        public int BonesPerRarity { get; private set; }
        public long SetShards { get; private set; }
        public double RarityStep { get; private set; }
        public IReadOnlyList<GearAffixRule> Affixes { get; private set; } = Array.Empty<GearAffixRule>();
        public IReadOnlyList<GearSet> Sets { get; private set; } = Array.Empty<GearSet>();

        public GearSlotRule? Slot(CompanionGearSlot slot)
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Slot == slot)
                    return Slots[i];
            }
            return null;
        }

        public GearSet? Set(string id)
        {
            for (var i = 0; i < Sets.Count; i++)
            {
                if (Sets[i].Id == id)
                    return Sets[i];
            }
            return null;
        }

        public int AffixCount(int rarity) => System.Math.Min(Affixes.Count, (rarity + 1) / 2 + 1);

        public static readonly Dictionary<string, CompanionGearSlot> SlotNames = new Dictionary<string, CompanionGearSlot>(StringComparer.Ordinal)
        {
            ["collar"] = CompanionGearSlot.Collar,
            ["bag"] = CompanionGearSlot.Bag,
            ["amulet"] = CompanionGearSlot.Amulet
        };

        public static readonly Dictionary<string, CompanionGearStat> StatNames = new Dictionary<string, CompanionGearStat>(StringComparer.Ordinal)
        {
            ["duration"] = CompanionGearStat.Duration,
            ["loot"] = CompanionGearStat.Loot,
            ["rare"] = CompanionGearStat.Rare,
            ["homeTraits"] = CompanionGearStat.HomeTraits,
            ["experience"] = CompanionGearStat.Experience,
            ["bond"] = CompanionGearStat.Bond
        };

        private static readonly Dictionary<string, CompanionSetPower> Powers = new Dictionary<string, CompanionSetPower>(StringComparer.Ordinal)
        {
            ["none"] = CompanionSetPower.None,
            ["instantTrip"] = CompanionSetPower.InstantTrip,
            ["homeSlot"] = CompanionSetPower.HomeSlot
        };

        internal static CompanionGearRules Load(ContentNode node, int stages, int maxStars)
        {
            var slots = new List<GearSlotRule>();
            foreach (var item in node.Field("slots").Items())
            {
                var name = item.Field("slot").AsString();
                if (!SlotNames.TryGetValue(name, out var slot))
                    throw item.Field("slot").Error($"unknown gear slot '{name}'");
                slots.Add(new GearSlotRule(slot, item.Field("stage").AsInt(0, stages - 1), item.Field("stars").AsInt(0, maxStars)));
            }
            var affixes = new List<GearAffixRule>();
            foreach (var item in node.Field("affixes").Items())
            {
                var min = item.Field("min").AsNumber(0, 1);
                affixes.Add(new GearAffixRule(Stat(item.Field("stat")), min, item.Field("max").AsNumber(min, 1)));
            }
            var sets = new List<GearSet>();
            foreach (var item in node.Field("sets").Items())
            {
                var bonuses = new List<GearSetBonus>();
                foreach (var bonus in item.Field("bonuses").Items())
                {
                    var power = bonus.HasField("power") ? bonus.Field("power").AsString() : "none";
                    if (!Powers.TryGetValue(power, out var parsed))
                        throw bonus.Field("power").Error($"unknown set power '{power}'");
                    bonuses.Add(new GearSetBonus(bonus.Field("pieces").AsInt(1, 3), Stat(bonus.Field("stat")), bonus.Field("value").AsNumber(0, 1), parsed));
                }
                sets.Add(new GearSet(item.Field("id").AsString(), bonuses));
            }
            return new CompanionGearRules
            {
                Building = node.Field("building").AsString(),
                Slots = slots,
                MaxRarity = node.Field("maxRarity").AsInt(1, 20),
                ShardsPerRarity = node.Field("shardsPerRarity").AsLong(0),
                BonesPerRarity = node.Field("bonesPerRarity").AsInt(0, 1000),
                SetShards = node.Field("setShards").AsLong(0),
                RarityStep = node.Field("rarityStep").AsNumber(0, 10),
                Affixes = affixes,
                Sets = sets
            };
        }

        private static CompanionGearStat Stat(ContentNode node)
        {
            var name = node.AsString();
            if (!StatNames.TryGetValue(name, out var stat))
                throw node.Error($"unknown gear stat '{name}'");
            return stat;
        }
    }
}
