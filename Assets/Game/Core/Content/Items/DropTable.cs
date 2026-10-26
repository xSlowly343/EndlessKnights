namespace Game.Core.Content.Items
{
    public sealed class DropTable
    {
        public DropTable(int items, ItemRarity minRarity, ItemRarity maxRarity)
        {
            Items = items;
            MinRarity = minRarity;
            MaxRarity = maxRarity;
        }

        public int Items { get; }
        public ItemRarity MinRarity { get; }
        public ItemRarity MaxRarity { get; }

        public static DropTable Read(ContentNode node, int maxItems)
        {
            var min = Rarity(node, "minRarity", ItemRarity.Simple);
            var max = Rarity(node, "maxRarity", ItemRarity.Rift);
            if (max < min)
                throw node.Field("maxRarity").Error($"must not be below minRarity '{ItemRarityNames.Of(min)}'");
            return new DropTable(node.HasField("items") ? node.Field("items").AsInt(1, maxItems) : 1, min, max);
        }

        public static ItemRarity Rarity(ContentNode node, string field, ItemRarity fallback)
        {
            if (!node.HasField(field))
                return fallback;
            var value = node.Field(field);
            if (!ItemRarityNames.ByName.TryGetValue(value.AsString(), out var rarity) || rarity > ItemRarity.Rift)
                throw value.Error($"unknown rarity '{value.AsString()}', expected simple to rift");
            return rarity;
        }
    }
}
