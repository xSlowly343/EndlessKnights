using System;
using System.Collections.Generic;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class ItemCatalog
    {
        public const string FileName = "items.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, GearBase> _bases;
        private readonly Dictionary<string, SetDef> _sets;
        private readonly Dictionary<string, AffixDef> _affixes;
        private readonly RarityRule[] _rarities;

        private ItemCatalog(List<GearBase> bases, List<AffixDef> affixes, List<SetDef> sets, RarityRule[] rarities, int tiers, double topWeight, double nextWeight, double restWeight)
        {
            Sets = sets;
            _sets = new Dictionary<string, SetDef>(StringComparer.Ordinal);
            foreach (var set in sets)
                _sets.Add(set.Id, set);
            Bases = bases;
            Affixes = affixes;
            _rarities = rarities;
            Tiers = tiers;
            TopWeight = topWeight;
            NextWeight = nextWeight;
            RestWeight = restWeight;
            _bases = new Dictionary<string, GearBase>(StringComparer.Ordinal);
            foreach (var gearBase in bases)
                _bases.Add(gearBase.Id, gearBase);
            _affixes = new Dictionary<string, AffixDef>(StringComparer.Ordinal);
            foreach (var affix in affixes)
                _affixes.Add(affix.Id, affix);
        }

        public IReadOnlyList<GearBase> Bases { get; }
        public IReadOnlyList<AffixDef> Affixes { get; }
        public IReadOnlyList<SetDef> Sets { get; }
        public IReadOnlyList<UniqueDef> Uniques { get; private set; } = Array.Empty<UniqueDef>();
        public bool TryGetUnique(string id, out UniqueDef unique)
        {
            foreach (var candidate in Uniques)
            {
                if (candidate.Id == id)
                {
                    unique = candidate;
                    return true;
                }
            }
            unique = null!;
            return false;
        }

        public UniqueDef? PickUnique(ItemSlot slot, ItemRarity ceiling, Rng rng)
        {
            var count = 0;
            foreach (var unique in Uniques)
            {
                if (unique.Slot == slot && unique.Rarity <= ceiling)
                    count++;
            }
            if (count == 0)
                return null;
            var pick = rng.NextInt(0, count);
            foreach (var unique in Uniques)
            {
                if (unique.Slot == slot && unique.Rarity <= ceiling && pick-- == 0)
                    return unique;
            }
            return null;
        }

        public bool TryGetSet(string id, out SetDef set) => _sets.TryGetValue(id, out set!);

        public double[] CraftBaseCost { get; private set; } = new double[Enum.GetValues(typeof(ItemRarity)).Length];
        public IReadOnlyList<double> CraftSteps { get; private set; } = new[] { 1.0 };

        public double CraftCost(SetDef set, int collected, bool complete)
        {
            var baseCost = CraftBaseCost[(int)set.Rarity];
            return complete ? baseCost : baseCost * CraftSteps[System.Math.Min(collected, CraftSteps.Count - 1)];
        }

        public SetDef? PickSet(ItemSlot slot, ItemRarity rarity, Rng rng)
        {
            var candidates = new List<SetDef>();
            var best = ItemRarity.Simple;
            foreach (var set in Sets)
            {
                if (set.Has(slot) && set.Rarity <= rarity && set.Rarity > best)
                    best = set.Rarity;
            }
            foreach (var set in Sets)
            {
                if (set.Has(slot) && set.Rarity == best)
                    candidates.Add(set);
            }
            return candidates.Count == 0 ? null : candidates[rng.NextInt(0, candidates.Count)];
        }

        public GearItem RollSetPiece(SetDef set, ItemSlot slot, string? color, ItemRarity rarity, int itemLevel, Rng rng)
        {
            var gearBase = PickBase(slot, set.Color ?? color, itemLevel, rng);
            return new GearItem(gearBase, rarity, itemLevel, RollAffixes(slot, rarity, itemLevel, rng), set);
        }
        public int Tiers { get; }
        public double TopWeight { get; }
        public double NextWeight { get; }
        public double RestWeight { get; }

        public RarityRule RuleOf(ItemRarity rarity) => _rarities[(int)rarity];

        public bool TryGetBase(string id, out GearBase gearBase) => _bases.TryGetValue(id, out gearBase!);

        public bool TryGetAffix(string id, out AffixDef affix) => _affixes.TryGetValue(id, out affix!);

        public GearItem Roll(ItemSlot slot, string? color, ItemRarity rarity, int itemLevel, Rng rng)
        {
            var gearBase = PickBase(slot, color, itemLevel, rng);
            return new GearItem(gearBase, rarity, itemLevel, RollAffixes(slot, rarity, itemLevel, rng));
        }

        public GearBase PickBase(ItemSlot slot, string? color, int itemLevel, Rng rng) =>
            PickBase(slot, color, itemLevel, rng, exactColor: color != null && HasColored(slot, color, itemLevel));

        private bool HasColored(ItemSlot slot, string color, int itemLevel)
        {
            foreach (var gearBase in Bases)
            {
                if (gearBase.Slot == slot && gearBase.Color == color && gearBase.FromBiome <= itemLevel)
                    return true;
            }
            return false;
        }

        private GearBase PickBase(ItemSlot slot, string? color, int itemLevel, Rng rng, bool exactColor)
        {
            var candidates = new List<GearBase>();
            GearBase? best = null;
            foreach (var gearBase in Bases)
            {
                if (gearBase.Slot != slot || (color != null && gearBase.Color != null && gearBase.Color != color) || (exactColor && gearBase.Color != color))
                    continue;
                if (gearBase.FromBiome <= itemLevel)
                    candidates.Add(gearBase);
                else if (best == null || gearBase.FromBiome < best.FromBiome)
                    best = gearBase;
            }
            if (candidates.Count == 0)
                return best ?? throw new InvalidOperationException($"no base for slot {slot}");
            return candidates[rng.NextInt(0, candidates.Count)];
        }

        public IReadOnlyList<GearAffix> RollAffixes(ItemSlot slot, ItemRarity rarity, int itemLevel, Rng rng)
        {
            var rule = RuleOf(rarity);
            var chosen = new List<GearAffix>();
            var suffixes = (rule.Affixes + 1) / 2;
            var prefixes = rule.Affixes - suffixes;
            Pick(slot, true, prefixes, rule, itemLevel, rng, chosen);
            Pick(slot, false, suffixes, rule, itemLevel, rng, chosen);
            return chosen;
        }

        private void Pick(ItemSlot slot, bool prefix, int count, RarityRule rule, int itemLevel, Rng rng, List<GearAffix> chosen)
        {
            for (var n = 0; n < count; n++)
            {
                var pool = new List<AffixDef>();
                foreach (var affix in Affixes)
                {
                    if (affix.Prefix == prefix && affix.FitsSlot(slot) && Free(affix, chosen))
                        pool.Add(affix);
                }
                if (pool.Count == 0)
                    return;
                var def = pool[rng.NextInt(0, pool.Count)];
                var tier = RollTier(rule, itemLevel, rng);
                chosen.Add(new GearAffix(def, tier, def.ValueAt(tier, Tiers)));
            }
        }

        private static bool Free(AffixDef affix, List<GearAffix> chosen)
        {
            foreach (var taken in chosen)
            {
                if (ReferenceEquals(taken.Def, affix) || (affix.Group != null && taken.Def.Group == affix.Group))
                    return false;
            }
            return true;
        }

        public int RollTier(RarityRule rule, int itemLevel, Rng rng)
        {
            var open = System.Math.Max(1, System.Math.Min(Tiers, itemLevel));
            if (rule.TopTiers > 0)
                return System.Math.Max(1, open - rng.NextInt(0, System.Math.Min(rule.TopTiers, open)));
            if (open == 1)
                return 1;
            var roll = rng.NextDouble() * (TopWeight + NextWeight + (open > 2 ? RestWeight : 0));
            if (roll < TopWeight)
                return open;
            if (roll < TopWeight + NextWeight || open == 2)
                return open - 1;
            return rng.NextInt(1, open - 1);
        }

        public static ItemCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var tiers = root.Field("tiers").AsInt(1, 20);
            var weights = root.Field("tierWeights");
            var rarities = ReadRarities(root.Field("rarities"), tiers);
            var bases = new List<GearBase>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("bases").Items())
            {
                var gearBase = ReadBase(item);
                if (!ids.Add(gearBase.Id))
                    throw item.Field("id").Error($"base id '{gearBase.Id}' is used twice");
                bases.Add(gearBase);
            }
            var affixes = new List<AffixDef>();
            var affixIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("affixes").Items())
            {
                var affix = ReadAffix(item);
                if (!affixIds.Add(affix.Id))
                    throw item.Field("id").Error($"affix id '{affix.Id}' is used twice");
                affixes.Add(affix);
            }
            var sets = new List<SetDef>();
            var setIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("sets").Items())
            {
                var set = ReadSet(item);
                if (!setIds.Add(set.Id))
                    throw item.Field("id").Error($"set id '{set.Id}' is used twice");
                sets.Add(set);
            }
            var catalog = new ItemCatalog(bases, affixes, sets, rarities, tiers,
                weights.Field("top").AsNumber(0, 1), weights.Field("next").AsNumber(0, 1), weights.Field("rest").AsNumber(0, 1));
            ReadCraft(root.Field("craft"), catalog);
            catalog.Uniques = ReadUniques(root.Field("uniques"), catalog);
            return catalog;
        }

        private static List<UniqueDef> ReadUniques(ContentNode node, ItemCatalog catalog)
        {
            var uniques = new List<UniqueDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"unique id '{id}' is used twice");
                var baseNode = item.Field("base");
                if (!catalog.TryGetBase(baseNode.AsString(), out var gearBase))
                    throw baseNode.Error($"unknown base '{baseNode.AsString()}'");
                var rarityNode = item.Field("rarity");
                if (!ItemRarityNames.ByName.TryGetValue(rarityNode.AsString(), out var rarity))
                    throw rarityNode.Error($"unknown rarity '{rarityNode.AsString()}'");
                var name = item.Field("name");
                var rule = item.Field("rule");
                uniques.Add(new UniqueDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), gearBase, rarity,
                    ModifierReader.ReadList(item.Field("lines")), rule.Field("ru").AsString(), rule.Field("en").AsString()));
            }
            return uniques;
        }

        private static void ReadCraft(ContentNode node, ItemCatalog catalog)
        {
            var costs = new double[Enum.GetValues(typeof(ItemRarity)).Length];
            var baseCost = node.Field("baseCost");
            foreach (var name in baseCost.FieldNames())
            {
                if (!ItemRarityNames.ByName.TryGetValue(name, out var rarity) || rarity < ItemRarity.Rare)
                    throw baseCost.Field(name).Error("craft costs are for set rarities from rare up");
                costs[(int)rarity] = baseCost.Field(name).AsNumber(0, 1e9);
            }
            var steps = new List<double>();
            foreach (var step in node.Field("steps").Items())
                steps.Add(step.AsNumber(0, 1e6));
            if (steps.Count == 0)
                throw node.Field("steps").Error("needs at least one step");
            catalog.CraftBaseCost = costs;
            catalog.CraftSteps = steps;
        }

        private static RarityRule[] ReadRarities(ContentNode node, int tiers)
        {
            var rules = new RarityRule[Enum.GetValues(typeof(ItemRarity)).Length];
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                var item = node.Field(ItemRarityNames.Of(rarity));
                rules[(int)rarity] = new RarityRule(item.Field("affixes").AsInt(0, 20), item.Field("topTiers").AsInt(0, tiers));
            }
            return rules;
        }

        private static GearBase ReadBase(ContentNode node)
        {
            var slotNode = node.Field("slot");
            if (!ItemSlotNames.ByName.TryGetValue(slotNode.AsString(), out var slot) || slot == ItemSlot.Weapon)
                throw slotNode.Error($"unknown gear slot '{slotNode.AsString()}'");
            var name = node.Field("name");
            return new GearBase(
                node.Field("id").AsString(), slot,
                node.HasField("color") ? node.Field("color").AsString() : null,
                node.Field("fromBiome").AsInt(1, 100),
                name.Field("ru").AsString(), name.Field("en").AsString(),
                ModifierReader.ReadList(node.Field("implicit")));
        }

        private static SetDef ReadSet(ContentNode node)
        {
            var rarityNode = node.Field("rarity");
            if (!ItemRarityNames.ByName.TryGetValue(rarityNode.AsString(), out var rarity) || rarity < ItemRarity.Rare)
                throw rarityNode.Error("a set needs a rarity from rare up");
            var pieces = new Dictionary<ItemSlot, IReadOnlyList<Modifier>>();
            var piecesNode = node.Field("pieces");
            foreach (var slotName in piecesNode.FieldNames())
            {
                if (!ItemSlotNames.ByName.TryGetValue(slotName, out var slot) || slot == ItemSlot.Ring || slot == ItemSlot.Amulet)
                    throw piecesNode.Field(slotName).Error($"a set piece goes to a weapon hand or armour slots, not '{slotName}'");
                pieces[slot] = ModifierReader.ReadList(piecesNode.Field(slotName));
            }
            if (pieces.Count == 0)
                throw piecesNode.Error("a set needs pieces");
            var weaponTypes = new List<string>();
            if (pieces.ContainsKey(ItemSlot.Weapon))
            {
                foreach (var type in node.Field("weaponTypes").Items())
                    weaponTypes.Add(type.AsString());
                if (weaponTypes.Count == 0)
                    throw node.Field("weaponTypes").Error("a set with a weapon piece needs weapon types");
            }
            var name = node.Field("name");
            return new SetDef(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), rarity,
                node.HasField("color") ? node.Field("color").AsString() : null, pieces, ModifierReader.ReadList(node.Field("bonus")),
                node.HasField("theme") ? node.Field("theme").AsString() : null, weaponTypes);
        }

        private static AffixDef ReadAffix(ContentNode node)
        {
            var slots = new List<ItemSlot>();
            foreach (var slotNode in node.Field("slots").Items())
            {
                if (!ItemSlotNames.ByName.TryGetValue(slotNode.AsString(), out var slot) || slot == ItemSlot.Weapon)
                    throw slotNode.Error($"unknown gear slot '{slotNode.AsString()}'");
                slots.Add(slot);
            }
            var part = node.Field("part").AsString();
            if (part != "prefix" && part != "suffix")
                throw node.Field("part").Error("expected prefix or suffix");
            var stats = new List<AffixStat>();
            foreach (var stat in node.Field("stats").Items())
                stats.Add(new AffixStat(new StatId(stat.Field("stat").AsString()), ModifierReader.ReadKind(stat.Field("kind")),
                    stat.HasField("tags") ? ModifierReader.ReadTags(stat.Field("tags")) : StatTags.None));
            var tiers = node.Field("tiers").Items();
            if (tiers.Count != 2)
                throw node.Field("tiers").Error("expected the first and the last tier values");
            return new AffixDef(node.Field("id").AsString(), slots, part == "prefix", stats,
                tiers[0].AsNumber(-1e6, 1e6), tiers[1].AsNumber(-1e6, 1e6),
                node.HasField("group") ? node.Field("group").AsString() : null);
        }
    }
}
