using System.Collections.Generic;
using Game.Core.Content.Tree;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Build
{
    public readonly struct TreeNodeDelta
    {
        public readonly string What;
        public readonly string From;
        public readonly string To;

        public TreeNodeDelta(string what, string from, string to)
        {
            What = what;
            From = from;
            To = to;
        }
    }

    public sealed class TreeNodeCard
    {
        public const int MaxDeltas = 3;

        private TreeNodeCard(string name, string kind, IReadOnlyList<string> stats, string description, IReadOnlyList<TreeNodeDelta> deltas, string status)
        {
            Name = name;
            Kind = kind;
            Stats = stats;
            Description = description;
            Deltas = deltas;
            Status = status;
        }

        public string Name { get; }
        public string Kind { get; }
        public IReadOnlyList<string> Stats { get; }
        public string Description { get; }
        public IReadOnlyList<TreeNodeDelta> Deltas { get; }
        public string Status { get; }

        public TreeNodeCard Shortened(int maxStats)
        {
            var stats = new List<string>();
            for (var i = 0; i < Stats.Count && i < maxStats; i++)
                stats.Add(Stats[i]);
            return new TreeNodeCard(Name, Kind, stats, string.Empty, Deltas, Status);
        }

        public static TreeNodeCard Build(TreeNodeDef node, string kind, IReadOnlyList<string> stats, string status, IReadOnlyList<Modifier> active, bool taken, StatSheet hero) =>
            new TreeNodeCard(Texts.Pick(node.NameRu, node.NameEn), kind, stats, DescriptionOf(node), DeltasOf(active, taken, hero), status);

        public static string DescriptionOf(TreeNodeDef node) =>
            node.HasDescription ? Texts.Pick(node.DescriptionRu, node.DescriptionEn) : Texts.Get("tree.about." + TypeKey(node.Type));

        public static IReadOnlyList<TreeNodeDelta> DeltasOf(IReadOnlyList<Modifier> active, bool taken, StatSheet hero)
        {
            var deltas = new List<TreeNodeDelta>();
            foreach (var modifier in active)
            {
                if (deltas.Count == MaxDeltas)
                    break;
                if (TryDelta(modifier, taken, hero.TotalsOf(modifier.Stat, modifier.Tags), out var delta))
                    deltas.Add(delta);
            }
            return deltas;
        }

        private static bool TryDelta(Modifier modifier, bool taken, StatSheet.Totals totals, out TreeNodeDelta delta)
        {
            var what = Capitalized(ModifierText.Subject(modifier));
            var value = modifier.Value;
            switch (modifier.Kind)
            {
                case ModifierKind.Increased:
                {
                    var now = totals.IncreasedMultiplier - 1;
                    var (from, to) = taken ? (now - value, now) : (now, now + value);
                    delta = new TreeNodeDelta(what, ModifierText.Percent(from), ModifierText.Percent(to));
                    return true;
                }
                case ModifierKind.More:
                {
                    var now = totals.MoreMultiplier;
                    var step = 1 + value;
                    if (step <= 0 && taken)
                    {
                        delta = default;
                        return false;
                    }
                    var (from, to) = taken ? (now / step, now) : (now, now * step);
                    delta = new TreeNodeDelta(what, ModifierText.Times(from), ModifierText.Times(to));
                    return true;
                }
                default:
                {
                    var now = totals.Flat;
                    var (from, to) = taken ? (now - value, now) : (now, now + value);
                    delta = new TreeNodeDelta(what, ModifierText.FlatAmount(modifier.Stat, from, true), ModifierText.FlatAmount(modifier.Stat, to, true));
                    return true;
                }
            }
        }

        private static string Capitalized(string text) =>
            text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        private static string TypeKey(TreeNodeType type) => type switch
        {
            TreeNodeType.Small => "small",
            TreeNodeType.Notable => "notable",
            TreeNodeType.Keystone => "keystone",
            _ => "start"
        };
    }
}
