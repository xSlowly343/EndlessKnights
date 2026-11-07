using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class TownBuildReply
    {
        private TownBuildReply(JsonValue json)
        {
            var built = new List<string>(CosmeticsMineView.Ids(json, "startBuilt"));
            built.AddRange(CosmeticsMineView.Ids(json, "built"));
            Built = built;
            Project = NakamaJson.Text(json, "project");
            Progress = (long)NakamaJson.Number(json, "progress");
            Target = (long)NakamaJson.Number(json, "target");
            Accepted = (long)NakamaJson.Number(json, "accepted");
            SentToday = (long)NakamaJson.Number(json, "sentToday");
            DailyCap = (long)NakamaJson.Number(json, "dailyCap");
        }

        public IReadOnlyList<string> Built { get; }
        public string Project { get; }
        public long Progress { get; }
        public long Target { get; }
        public long Accepted { get; }
        public long SentToday { get; }
        public long DailyCap { get; }
        public long LeftToday => System.Math.Max(0, DailyCap - SentToday);

        public static TownBuildReply From(JsonValue json) => new TownBuildReply(json);

        public static string AddPayload(long actions) => NakamaJson.Object(("actions", actions.ToString(CultureInfo.InvariantCulture)));

        public static async Task<TownBuildReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_build", "{}", cancel).ConfigureAwait(false)));

        public static async Task<TownBuildReply> AddAsync(NakamaClient client, NakamaSession session, long actions, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_build_add", AddPayload(actions), cancel).ConfigureAwait(false)));
    }
}
