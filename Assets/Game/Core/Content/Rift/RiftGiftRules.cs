using System.Collections.Generic;
using Game.Core.Content.Items;
using Game.Core.Stats;

namespace Game.Core.Content.Rift
{
    public sealed class RiftGift
    {
        public RiftGift(string id, IReadOnlyList<Modifier> modifiers)
        {
            Id = id;
            Modifiers = modifiers;
        }

        public string Id { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class RiftGiftRules
    {
        public static readonly RiftGiftRules None = new RiftGiftRules(0, 0, System.Array.Empty<RiftGift>());

        private readonly Dictionary<string, RiftGift> _byId = new Dictionary<string, RiftGift>(System.StringComparer.Ordinal);

        public RiftGiftRules(long every, int offer, IReadOnlyList<RiftGift> pool)
        {
            Every = every;
            Offer = offer;
            Pool = pool;
            foreach (var gift in pool)
                _byId[gift.Id] = gift;
        }

        public long Every { get; }
        public int Offer { get; }
        public IReadOnlyList<RiftGift> Pool { get; }

        public bool TryGet(string id, out RiftGift gift) => _byId.TryGetValue(id, out gift!);

        public int EarnedBy(long stage) => Every <= 0 ? 1 : 1 + (int)(System.Math.Max(0, stage) / Every);

        public static RiftGiftRules Read(ContentNode node)
        {
            var pool = new List<RiftGift>();
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var item in node.Field("pool").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"gift '{id}' is listed twice");
                pool.Add(new RiftGift(id, ModifierReader.ReadList(item.Field("modifiers"))));
            }
            var offer = node.Field("offer").AsInt(1, 10);
            if (pool.Count < offer)
                throw node.Field("pool").Error($"needs at least {offer} gifts to offer");
            return new RiftGiftRules(node.Field("every").AsLong(1), offer, pool);
        }
    }

    public sealed class RiftWindowSetRules
    {
        public RiftWindowSetRules(ItemRarity rarity, int itemLevel)
        {
            Rarity = rarity;
            ItemLevel = itemLevel;
        }

        public ItemRarity Rarity { get; }
        public int ItemLevel { get; }

        public static RiftWindowSetRules Read(ContentNode node) =>
            new RiftWindowSetRules(ReadRarity(node.Field("rarity")), node.Field("itemLevel").AsInt(1, 1000));

        private static ItemRarity ReadRarity(ContentNode node)
        {
            var name = node.AsString();
            if (!ItemRarityNames.ByName.TryGetValue(name, out var rarity))
                throw node.Error($"unknown rarity '{name}'");
            return rarity;
        }
    }
}
