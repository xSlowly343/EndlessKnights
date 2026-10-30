using System.Collections.Generic;

namespace Game.Core.Content.Rift
{
    public sealed class RiftMilestoneStep
    {
        public RiftMilestoneStep(long from, RiftReward reward, bool rareGem)
        {
            From = from;
            Reward = reward;
            RareGem = rareGem;
        }

        public long From { get; }
        public RiftReward Reward { get; }
        public bool RareGem { get; }
    }

    public sealed class RiftMilestoneRules
    {
        private RiftMilestoneRules(long every, IReadOnlyList<RiftMilestoneStep> steps, IReadOnlyList<string> rareGems, int rareGemsPerRift)
        {
            Every = every;
            Steps = steps;
            RareGems = rareGems;
            RareGemsPerRift = rareGemsPerRift;
        }

        public long Every { get; }
        public IReadOnlyList<RiftMilestoneStep> Steps { get; }
        public IReadOnlyList<string> RareGems { get; }
        public int RareGemsPerRift { get; }

        public RiftMilestoneStep? StepAt(long stage)
        {
            if (stage <= 0 || stage % Every != 0)
                return null;
            RiftMilestoneStep? found = null;
            foreach (var step in Steps)
            {
                if (step.From <= stage)
                    found = step;
            }
            return found;
        }

        public IReadOnlyList<string> GemsFor(long riftId)
        {
            var picked = RiftSchedule.PickModifiers(riftId, RareGems.Count, RareGemsPerRift);
            var gems = new string[picked.Count];
            for (var i = 0; i < picked.Count; i++)
                gems[i] = RareGems[picked[i]];
            return gems;
        }

        public string GemAt(long riftId, long stage)
        {
            var gems = GemsFor(riftId);
            return gems.Count == 0 ? string.Empty : gems[(int)((stage / Every) % gems.Count)];
        }

        public void RequireGems(Gems.GemCatalog gems)
        {
            foreach (var id in RareGems)
            {
                if (!gems.TryGet(id, out _))
                    throw new ContentException(RiftRules.FileName, "milestones.rareGems", $"unknown gem '{id}'");
            }
        }

        public static RiftMilestoneRules Read(ContentNode node)
        {
            var steps = new List<RiftMilestoneStep>();
            var last = 0L;
            foreach (var item in node.Field("steps").Items())
            {
                var from = item.Field("from").AsLong(1);
                if (from <= last)
                    throw item.Field("from").Error("milestone steps go up");
                last = from;
                steps.Add(new RiftMilestoneStep(from,
                    new RiftReward(item.HasField("shards") ? item.Field("shards").AsLong(0) : 0, item.HasField("uncutGems") ? item.Field("uncutGems").AsInt(0, 100) : 0, 0,
                        item.HasField("friendship") ? item.Field("friendship").AsLong(0, 1000) : 0),
                    item.HasField("rareGem") && item.Field("rareGem").AsBool()));
            }
            var gems = new List<string>();
            foreach (var item in node.Field("rareGems").Items())
                gems.Add(item.AsString());
            var perRift = node.Field("rareGemsPerRift").AsInt(0, 100);
            if (perRift > gems.Count)
                throw node.Field("rareGemsPerRift").Error($"there are only {gems.Count} rare gems");
            return new RiftMilestoneRules(node.Field("every").AsLong(1, 10_000), steps, gems, perRift);
        }
    }
}
