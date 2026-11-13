using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct RearSupplyLine
    {
        public RearSupplyLine(string item, string name, string effects, bool riftOnly)
        {
            Item = item;
            Name = name;
            Effects = effects;
            RiftOnly = riftOnly;
        }

        public string Item { get; }
        public string Name { get; }
        public string Effects { get; }
        public bool RiftOnly { get; }
    }

    public static class RiftRearView
    {
        public static List<RearSupplyLine> Supplies(GameLoop loop)
        {
            var lines = new List<RearSupplyLine>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var meal in loop.State.Cooking.Meals)
            {
                var dish = loop.Data.Cooking.Dish(meal);
                var item = RiftSupplySystem.DishPrefix + meal;
                if (dish == null || !seen.Add(item))
                    continue;
                lines.Add(new RearSupplyLine(item, Texts.Pick(dish.NameRu, dish.NameEn), Effects(loop.RiftSupply.ModifiersOf(item)), dish.RiftOnly));
            }
            foreach (var potion in loop.State.Alchemy.Potions)
            {
                var recipe = potion.Unknown ? null : loop.Data.Alchemy.Recipe(potion.Recipe);
                var item = RiftSupplySystem.PotionPrefix + potion.Recipe + (potion.Fine ? RiftSupplySystem.FineSuffix : string.Empty);
                if (recipe == null || !seen.Add(item))
                    continue;
                var name = Texts.Pick(recipe.NameRu, recipe.NameEn);
                lines.Add(new RearSupplyLine(item, potion.Fine ? Texts.Format("rear.fine", name) : name, Effects(loop.RiftSupply.ModifiersOf(item)), false));
            }
            lines.Sort((a, b) => b.RiftOnly.CompareTo(a.RiftOnly));
            return lines;
        }

        public static string Status(int sent, int perWindow, bool open) =>
            open ? Texts.Format("rear.sent", sent, perWindow) : Texts.Get("rear.closed");

        public static string Mine(GameLoop loop, string fromName) =>
            loop.RiftSupply.HasSupply ? Texts.Format("rear.mine", fromName, Name(loop, loop.RiftSupply.Supply)) : Texts.Get("rear.mine.none");

        private static string Name(GameLoop loop, string item)
        {
            if (item.StartsWith(RiftSupplySystem.DishPrefix, System.StringComparison.Ordinal))
            {
                var dish = loop.Data.Cooking.Dish(item.Substring(RiftSupplySystem.DishPrefix.Length));
                return dish == null ? item : Texts.Pick(dish.NameRu, dish.NameEn);
            }
            var fine = item.EndsWith(RiftSupplySystem.FineSuffix, System.StringComparison.Ordinal);
            var id = item.Substring(RiftSupplySystem.PotionPrefix.Length, item.Length - RiftSupplySystem.PotionPrefix.Length - (fine ? RiftSupplySystem.FineSuffix.Length : 0));
            var recipe = loop.Data.Alchemy.Recipe(id);
            if (recipe == null)
                return item;
            var name = Texts.Pick(recipe.NameRu, recipe.NameEn);
            return fine ? Texts.Format("rear.fine", name) : name;
        }

        private static string Effects(IReadOnlyList<Modifier> modifiers)
        {
            var parts = new List<string>(modifiers.Count);
            foreach (var modifier in modifiers)
                parts.Add(ModifierText.Describe(modifier));
            return string.Join(", ", parts);
        }
    }
}
