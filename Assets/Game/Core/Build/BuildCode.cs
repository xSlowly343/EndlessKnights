using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public enum BuildImportResult
    {
        Done,
        BadCode,
        NoClass,
        OtherClass
    }

    public static class BuildCode
    {
        public const string Prefix = "RW1-";
        public const int MaxLength = 1800;

        private const char PartSeparator = ';';
        private const char ItemSeparator = ',';
        private const char PairSeparator = ':';
        private const char SupportSeparator = '+';
        private const char CheckSeparator = '-';
        private const int Parts = 4;
        private const int CheckDigits = 4;

        public static string Encode(ClassTree tree, ClassTreeAllocation allocation, IReadOnlyList<SkillSlot> skills)
        {
            var text = new StringBuilder(tree.Id).Append(PartSeparator);
            foreach (var node in tree.Nodes)
                text.Append((char)('0' + System.Math.Clamp(allocation.RankOf(node.Id), 0, TreeTemplate.Ranks)));
            text.Append(PartSeparator);
            var first = true;
            foreach (var slot in tree.Majors)
            {
                var option = allocation.Majors.TryGetValue(slot.At, out var talent) ? OptionOf(slot, talent) : -1;
                if (option < 0)
                    continue;
                if (!first)
                    text.Append(ItemSeparator);
                first = false;
                text.Append(slot.At.ToString(CultureInfo.InvariantCulture)).Append(PairSeparator).Append(option.ToString(CultureInfo.InvariantCulture));
            }
            text.Append(PartSeparator);
            for (var i = 0; i < skills.Count; i++)
            {
                if (i > 0)
                    text.Append(ItemSeparator);
                text.Append(skills[i].Skill.Id);
                foreach (var support in skills[i].Supports)
                {
                    if (support != null)
                        text.Append(SupportSeparator).Append(support.Id);
                }
            }
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text.ToString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return Prefix + payload + CheckSeparator + Check(payload);
        }

        public static SharedBuild? Decode(string? code, Func<string, ClassTree?> treeOf, GemCatalog gems)
        {
            var text = Unwrap(code);
            if (text == null)
                return null;
            var parts = text.Split(PartSeparator);
            if (parts.Length != Parts || treeOf(parts[0]) is not { } tree || parts[1].Length != tree.Nodes.Count)
                return null;
            var ranks = new int[tree.Nodes.Count];
            for (var i = 0; i < ranks.Length; i++)
            {
                ranks[i] = parts[1][i] - '0';
                if (ranks[i] < 0 || ranks[i] > TreeTemplate.Ranks)
                    return null;
            }
            var majors = new Dictionary<int, int>();
            foreach (var pair in Items(parts[2]))
            {
                var sides = pair.Split(PairSeparator);
                if (sides.Length != 2 || !Number(sides[0], out var at) || !Number(sides[1], out var option))
                    return null;
                if (tree.MajorAt(at) is not { } slot || option >= slot.Options.Count)
                    return null;
                majors[at] = option;
            }
            var skills = new List<IReadOnlyList<string>>();
            foreach (var entry in Items(parts[3]))
            {
                var ids = entry.Split(SupportSeparator);
                if (skills.Count >= SkillLoadout.MaxSlots || ids.Length > 1 + SkillSlot.SocketCapacity)
                    return null;
                foreach (var id in ids)
                {
                    if (!gems.TryGet(id, out _))
                        return null;
                }
                skills.Add(ids);
            }
            return new SharedBuild(tree.Id, ranks, majors, skills);
        }

        public static void UseAsPlan(SharedBuild build, ClassTree tree, ClassTreeAllocation allocation)
        {
            var plan = new List<string>();
            for (var row = 1; row <= tree.Rows; row++)
            {
                for (var i = 0; i < tree.Nodes.Count && i < build.Ranks.Count; i++)
                {
                    if (tree.Nodes[i].Row != row)
                        continue;
                    for (var rank = 0; rank < build.Ranks[i]; rank++)
                        plan.Add(tree.Nodes[i].Id);
                }
            }
            allocation.RestorePlan(plan);
            foreach (var pair in build.Majors)
            {
                if (tree.MajorAt(pair.Key) is { } slot && pair.Value < slot.Options.Count)
                    allocation.RestoreMajor(pair.Key, slot.Options[pair.Value].Id);
            }
        }

        private static string? Unwrap(string? code)
        {
            if (string.IsNullOrWhiteSpace(code) || code!.Length > MaxLength)
                return null;
            var trimmed = code.Trim();
            var cut = trimmed.LastIndexOf(CheckSeparator);
            if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal) || cut != trimmed.Length - CheckDigits - 1 || cut <= Prefix.Length)
                return null;
            var payload = trimmed.Substring(Prefix.Length, cut - Prefix.Length);
            if (Check(payload) != trimmed.Substring(cut + 1))
                return null;
            var base64 = payload.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string Check(string payload)
        {
            var hash = 2166136261u;
            foreach (var symbol in payload)
                hash = (hash ^ symbol) * 16777619u;
            return ((hash ^ (hash >> 16)) & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture);
        }

        private static string[] Items(string part) => part.Length == 0 ? Array.Empty<string>() : part.Split(ItemSeparator);

        private static bool Number(string text, out int value) =>
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

        private static int OptionOf(MajorTalentSlot slot, string talentId)
        {
            for (var i = 0; i < slot.Options.Count; i++)
            {
                if (slot.Options[i].Id == talentId)
                    return i;
            }
            return -1;
        }
    }
}
