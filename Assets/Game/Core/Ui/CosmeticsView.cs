using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class CosmeticLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Slot { get; internal set; } = string.Empty;
        public long Price { get; internal set; }
        public string Source { get; internal set; } = string.Empty;
        public bool Owned { get; internal set; }
        public bool Equipped { get; internal set; }
        public bool ForSale { get; internal set; }
    }

    public static class CosmeticsView
    {
        public static List<CosmeticLine> Lines(CosmeticSystem cosmetics, string slot, string heroOrWeaponType = "", bool russian = true)
        {
            var lines = new List<CosmeticLine>();
            foreach (var item in cosmetics.Catalog.All)
            {
                if (item.Slot != slot)
                    continue;
                if (heroOrWeaponType.Length > 0 && item.Hero != heroOrWeaponType && item.WeaponType != heroOrWeaponType)
                    continue;
                var owned = cosmetics.Owns(item.Id);
                if (!item.ForSale && !owned)
                    continue;
                lines.Add(new CosmeticLine
                {
                    Id = item.Id,
                    Name = russian ? item.NameRu : item.NameEn,
                    Slot = item.Slot,
                    Price = item.Price,
                    Source = item.Source,
                    Owned = owned,
                    Equipped = cosmetics.IsEquipped(item.Id),
                    ForSale = item.ForSale && !owned
                });
            }
            return lines;
        }
    }
}
