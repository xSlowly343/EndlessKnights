using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ClanDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public ClanDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<ClanView?> MineAsync(CancellationToken cancel = default) =>
            ClanView.From(NakamaJson.Field(await Call("clan_mine", "{}", cancel).ConfigureAwait(false), "clan"));

        public async Task<ClanView> CreateAsync(string? access, string? adjective, string? noun, string? language, CancellationToken cancel = default) =>
            View(await Call("clan_create", Card(access, adjective, noun, language), cancel).ConfigureAwait(false));

        public async Task<ClanView> JoinByCodeAsync(string code, CancellationToken cancel = default) =>
            View(await Call("clan_join", NakamaJson.Object(("code", NakamaJson.Quote(code))), cancel).ConfigureAwait(false));

        public async Task<ClanView> JoinOpenAsync(string clanId, CancellationToken cancel = default) =>
            View(await Call("clan_join", NakamaJson.Object(("clanId", NakamaJson.Quote(clanId))), cancel).ConfigureAwait(false));

        public Task LeaveAsync(CancellationToken cancel = default) => Call("clan_leave", "{}", cancel);

        public Task<ClanView> KickAsync(string userId, CancellationToken cancel = default) => OnMember("clan_kick", userId, cancel);

        public Task<ClanView> PromoteAsync(string userId, CancellationToken cancel = default) => OnMember("clan_promote", userId, cancel);

        public Task<ClanView> DemoteAsync(string userId, CancellationToken cancel = default) => OnMember("clan_demote", userId, cancel);

        public Task<ClanView> HandOverAsync(string userId, CancellationToken cancel = default) => OnMember("clan_lead", userId, cancel);

        public async Task<ClanView> UpdateAsync(string? access, string? adjective, string? noun, string? language, CancellationToken cancel = default) =>
            View(await Call("clan_update", Card(access, adjective, noun, language), cancel).ConfigureAwait(false));

        public async Task<IReadOnlyList<ClanCard>> ListAsync(string? language, CancellationToken cancel = default, bool byRequest = false)
        {
            var fields = new List<(string, string)>();
            if (language != null)
                fields.Add(("language", NakamaJson.Quote(language)));
            if (byRequest)
                fields.Add(("closed", "true"));
            var reply = await Call("clan_list", NakamaJson.Object(fields.ToArray()), cancel).ConfigureAwait(false);
            var cards = new List<ClanCard>();
            foreach (var card in NakamaJson.Items(reply, "clans"))
                cards.Add(new ClanCard(card));
            return cards;
        }

        public async Task<ClanView?> QuickAsync(string language, CancellationToken cancel = default) =>
            ClanView.From(NakamaJson.Field(await Call("clan_quick", NakamaJson.Object(("language", NakamaJson.Quote(language))), cancel).ConfigureAwait(false), "clan"));

        public async Task<ClanTreeView> TreeAsync(CancellationToken cancel = default) =>
            ClanTreeView.From(await Call("clan_tree", "{}", cancel).ConfigureAwait(false));

        public async Task<ClanTreeView> DonateAsync(long seals, CancellationToken cancel = default) =>
            ClanTreeView.From(await Call("clan_donate", ClanTreeView.DonatePayload(seals), cancel).ConfigureAwait(false));

        public async Task<ClanTreeView> OpenNodeAsync(string nodeId, CancellationToken cancel = default) =>
            ClanTreeView.From(await Call("clan_tree_buy", ClanTreeView.BuyPayload(nodeId), cancel).ConfigureAwait(false));

        public Task ApplyAsync(string clanId, CancellationToken cancel = default) =>
            Call("clan_apply", NakamaJson.Object(("clanId", NakamaJson.Quote(clanId))), cancel);

        public async Task<ClanRequestsView> RequestsAsync(CancellationToken cancel = default) =>
            ClanRequestsView.From(await Call("clan_requests", "{}", cancel).ConfigureAwait(false));

        public async Task<ClanRequestsView> AnswerAsync(string userId, bool accept, CancellationToken cancel = default) =>
            ClanRequestsView.From(await Call("clan_answer", NakamaJson.Object(("userId", NakamaJson.Quote(userId)), ("accept", accept ? "true" : "false")), cancel).ConfigureAwait(false));

        public async Task<IReadOnlyList<ClanApplicationRow>> ApplicationsAsync(CancellationToken cancel = default) =>
            Applications(await Call("clan_applications", "{}", cancel).ConfigureAwait(false));

        public async Task<IReadOnlyList<ClanApplicationRow>> WithdrawAsync(string clanId, CancellationToken cancel = default) =>
            Applications(await Call("clan_withdraw", NakamaJson.Object(("clanId", NakamaJson.Quote(clanId))), cancel).ConfigureAwait(false));

        private static IReadOnlyList<ClanApplicationRow> Applications(JsonValue reply)
        {
            var rows = new List<ClanApplicationRow>();
            foreach (var row in NakamaJson.Items(reply, "clans"))
                rows.Add(new ClanApplicationRow(row));
            return rows;
        }

        private async Task<ClanView> OnMember(string rpc, string userId, CancellationToken cancel) =>
            View(await Call(rpc, NakamaJson.Object(("userId", NakamaJson.Quote(userId))), cancel).ConfigureAwait(false));

        private async Task<JsonValue> Call(string rpc, string payload, CancellationToken cancel) =>
            NakamaJson.Parse(await _client.RpcAsync(_session, rpc, payload, cancel).ConfigureAwait(false));

        private static ClanView View(JsonValue json) => ClanView.From(json) ?? throw new NakamaException(0, "the server sent no clan");

        private static string Card(string? access, string? adjective, string? noun, string? language)
        {
            var fields = new List<(string, string)>();
            if (access != null)
                fields.Add(("access", NakamaJson.Quote(access)));
            if (adjective != null)
                fields.Add(("adjective", NakamaJson.Quote(adjective)));
            if (noun != null)
                fields.Add(("noun", NakamaJson.Quote(noun)));
            if (language != null)
                fields.Add(("language", NakamaJson.Quote(language)));
            return NakamaJson.Object(fields.ToArray());
        }
    }
}
