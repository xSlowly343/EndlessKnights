using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public enum CollectionKind
    {
        Fish,
        Plants,
        Dishes,
        Recipes,
        Finds,
        Skeletons
    }

    public enum MuseumPerk
    {
        BiteFaster,
        BottleChance,
        ReelEasier,
        TemperStep,
        RareFind,
        LegendChance,
        MutationChance,
        DoubleMeal,
        FinePotion,
        ExpeditionLoot,
        LegendTire,
        HookWindow,
        GrowFaster,
        SeedBack,
        StirFaster,
        FewerChops,
        MealLonger,
        IngredientBack,
        SlowerCooling,
        FewerHammers,
        MoreOre,
        WiderZone,
        ShorterTrips,
        LetterChoice,
        ExpeditionShards,
        Reputation,
        OrderDust,
        OrderRecipe,
        ForecastSight,
        BuildBoost,
        DoublePotion,
        FreeRedraw,
        ExtraHerb,
        MoreBones,
        LighterBrush,
        LongerRoad
    }

    public readonly struct PerkValue
    {
        public PerkValue(MuseumPerk perk, double value)
        {
            Perk = perk;
            Value = value;
        }

        public MuseumPerk Perk { get; }
        public double Value { get; }
    }

    public sealed class CollectionDef
    {
        public CollectionDef(string id, string nameRu, string nameEn, CollectionKind kind, IReadOnlyList<string> items, string trophy, string titleRu, string titleEn, IReadOnlyList<Modifier> bonus, IReadOnlyList<PerkValue> perks)
        {
            Perks = perks;
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Kind = kind;
            Items = items;
            Trophy = trophy;
            TitleRu = titleRu;
            TitleEn = titleEn;
            Bonus = bonus;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public CollectionKind Kind { get; }
        public IReadOnlyList<string> Items { get; }
        public string Trophy { get; }
        public string TitleRu { get; }
        public string TitleEn { get; }
        public bool HasTitle => TitleRu.Length > 0;
        public IReadOnlyList<Modifier> Bonus { get; }
        public IReadOnlyList<PerkValue> Perks { get; }
    }

    public sealed class MuseumRules
    {
        public const string FileName = "museum.json";
        public const int SupportedVersion = 1;

        private static readonly IReadOnlyDictionary<string, CollectionKind> Kinds = new Dictionary<string, CollectionKind>(StringComparer.Ordinal)
        {
            ["fish"] = CollectionKind.Fish,
            ["plants"] = CollectionKind.Plants,
            ["dishes"] = CollectionKind.Dishes,
            ["recipes"] = CollectionKind.Recipes,
            ["finds"] = CollectionKind.Finds,
            ["skeletons"] = CollectionKind.Skeletons
        };

        private static readonly IReadOnlyDictionary<string, MuseumPerk> PerkNames = new Dictionary<string, MuseumPerk>(StringComparer.Ordinal)
        {
            ["biteFaster"] = MuseumPerk.BiteFaster,
            ["bottleChance"] = MuseumPerk.BottleChance,
            ["reelEasier"] = MuseumPerk.ReelEasier,
            ["temperStep"] = MuseumPerk.TemperStep,
            ["rareFind"] = MuseumPerk.RareFind,
            ["legendChance"] = MuseumPerk.LegendChance,
            ["mutationChance"] = MuseumPerk.MutationChance,
            ["doubleMeal"] = MuseumPerk.DoubleMeal,
            ["finePotion"] = MuseumPerk.FinePotion,
            ["expeditionLoot"] = MuseumPerk.ExpeditionLoot,
            ["legendTire"] = MuseumPerk.LegendTire,
            ["hookWindow"] = MuseumPerk.HookWindow,
            ["growFaster"] = MuseumPerk.GrowFaster,
            ["seedBack"] = MuseumPerk.SeedBack,
            ["stirFaster"] = MuseumPerk.StirFaster,
            ["fewerChops"] = MuseumPerk.FewerChops,
            ["mealLonger"] = MuseumPerk.MealLonger,
            ["ingredientBack"] = MuseumPerk.IngredientBack,
            ["slowerCooling"] = MuseumPerk.SlowerCooling,
            ["fewerHammers"] = MuseumPerk.FewerHammers,
            ["moreOre"] = MuseumPerk.MoreOre,
            ["widerZone"] = MuseumPerk.WiderZone,
            ["shorterTrips"] = MuseumPerk.ShorterTrips,
            ["letterChoice"] = MuseumPerk.LetterChoice,
            ["expeditionShards"] = MuseumPerk.ExpeditionShards,
            ["reputation"] = MuseumPerk.Reputation,
            ["orderDust"] = MuseumPerk.OrderDust,
            ["orderRecipe"] = MuseumPerk.OrderRecipe,
            ["forecastSight"] = MuseumPerk.ForecastSight,
            ["buildBoost"] = MuseumPerk.BuildBoost,
            ["doublePotion"] = MuseumPerk.DoublePotion,
            ["freeRedraw"] = MuseumPerk.FreeRedraw,
            ["extraHerb"] = MuseumPerk.ExtraHerb,
            ["moreBones"] = MuseumPerk.MoreBones,
            ["lighterBrush"] = MuseumPerk.LighterBrush,
            ["longerRoad"] = MuseumPerk.LongerRoad
        };

        public const double MaxPerk = 0.5;
        public const double MaxSecondsPerk = 10;

        public static bool TryPerk(string name, out MuseumPerk perk) => PerkNames.TryGetValue(name, out perk);

        public static double MaxOf(MuseumPerk perk) => perk == MuseumPerk.HookWindow ? MaxSecondsPerk : MaxPerk;
        public static int PerkCount => PerkNames.Count;

        public double MaxEach { get; private set; }
        public double MaxTotal { get; private set; }
        public int Shelf { get; private set; }
        public IReadOnlyList<CollectionDef> Collections { get; private set; } = Array.Empty<CollectionDef>();

        public CollectionDef? Collection(string id)
        {
            for (var i = 0; i < Collections.Count; i++)
            {
                if (Collections[i].Id == id)
                    return Collections[i];
            }
            return null;
        }

        public static MuseumRules Load(string json, FishingRules fishing, GardenRules garden, CookingRules cooking, AlchemyRules alchemy, ExpeditionRules expeditions, DigRules digs)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var rules = new MuseumRules
            {
                MaxEach = root.Field("maxEach").AsNumber(0, 0.03),
                MaxTotal = root.Field("maxTotal").AsNumber(0, 0.15),
                Shelf = root.Field("shelf").AsInt(1, 100)
            };
            var collections = new List<CollectionDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var total = 0.0;
            foreach (var node in root.Field("collections").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"collection '{id}' is listed twice");
                var kindName = node.Field("kind").AsString();
                if (!Kinds.TryGetValue(kindName, out var kind))
                    throw node.Field("kind").Error($"unknown collection kind '{kindName}'");
                var items = new List<string>();
                foreach (var item in node.Field("items").Items())
                {
                    var itemId = item.AsString();
                    if (!Known(kind, itemId, fishing, garden, cooking, alchemy, expeditions, digs))
                        throw item.Error($"'{itemId}' is not a known {kindName} entry");
                    items.Add(itemId);
                }
                if (items.Count == 0)
                    throw node.Field("items").Error("a collection needs items");
                var bonus = ModifierReader.ReadList(node.Field("bonus"));
                foreach (var modifier in bonus)
                {
                    if (modifier.Kind != ModifierKind.Increased || modifier.Value > rules.MaxEach + 1e-12)
                        throw node.Field("bonus").Error($"a collection gives at most {rules.MaxEach} increased, '{id}' gives {modifier.Value}");
                    total += modifier.Value;
                }
                if (total > rules.MaxTotal + 1e-12)
                    throw node.Field("bonus").Error($"all collections give at most {rules.MaxTotal} together, '{id}' brings it to {total}");
                var perks = new List<PerkValue>();
                if (node.HasField("perks"))
                {
                    foreach (var perkNode in node.Field("perks").Items())
                    {
                        var perkName = perkNode.Field("perk").AsString();
                        if (!PerkNames.TryGetValue(perkName, out var perk))
                            throw perkNode.Field("perk").Error($"unknown museum perk '{perkName}'");
                        perks.Add(new PerkValue(perk, perkNode.Field("value").AsNumber(0, MaxOf(perk))));
                    }
                }
                var name = node.Field("name");
                var hasTitle = node.HasField("title");
                collections.Add(new CollectionDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), kind, items, node.Field("trophy").AsString(),
                    hasTitle ? node.Field("title").Field("ru").AsString() : string.Empty, hasTitle ? node.Field("title").Field("en").AsString() : string.Empty, bonus, perks));
            }
            rules.Collections = collections;
            return rules;
        }

        private static bool Known(CollectionKind kind, string id, FishingRules fishing, GardenRules garden, CookingRules cooking, AlchemyRules alchemy, ExpeditionRules expeditions, DigRules digs)
        {
            switch (kind)
            {
                case CollectionKind.Fish:
                    return fishing.Fish(id) != null || fishing.Legend(id) != null;
                case CollectionKind.Plants:
                    return garden.Plant(id) != null;
                case CollectionKind.Dishes:
                    return cooking.Dish(id) != null;
                case CollectionKind.Recipes:
                    return alchemy.Recipe(id) != null;
                case CollectionKind.Skeletons:
                    return digs.Site(id) != null;
                default:
                    foreach (var biome in expeditions.Biomes)
                    {
                        if (biome.Rare == id)
                            return true;
                    }
                    return false;
            }
        }
    }
}
