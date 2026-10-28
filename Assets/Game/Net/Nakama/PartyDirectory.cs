using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class PartyDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public PartyDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<PartyView?> MineAsync(CancellationToken cancel = default) =>
            PartyView.From(NakamaJson.Field(await Call("party_mine", "{}", cancel).ConfigureAwait(false), "party"));

        public async Task<PartyView> CreateAsync(string access, string? title, string? language, string? wanted, CancellationToken cancel = default) =>
            View(await Call("party_create", Card(access, title, language, wanted), cancel).ConfigureAwait(false));

        public async Task<PartyView> JoinByCodeAsync(string code, CancellationToken cancel = default) =>
            View(await Call("party_join", NakamaJson.Object(("code", NakamaJson.Quote(code))), cancel).ConfigureAwait(false));

        public async Task<PartyView> JoinOpenAsync(string partyId, CancellationToken cancel = default) =>
            View(await Call("party_join", NakamaJson.Object(("partyId", NakamaJson.Quote(partyId))), cancel).ConfigureAwait(false));

        public Task LeaveAsync(CancellationToken cancel = default) => Call("party_leave", "{}", cancel);

        public async Task<PartyView> KickAsync(string userId, CancellationToken cancel = default) =>
            View(await Call("party_kick", NakamaJson.Object(("userId", NakamaJson.Quote(userId))), cancel).ConfigureAwait(false));

        public async Task<PartyView> PromoteAsync(string userId, CancellationToken cancel = default) =>
            View(await Call("party_promote", NakamaJson.Object(("userId", NakamaJson.Quote(userId))), cancel).ConfigureAwait(false));

        public async Task<PartyView> UpdateAsync(string? access, string? title, string? language, string? wanted, CancellationToken cancel = default) =>
            View(await Call("party_update", Card(access, title, language, wanted), cancel).ConfigureAwait(false));

        public async Task<string> ConnectAsync(CancellationToken cancel = default) =>
            NakamaJson.Text(await Call("party_connect", "{}", cancel).ConfigureAwait(false), "matchId");

        public async Task<IReadOnlyList<PartyCard>> ListAsync(string? language, string role, string? wanted, int utcOffsetMinutes, CancellationToken cancel = default) =>
            await ListAsync(language, role, wanted, null, utcOffsetMinutes, cancel).ConfigureAwait(false);

        public async Task<IReadOnlyList<PartyCard>> ListAsync(string? language, string role, string? wanted, string? slot, int utcOffsetMinutes, CancellationToken cancel = default)
        {
            var fields = new List<(string, string)> { ("role", NakamaJson.Quote(role)), ("utcOffsetMinutes", Number(utcOffsetMinutes)) };
            if (slot != null)
                fields.Add(("slot", NakamaJson.Quote(slot)));
            if (language != null)
                fields.Add(("language", NakamaJson.Quote(language)));
            if (wanted != null)
                fields.Add(("wanted", NakamaJson.Quote(wanted)));
            var reply = await Call("party_list", NakamaJson.Object(fields.ToArray()), cancel).ConfigureAwait(false);
            var cards = new List<PartyCard>();
            foreach (var card in NakamaJson.Items(reply, "parties"))
                cards.Add(new PartyCard(card));
            return cards;
        }

        public async Task<PartyView> QuickAsync(string language, string role, int utcOffsetMinutes, CancellationToken cancel = default) =>
            View(await Call("party_quick", NakamaJson.Object(("language", NakamaJson.Quote(language)), ("role", NakamaJson.Quote(role)), ("utcOffsetMinutes", Number(utcOffsetMinutes))), cancel)
                .ConfigureAwait(false));

        public Task BlockAsync(string userId, CancellationToken cancel = default) => Call("party_block", NakamaJson.Object(("userId", NakamaJson.Quote(userId))), cancel);

        public Task UnblockAsync(string userId, CancellationToken cancel = default) => Call("party_unblock", NakamaJson.Object(("userId", NakamaJson.Quote(userId))), cancel);

        public Task ReportAsync(string userId, string reason, CancellationToken cancel = default) =>
            Call("party_report", NakamaJson.Object(("userId", NakamaJson.Quote(userId)), ("reason", NakamaJson.Quote(reason))), cancel);

        public async Task<PartyChronicleView> ChronicleAsync(CancellationToken cancel = default) =>
            PartyChronicleView.From(await Call("party_chronicle", "{}", cancel).ConfigureAwait(false)) ?? throw new NakamaException(0, "the server sent no chronicle");

        public async Task<RiftBoardView> RiftBoardAsync(long riftId, CancellationToken cancel = default) =>
            RiftBoardView.From(await Call("rift_board", NakamaJson.Object(("riftId", riftId.ToString(CultureInfo.InvariantCulture))), cancel).ConfigureAwait(false))
            ?? throw new NakamaException(0, "the server sent no board");

        private async Task<JsonValue> Call(string rpc, string payload, CancellationToken cancel) =>
            NakamaJson.Parse(await _client.RpcAsync(_session, rpc, payload, cancel).ConfigureAwait(false));

        private static PartyView View(JsonValue json) => PartyView.From(json) ?? throw new NakamaException(0, "the server sent no party");

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Card(string? access, string? title, string? language, string? wanted)
        {
            var fields = new List<(string, string)>();
            if (access != null)
                fields.Add(("access", NakamaJson.Quote(access)));
            if (title != null)
                fields.Add(("title", NakamaJson.Quote(title)));
            if (language != null)
                fields.Add(("language", NakamaJson.Quote(language)));
            if (wanted != null)
                fields.Add(("wanted", NakamaJson.Quote(wanted)));
            return NakamaJson.Object(fields.ToArray());
        }
    }
}
