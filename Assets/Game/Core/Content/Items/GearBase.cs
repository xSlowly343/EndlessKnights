using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class GearBase
    {
        public GearBase(string id, ItemSlot slot, string? color, int fromBiome, string nameRu, string nameEn, IReadOnlyList<Modifier> implicitModifiers)
        {
            Id = id;
            Slot = slot;
            Color = color;
            FromBiome = fromBiome;
            NameRu = nameRu;
            NameEn = nameEn;
            Implicit = implicitModifiers;
        }

        public string Id { get; }
        public ItemSlot Slot { get; }
        public string? Color { get; }
        public int FromBiome { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Implicit { get; }
    }
}
