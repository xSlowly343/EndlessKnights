using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public static class PartyTreeBonus
    {
        public static void Build(PartyTreeRules rules, IReadOnlyDictionary<int, int> levels, IReadOnlyList<string> majors, int members, List<Modifier> output)
        {
            output.Clear();
            var points = PartyTreeRules.Points(levels);
            var scale = 1 + MajorScale(rules, majors, points);
            foreach (var pair in levels)
            {
                var node = rules.Node(pair.Key);
                if (node == null || pair.Value <= 0)
                    continue;
                AddLines(node.A, node.AMultiplier(pair.Value) * scale, members, output);
                if (pair.Value >= 3)
                    AddLines(node.B, scale, members, output);
                if (pair.Value >= 5)
                    AddLines(node.C, scale, members, output);
            }
            if (rules.BarPassives.Count > 0)
            {
                var steps = points / rules.BarEvery;
                for (var i = 0; i < steps; i++)
                    AddLine(rules.BarPassives[i % rules.BarPassives.Count], scale, members, output);
            }
            foreach (var slot in rules.Majors)
            {
                var major = Chosen(slot, majors, points);
                if (major != null)
                    AddLines(major.Lines, 1, members, output);
            }
        }

        public static PartyTreeMajor? Chosen(PartyTreeMajorSlot slot, IReadOnlyList<string> majors, int points)
        {
            if (points < slot.At)
                return null;
            foreach (var option in slot.Options)
            {
                for (var i = 0; i < majors.Count; i++)
                {
                    if (majors[i] == option.Id)
                        return option;
                }
            }
            return null;
        }

        private static double MajorScale(PartyTreeRules rules, IReadOnlyList<string> majors, int points)
        {
            var scale = 0.0;
            foreach (var slot in rules.Majors)
            {
                var major = Chosen(slot, majors, points);
                if (major == null)
                    continue;
                foreach (var line in major.Lines)
                    scale += line.TreeScale;
            }
            return scale;
        }

        private static void AddLines(IReadOnlyList<PartyTreeLine> lines, double multiplier, int members, List<Modifier> output)
        {
            for (var i = 0; i < lines.Count; i++)
                AddLine(lines[i], multiplier, members, output);
        }

        private static void AddLine(PartyTreeLine line, double multiplier, int members, List<Modifier> output)
        {
            if (!line.Works || !line.Modifier.HasValue || members < line.MinMembers)
                return;
            var modifier = line.Modifier.Value;
            output.Add(modifier.WithValue(modifier.Value * multiplier * (line.PerMember ? System.Math.Max(1, members) : 1)));
        }
    }
}
