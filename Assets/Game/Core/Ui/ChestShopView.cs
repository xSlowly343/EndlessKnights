using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class ChestLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Contents { get; internal set; } = string.Empty;
        public string Price { get; internal set; } = string.Empty;
        public string Left { get; internal set; } = string.Empty;
        public string Button { get; internal set; } = string.Empty;
        public int LeftThisWeek { get; internal set; }
        public bool CanBuy { get; internal set; }
    }

    public static class ChestShopView
    {
        public static string Hint => Texts.Get("chest.hint");

        public static List<ChestLine> Lines(ChestCatalog catalog, IReadOnlyDictionary<string, int> boughtThisWeek, long crystals)
        {
            var lines = new List<ChestLine>(catalog.Chests.Count);
            foreach (var chest in catalog.Chests)
            {
                boughtThisWeek.TryGetValue(chest.Id, out var bought);
                var left = System.Math.Max(0, chest.PerWeek - bought);
                var affordable = crystals >= chest.Price;
                lines.Add(new ChestLine
                {
                    Id = chest.Id,
                    Name = Texts.Pick(chest.NameRu, chest.NameEn),
                    Contents = Contents(chest),
                    Price = Texts.Format("chest.price", chest.Price),
                    Left = Texts.Format("chest.left", left, chest.PerWeek),
                    LeftThisWeek = left,
                    CanBuy = left > 0 && affordable,
                    Button = Texts.Get(left == 0 ? "chest.soldOut" : affordable ? "chest.buy" : "chest.noCrystals")
                });
            }
            return lines;
        }

        public static string Contents(ChestDef chest)
        {
            var parts = new List<string>(2);
            if (chest.Ingots > 0)
                parts.Add(Texts.Format("chest.contents.ingots", chest.Ingots));
            if (chest.Essence > 0)
                parts.Add(Texts.Format("chest.contents.essence", chest.Essence));
            return string.Join(" · ", parts);
        }
    }
}
