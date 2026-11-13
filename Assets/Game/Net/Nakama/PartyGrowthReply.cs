using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class PartyGrowthReply
    {
        private PartyGrowthReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Experience = (long)NakamaJson.Number(json, "experience");
            Level = (int)NakamaJson.Number(json, "level");
            Into = (long)NakamaJson.Number(json, "into");
            Need = (long)NakamaJson.Number(json, "need");
        }

        public string PartyId { get; }
        public long Experience { get; }
        public int Level { get; }
        public long Into { get; }
        public long Need { get; }

        public static PartyGrowthReply From(JsonValue json) => new PartyGrowthReply(json);

        public static string ReportPayload(long activeSeconds) => NakamaJson.Object(("activeSeconds", activeSeconds.ToString(CultureInfo.InvariantCulture)));

        public static async Task<PartyGrowthReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "party_growth", "{}", cancel).ConfigureAwait(false)));

        public static async Task<PartyGrowthReply> ReportAsync(NakamaClient client, NakamaSession session, long activeSeconds, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "party_growth_report", ReportPayload(activeSeconds), cancel).ConfigureAwait(false)));
    }
}
