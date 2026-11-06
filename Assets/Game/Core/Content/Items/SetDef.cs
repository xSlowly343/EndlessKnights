using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public readonly struct SetPiece
    {
        public SetPiece(ItemSlot slot, bool secondRing)
        {
            Slot = slot;
            SecondRing = secondRing;
        }

        public ItemSlot Slot { get; }
        public bool SecondRing { get; }
    }

    public sealed class SetDef
    {
        public const string SecondRingKey = "ring_right";

        private readonly Dictionary<ItemSlot, IReadOnlyList<Modifier>> _pieces;
        private readonly IReadOnlyList<Modifier>? _secondRing;

        public SetDef(string id, string nameRu, string nameEn, ItemRarity rarity, string? color, Dictionary<ItemSlot, IReadOnlyList<Modifier>> pieces, IReadOnlyList<Modifier> bonus, string? theme = null,
            IReadOnlyList<string>? weaponTypes = null, IReadOnlyList<Modifier>? secondRing = null, double craftMultiplier = 1, IReadOnlyList<PerkValue>? perks = null)
        {
            Perks = perks ?? System.Array.Empty<PerkValue>();
            WeaponTypes = weaponTypes ?? System.Array.Empty<string>();
            Theme = theme;
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Rarity = rarity;
            Color = color;
            _pieces = pieces;
            _secondRing = secondRing;
            Bonus = bonus;
            CraftMultiplier = craftMultiplier;
            Slots = new List<ItemSlot>(pieces.Keys);
            var parts = new List<SetPiece>();
            foreach (var slot in pieces.Keys)
                parts.Add(new SetPiece(slot, false));
            if (secondRing != null)
                parts.Add(new SetPiece(ItemSlot.Ring, true));
            Pieces = parts;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public ItemRarity Rarity { get; }
        public string? Color { get; }
        public string? Theme { get; }
        public IReadOnlyList<ItemSlot> Slots { get; }
        public IReadOnlyList<SetPiece> Pieces { get; }
        public IReadOnlyList<Modifier> Bonus { get; }
        public IReadOnlyList<string> WeaponTypes { get; }
        public double CraftMultiplier { get; }
        public IReadOnlyList<PerkValue> Perks { get; }
        public string? Event { get; internal set; }
        public IReadOnlyList<Modifier> Primary { get; internal set; } = System.Array.Empty<Modifier>();
        public IReadOnlyList<Modifier> PerRaritySet { get; internal set; } = System.Array.Empty<Modifier>();
        public bool Peaceful => Perks.Count > 0;
        public bool HasSecondRing => _secondRing != null;
        public bool Jewelry => _pieces.ContainsKey(ItemSlot.Amulet) || _pieces.ContainsKey(ItemSlot.Ring);

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

        public IReadOnlyList<Modifier> PieceLines(ItemSlot slot, bool secondRing = false)
        {
            if (secondRing && slot == ItemSlot.Ring)
                return _secondRing ?? System.Array.Empty<Modifier>();
            return _pieces.TryGetValue(slot, out var lines) ? lines : System.Array.Empty<Modifier>();
        }

        public static string PieceKey(string setId, ItemSlot slot, bool secondRing = false) =>
            setId + ":" + (secondRing && slot == ItemSlot.Ring ? SecondRingKey : ItemSlotNames.Of(slot));

        public static string PieceKey(string setId, SetPiece piece) => PieceKey(setId, piece.Slot, piece.SecondRing);
    }
}
