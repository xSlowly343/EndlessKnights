using System.Collections.Generic;

namespace Game.Core.Content.Rift
{
    public readonly struct RiftReward
    {
        public RiftReward(long shards, int uncutGems, long ingots, long friendship = 0)
        {
            Shards = shards;
            UncutGems = uncutGems;
            Ingots = ingots;
            Friendship = friendship;
        }

        public long Shards { get; }
        public int UncutGems { get; }
        public long Ingots { get; }
        public long Friendship { get; }
        public bool IsEmpty => Shards == 0 && UncutGems == 0 && Ingots == 0 && Friendship == 0;
    }

    public sealed class RiftBoardPlace
    {
        public RiftBoardPlace(int upTo, RiftReward reward)
        {
            UpTo = upTo;
            Reward = reward;
        }

        public int UpTo { get; }
        public RiftReward Reward { get; }
        public bool Rest => UpTo == 0;
    }

    public sealed class RiftBoardRules
    {
        private RiftBoardRules(double updateMinutes, int shown, IReadOnlyList<RiftBoardPlace> places)
        {
            UpdateMinutes = updateMinutes;
            Shown = shown;
            Places = places;
        }

        public double UpdateMinutes { get; }
        public int Shown { get; }
        public IReadOnlyList<RiftBoardPlace> Places { get; }

        public RiftReward RewardFor(int place)
        {
            foreach (var tier in Places)
            {
                if (tier.Rest || place <= tier.UpTo)
                    return tier.Reward;
            }
            return default;
        }

        public static RiftBoardRules Read(ContentNode node)
        {
            var places = new List<RiftBoardPlace>();
            var placesNode = node.Field("places");
            var items = placesNode.Items();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var last = i == items.Count - 1;
                if (last == item.HasField("upTo"))
                    throw item.Error(last ? "the last tier is everyone else and has no upTo" : "every tier but the last needs upTo");
                var upTo = last ? 0 : item.Field("upTo").AsInt(1, 100000);
                if (!last && places.Count > 0 && upTo <= places[places.Count - 1].UpTo)
                    throw item.Field("upTo").Error("places must grow");
                places.Add(new RiftBoardPlace(upTo, new RiftReward(item.Field("shards").AsLong(0, 1000000), item.Field("uncutGems").AsInt(0, 100), item.Field("ingots").AsLong(0, 10000),
                    item.HasField("friendship") ? item.Field("friendship").AsLong(0, 10000) : 0)));
            }
            if (places.Count == 0)
                throw placesNode.Error("must list at least one tier");
            return new RiftBoardRules(node.Field("updateMinutes").AsNumber(1, 60), node.Field("shown").AsInt(1, 100), places);
        }
    }
}
