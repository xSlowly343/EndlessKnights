using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class SetDef
    {
        private readonly Dictionary<ItemSlot, IReadOnlyList<Modifier>> _pieces;

        public SetDef(string id, string nameRu, string nameEn, ItemRarity rarity, string? color, Dictionary<ItemSlot, IReadOnlyList<Modifier>> pieces, IReadOnlyList<Modifier> bonus, string? theme = null,
            IReadOnlyList<string>? weaponTypes = null)
        {
            WeaponTypes = weaponTypes ?? System.Array.Empty<string>();
            Theme = theme;
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Rarity = rarity;
            Color = color;
            _pieces = pieces;
            Bonus = bonus;
            Slots = new List<ItemSlot>(pieces.Keys);
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public ItemRarity Rarity { get; }
        public string? Color { get; }
        public string? Theme { get; }
        public IReadOnlyList<ItemSlot> Slots { get; }
        public IReadOnlyList<Modifier> Bonus { get; }
        public IReadOnlyList<string> WeaponTypes { get; }

        public bool AllowsType(string typeId)
        {
            foreach (var type in WeaponTypes)
            {
                if (string.Equals(type, typeId, System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public bool Has(ItemSlot slot) => _pieces.ContainsKey(slot);

        public IReadOnlyList<Modifier> PieceLines(ItemSlot slot) =>
            _pieces.TryGetValue(slot, out var lines) ? lines : System.Array.Empty<Modifier>();

        public static string PieceKey(string setId, ItemSlot slot) => setId + ":" + ItemSlotNames.Of(slot);
    }
}
