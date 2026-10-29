using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class ClanRules
    {
        public const string FileName = "clan.json";
        public const int SupportedVersion = 1;
        public const string Leader = "leader";
        public const string Officer = "officer";
        public const string Member = "member";

        private ClanRules(int maxMembers, int maxOfficers, int codeLength, int listLimit, int activeMinutes, int inactiveDays, int leaderAwayDays,
            IReadOnlyList<string> adjectives, IReadOnlyList<string> nouns)
        {
            MaxMembers = maxMembers;
            MaxOfficers = maxOfficers;
            CodeLength = codeLength;
            ListLimit = listLimit;
            ActiveMinutes = activeMinutes;
            InactiveDays = inactiveDays;
            LeaderAwayDays = leaderAwayDays;
            Adjectives = adjectives;
            Nouns = nouns;
        }

        public int MaxMembers { get; }
        public int MaxOfficers { get; }
        public int CodeLength { get; }
        public int ListLimit { get; }
        public int ActiveMinutes { get; }
        public int InactiveDays { get; }
        public int LeaderAwayDays { get; }
        public IReadOnlyList<string> Adjectives { get; }
        public IReadOnlyList<string> Nouns { get; }

        public static ClanRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var maxMembers = root.Field("maxMembers").AsInt(2, 100);
            var inactiveDays = root.Field("inactiveDays").AsInt(1, 365);
            var leaderAwayDays = root.Field("leaderAwayDays").AsInt(1, 365);
            if (leaderAwayDays > inactiveDays)
                throw root.Field("leaderAwayDays").Error($"must not be longer than inactiveDays ({inactiveDays})");
            var names = root.Field("names");
            return new ClanRules(maxMembers, root.Field("maxOfficers").AsInt(0, maxMembers - 1), root.Field("codeLength").AsInt(4, 10), root.Field("listLimit").AsInt(1, 100),
                root.Field("activeMinutes").AsInt(1, 1440), inactiveDays, leaderAwayDays, Ids(names.Field("adjectives")), Ids(names.Field("nouns")));
        }

        private static IReadOnlyList<string> Ids(ContentNode node)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>();
            foreach (var item in node.Items())
            {
                var id = item.AsString();
                if (id.Length == 0 || !seen.Add(id))
                    throw item.Error($"\"{id}\" is empty or repeated");
                foreach (var c in id)
                {
                    if (!(c >= 'a' && c <= 'z') && c != '_')
                        throw item.Error($"\"{id}\" may contain only a-z and _");
                }
                ids.Add(id);
            }
            if (ids.Count == 0)
                throw node.Error("must not be empty");
            return ids;
        }
    }
}
