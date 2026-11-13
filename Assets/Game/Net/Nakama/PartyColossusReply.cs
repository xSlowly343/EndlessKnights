using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct PartyColossusShare
    {
        public PartyColossusShare(string userId, string name, long points, double share)
        {
            UserId = userId;
            Name = name;
            Points = points;
            Share = share;
        }

        public string UserId { get; }
        public string Name { get; }
        public long Points { get; }
        public double Share { get; }
    }

    public sealed class PartyColossusAccepted
    {
        internal PartyColossusAccepted(JsonValue json)
        {
            Live = NakamaJson.Number(json, "live");
            Offline = NakamaJson.Number(json, "offline");
            var killed = new List<int>();
            foreach (var tier in NakamaJson.Items(json, "killed"))
            {
                if (tier.Kind == JsonKind.Number)
                    killed.Add((int)tier.Number);
            }
            Killed = killed;
        }

        public double Live { get; }
        public double Offline { get; }
        public IReadOnlyList<int> Killed { get; }
    }

    public sealed class PartyColossusReply
    {
        private PartyColossusReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Tier = (int)NakamaJson.Number(json, "tier");
            Hp = NakamaJson.Number(json, "hp");
            HpFull = NakamaJson.Number(json, "hpFull");
            Ceiling = (int)NakamaJson.Number(json, "ceiling");
            Waiting = NakamaJson.Flag(json, "waiting");
            Kills = (int)NakamaJson.Number(json, "kills");
            Yours = (long)NakamaJson.Number(json, "yours");
            ReportSeconds = NakamaJson.Number(json, "reportSeconds");
            var shares = new List<PartyColossusShare>();
            foreach (var share in NakamaJson.Items(json, "shares"))
                shares.Add(new PartyColossusShare(NakamaJson.Text(share, "userId"), NakamaJson.Text(share, "name"),
                    (long)NakamaJson.Number(share, "points"), NakamaJson.Number(share, "share")));
            Shares = shares;
            var accepted = NakamaJson.Field(json, "accepted");
            Accepted = accepted != null && accepted.Kind == JsonKind.Object ? new PartyColossusAccepted(accepted) : null;
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public int Tier { get; }
        public double Hp { get; }
        public double HpFull { get; }
        public int Ceiling { get; }
        public bool Waiting { get; }
        public int Kills { get; }
        public long Yours { get; }
        public double ReportSeconds { get; }
        public IReadOnlyList<PartyColossusShare> Shares { get; }
        public PartyColossusAccepted? Accepted { get; }
        public double HpShare => HpFull > 0 ? System.Math.Max(0, System.Math.Min(1, Hp / HpFull)) : 0;

        public static PartyColossusReply From(JsonValue json) => new PartyColossusReply(json);

        public static string ReportPayload(double points, double offlinePoints, double offlineSeconds) =>
            NakamaJson.Object(("points", Number(points)), ("offlinePoints", Number(offlinePoints)), ("offlineSeconds", Number(offlineSeconds)));

        public static Task<PartyColossusReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_colossus", "{}", cancel);

        public static Task<PartyColossusReply> ReportAsync(NakamaClient client, NakamaSession session, double points, double offlinePoints, double offlineSeconds, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_colossus_report", ReportPayload(points, offlinePoints, offlineSeconds), cancel);

        private static string Number(double value) => System.Math.Max(0, System.Math.Round(value, 3)).ToString("0.###", CultureInfo.InvariantCulture);

        private static async Task<PartyColossusReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
