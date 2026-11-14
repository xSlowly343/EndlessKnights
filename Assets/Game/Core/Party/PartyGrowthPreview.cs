using System.Collections.Generic;

namespace Game.Core.Party
{
    public enum PartyUpgradeKind
    {
        TreeNode,
        TreeMajor,
        Relic,
        Rite,
        MascotLevels,
        TrialTier,
        Mascot,
        Crest,
        PartySet
    }

    public readonly struct PartyUpgrade
    {
        private PartyUpgrade(PartyUpgradeKind kind, string id, int amount)
        {
            Kind = kind;
            Id = id;
            Amount = amount;
        }

        public PartyUpgradeKind Kind { get; }
        public string Id { get; }
        public int Amount { get; }

        public static PartyUpgrade TreeNode(int node) => new PartyUpgrade(PartyUpgradeKind.TreeNode, string.Empty, node);
        public static PartyUpgrade TreeMajor(string id) => new PartyUpgrade(PartyUpgradeKind.TreeMajor, id, 0);
        public static PartyUpgrade Relic(string id) => new PartyUpgrade(PartyUpgradeKind.Relic, id, 1);
        public static PartyUpgrade Rite(string id) => new PartyUpgrade(PartyUpgradeKind.Rite, id, 1);
        public static PartyUpgrade MascotLevels(int levels) => new PartyUpgrade(PartyUpgradeKind.MascotLevels, string.Empty, levels);
        public static PartyUpgrade TrialTier(int trial) => new PartyUpgrade(PartyUpgradeKind.TrialTier, string.Empty, trial);
        public static PartyUpgrade Mascot(string beast) => new PartyUpgrade(PartyUpgradeKind.Mascot, beast, 0);
        public static PartyUpgrade Crest(string crest) => new PartyUpgrade(PartyUpgradeKind.Crest, crest, 0);
        public static PartyUpgrade PartySet(string set) => new PartyUpgrade(PartyUpgradeKind.PartySet, set, 1);
    }

    public readonly struct PartyGain
    {
        public PartyGain(PartyGrowthSummary before, PartyGrowthSummary after)
        {
            Before = before;
            After = after;
        }

        public PartyGrowthSummary Before { get; }
        public PartyGrowthSummary After { get; }
    }

    public static class PartyGrowthPreview
    {
        public static PartyGain Of(PartyGrowthLayer layer, PartyUpgrade upgrade)
        {
            var scratch = layer.Scratch();
            Apply(scratch, upgrade);
            return new PartyGain(PartyGrowthSummary.Of(layer), PartyGrowthSummary.Of(scratch));
        }

        private static void Apply(PartyGrowthLayer layer, PartyUpgrade upgrade)
        {
            switch (upgrade.Kind)
            {
                case PartyUpgradeKind.TreeNode:
                    var levels = new Dictionary<int, int>(Copy(layer.TreeLevels));
                    levels[upgrade.Amount] = (levels.TryGetValue(upgrade.Amount, out var rank) ? rank : 0) + 1;
                    layer.SetTree(levels, layer.TreeMajors);
                    break;
                case PartyUpgradeKind.TreeMajor:
                    layer.SetTree(layer.TreeLevels, WithMajor(layer, upgrade.Id));
                    break;
                case PartyUpgradeKind.Relic:
                    layer.SetRelics(Raised(layer.RelicLevels, upgrade.Id));
                    break;
                case PartyUpgradeKind.Rite:
                    layer.SetMascot(layer.MascotBeast, layer.MascotLevel, Raised(layer.RiteLevels, upgrade.Id));
                    break;
                case PartyUpgradeKind.MascotLevels:
                    layer.SetMascot(layer.MascotBeast, System.Math.Min(layer.Rules.Mascot.MaxLevel, layer.MascotLevel + upgrade.Amount), layer.RiteLevels);
                    break;
                case PartyUpgradeKind.Mascot:
                    layer.SetMascot(upgrade.Id, System.Math.Max(1, layer.MascotLevel), layer.RiteLevels);
                    break;
                case PartyUpgradeKind.PartySet:
                    layer.SetPartySets(Raised(layer.PartySetCounts, upgrade.Id));
                    break;
                case PartyUpgradeKind.Crest:
                    var crests = new List<string>(layer.Crests) { upgrade.Id };
                    layer.SetBanner(crests);
                    break;
                case PartyUpgradeKind.TrialTier:
                    var best = new List<int>(layer.TrialBest);
                    while (best.Count <= upgrade.Amount)
                        best.Add(0);
                    best[upgrade.Amount] = System.Math.Min(layer.Rules.Trials.MaxTier, best[upgrade.Amount] + 1);
                    layer.SetTrials(best);
                    break;
            }
        }

        private static Dictionary<int, int> Copy(IReadOnlyDictionary<int, int> source)
        {
            var copy = new Dictionary<int, int>();
            foreach (var pair in source)
                copy[pair.Key] = pair.Value;
            return copy;
        }

        private static Dictionary<string, int> Raised(IReadOnlyDictionary<string, int> source, string id)
        {
            var copy = new Dictionary<string, int>();
            foreach (var pair in source)
                copy[pair.Key] = pair.Value;
            copy[id] = (copy.TryGetValue(id, out var level) ? level : 0) + 1;
            return copy;
        }

        private static List<string> WithMajor(PartyGrowthLayer layer, string id)
        {
            var majors = new List<string>();
            PartyTreeMajorSlot? slot = null;
            foreach (var candidate in layer.Rules.Tree.Majors)
            {
                foreach (var option in candidate.Options)
                {
                    if (option.Id == id)
                        slot = candidate;
                }
            }
            foreach (var major in layer.TreeMajors)
            {
                var sameSlot = false;
                if (slot != null)
                {
                    foreach (var option in slot.Options)
                        sameSlot |= option.Id == major;
                }
                if (!sameSlot)
                    majors.Add(major);
            }
            majors.Add(id);
            return majors;
        }
    }
}
