using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class RecipeLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public bool Known { get; internal set; }
        public bool CanBrew { get; internal set; }
        public IReadOnlyList<string> Herbs { get; internal set; } = System.Array.Empty<string>();
    }

    public sealed class PotionLine
    {
        public int Index { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public bool Fine { get; internal set; }
        public bool Unknown { get; internal set; }
    }

    public static class AlchemyView
    {
        public static List<RecipeLine> Book(AlchemySystem alchemy, CampSystem camp, bool russian = true)
        {
            var lines = new List<RecipeLine>();
            foreach (var recipe in alchemy.Rules.Recipes)
            {
                var known = alchemy.Knows(recipe.Id);
                var enough = true;
                foreach (var herb in recipe.Herbs)
                {
                    var need = 0;
                    foreach (var other in recipe.Herbs)
                        need += other == herb ? 1 : 0;
                    enough &= camp.Count(herb) >= need;
                }
                lines.Add(new RecipeLine
                {
                    Id = recipe.Id,
                    Name = known ? (russian ? recipe.NameRu : recipe.NameEn) : "?",
                    Known = known,
                    CanBrew = known && enough && !alchemy.Brewing,
                    Herbs = known ? recipe.Herbs : System.Array.Empty<string>()
                });
            }
            return lines;
        }

        public static List<PotionLine> Potions(AlchemySystem alchemy, bool russian = true)
        {
            var lines = new List<PotionLine>();
            var potions = alchemy.State.Potions;
            for (var i = 0; i < potions.Count; i++)
            {
                var recipe = alchemy.Rules.Recipe(potions[i].Recipe);
                lines.Add(new PotionLine
                {
                    Index = i,
                    Name = potions[i].Unknown || recipe == null ? "?" : russian ? recipe.NameRu : recipe.NameEn,
                    Fine = potions[i].Fine,
                    Unknown = potions[i].Unknown
                });
            }
            return lines;
        }
    }
}
