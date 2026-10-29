using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public readonly struct ClanChallengeRule
    {
        public ClanChallengeRule(string id, string counter, double perMember, int seals)
        {
            Id = id;
            Counter = counter;
            PerMember = perMember;
            Seals = seals;
        }

        public string Id { get; }
        public string Counter { get; }
        public double PerMember { get; }
        public int Seals { get; }
    }

    public sealed class ClanChallengeRules
    {
        public const string FileName = "clan_challenges.json";
        public const int SupportedVersion = 1;
        public static readonly IReadOnlyList<string> Counters = new[] { "keys", "clicks", "mouseMeters", "bosses" };

        private ClanChallengeRules(int perDay, IReadOnlyList<ClanChallengeRule> challenges)
        {
            PerDay = perDay;
            Challenges = challenges;
        }

        public int PerDay { get; }
        public IReadOnlyList<ClanChallengeRule> Challenges { get; }

        public static ClanChallengeRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var caps = root.Field("capsPerSecond");
            foreach (var counter in Counters)
                caps.Field(counter).AsNumber(0.01, 1000);
            var challenges = new List<ClanChallengeRule>();
            var ids = new HashSet<string>();
            var list = root.Field("challenges");
            foreach (var item in list.Items())
            {
                var id = item.Field("id").AsString();
                if (id.Length == 0 || !ids.Add(id))
                    throw item.Field("id").Error("must be a new, non-empty id");
                var counterNode = item.Field("counter");
                var counter = counterNode.AsString();
                if (!Contains(counter))
                    throw counterNode.Error($"unknown counter \"{counter}\", expected one of {string.Join(", ", Counters)}");
                challenges.Add(new ClanChallengeRule(id, counter, item.Field("perMember").AsNumber(0.01, 1e9), item.Field("seals").AsInt(0, 10000)));
            }
            var perDay = root.Field("perDay").AsInt(1, 10);
            if (perDay > challenges.Count)
                throw root.Field("perDay").Error($"cannot be more than the {challenges.Count} challenges listed");
            root.Field("dayUtcOffsetMinutes").AsInt(-720, 840);
            root.Field("minMembers").AsInt(1, 100);
            return new ClanChallengeRules(perDay, challenges);
        }

        private static bool Contains(string counter)
        {
            for (var i = 0; i < Counters.Count; i++)
            {
                if (Counters[i] == counter)
                    return true;
            }
            return false;
        }
    }
}
