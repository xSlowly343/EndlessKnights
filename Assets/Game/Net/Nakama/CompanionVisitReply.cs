using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class VisitingGuest
    {
        public VisitingGuest(string fromName, string companion, string name, int stage, long endsAtUnixMs)
        {
            FromName = fromName;
            Companion = companion;
            Name = name;
            Stage = stage;
            EndsAtUnixMs = endsAtUnixMs;
        }

        public string FromName { get; }
        public string Companion { get; }
        public string Name { get; }
        public int Stage { get; }
        public long EndsAtUnixMs { get; }
    }

    public sealed class CompanionVisitReply
    {
        private CompanionVisitReply(JsonValue json)
        {
            var away = NakamaJson.Field(json, "away");
            if (away != null && away.Kind == JsonKind.Object)
            {
                AwayHost = NakamaJson.Text(away, "hostName");
                AwayCompanion = NakamaJson.Text(away, "companion");
                AwayEndsAtUnixMs = (long)NakamaJson.Number(away, "endsAt");
                Ready = NakamaJson.Flag(away, "ready");
            }
            VisitsLeft = (int)NakamaJson.Number(json, "visitsLeft");
            NowUnixMs = (long)NakamaJson.Number(json, "now");
            var guests = new List<VisitingGuest>();
            foreach (var item in NakamaJson.Items(json, "guests"))
                guests.Add(new VisitingGuest(NakamaJson.Text(item, "fromName"), NakamaJson.Text(item, "companion"), NakamaJson.Text(item, "name"),
                    (int)NakamaJson.Number(item, "stage"), (long)NakamaJson.Number(item, "endsAt")));
            Guests = guests;
            Gifts = HideoutReply.Amounts(NakamaJson.Field(json, "gifts"));
            BackCompanion = NakamaJson.Text(json, "companion");
        }

        public string AwayHost { get; } = string.Empty;
        public string AwayCompanion { get; } = string.Empty;
        public long AwayEndsAtUnixMs { get; }
        public bool Ready { get; }
        public bool Away => AwayCompanion.Length > 0;
        public int VisitsLeft { get; }
        public long NowUnixMs { get; }
        public IReadOnlyList<VisitingGuest> Guests { get; }
        public IReadOnlyDictionary<string, long> Gifts { get; }
        public string BackCompanion { get; }

        public static CompanionVisitReply From(JsonValue json) => new CompanionVisitReply(json);

        public static string SendPayload(string host, string companion, string name, int stage) =>
            NakamaJson.Object(("host", NakamaJson.Quote(host)), ("companion", NakamaJson.Quote(companion)), ("name", NakamaJson.Quote(name)),
                ("stage", stage.ToString(CultureInfo.InvariantCulture)));

        public static async Task<CompanionVisitReply> SendAsync(NakamaClient client, NakamaSession session, string host, string companion, string name, int stage, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "companion_visit_send", SendPayload(host, companion, name, stage), cancel).ConfigureAwait(false)));

        public static async Task<CompanionVisitReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "companion_visit", string.Empty, cancel).ConfigureAwait(false)));

        public static async Task<CompanionVisitReply> ClaimAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "companion_visit_claim", "{}", cancel).ConfigureAwait(false)));
    }
}
