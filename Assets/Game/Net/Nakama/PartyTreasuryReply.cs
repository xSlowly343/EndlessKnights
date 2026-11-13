using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct PartyTreasuryDonor
    {
        public PartyTreasuryDonor(string userId, string name, long amount)
        {
            UserId = userId;
            Name = name;
            Amount = amount;
        }

        public string UserId { get; }
        public string Name { get; }
        public long Amount { get; }
    }

    public enum PartyResetOutcome
    {
        None,
        Passed,
        Rejected,
        Cancelled,
        Bought,
        Chosen
    }

    public sealed class PartyResetVote
    {
        internal PartyResetVote(JsonValue json)
        {
            By = NakamaJson.Text(json, "by");
            Until = (long)NakamaJson.Number(json, "until");
            Yes = PartyTreasuryReply.Texts(NakamaJson.Items(json, "yes"));
            No = PartyTreasuryReply.Texts(NakamaJson.Items(json, "no"));
            Members = (int)NakamaJson.Number(json, "members");
        }

        public string By { get; }
        public long Until { get; }
        public IReadOnlyList<string> Yes { get; }
        public IReadOnlyList<string> No { get; }
        public int Members { get; }
    }

    public sealed class PartyTreasuryReply
    {
        private static readonly Dictionary<string, PartyResetOutcome> Outcomes = new Dictionary<string, PartyResetOutcome>
        {
            ["passed"] = PartyResetOutcome.Passed,
            ["rejected"] = PartyResetOutcome.Rejected,
            ["cancelled"] = PartyResetOutcome.Cancelled,
            ["bought"] = PartyResetOutcome.Bought,
            ["chosen"] = PartyResetOutcome.Chosen
        };

        private PartyTreasuryReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Leader = NakamaJson.Flag(json, "leader");
            Treasury = (long)NakamaJson.Number(json, "treasury");
            Yours = (long)NakamaJson.Number(json, "yours");
            Spent = (long)NakamaJson.Number(json, "spent");
            VotesLeft = (int)NakamaJson.Number(json, "votesLeft");
            ResetFee = (long)NakamaJson.Number(json, "resetFee");
            FreeResetAt = (long)NakamaJson.Number(json, "freeResetAt");
            Points = (int)NakamaJson.Number(json, "points");
            MajorChangeAt = (long)NakamaJson.Number(json, "majorChangeAt");
            Majors = Texts(NakamaJson.Items(json, "majors"));
            var levels = new Dictionary<int, int>();
            var votes = new Dictionary<int, IReadOnlyList<string>>();
            ReadNodes(NakamaJson.Field(json, "levels"), (node, value) => levels[node] = value.Kind == JsonKind.Number ? (int)value.Number : 0);
            ReadNodes(NakamaJson.Field(json, "votes"), (node, value) => votes[node] = Texts(value.Items));
            Levels = levels;
            Votes = votes;
            var donors = new List<PartyTreasuryDonor>();
            foreach (var donor in NakamaJson.Items(json, "donors"))
                donors.Add(new PartyTreasuryDonor(NakamaJson.Text(donor, "userId"), NakamaJson.Text(donor, "name"), (long)NakamaJson.Number(donor, "amount")));
            Donors = donors;
            var mine = new List<int>();
            foreach (var node in NakamaJson.Items(json, "mine"))
            {
                if (node.Kind == JsonKind.Number)
                    mine.Add((int)node.Number);
            }
            Mine = mine;
            var reset = NakamaJson.Field(json, "reset");
            Reset = reset != null && reset.Kind == JsonKind.Object ? new PartyResetVote(reset) : null;
            Outcome = Outcomes.TryGetValue(NakamaJson.Text(json, "outcome"), out var outcome) ? outcome : PartyResetOutcome.None;
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public bool Leader { get; }
        public long Treasury { get; }
        public long Yours { get; }
        public long Spent { get; }
        public int VotesLeft { get; }
        public long ResetFee { get; }
        public long FreeResetAt { get; }
        public int Points { get; }
        public long MajorChangeAt { get; }
        public IReadOnlyList<string> Majors { get; }
        public IReadOnlyDictionary<int, int> Levels { get; }
        public IReadOnlyDictionary<int, IReadOnlyList<string>> Votes { get; }
        public IReadOnlyList<PartyTreasuryDonor> Donors { get; }
        public IReadOnlyList<int> Mine { get; }
        public PartyResetVote? Reset { get; }
        public PartyResetOutcome Outcome { get; }

        public int VotesFor(int node) => Votes.TryGetValue(node, out var voters) ? voters.Count : 0;

        public static PartyTreasuryReply From(JsonValue json) => new PartyTreasuryReply(json);

        public static string DonatePayload(long amount) => NakamaJson.Object(("amount", amount.ToString(CultureInfo.InvariantCulture)));

        public static string VotePayload(int node, bool add) =>
            NakamaJson.Object(("node", node.ToString(CultureInfo.InvariantCulture)), ("add", add ? "true" : "false"));

        public static string BuyPayload(int node) => NakamaJson.Object(("node", node.ToString(CultureInfo.InvariantCulture)));

        public static string MajorPayload(string id) => NakamaJson.Object(("id", NakamaJson.Quote(id)));

        public static string ResetVotePayload(bool yes) => NakamaJson.Object(("yes", yes ? "true" : "false"));

        public static Task<PartyTreasuryReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_treasury", "{}", cancel);

        public static Task<PartyTreasuryReply> DonateAsync(NakamaClient client, NakamaSession session, long amount, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_treasury_donate", DonatePayload(amount), cancel);

        public static Task<PartyTreasuryReply> VoteAsync(NakamaClient client, NakamaSession session, int node, bool add, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_treasury_vote", VotePayload(node, add), cancel);

        public static Task<PartyTreasuryReply> BuyAsync(NakamaClient client, NakamaSession session, int node, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_tree_buy", BuyPayload(node), cancel);

        public static Task<PartyTreasuryReply> ChooseMajorAsync(NakamaClient client, NakamaSession session, string id, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_tree_major", MajorPayload(id), cancel);

        public static Task<PartyTreasuryReply> ProposeResetAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_treasury_reset_propose", "{}", cancel);

        public static Task<PartyTreasuryReply> VoteResetAsync(NakamaClient client, NakamaSession session, bool yes, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_treasury_reset_vote", ResetVotePayload(yes), cancel);

        public static Task<PartyTreasuryReply> CancelResetAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_treasury_reset_cancel", "{}", cancel);

        internal static IReadOnlyList<string> Texts(IReadOnlyList<JsonValue> items)
        {
            var texts = new List<string>(items.Count);
            foreach (var item in items)
            {
                if (item.Kind == JsonKind.String)
                    texts.Add(item.Text);
            }
            return texts;
        }

        private static void ReadNodes(JsonValue? field, System.Action<int, JsonValue> read)
        {
            if (field == null || field.Kind != JsonKind.Object)
                return;
            foreach (var pair in field.Fields)
            {
                if (int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var node))
                    read(node, pair.Value);
            }
        }

        private static async Task<PartyTreasuryReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
