using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct TournamentRow
    {
        public TournamentRow(int rank, string userId, string name, long score)
        {
            Rank = rank;
            UserId = userId;
            Name = name;
            Score = score;
        }

        public int Rank { get; }
        public string UserId { get; }
        public string Name { get; }
        public long Score { get; }
    }

    public sealed class TournamentReply
    {
        private TournamentReply(JsonValue json)
        {
            Index = (long)NakamaJson.Number(json, "index");
            Format = NakamaJson.Text(json, "format");
            StartsAtUnixMs = (long)NakamaJson.Number(json, "startsAt");
            EndsAtUnixMs = (long)NakamaJson.Number(json, "endsAt");
            JoinUntilUnixMs = (long)NakamaJson.Number(json, "joinUntil");
            Joined = NakamaJson.Flag(json, "joined");
            Band = (int)NakamaJson.Number(json, "band");
            Group = (int)NakamaJson.Number(json, "group");
            var top = new List<TournamentRow>();
            foreach (var row in NakamaJson.Items(json, "top"))
                top.Add(Row(row));
            Top = top;
            var mine = NakamaJson.Field(json, "mine");
            Mine = mine != null && mine.Kind == JsonKind.Object ? Row(mine) : (TournamentRow?)null;
            Fresh = CosmeticsMineView.Ids(json, "fresh");
        }

        public long Index { get; }
        public string Format { get; }
        public long StartsAtUnixMs { get; }
        public long EndsAtUnixMs { get; }
        public long JoinUntilUnixMs { get; }
        public bool Joined { get; }
        public int Band { get; }
        public int Group { get; }
        public IReadOnlyList<TournamentRow> Top { get; }
        public TournamentRow? Mine { get; }
        public IReadOnlyList<string> Fresh { get; }

        public bool JoinOpen(long nowUnixMs) => nowUnixMs < JoinUntilUnixMs;

        public static TournamentReply From(JsonValue json) => new TournamentReply(json);

        public static string JoinPayload(long recordStage) => NakamaJson.Object(("record", recordStage.ToString(CultureInfo.InvariantCulture)));

        public static string SubmitPayload(long index, long score) =>
            NakamaJson.Object(("index", index.ToString(CultureInfo.InvariantCulture)), ("score", score.ToString(CultureInfo.InvariantCulture)));

        public static async Task<TournamentReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "tournament_mine", "{}", cancel).ConfigureAwait(false)));

        public static async Task<TournamentReply> JoinAsync(NakamaClient client, NakamaSession session, long recordStage, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "tournament_join", JoinPayload(recordStage), cancel).ConfigureAwait(false)));

        public static async Task<TournamentReply> SubmitAsync(NakamaClient client, NakamaSession session, long index, long score, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "tournament_submit", SubmitPayload(index, score), cancel).ConfigureAwait(false)));

        private static TournamentRow Row(JsonValue json) =>
            new TournamentRow((int)NakamaJson.Number(json, "rank"), NakamaJson.Text(json, "userId"), NakamaJson.Text(json, "name"), (long)NakamaJson.Number(json, "score"));
    }
}
