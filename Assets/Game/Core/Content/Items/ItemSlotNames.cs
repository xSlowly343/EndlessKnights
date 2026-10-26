using System;
using System.Collections.Generic;

namespace Game.Core.Content.Items
{
    public static class ItemSlotNames
    {
        public static readonly IReadOnlyDictionary<string, ItemSlot> ByName = Build();

        public static string Of(ItemSlot slot) => slot.ToString().ToLowerInvariant();

        private static Dictionary<string, ItemSlot> Build()
        {
            var names = new Dictionary<string, ItemSlot>(StringComparer.Ordinal);
            foreach (ItemSlot slot in Enum.GetValues(typeof(ItemSlot)))
                names[Of(slot)] = slot;
            return names;
        }
    }
}
