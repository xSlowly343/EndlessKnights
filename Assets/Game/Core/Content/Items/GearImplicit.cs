using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content.Items
{
    public sealed class GearImplicit
    {
        public GearImplicit(string id, ItemSlot slot, IReadOnlyList<Modifier> lines)
        {
            Id = id;
            Slot = slot;
            Lines = lines;
        }

        public string Id { get; }
        public ItemSlot Slot { get; }
        public IReadOnlyList<Modifier> Lines { get; }
    }
}
