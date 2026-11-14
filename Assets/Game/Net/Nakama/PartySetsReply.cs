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

    public readonly struct PartySetPieceDrop
    {
        public PartySetPieceDrop(string set, string piece)
        {
            Set = set;
            Piece = piece;
        }

        public string Set { get; }
        public string Piece { get; }
    }

    public sealed class PartySetDropsReply
    {
        private PartySetDropsReply(JsonValue json)
        {
            var drops = new List<PartySetPieceDrop>();
            foreach (var drop in NakamaJson.Items(json, "drops"))
                drops.Add(new PartySetPieceDrop(NakamaJson.Text(drop, "set"), NakamaJson.Text(drop, "piece")));
            Drops = drops;
            Left = (int)NakamaJson.Number(json, "left");
        }

        public IReadOnlyList<PartySetPieceDrop> Drops { get; }
        public int Left { get; }

        public static PartySetDropsReply From(JsonValue json) => new PartySetDropsReply(json);

        public static string ClaimPayload(int room) => NakamaJson.Object(("room", room.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public static async Task<PartySetDropsReply> ClaimAsync(NakamaClient client, NakamaSession session, int room, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "party_set_drops", ClaimPayload(room), cancel).ConfigureAwait(false)));
    }

    public sealed class PartySetCraftReply
    {
        private PartySetCraftReply(JsonValue json)
        {
            Piece = new PartySetPieceDrop(NakamaJson.Text(json, "set"), NakamaJson.Text(json, "piece"));
            Cost = (long)NakamaJson.Number(json, "cost");
            Oath = (long)NakamaJson.Number(json, "oath");
        }

        public PartySetPieceDrop Piece { get; }
        public long Cost { get; }
        public long Oath { get; }

        public static PartySetCraftReply From(JsonValue json) => new PartySetCraftReply(json);

        public static string CraftPayload(string set, string piece) => NakamaJson.Object(("set", NakamaJson.Quote(set)), ("piece", NakamaJson.Quote(piece)));

        public static async Task<PartySetCraftReply> CraftAsync(NakamaClient client, NakamaSession session, string set, string piece, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "party_set_craft", CraftPayload(set, piece), cancel).ConfigureAwait(false)));
    }

    public sealed class PartySetsReply
    {
        private PartySetsReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Yours = PartyTreasuryReply.Texts(NakamaJson.Items(json, "yours"));
            Refused = PartyTreasuryReply.Texts(NakamaJson.Items(json, "refused"));
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
        public IReadOnlyList<string> Refused { get; }
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
