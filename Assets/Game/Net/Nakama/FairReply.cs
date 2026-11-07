using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct FairRow
    {
        public FairRow(int rank, string name, double score)
        {
            Rank = rank;
            Name = name;
            Score = score;
        }

        public int Rank { get; }
        public string Name { get; }
        public double Score { get; }
    }

    public sealed class FairContestReply
    {
        internal FairContestReply(JsonValue json)
        {
            Id = NakamaJson.Text(json, "id");
            Entrants = (int)NakamaJson.Number(json, "entrants");
            var top = new List<FairRow>();
            foreach (var row in NakamaJson.Items(json, "top"))
                top.Add(new FairRow((int)NakamaJson.Number(row, "rank"), NakamaJson.Text(row, "name"), NakamaJson.Number(row, "score")));
            Top = top;
            var mine = NakamaJson.Field(json, "mine");
            if (mine != null && mine.Kind == JsonKind.Object)
            {
                MyRank = (int)NakamaJson.Number(mine, "rank");
                MyScore = NakamaJson.Number(mine, "score");
            }
        }

        public string Id { get; }
        public int Entrants { get; }
        public IReadOnlyList<FairRow> Top { get; }
        public int MyRank { get; }
        public double MyScore { get; }
    }

    public sealed class FairReply
    {
        private FairReply(JsonValue json)
        {
            Week = (long)NakamaJson.Number(json, "week");
            EndsAtUnixMs = (long)NakamaJson.Number(json, "endsAt");
            var contests = new List<FairContestReply>();
            foreach (var contest in NakamaJson.Items(json, "contests"))
                contests.Add(new FairContestReply(contest));
            Contests = contests;
            var fish = NakamaJson.Field(json, "fish");
            if (fish != null && fish.Kind == JsonKind.Object)
            {
                BestFish = NakamaJson.Text(fish, "fish");
                BestCm = NakamaJson.Number(fish, "cm");
            }
        }

        public long Week { get; }
        public long EndsAtUnixMs { get; }
        public IReadOnlyList<FairContestReply> Contests { get; }
        public string BestFish { get; } = string.Empty;
        public double BestCm { get; }

        public FairContestReply? Contest(string id)
        {
            foreach (var contest in Contests)
            {
                if (contest.Id == id)
                    return contest;
            }
            return null;
        }

        public static FairReply From(JsonValue json) => new FairReply(json);

        public static string FishPayload(string fish, double cm) =>
            NakamaJson.Object(("fish", NakamaJson.Quote(fish)), ("cm", cm.ToString("0.0", CultureInfo.InvariantCulture)));

        public static string GardenPayload(long harvests, long mutations) =>
            NakamaJson.Object(("harvests", harvests.ToString(CultureInfo.InvariantCulture)), ("mutations", mutations.ToString(CultureInfo.InvariantCulture)));

        public static async Task<FairReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "fair_view", "{}", cancel).ConfigureAwait(false)));

        public static async Task<FairReply> FishAsync(NakamaClient client, NakamaSession session, string fish, double cm, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "fair_fish", FishPayload(fish, cm), cancel).ConfigureAwait(false)));

        public static async Task<FairReply> GardenAsync(NakamaClient client, NakamaSession session, long harvests, long mutations, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "fair_garden", GardenPayload(harvests, mutations), cancel).ConfigureAwait(false)));
    }
}
