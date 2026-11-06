using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CosmeticDef
    {
        public CosmeticDef(string id, string slot, string nameRu, string nameEn, long price, string source, string hero, string weaponType, bool clan, string achievement)
        {
            Id = id;
            Slot = slot;
            NameRu = nameRu;
            NameEn = nameEn;
            Price = price;
            Source = source;
            Hero = hero;
            WeaponType = weaponType;
            Clan = clan;
            Achievement = achievement;
        }

        public string Id { get; }
        public string Slot { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long Price { get; }
        public string Source { get; }
        public string Hero { get; }
        public string WeaponType { get; }
        public bool Clan { get; }
        public string Achievement { get; }
        public bool ForSale => Source == CosmeticCatalog.ShopSource && Price > 0;

        public string EquipKey => Hero.Length > 0 ? Slot + ":" + Hero : WeaponType.Length > 0 ? Slot + ":" + WeaponType : Slot;
    }

    public sealed class CosmeticCatalog
    {
        public const string FileName = "cosmetics.json";
        public const int SupportedVersion = 1;
        public const string ShopSource = "shop";
        public static readonly IReadOnlyList<string> Sources = new[] { ShopSource, "pass", "drop", "reward", "event" };

        private readonly Dictionary<string, CosmeticDef> _byId;

        private CosmeticCatalog(List<CosmeticDef> all, IReadOnlyList<string> slots, int crystalsPerDollar, double weeklyDiscount, double bundleDiscount)
        {
            All = all;
            Slots = slots;
            CrystalsPerDollar = crystalsPerDollar;
            WeeklyDiscount = weeklyDiscount;
            BundleDiscount = bundleDiscount;
            _byId = new Dictionary<string, CosmeticDef>(StringComparer.Ordinal);
            foreach (var item in all)
                _byId.Add(item.Id, item);
        }

        public IReadOnlyList<CosmeticDef> All { get; }
        public IReadOnlyList<string> Slots { get; }
        public int CrystalsPerDollar { get; }
        public double WeeklyDiscount { get; }
        public double BundleDiscount { get; }

        public bool TryGet(string id, out CosmeticDef item) => _byId.TryGetValue(id, out item!);

        public static CosmeticCatalog Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var slots = new List<string>();
            foreach (var slot in root.Field("slots").Items())
                slots.Add(slot.AsString());
            var all = new List<CosmeticDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("items").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"cosmetic '{id}' is listed twice");
                var slot = node.Field("slot").AsString();
                if (!slots.Contains(slot))
                    throw node.Field("slot").Error($"unknown slot '{slot}'");
                var source = node.Field("source").AsString();
                if (!((IList<string>)Sources).Contains(source))
                    throw node.Field("source").Error($"unknown source '{source}'");
                var price = node.Field("price").AsLong(0);
                if (source == ShopSource && price <= 0)
                    throw node.Field("price").Error($"cosmetic '{id}' is sold in the shop but has no price");
                if (source != ShopSource && price != 0)
                    throw node.Field("price").Error($"cosmetic '{id}' is not sold but has a price");
                var name = node.Field("name");
                all.Add(new CosmeticDef(id, slot, name.Field("ru").AsString(), name.Field("en").AsString(), price, source,
                    node.HasField("hero") ? node.Field("hero").AsString() : string.Empty,
                    node.HasField("weaponType") ? node.Field("weaponType").AsString() : string.Empty,
                    node.HasField("clan") && node.Field("clan").AsBool(),
                    node.HasField("achievement") ? node.Field("achievement").AsString() : string.Empty));
            }
            return new CosmeticCatalog(all, slots, root.Field("crystalsPerDollar").AsInt(1), root.Field("weeklyDiscount").AsNumber(0, 0.9), root.Field("bundleDiscount").AsNumber(0, 0.9));
        }
    }
}
