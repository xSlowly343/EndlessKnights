using System.Collections.Generic;
using Game.Core.Content.Rift;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class RiftShopLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Contents { get; internal set; } = string.Empty;
        public string Price { get; internal set; } = string.Empty;
        public string Left { get; internal set; } = string.Empty;
        public string Button { get; internal set; } = string.Empty;
        public int LeftThisWindow { get; internal set; }
        public bool CanBuy { get; internal set; }
    }

    public static class RiftShopView
    {
        public static string Title => Texts.Get("rift.shop.title");
        public static string Hint => Texts.Get("rift.shop.hint");

        public static bool Open(GameLoop loop) => loop.Rift.Inside && loop.Rift.Rules.Forge.Shop.Count > 0;

        public static string Sparks(GameLoop loop) => Texts.Format("rift.sparks", loop.Rift.Sparks);

        public static List<RiftShopLine> Lines(GameLoop loop, IReadOnlyDictionary<string, int> boughtThisWindow, long crystals)
        {
            var offers = loop.Rift.Rules.Forge.Shop;
            var lines = new List<RiftShopLine>(offers.Count);
            var open = Open(loop);
            foreach (var offer in offers)
            {
                boughtThisWindow.TryGetValue(offer.Id, out var bought);
                var left = System.Math.Max(0, offer.PerWindow - bought);
                var affordable = crystals >= offer.Price;
                lines.Add(new RiftShopLine
                {
                    Id = offer.Id,
                    Name = Texts.Get("rift.shop.offer." + offer.Id),
                    Contents = Texts.Format("rift.shop.contents", offer.Sparks),
                    Price = Texts.Format("chest.price", offer.Price),
                    Left = Texts.Format("rift.shop.left", left, offer.PerWindow),
                    LeftThisWindow = left,
                    CanBuy = open && left > 0 && affordable,
                    Button = Texts.Get(!open ? "rift.shop.closed" : left == 0 ? "chest.soldOut" : affordable ? "chest.buy" : "chest.noCrystals")
                });
            }
            return lines;
        }

        public static long Credit(GameLoop loop, long riftId, long boughtSparks) => loop.Rift.CreditShop(riftId, boughtSparks);
    }
}
