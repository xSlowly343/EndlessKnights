using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class CosmeticDef
    {
        public CosmeticDef(string id, string slot, string nameRu, string nameEn, long price, string source, string hero, string weaponType, bool clan, string achievement, int look = 0, string color = "", string pattern = "", string color2 = "", string color3 = "", int frames = 0, double seconds = 0)
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
            Look = look;
            Color = color;
            Pattern = pattern;
            Color2 = color2;
            Color3 = color3;
            Frames = frames;
            Seconds = seconds;
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
        public int Look { get; }
        public string Color { get; }
        public string Pattern { get; }
        public string Color2 { get; }
        public string Color3 { get; }
        public int Frames { get; }
        public double Seconds { get; }
        public bool Living => Frames > 1;
        public bool Starter => Source == CosmeticCatalog.StarterSource;
        public bool ForSale => Source == CosmeticCatalog.ShopSource && Price > 0;

        public string EquipKey => Hero.Length > 0 ? Slot + ":" + Hero : WeaponType.Length > 0 ? Slot + ":" + WeaponType : Slot;
    }

    public sealed class CosmeticCatalog
    {
        public const string FileName = "cosmetics.json";
        public const int SupportedVersion = 1;
        public const string ShopSource = "shop";
        public const string StarterSource = "starter";
        public const string CloakSlot = "heroCloak";
        public const string EyesSlot = "heroEyes";
        public const string StrikeSlot = "heroStrike";
        public const int MaxLook = byte.MaxValue;
        public static readonly IReadOnlyList<string> Sources = new[] { ShopSource, "pass", "drop", "reward", "event", StarterSource };
        public static readonly IReadOnlyList<string> LookSlots = new[] { CloakSlot, EyesSlot, StrikeSlot };
        public const int MinCloakFrames = 2;
        public const int MaxCloakFrames = 16;
        public const double MinCloakSeconds = 0.5;
        public const double MaxCloakSeconds = 10;
        public static readonly IReadOnlyList<string> CloakPatterns = new[] { "gradient", "trim", "duotone", "stripes", "stars" };
        public static readonly IReadOnlyList<string> LivingCloakPatterns = new[] { "pearl", "aurora", "embers", "twinkle", "lightning", "frost", "ripple", "thread" };

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

        public CosmeticDef? ByLook(string slot, int look)
        {
            foreach (var item in All)
            {
                if (item.Look == look && item.Slot == slot)
                    return item;
            }
            return null;
        }

        public static bool IsLookSlot(string slot) => ((IList<string>)LookSlots).Contains(slot);

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
            var looks = new HashSet<string>(StringComparer.Ordinal);
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
                var look = node.HasField("look") ? node.Field("look").AsInt(0, MaxLook) : 0;
                if (IsLookSlot(slot) && !looks.Add(slot + ":" + look))
                    throw node.Field("look").Error($"look {look} is used twice in slot '{slot}'");
                if (IsLookSlot(slot) != node.HasField("look"))
                    throw node.Field("slot").Error($"cosmetic '{id}': only hero looks carry a look number");
                var pattern = node.HasField("pattern") ? node.Field("pattern").AsString() : string.Empty;
                var living = ((IList<string>)LivingCloakPatterns).Contains(pattern);
                if (pattern.Length > 0 && (slot != CloakSlot || !(living || ((IList<string>)CloakPatterns).Contains(pattern))))
                    throw node.Field("pattern").Error($"cosmetic '{id}': unknown cloak pattern '{pattern}'");
                if ((pattern.Length > 0) != node.HasField("color2"))
                    throw node.Field("slot").Error($"cosmetic '{id}': a cloak pattern and its second colour go together");
                if (living != node.HasField("color3") || living != node.HasField("frames") || living != node.HasField("seconds"))
                    throw node.Field("slot").Error($"cosmetic '{id}': a living cloak pattern, and only it, carries color3, frames and seconds");
                var name = node.Field("name");
                all.Add(new CosmeticDef(id, slot, name.Field("ru").AsString(), name.Field("en").AsString(), price, source,
                    node.HasField("hero") ? node.Field("hero").AsString() : string.Empty,
                    node.HasField("weaponType") ? node.Field("weaponType").AsString() : string.Empty,
                    node.HasField("clan") && node.Field("clan").AsBool(),
                    node.HasField("achievement") ? node.Field("achievement").AsString() : string.Empty,
                    look,
                    node.HasField("color") ? node.Field("color").AsString() : string.Empty,
                    pattern,
                    node.HasField("color2") ? node.Field("color2").AsString() : string.Empty,
                    living ? node.Field("color3").AsString() : string.Empty,
                    living ? node.Field("frames").AsInt(MinCloakFrames, MaxCloakFrames) : 0,
                    living ? node.Field("seconds").AsNumber(MinCloakSeconds, MaxCloakSeconds) : 0));
            }
            return new CosmeticCatalog(all, slots, root.Field("crystalsPerDollar").AsInt(1), root.Field("weeklyDiscount").AsNumber(0, 0.9), root.Field("bundleDiscount").AsNumber(0, 0.9));
        }
    }
}
