using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class HelpInvite
    {
        public HelpInvite(string from, string name, long untilUnixMs)
        {
            From = from;
            Name = name;
            UntilUnixMs = untilUnixMs;
        }

        public string From { get; }
        public string Name { get; }
        public long UntilUnixMs { get; }
    }

    public readonly struct HelpAnswer
    {
        public HelpAnswer(string matchId, double seconds)
        {
            MatchId = matchId;
            Seconds = seconds;
        }

        public string MatchId { get; }
        public double Seconds { get; }
    }

    public sealed class HelpDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public HelpDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<string> AcceptAsync(string? accept = null, CancellationToken cancel = default)
        {
            var payload = accept == null ? "{}" : NakamaJson.Object(("accept", NakamaJson.Quote(accept)));
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "help_settings", payload, cancel).ConfigureAwait(false));
            return NakamaJson.Text(reply, "accept");
        }

        public async Task<long> CallAsync(string userId, CancellationToken cancel = default)
        {
            var reply = NakamaJson.Parse(await _client.RpcAsync(_session, "help_call", NakamaJson.Object(("userId", NakamaJson.Quote(userId))), cancel).ConfigureAwait(false));
            return (long)NakamaJson.Number(reply, "until");
        }

        public async Task<IReadOnlyList<HelpInvite>> InvitesAsync(CancellationToken cancel = default) =>
            Invites(NakamaJson.Parse(await _client.RpcAsync(_session, "help_invites", "{}", cancel).ConfigureAwait(false)));

        public async Task<HelpAnswer> AnswerAsync(string from, CancellationToken cancel = default) =>
            Answer(NakamaJson.Parse(await _client.RpcAsync(_session, "help_answer", NakamaJson.Object(("from", NakamaJson.Quote(from))), cancel).ConfigureAwait(false)));

        public static IReadOnlyList<HelpInvite> Invites(JsonValue reply)
        {
            var invites = new List<HelpInvite>();
            foreach (var invite in NakamaJson.Items(reply, "invites"))
                invites.Add(new HelpInvite(NakamaJson.Text(invite, "from"), NakamaJson.Text(invite, "name"), (long)NakamaJson.Number(invite, "until")));
            return invites;
        }

        public static HelpAnswer Answer(JsonValue reply)
        {
            var matchId = NakamaJson.Text(reply, "matchId");
            return matchId.Length > 0 ? new HelpAnswer(matchId, NakamaJson.Number(reply, "seconds")) : throw new NakamaException(0, "the server sent no match");
        }
    }
}
