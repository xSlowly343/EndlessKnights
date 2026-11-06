using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class DishLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public IReadOnlyList<string> Ingredients { get; internal set; } = System.Array.Empty<string>();
        public bool CanCook { get; internal set; }
        public bool Cooked { get; internal set; }
        public bool Favorite { get; internal set; }
        public double Hours { get; internal set; }
    }

    public static class CookingView
    {
        public static List<DishLine> Book(CookingSystem cooking, bool russian = true)
        {
            var lines = new List<DishLine>();
            foreach (var dish in cooking.Rules.Dishes)
            {
                lines.Add(new DishLine
                {
                    Id = dish.Id,
                    Name = russian ? dish.NameRu : dish.NameEn,
                    Ingredients = dish.Ingredients,
                    CanCook = cooking.Open && !cooking.Busy && cooking.HasIngredients(dish),
                    Cooked = cooking.State.Book.Contains(dish.Id),
                    Favorite = cooking.Favorite(dish.Id),
                    Hours = cooking.SecondsOf(dish) / 3600
                });
            }
            return lines;
        }

        public static (double Chops, double Plates) Progress(CookingSystem cooking)
        {
            var dish = cooking.Rules.Dish(cooking.State.Cooking);
            return dish == null ? (0, 0) : (System.Math.Min(1, cooking.State.Chops / dish.Chops), System.Math.Min(1, cooking.State.Plates / dish.Plates));
        }
    }
}
