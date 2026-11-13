using System;
using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class DishDef
    {
        public DishDef(string id, string nameRu, string nameEn, IReadOnlyList<string> ingredients, int chops, int plates, double seconds, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Ingredients = ingredients;
            Chops = chops;
            Plates = plates;
            Seconds = seconds;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<string> Ingredients { get; }
        public int Chops { get; }
        public int Plates { get; }
        public double Seconds { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
        public bool RiftOnly { get; internal set; }
    }

    public sealed class CookingRules
    {
        public const string FileName = "cooking.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<HeroClass, string> _favorites = new Dictionary<HeroClass, string>();

        public string Building { get; private set; } = string.Empty;
        public double MaxShare { get; private set; }
        public double ComboShare { get; private set; }
        public double FavoriteMultiplier { get; private set; }
        public int MealsKept { get; private set; }
        public IReadOnlyList<DishDef> Dishes { get; private set; } = Array.Empty<DishDef>();

        public DishDef? Dish(string id)
        {
            for (var i = 0; i < Dishes.Count; i++)
            {
                if (Dishes[i].Id == id)
                    return Dishes[i];
            }
            return null;
        }

        public string FavoriteOf(HeroClass heroClass) => _favorites.TryGetValue(heroClass, out var dish) ? dish : string.Empty;

        public static CookingRules Load(string json, CampRules camp, AlchemyRules alchemy)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var building = root.Field("building").AsString();
            if (camp.Building(building) == null)
                throw root.Field("building").Error($"unknown building '{building}'");
            var rules = new CookingRules
            {
                Building = building,
                MaxShare = root.Field("maxShare").AsNumber(0, 1),
                ComboShare = root.Field("comboShare").AsNumber(0, 1),
                FavoriteMultiplier = root.Field("favoriteMultiplier").AsNumber(1, 10),
                MealsKept = root.Field("mealsKept").AsInt(1, 1000)
            };
            var lowerIsBetter = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Field("lowerIsBetter").Items())
                lowerIsBetter.Add(item.AsString());
            var flatCaps = new Dictionary<string, double>(StringComparer.Ordinal);
            var flat = root.Field("flat");
            foreach (var key in flat.FieldNames())
                flatCaps[key] = flat.Field(key).AsNumber(0, 1000);
            var dishes = new List<DishDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("dishes").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"dish '{id}' is listed twice");
                var ingredients = new List<string>();
                foreach (var item in node.Field("ingredients").Items())
                    ingredients.Add(item.AsString());
                if (ingredients.Count == 0)
                    throw node.Field("ingredients").Error("a dish needs ingredients");
                var modifiers = ModifierReader.ReadList(node.Field("modifiers"));
                foreach (var modifier in modifiers)
                {
                    if (modifier.Kind == ModifierKind.Flat)
                    {
                        if (!flatCaps.TryGetValue(modifier.Stat.Name, out var cap))
                            throw node.Field("modifiers").Error($"food gives only increased bonuses and the listed flat ones, '{id}' gives flat {modifier.Stat.Name}");
                        if (modifier.Value <= 0 || modifier.Value > cap + 1e-9)
                            throw node.Field("modifiers").Error($"food gives at most {cap} flat {modifier.Stat.Name}, '{id}' gives {modifier.Value}");
                        continue;
                    }
                    if (modifier.Kind != ModifierKind.Increased)
                        throw node.Field("modifiers").Error($"food gives only increased bonuses, '{id}' gives {modifier.Kind}");
                    if (modifier.Value < 0 && !lowerIsBetter.Contains(modifier.Stat.Name))
                        throw node.Field("modifiers").Error($"food never lowers {modifier.Stat.Name}, '{id}' gives {modifier.Value}");
                    if (System.Math.Abs(modifier.Value) > rules.MaxShare + 1e-9)
                        throw node.Field("modifiers").Error($"food gives at most {rules.MaxShare} to one stat, '{id}' gives {modifier.Value}");
                    if (modifier.Value < 0)
                        continue;
                    var potion = BestPotion(alchemy, modifier);
                    if (potion + modifier.Value > rules.ComboShare + 1e-9)
                        throw node.Field("modifiers").Error($"a potion and food give at most {rules.ComboShare} to one stat, '{id}' with a potion gives {potion + modifier.Value}");
                }
                var name = node.Field("name");
                dishes.Add(new DishDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), ingredients, node.Field("chops").AsInt(1, 100000),
                    node.Field("plates").AsInt(1, 1000), node.Field("hours").AsNumber(0.1, 24) * 3600, modifiers)
                {
                    RiftOnly = node.HasField("riftOnly") && node.Field("riftOnly").AsBool()
                });
            }
            rules.Dishes = dishes;
            var favorites = root.Field("favorites");
            foreach (var pair in HeroClassNames.ByName)
            {
                if (!favorites.HasField(pair.Key))
                    continue;
                var dish = favorites.Field(pair.Key).AsString();
                if (rules.Dish(dish) == null)
                    throw favorites.Field(pair.Key).Error($"unknown dish '{dish}'");
                rules._favorites[pair.Value] = dish;
            }
            return rules;
        }

        private static double BestPotion(AlchemyRules alchemy, Modifier food)
        {
            var best = 0.0;
            foreach (var recipe in alchemy.Recipes)
            {
                var fine = alchemy.FineMultiplier;
                foreach (var modifier in recipe.Modifiers)
                {
                    if (modifier.Stat.Equals(food.Stat) && modifier.Kind == food.Kind)
                        best = System.Math.Max(best, modifier.Value * fine);
                }
            }
            return best;
        }
    }
}
