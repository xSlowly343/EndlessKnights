using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class PotionRecipe
    {
        public PotionRecipe(string id, string nameRu, string nameEn, bool known, IReadOnlyList<string> herbs, int stirs, double seconds, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Known = known;
            Herbs = herbs;
            Stirs = stirs;
            Seconds = seconds;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public bool Known { get; }
        public IReadOnlyList<string> Herbs { get; }
        public int Stirs { get; }
        public double Seconds { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }

        public bool MadeOf(IReadOnlyList<string> herbs)
        {
            if (herbs.Count != Herbs.Count)
                return false;
            var left = new List<string>(Herbs);
            foreach (var herb in herbs)
            {
                if (!left.Remove(herb))
                    return false;
            }
            return true;
        }
    }

    public sealed class AlchemyRules
    {
        public const string FileName = "alchemy.json";
        public const int SupportedVersion = 1;
        public const double MaxBossTimerSeconds = 3;

        public IReadOnlyList<PotionRecipe> Recipes { get; private set; } = Array.Empty<PotionRecipe>();
        public IReadOnlyList<string> RareHerbs { get; private set; } = Array.Empty<string>();
        public double FineMultiplier { get; private set; }
        public double MaxShare { get; private set; }

        public PotionRecipe? Recipe(string id)
        {
            for (var i = 0; i < Recipes.Count; i++)
            {
                if (Recipes[i].Id == id)
                    return Recipes[i];
            }
            return null;
        }

        public PotionRecipe? RecipeOf(IReadOnlyList<string> herbs)
        {
            foreach (var recipe in Recipes)
            {
                if (recipe.MadeOf(herbs))
                    return recipe;
            }
            return null;
        }

        public bool Fine(IReadOnlyList<string> herbs)
        {
            foreach (var herb in herbs)
            {
                if (((IList<string>)RareHerbs).Contains(herb))
                    return true;
            }
            return false;
        }

        public static AlchemyRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var maxShare = root.Field("maxShare").AsNumber(0, 1);
            var fine = root.Field("fineMultiplier").AsNumber(1, 10);
            var rare = new List<string>();
            foreach (var item in root.Field("rareHerbs").Items())
                rare.Add(item.AsString());
            var recipes = new List<PotionRecipe>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("recipes").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"recipe '{id}' is listed twice");
                var herbs = new List<string>();
                foreach (var herb in node.Field("herbs").Items())
                    herbs.Add(herb.AsString());
                if (herbs.Count == 0)
                    throw node.Field("herbs").Error("a recipe needs herbs");
                var modifiers = ModifierReader.ReadList(node.Field("modifiers"));
                var best = fine;
                foreach (var modifier in modifiers)
                {
                    var limit = modifier.Kind == ModifierKind.Flat ? MaxBossTimerSeconds : maxShare;
                    if (modifier.Value * best > limit + 1e-9)
                        throw node.Field("modifiers").Error($"a potion gives at most {limit} to one stat, '{id}' gives {modifier.Value * best}");
                }
                var name = node.Field("name");
                recipes.Add(new PotionRecipe(id, name.Field("ru").AsString(), name.Field("en").AsString(), node.HasField("known") && node.Field("known").AsBool(),
                    herbs, node.Field("stirs").AsInt(1, 100000), node.Field("minutes").AsNumber(1, 1440) * 60, modifiers));
            }
            foreach (var recipe in recipes)
            {
                foreach (var other in recipes)
                {
                    if (!ReferenceEquals(recipe, other) && recipe.MadeOf(other.Herbs))
                        throw root.Field("recipes").Error($"recipes '{recipe.Id}' and '{other.Id}' use the same herbs");
                }
            }
            return new AlchemyRules { Recipes = recipes, RareHerbs = rare, FineMultiplier = fine, MaxShare = maxShare };
        }
    }
}
