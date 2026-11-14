using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct PartySetsMember
    {
        public PartySetsMember(string userId, string name, IReadOnlyList<string> sets)
        {
            UserId = userId;
            Name = name;
            Sets = sets;
        }

        public string UserId { get; }
        public string Name { get; }
        public IReadOnlyList<string> Sets { get; }
    }

    public sealed class PartySetsReply
    {
        private PartySetsReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Yours = PartyTreasuryReply.Texts(NakamaJson.Items(json, "yours"));
            var counts = new Dictionary<string, int>();
            var field = NakamaJson.Field(json, "counts");
            if (field != null && field.Kind == JsonKind.Object)
            {
                foreach (var pair in field.Fields)
                {
                    if (pair.Value.Kind == JsonKind.Number)
                        counts[pair.Key] = (int)pair.Value.Number;
                }
            }
            Counts = counts;
            var members = new List<PartySetsMember>();
            foreach (var member in NakamaJson.Items(json, "members"))
                members.Add(new PartySetsMember(NakamaJson.Text(member, "userId"), NakamaJson.Text(member, "name"), PartyTreasuryReply.Texts(NakamaJson.Items(member, "sets"))));
            Members = members;
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public IReadOnlyDictionary<string, int> Counts { get; }
        public IReadOnlyList<string> Yours { get; }
        public IReadOnlyList<PartySetsMember> Members { get; }

        public static PartySetsReply From(JsonValue json) => new PartySetsReply(json);

        public static string ReportPayload(IReadOnlyList<string> sets)
        {
            var quoted = new string[sets.Count];
            for (var i = 0; i < sets.Count; i++)
                quoted[i] = NakamaJson.Quote(sets[i]);
            return NakamaJson.Object(("sets", "[" + string.Join(",", quoted) + "]"));
        }

        public static Task<PartySetsReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_sets", "{}", cancel);

        public static Task<PartySetsReply> ReportAsync(NakamaClient client, NakamaSession session, IReadOnlyList<string> sets, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_sets_report", ReportPayload(sets), cancel);

        private static async Task<PartySetsReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
