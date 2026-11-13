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
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoKeys = new Dictionary<string, IReadOnlyList<string>>();

        public RiftWindowSetRules(ItemRarity rarity, int itemLevel, double softenShare = 0, IReadOnlyDictionary<string, IReadOnlyList<string>>? keys = null)
        {
            Rarity = rarity;
            ItemLevel = itemLevel;
            SoftenShare = softenShare;
            Keys = keys ?? NoKeys;
        }

        public ItemRarity Rarity { get; }
        public int ItemLevel { get; }
        public double SoftenShare { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Keys { get; }

        public IReadOnlyList<string> KeysOf(string modifierId) =>
            Keys.TryGetValue(modifierId, out var sets) ? sets : System.Array.Empty<string>();

        public void CheckSets(ItemCatalog items)
        {
            var keyed = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var pair in Keys)
            {
                foreach (var id in pair.Value)
                {
                    if (!items.TryGetSet(id, out var set) || !set.RiftOnly)
                        throw new ContentException(RiftRules.FileName, "windowSet.keys." + pair.Key, $"'{id}' is not a riftOnly set in {ItemCatalog.FileName}");
                    keyed.Add(id);
                }
            }
            foreach (var set in items.Sets)
            {
                if (set.RiftOnly && !keyed.Contains(set.Id))
                    throw new ContentException(ItemCatalog.FileName, "sets." + set.Id, $"a riftOnly set must be a window key in {RiftRules.FileName}");
            }
        }

        public static RiftWindowSetRules Read(ContentNode node) =>
            new RiftWindowSetRules(ReadRarity(node.Field("rarity")), node.Field("itemLevel").AsInt(1, 1000),
                node.HasField("softenShare") ? node.Field("softenShare").AsNumber(0, 1) : 0, ReadKeys(node));

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadKeys(ContentNode node)
        {
            var keys = new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.Ordinal);
            if (!node.HasField("keys"))
                return keys;
            foreach (var key in node.Field("keys").Items())
            {
                var modifier = key.Field("modifier").AsString();
                var sets = new List<string>();
                foreach (var set in key.Field("sets").Items())
                    sets.Add(set.AsString());
                if (sets.Count == 0)
                    throw key.Field("sets").Error("a key needs at least one set");
                if (keys.ContainsKey(modifier))
                    throw key.Field("modifier").Error($"modifier '{modifier}' has keys twice");
                keys[modifier] = sets;
            }
            return keys;
        }

        private static ItemRarity ReadRarity(ContentNode node)
        {
            var name = node.AsString();
            if (!ItemRarityNames.ByName.TryGetValue(name, out var rarity))
                throw node.Error($"unknown rarity '{name}'");
            return rarity;
        }
    }
}
