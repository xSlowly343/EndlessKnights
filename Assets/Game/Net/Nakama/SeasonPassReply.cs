using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class SeasonPassReply
    {
        private SeasonPassReply(JsonValue json)
        {
            Season = (int)NakamaJson.Number(json, "season");
            Id = NakamaJson.Text(json, "id");
            Points = (long)NakamaJson.Number(json, "points");
            Level = (int)NakamaJson.Number(json, "level");
            Premium = NakamaJson.Flag(json, "premium");
            Today = (long)NakamaJson.Number(json, "today");
            DailyCap = (long)NakamaJson.Number(json, "dailyCap");
            EndsAtUnixMs = (long)NakamaJson.Number(json, "endsAt");
            Accepted = (long)NakamaJson.Number(json, "accepted");
            Fresh = CosmeticsMineView.Ids(json, "fresh");
            FreshCrystals = (long)NakamaJson.Number(json, "freshCrystals");
            Crystals = (long)NakamaJson.Number(json, "crystals");
        }

        public int Season { get; }
        public string Id { get; }
        public long Points { get; }
        public int Level { get; }
        public bool Premium { get; }
        public long Today { get; }
        public long DailyCap { get; }
        public long EndsAtUnixMs { get; }
        public long Accepted { get; }
        public IReadOnlyList<string> Fresh { get; }
        public long FreshCrystals { get; }
        public long Crystals { get; }

        public static SeasonPassReply From(JsonValue json) => new SeasonPassReply(json);

        public static string AddPayload(long points) => NakamaJson.Object(("points", points.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public static async Task<SeasonPassReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "season_pass_mine", "{}", cancel).ConfigureAwait(false)));

        public static async Task<SeasonPassReply> AddAsync(NakamaClient client, NakamaSession session, long points, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "season_pass_add", AddPayload(points), cancel).ConfigureAwait(false)));

        public static async Task<SeasonPassReply> PremiumAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "season_pass_premium", "{}", cancel).ConfigureAwait(false)));
    }

    public enum CrystalChangeReason
    {
        Subclass,
        Class
    }

    public static class CrystalChange
    {
        public static string Payload(CrystalChangeReason reason) =>
            NakamaJson.Object(("reason", NakamaJson.Quote(reason == CrystalChangeReason.Class ? "class_change" : "subclass_change")));

        public static async Task<long> SpendAsync(NakamaClient client, NakamaSession session, CrystalChangeReason reason, CancellationToken cancel = default) =>
            (long)NakamaJson.Number(NakamaJson.Parse(await client.RpcAsync(session, "crystal_change", Payload(reason), cancel).ConfigureAwait(false)), "crystals");
    }
}
