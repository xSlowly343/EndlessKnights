using System.Collections.Generic;

namespace Game.Core.Content.Rift
{
    public sealed class RiftMilestoneStep
    {
        public RiftMilestoneStep(long from, RiftReward reward)
        {
            From = from;
            Reward = reward;
        }

        public long From { get; }
        public RiftReward Reward { get; }
    }

    public sealed class RiftMilestoneRules
    {
        private RiftMilestoneRules(long every, IReadOnlyList<RiftMilestoneStep> steps)
        {
            Every = every;
            Steps = steps;
        }

        public long Every { get; }
        public IReadOnlyList<RiftMilestoneStep> Steps { get; }

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
                    new RiftReward(item.HasField("shards") ? item.Field("shards").AsLong(0) : 0, 0,
                        item.HasField("friendship") ? item.Field("friendship").AsLong(0, 1000) : 0)));
            }
            return new RiftMilestoneRules(node.Field("every").AsLong(1, 10_000), steps);
        }
    }
}
