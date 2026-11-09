using System.Collections.Generic;
using Game.Core.Sim;

namespace Game.Core.Content
{
    public sealed class OddityDef
    {
        public OddityDef(OddityKind kind, double chance, long minStage, double hpMultiplier)
        {
            Kind = kind;
            Chance = chance;
            MinStage = minStage;
            HpMultiplier = hpMultiplier;
        }

        public OddityKind Kind { get; }
        public double Chance { get; }
        public long MinStage { get; }
        public double HpMultiplier { get; }
    }

    public sealed class WispReward
    {
        public WispReward(WispRewardKind kind, double weight, double amount, double seconds)
        {
            Kind = kind;
            Weight = weight;
            Amount = amount;
            Seconds = seconds;
        }

        public WispRewardKind Kind { get; }
        public double Weight { get; }
        public double Amount { get; }
        public double Seconds { get; }
    }

    public sealed class OddityRules
    {
        private static readonly Dictionary<string, OddityKind> Kinds = new Dictionary<string, OddityKind>
        {
            ["miser"] = OddityKind.Miser,
            ["barrel"] = OddityKind.Barrel,
            ["bee"] = OddityKind.Bee,
            ["dummy"] = OddityKind.Dummy,
            ["splitter"] = OddityKind.Splitter,
            ["sprout"] = OddityKind.Sprout
        };

        private static readonly Dictionary<string, WispRewardKind> RewardKinds = new Dictionary<string, WispRewardKind>
        {
            ["gold"] = WispRewardKind.Gold,
            ["resource"] = WispRewardKind.Resource,
            ["goldSpree"] = WispRewardKind.GoldSpree,
            ["damageSpree"] = WispRewardKind.DamageSpree,
            ["shards"] = WispRewardKind.Shards
        };

        public static readonly OddityRules None = new OddityRules();

        public OddityDef[] Order { get; private set; } = new OddityDef[0];
        public long[] ExcludedStages { get; private set; } = new long[0];
        public int TrailStages { get; private set; }
        public double TrailGold { get; private set; } = 1;
        public double BarrelShardShare { get; private set; }
        public double BeeResourceShare { get; private set; }
        public double DummySeconds { get; private set; }
        public double DummyMaxKills { get; private set; }
        public int SplitterParts { get; private set; }
        public double SplitterPartHp { get; private set; }
        public double SplitterSeconds { get; private set; }
        public int CrowdStages { get; private set; }
        public double CrowdCut { get; private set; }
        public double SproutDamage { get; private set; }
        public int SproutMaxStacks { get; private set; }
        public double WispEverySeconds { get; private set; }
        public double WispFlySeconds { get; private set; }
        public double WispActions { get; private set; }
        public double WispMousePxPerAction { get; private set; } = 100;
        public WispReward[] WispRewards { get; private set; } = new WispReward[0];
        public double WispRewardWeight { get; private set; }

        public bool Excludes(long stage)
        {
            for (var i = 0; i < ExcludedStages.Length; i++)
            {
                if (ExcludedStages[i] == stage)
                    return true;
            }
            return false;
        }

        public OddityDef? Find(OddityKind kind)
        {
            for (var i = 0; i < Order.Length; i++)
            {
                if (Order[i].Kind == kind)
                    return Order[i];
            }
            return null;
        }

        public static OddityRules Read(ContentNode encounters)
        {
            if (!encounters.HasField("oddities") && !encounters.HasField("excludeStages") && !encounters.HasField("wisp"))
                return None;
            var rules = new OddityRules();
            if (encounters.HasField("excludeStages"))
                rules.ExcludedStages = ReadStages(encounters.Field("excludeStages"));
            if (encounters.HasField("oddities"))
                rules.ReadOrder(encounters.Field("oddities"));
            if (encounters.HasField("wisp"))
                rules.ReadWisp(encounters.Field("wisp"));
            return rules;
        }

        private void ReadOrder(ContentNode oddities)
        {
            var order = new List<OddityDef>();
            foreach (var node in oddities.Items())
            {
                var def = ReadOddity(node);
                Order = order.ToArray();
                if (Find(def.Kind) != null)
                    throw node.Field("id").Error("listed twice");
                order.Add(def);
            }
            Order = order.ToArray();
        }

        private static long[] ReadStages(ContentNode node)
        {
            var stages = new List<long>();
            foreach (var item in node.Items())
                stages.Add(item.AsLong(1));
            return stages.ToArray();
        }

        private OddityDef ReadOddity(ContentNode node)
        {
            var id = node.Field("id");
            if (!Kinds.TryGetValue(id.AsString(), out var kind))
                throw id.Error("unknown oddity");
            var def = new OddityDef(kind, node.Field("chance").AsNumber(0, 1), node.Field("minStage").AsLong(1), node.Field("hpMultiplier").AsNumber(0.01, 1e12));
            switch (kind)
            {
                case OddityKind.Miser:
                    TrailStages = node.Field("trailStages").AsInt(1, 1000);
                    TrailGold = node.Field("trailGold").AsNumber(1, 1000);
                    break;
                case OddityKind.Barrel:
                    BarrelShardShare = node.Field("shardShare").AsNumber(0, 100);
                    break;
                case OddityKind.Bee:
                    BeeResourceShare = node.Field("resourceShare").AsNumber(0, 1);
                    break;
                case OddityKind.Dummy:
                    DummySeconds = node.Field("seconds").AsNumber(0.1, 600);
                    DummyMaxKills = node.Field("maxKills").AsNumber(0, 1e6);
                    break;
                case OddityKind.Splitter:
                    SplitterParts = node.Field("parts").AsInt(1, 16);
                    SplitterPartHp = node.Field("partHp").AsNumber(0.01, 10);
                    SplitterSeconds = node.Field("seconds").AsNumber(0.1, 600);
                    CrowdStages = node.Field("crowdStages").AsInt(1, 1000);
                    CrowdCut = node.Field("crowdCut").AsNumber(0, 0.9);
                    break;
                case OddityKind.Sprout:
                    SproutDamage = node.Field("damage").AsNumber(0, 10);
                    SproutMaxStacks = node.Field("maxStacks").AsInt(1, 100000);
                    break;
            }
            return def;
        }

        private void ReadWisp(ContentNode node)
        {
            WispEverySeconds = node.Field("everySeconds").AsNumber(1, 86400);
            WispFlySeconds = node.Field("flySeconds").AsNumber(0.5, 600);
            WispActions = node.Field("actions").AsNumber(1, 1e6);
            WispMousePxPerAction = node.Field("mousePxPerAction").AsNumber(1, 1e6);
            var rewards = new List<WispReward>();
            var total = 0.0;
            foreach (var item in node.Field("rewards").Items())
            {
                var id = item.Field("kind");
                if (!RewardKinds.TryGetValue(id.AsString(), out var kind))
                    throw id.Error("unknown wisp reward");
                var reward = new WispReward(kind, item.Field("weight").AsNumber(0, 1e6), item.Field("amount").AsNumber(0, 1e6),
                    item.HasField("seconds") ? item.Field("seconds").AsNumber(0, 86400) : 0);
                total += reward.Weight;
                rewards.Add(reward);
            }
            if (!(total > 0))
                throw node.Field("rewards").Error("needs a reward with weight above zero");
            WispRewards = rewards.ToArray();
            WispRewardWeight = total;
        }
    }
}
