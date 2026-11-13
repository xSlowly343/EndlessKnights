using System.Collections.Generic;
using Game.Core.Content.Items;

namespace Game.Core.Content.Rift
{
    public sealed class RiftShopOffer
    {
        public RiftShopOffer(string id, long price, long sparks, int perWindow)
        {
            Id = id;
            Price = price;
            Sparks = sparks;
            PerWindow = perWindow;
        }

        public string Id { get; }
        public long Price { get; }
        public long Sparks { get; }
        public int PerWindow { get; }
    }

    public sealed class RiftForgeRules
    {
        public static readonly RiftForgeRules None = new RiftForgeRules(0, 0, 0, 0, 0, 0, System.Array.Empty<RiftShopOffer>());

        public RiftForgeRules(long bossSparks, long guardianSparks, long salvageBase, long salvagePerRarity, long milestoneEvery, long milestoneSparks, IReadOnlyList<RiftShopOffer> shop)
        {
            BossSparks = bossSparks;
            GuardianSparks = guardianSparks;
            SalvageBase = salvageBase;
            SalvagePerRarity = salvagePerRarity;
            MilestoneEvery = milestoneEvery;
            MilestoneSparks = milestoneSparks;
            Shop = shop;
        }

        public long BossSparks { get; }
        public long GuardianSparks { get; }
        public long SalvageBase { get; }
        public long SalvagePerRarity { get; }
        public long MilestoneEvery { get; }
        public long MilestoneSparks { get; }
        public IReadOnlyList<RiftShopOffer> Shop { get; }

        public long SalvageSparks(ItemRarity rarity) => SalvageBase + SalvagePerRarity * (int)rarity;

        public long MilestonesUpTo(long stage) => MilestoneEvery <= 0 ? 0 : System.Math.Max(0, stage) / MilestoneEvery;

        public RiftShopOffer? Offer(string id)
        {
            foreach (var offer in Shop)
            {
                if (offer.Id == id)
                    return offer;
            }
            return null;
        }

        public static RiftForgeRules Read(ContentNode node)
        {
            var shop = new List<RiftShopOffer>();
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var item in node.Field("shop").Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"shop offer '{id}' is used twice");
                shop.Add(new RiftShopOffer(id, item.Field("price").AsLong(1, 1_000_000), item.Field("sparks").AsLong(1, 1_000_000), item.Field("perWindow").AsInt(1, 100)));
            }
            var sparks = node.Field("sparks");
            var salvage = sparks.Field("salvage");
            var milestone = sparks.Field("milestone");
            return new RiftForgeRules(sparks.Field("boss").AsLong(0, 1000), sparks.Field("guardian").AsLong(0, 1000),
                salvage.Field("base").AsLong(0, 1000), salvage.Field("perRarity").AsLong(0, 1000),
                milestone.Field("every").AsLong(1, 1_000_000), milestone.Field("sparks").AsLong(0, 1_000_000), shop);
        }
    }
}
