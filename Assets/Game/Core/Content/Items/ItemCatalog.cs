using System;
using System.Collections.Generic;
using Game.Core.Content.Gems;
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

        public IReadOnlyDictionary<string, PartySetReader.Drop> PartySetDrops { get; private set; } = new Dictionary<string, PartySetReader.Drop>();

        public double[] CraftBaseCost { get; private set; } = new double[Enum.GetValues(typeof(ItemRarity)).Length];
        public IReadOnlyList<double> CraftSteps { get; private set; } = new[] { 1.0 };
        public double JewelryCraftMultiplier { get; private set; } = 1;
        public double JewelryPairBonus { get; private set; }
        public double MasterySpentPerLevel { get; private set; } = 500;
        public double MasteryRate { get; private set; }
        public int MasterySlowAfter { get; private set; }
        public double MasterySlowRate { get; private set; }
        public int UniqueCraftMastery { get; private set; } = int.MaxValue;
        public double UniqueCraftCost { get; private set; }

        public const int MaxEchoSets = 32;

        public IReadOnlyList<ConsonanceDef> Consonances { get; private set; } = Array.Empty<ConsonanceDef>();

        public SetDef? SetOf(string theme, ItemRarity rarity)
        {
            foreach (var set in Sets)
            {
                if (set.Rarity == rarity && set.Ordinary && string.Equals(set.Theme, theme, StringComparison.Ordinal))
                    return set;
            }
            return null;
        }

        private static List<ConsonanceDef> ReadConsonances(ContentNode node, List<SetDef> sets)
        {
            var consonances = new List<ConsonanceDef>();
            foreach (var item in node.Items())
            {
                var armor = item.Field("armor").AsString();
                var jewelry = item.Field("jewelry").AsString();
                if (!sets.Exists(s => s.Theme == armor && !s.Jewelry))
                    throw item.Field("armor").Error($"no armour line '{armor}'");
                if (!sets.Exists(s => s.Theme == jewelry && s.Jewelry))
                    throw item.Field("jewelry").Error($"no jewelry line '{jewelry}'");
                consonances.Add(new ConsonanceDef(armor, jewelry, item.HasField("lines") ? ModifierReader.ReadList(item.Field("lines")) : Array.Empty<Modifier>())
                {
                    Fixed = item.HasField("fixed") ? ModifierReader.ReadList(item.Field("fixed")) : Array.Empty<Modifier>(),
                    PerMuseumCollection = item.HasField("perMuseumCollection") ? ModifierReader.ReadList(item.Field("perMuseumCollection")) : Array.Empty<Modifier>(),
                    Perks = ReadPerks(item) ?? (IReadOnlyList<PerkValue>)Array.Empty<PerkValue>(),
                    PerkBoost = item.HasField("perkBoost") ? item.Field("perkBoost").AsNumber(0, 1) : 0,
                    NightOffline = item.HasField("nightOffline") && item.Field("nightOffline").AsBool(),
                    FateOptions = item.HasField("fateOptions") ? item.Field("fateOptions").AsInt(3, 4) : 0
                });
            }
            return consonances;
        }

        public IReadOnlyList<double> CollectionMorePerSet { get; private set; } = Array.Empty<double>();

        public double CollectionMore(ItemRarity rarity, int collected) =>
            (int)rarity < CollectionMorePerSet.Count ? System.Math.Pow(1 + CollectionMorePerSet[(int)rarity], collected) : 1;

        public int MasteryLevel(double spent) => 1 + (int)System.Math.Floor(System.Math.Max(0, spent) / MasterySpentPerLevel);

        public double MasteryScale(int level)
        {
            var steps = System.Math.Max(0, level - 1);
            var fast = System.Math.Min(steps, MasterySlowAfter);
            return System.Math.Pow(1 + MasteryRate, fast) * System.Math.Pow(1 + MasterySlowRate, steps - fast);
        }

        public static bool IsJewelry(ItemSlot slot) => slot == ItemSlot.Ring || slot == ItemSlot.Amulet;

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
                if (set.Ordinary && set.Has(slot) && set.Rarity <= rarity && set.Rarity > best)
                    best = set.Rarity;
            }
            foreach (var set in Sets)
            {
                if (set.Ordinary && set.Has(slot) && set.Rarity == best)
                    candidates.Add(set);
            }
            return candidates.Count == 0 ? null : candidates[rng.NextInt(0, candidates.Count)];
        }

        public GearItem RollSetPiece(SetDef set, ItemSlot slot, string? color, ItemRarity rarity, int itemLevel, Rng rng, bool? secondRing = null)
        {
            var gearBase = PickBase(slot, set.Color ?? color, itemLevel, rng);
            var affixes = RollAffixes(slot, rarity, itemLevel, rng);
            var second = slot == ItemSlot.Ring && set.HasSecondRing && (secondRing ?? rng.NextDouble() < 0.5);
            return new GearItem(gearBase, rarity, itemLevel, affixes, set, PickImplicit(slot, rng), null, second);
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
            var affixes = RollAffixes(slot, rarity, itemLevel, rng);
            return new GearItem(gearBase, rarity, itemLevel, affixes, null, PickImplicit(slot, rng));
        }

        private readonly Dictionary<ItemSlot, List<GearImplicit>> _implicits = new Dictionary<ItemSlot, List<GearImplicit>>();
        private readonly Dictionary<string, GearImplicit> _implicitIds = new Dictionary<string, GearImplicit>(StringComparer.Ordinal);

        public IReadOnlyList<GearImplicit> ImplicitsOf(ItemSlot slot) =>
            _implicits.TryGetValue(slot, out var pool) ? pool : (IReadOnlyList<GearImplicit>)Array.Empty<GearImplicit>();

        public bool TryGetImplicit(string id, out GearImplicit rolled) => _implicitIds.TryGetValue(id, out rolled!);

        public GearImplicit? PickImplicit(ItemSlot slot, Rng rng)
        {
            var pool = ImplicitsOf(slot);
            return pool.Count == 0 ? null : pool[rng.NextInt(0, pool.Count)];
        }

        private static void ReadImplicits(ContentNode node, ItemCatalog catalog)
        {
            foreach (var slotName in node.FieldNames())
            {
                var slotNode = node.Field(slotName);
                if (!ItemSlotNames.ByName.TryGetValue(slotName, out var slot) || slot == ItemSlot.Weapon)
                    throw slotNode.Error($"unknown gear slot '{slotName}'");
                var pool = new List<GearImplicit>();
                foreach (var item in slotNode.Items())
                {
                    var rolled = new GearImplicit(item.Field("id").AsString(), slot, ModifierReader.ReadList(item.Field("lines")));
                    if (rolled.Lines.Count == 0)
                        throw item.Field("lines").Error("an implicit needs at least one line");
                    if (catalog._implicitIds.ContainsKey(rolled.Id))
                        throw item.Field("id").Error($"implicit id '{rolled.Id}' is used twice");
                    catalog._implicitIds[rolled.Id] = rolled;
                    pool.Add(rolled);
                }
                catalog._implicits[slot] = pool;
            }
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
            Pick(slot, rarity, true, prefixes, rule, itemLevel, rng, chosen);
            Pick(slot, rarity, false, suffixes, rule, itemLevel, rng, chosen);
            return chosen;
        }

        private void Pick(ItemSlot slot, ItemRarity rarity, bool prefix, int count, RarityRule rule, int itemLevel, Rng rng, List<GearAffix> chosen)
        {
            for (var n = 0; n < count; n++)
            {
                var pool = new List<AffixDef>();
                foreach (var affix in Affixes)
                {
                    if (affix.Prefix == prefix && affix.FitsSlot(slot) && rarity >= affix.FromRarity && Free(affix, chosen))
                        pool.Add(affix);
                }
                if (pool.Count == 0)
                    return;
                var def = pool[rng.NextInt(0, pool.Count)];
                var tier = RollTier(rule, itemLevel, rng);
                var support = def.Supports.Count == 0 ? null : def.Supports[rng.NextInt(0, def.Supports.Count)];
                var roll = rng.NextDouble();
                chosen.Add(new GearAffix(def, tier, def.RollValue(tier, roll), support, roll));
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

        public IReadOnlyList<int> TierLevels { get; private set; } = Array.Empty<int>();

        public int OpenTiers(int itemLevel)
        {
            var open = 0;
            foreach (var level in TierLevels)
            {
                if (itemLevel >= level)
                    open++;
            }
            return System.Math.Max(1, System.Math.Min(Tiers, open));
        }

        public int RollTier(RarityRule rule, int itemLevel, Rng rng)
        {
            var open = OpenTiers(itemLevel);
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
            var echoBits = 0;
            var setIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("sets").Items())
            {
                var set = ReadSet(item);
                if (!setIds.Add(set.Id))
                    throw item.Field("id").Error($"set id '{set.Id}' is used twice");
                if (set.Echo.Count > 0)
                {
                    if (echoBits >= MaxEchoSets)
                        throw item.Field("echo").Error($"at most {MaxEchoSets} sets can echo in a party");
                    set.EchoBit = echoBits++;
                }
                sets.Add(set);
            }
            var partyDrops = root.HasField("partySets") ? PartySetReader.ReadDrops(root.Field("partySets")) : new Dictionary<string, PartySetReader.Drop>(StringComparer.Ordinal);
            if (root.HasField("partySets"))
                PartySetReader.Read(root.Field("partySets"), sets, setIds);
            foreach (var affix in affixes)
            {
                affix.UseTierCount(tiers);
                var problem = affix.RangeProblem(tiers);
                if (problem != null)
                    throw new ContentException(FileName, "affixes." + affix.Id + ".ranges", problem);
            }
            var catalog = new ItemCatalog(bases, affixes, sets, rarities, tiers,
                weights.Field("top").AsNumber(0, 1), weights.Field("next").AsNumber(0, 1), weights.Field("rest").AsNumber(0, 1));
            catalog.TierLevels = ReadTierLevels(root.Field("tierLevels"), tiers);
            catalog.PartySetDrops = partyDrops;
            ReadCraft(root.Field("craft"), catalog);
            if (root.HasField("implicitPools"))
                ReadImplicits(root.Field("implicitPools"), catalog);
            catalog.Uniques = ReadUniques(root.Field("uniques"), catalog);
            catalog.Consonances = root.HasField("consonances") ? ReadConsonances(root.Field("consonances"), sets) : System.Array.Empty<ConsonanceDef>();
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
            catalog.JewelryCraftMultiplier = node.HasField("jewelryMultiplier") ? node.Field("jewelryMultiplier").AsNumber(1, 100) : 1;
            catalog.JewelryPairBonus = node.HasField("jewelryPairBonus") ? node.Field("jewelryPairBonus").AsNumber(0, 10) : 0;
            if (node.HasField("mastery"))
            {
                var mastery = node.Field("mastery");
                catalog.MasterySpentPerLevel = mastery.Field("spentPerLevel").AsNumber(1, 1e9);
                catalog.MasteryRate = mastery.Field("rate").AsNumber(0, 1);
                catalog.MasterySlowAfter = mastery.Field("slowAfter").AsInt(0, 10000);
                catalog.MasterySlowRate = mastery.Field("slowRate").AsNumber(0, 1);
            }
            var perSet = new double[catalog.CraftBaseCost.Length];
            if (node.HasField("collectionMore"))
            {
                var more = node.Field("collectionMore");
                foreach (var name in more.FieldNames())
                {
                    if (!ItemRarityNames.ByName.TryGetValue(name, out var rarity))
                        throw more.Field(name).Error($"unknown rarity '{name}'");
                    perSet[(int)rarity] = more.Field(name).AsNumber(0, 1);
                }
            }
            catalog.CollectionMorePerSet = perSet;
            if (node.HasField("unique"))
            {
                catalog.UniqueCraftMastery = node.Field("unique").Field("mastery").AsInt(1, 10000);
                catalog.UniqueCraftCost = node.Field("unique").Field("cost").AsNumber(1, 1e9);
            }
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
            IReadOnlyList<Modifier>? secondRing = null;
            var piecesNode = node.Field("pieces");
            var jewelry = 0;
            foreach (var slotName in piecesNode.FieldNames())
            {
                if (slotName == "ring_left" || slotName == SetDef.SecondRingKey)
                {
                    var lines = ModifierReader.ReadList(piecesNode.Field(slotName));
                    if (slotName == "ring_left")
                        pieces[ItemSlot.Ring] = lines;
                    else
                        secondRing = lines;
                    jewelry++;
                    continue;
                }
                if (!ItemSlotNames.ByName.TryGetValue(slotName, out var slot) || slot == ItemSlot.Ring)
                    throw piecesNode.Field(slotName).Error($"a set piece goes to a weapon hand, armour, the amulet or ring_left / ring_right, not '{slotName}'");
                if (slot == ItemSlot.Amulet)
                    jewelry++;
                pieces[slot] = ModifierReader.ReadList(piecesNode.Field(slotName));
            }
            if (pieces.Count == 0 && secondRing == null)
                throw piecesNode.Error("a set needs pieces");
            if (jewelry > 0 && jewelry != pieces.Count + (secondRing != null ? 1 : 0))
                throw piecesNode.Error("a jewelry set holds only the amulet and rings");
            if (secondRing != null && !pieces.ContainsKey(ItemSlot.Ring))
                throw piecesNode.Error("ring_right needs ring_left");
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
                node.HasField("theme") ? node.Field("theme").AsString() : null, weaponTypes, secondRing,
                node.HasField("craftMultiplier") ? node.Field("craftMultiplier").AsNumber(1, 100) : 1, ReadPerks(node))
            {
                Primary = node.HasField("primary") ? ModifierReader.ReadList(node.Field("primary")) : System.Array.Empty<Modifier>(),
                PerRaritySet = node.HasField("perRaritySet") ? ModifierReader.ReadList(node.Field("perRaritySet")) : System.Array.Empty<Modifier>(),
                Event = node.HasField("event") ? node.Field("event").AsString() : null,
                RiftOnly = node.HasField("riftOnly") && node.Field("riftOnly").AsBool(),
                CarriedToRift = node.HasField("carriedToRift") && node.Field("carriedToRift").AsBool(),
                PerMuseumCollection = node.HasField("perMuseumCollection") ? ModifierReader.ReadList(node.Field("perMuseumCollection")) : System.Array.Empty<Modifier>(),
                Echo = node.HasField("echo") ? ModifierReader.ReadList(node.Field("echo")) : System.Array.Empty<Modifier>()
            };
        }

        private static IReadOnlyList<PerkValue>? ReadPerks(ContentNode node)
        {
            if (!node.HasField("perks"))
                return null;
            var perks = new List<PerkValue>();
            foreach (var perkNode in node.Field("perks").Items())
            {
                var name = perkNode.Field("perk").AsString();
                if (!MuseumRules.TryPerk(name, out var perk))
                    throw perkNode.Field("perk").Error($"unknown peaceful perk '{name}'");
                perks.Add(new PerkValue(perk, perkNode.Field("value").AsNumber(0, MuseumRules.MaxOf(perk))));
            }
            return perks;
        }

        private static AffixDef ReadAffix(ContentNode node)
        {
            var slots = new List<ItemSlot>();
            foreach (var slotNode in node.Field("slots").Items())
            {
                if (!ItemSlotNames.ByName.TryGetValue(slotNode.AsString(), out var slot))
                    throw slotNode.Error($"unknown gear slot '{slotNode.AsString()}'");
                slots.Add(slot);
            }
            if (slots.Contains(ItemSlot.Weapon) && slots.Count > 1)
                throw node.Field("slots").Error("a weapon affix belongs to the weapon slot only");
            var part = node.Field("part").AsString();
            if (part != "prefix" && part != "suffix")
                throw node.Field("part").Error("expected prefix or suffix");
            var stats = new List<AffixStat>();
            if (node.HasField("stats"))
            {
                foreach (var stat in node.Field("stats").Items())
                    stats.Add(ReadAffixStat(stat));
            }
            var tiers = node.Field("tiers").Items();
            if (tiers.Count != 2)
                throw node.Field("tiers").Error("expected the first and the last tier values");
            var fromRarity = ItemRarity.Simple;
            if (node.HasField("fromRarity") && !ItemRarityNames.ByName.TryGetValue(node.Field("fromRarity").AsString(), out fromRarity))
                throw node.Field("fromRarity").Error($"unknown rarity '{node.Field("fromRarity").AsString()}'");
            var (role, supports) = ReadBuiltIn(node, slots);
            if (stats.Count == 0 && role == null)
                throw node.Error("an affix needs stats or a built-in support");
            if (fromRarity != ItemRarity.Simple && fromRarity != AdvancedFrom && fromRarity != UltimateFrom)
                throw node.Field("fromRarity").Error("an affix opens with any rarity, with rare (advanced) or with ancient (ultimate)");
            if (fromRarity >= UltimateFrom && stats.Count + (role == null ? 0 : 1) < 2)
                throw node.Field("stats").Error("an ultimate affix has two lines at once");
            return new AffixDef(node.Field("id").AsString(), slots, part == "prefix", stats,
                tiers[0].AsNumber(-1e6, 1e6), tiers[1].AsNumber(-1e6, 1e6),
                node.HasField("group") ? node.Field("group").AsString() : null, fromRarity, role, supports, ReadRanges(node));
        }

        private static (Sim.SkillSlotRole? Role, List<string>? Supports) ReadBuiltIn(ContentNode node, List<ItemSlot> slots)
        {
            if (!node.HasField("builtIn"))
                return (null, null);
            var builtIn = node.Field("builtIn");
            if (!slots.Contains(ItemSlot.Weapon))
                throw builtIn.Error("only a weapon has a built-in support");
            var role = builtIn.Field("slot").AsString() switch
            {
                "left" => Sim.SkillSlotRole.Left,
                "right" => Sim.SkillSlotRole.Right,
                _ => throw builtIn.Field("slot").Error("expected left or right")
            };
            var supports = new List<string>();
            foreach (var item in builtIn.Field("supports").Items())
                supports.Add(item.AsString());
            if (supports.Count == 0)
                throw builtIn.Field("supports").Error("list at least one support");
            return (role, supports);
        }

        public void BindGems(GemCatalog gems)
        {
            foreach (var affix in Affixes)
            {
                if (affix.SupportIds.Count == 0)
                    continue;
                var supports = new List<GemDef>();
                foreach (var id in affix.SupportIds)
                {
                    if (!gems.TryGet(id, out var gem) || gem.Kind != GemKind.Support || gem.Support == null)
                        throw new ContentException(FileName, "affixes." + affix.Id, $"'{id}' is not a working support in {GemCatalog.FileName}");
                    supports.Add(gem);
                }
                affix.BindSupports(supports);
            }
        }

        public const ItemRarity AdvancedFrom = ItemRarity.Rare;
        public const ItemRarity UltimateFrom = ItemRarity.Ancient;

        private static int[] ReadTierLevels(ContentNode node, int tiers)
        {
            var items = node.Items();
            if (items.Count != tiers)
                throw node.Error($"expected the item level of each of the {tiers} tiers");
            var levels = new int[tiers];
            for (var i = 0; i < tiers; i++)
            {
                levels[i] = items[i].AsInt(1, 1000);
                if (i == 0 ? levels[i] != 1 : levels[i] <= levels[i - 1])
                    throw items[i].Error("the first tier opens at item level 1 and each next tier later");
            }
            return levels;
        }

        private static List<TierRange>? ReadRanges(ContentNode node)
        {
            if (!node.HasField("ranges"))
                return null;
            var ranges = new List<TierRange>();
            foreach (var item in node.Field("ranges").Items())
            {
                var pair = item.Items();
                if (pair.Count != 2)
                    throw item.Error("expected the low and the high value of a tier");
                ranges.Add(new TierRange(pair[0].AsNumber(-1e6, 1e6), pair[1].AsNumber(-1e6, 1e6)));
            }
            return ranges;
        }

        private static AffixStat ReadAffixStat(ContentNode stat)
        {
            var own = stat.HasField("tiers");
            var first = 0.0;
            var last = 0.0;
            if (own)
            {
                var tiers = stat.Field("tiers").Items();
                if (tiers.Count != 2)
                    throw stat.Field("tiers").Error("expected the first and the last tier values");
                first = tiers[0].AsNumber(-1e6, 1e6);
                last = tiers[1].AsNumber(-1e6, 1e6);
            }
            var ranges = ReadRanges(stat);
            if (ranges != null && !own)
                throw stat.Field("ranges").Error("a line with its own ranges needs its own tiers");
            if (stat.HasField("to"))
                return new AffixStat(ModifierReader.ReadCoopLine(stat).Source, ModifierKind.Flat, StatTags.None, own, first, last, ranges: ranges);
            return new AffixStat(new StatId(stat.Field("stat").AsString()), ModifierReader.ReadKind(stat.Field("kind")),
                stat.HasField("tags") ? ModifierReader.ReadTags(stat.Field("tags")) : StatTags.None, own, first, last,
                stat.HasField("when") ? ModifierReader.ReadCondition(stat.Field("when")) : ModifierCondition.Always, ranges);
        }
    }
}
