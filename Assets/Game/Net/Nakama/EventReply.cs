using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class EventReply
    {
        private EventReply(JsonValue json)
        {
            Window = NakamaJson.Text(json, "window");
            ShopWindow = NakamaJson.Text(json, "shopWindow");
            EndsDay = (long)NakamaJson.Number(json, "endsDay");
            ShopEndsDay = (long)NakamaJson.Number(json, "shopEndsDay");
            Tokens = (long)NakamaJson.Number(json, "tokens");
            Today = (long)NakamaJson.Number(json, "today");
            DailyCap = (long)NakamaJson.Number(json, "dailyCap");
            Goals = CosmeticsMineView.Ids(json, "goals");
            Owned = CosmeticsMineView.Ids(json, "owned");
            Accepted = (long)NakamaJson.Number(json, "accepted");
            FreshGoals = CosmeticsMineView.Ids(json, "freshGoals");
            FreshCrystals = (long)NakamaJson.Number(json, "freshCrystals");
            Fresh = CosmeticsMineView.Ids(json, "fresh");
        }

        public string Window { get; }
        public string ShopWindow { get; }
        public long EndsDay { get; }
        public long ShopEndsDay { get; }
        public long Tokens { get; }
        public long Today { get; }
        public long DailyCap { get; }
        public IReadOnlyList<string> Goals { get; }
        public IReadOnlyList<string> Owned { get; }
        public long Accepted { get; }
        public IReadOnlyList<string> FreshGoals { get; }
        public long FreshCrystals { get; }
        public IReadOnlyList<string> Fresh { get; }

        public static EventReply From(JsonValue json) => new EventReply(json);

        public static string ProgressPayload(string window, long tokens, long guests, IReadOnlyList<string> goals, long score)
        {
            var quoted = new string[goals.Count];
            for (var i = 0; i < quoted.Length; i++)
                quoted[i] = NakamaJson.Quote(goals[i]);
            return NakamaJson.Object(
                ("window", NakamaJson.Quote(window)),
                ("tokens", tokens.ToString(CultureInfo.InvariantCulture)),
                ("guests", guests.ToString(CultureInfo.InvariantCulture)),
                ("goals", "[" + string.Join(",", quoted) + "]"),
                ("score", score.ToString(CultureInfo.InvariantCulture)));
        }

        public static string BuyPayload(string cosmetic) => NakamaJson.Object(("cosmetic", NakamaJson.Quote(cosmetic)));

        public static async Task<EventReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "event_mine", "{}", cancel).ConfigureAwait(false)));

        public static async Task<EventReply> ProgressAsync(NakamaClient client, NakamaSession session, string window, long tokens, long guests, IReadOnlyList<string> goals, long score, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "event_progress", ProgressPayload(window, tokens, guests, goals, score), cancel).ConfigureAwait(false)));

        public static async Task<EventReply> BuyAsync(NakamaClient client, NakamaSession session, string cosmetic, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "event_buy", BuyPayload(cosmetic), cancel).ConfigureAwait(false)));
    }

    public sealed class EventBoardRow
    {
        public EventBoardRow(int rank, string userId, string name, long score)
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

    public sealed class EventBoardView
    {
        private EventBoardView(JsonValue json)
        {
            Window = NakamaJson.Text(json, "window");
            var top = new List<EventBoardRow>();
            foreach (var item in NakamaJson.Items(json, "top"))
                top.Add(Row(item));
            Top = top;
            var mine = NakamaJson.Field(json, "mine");
            Mine = mine == null || mine.Kind != JsonKind.Object ? null : Row(mine);
        }

        public string Window { get; }
        public IReadOnlyList<EventBoardRow> Top { get; }
        public EventBoardRow? Mine { get; }

        public static EventBoardView From(JsonValue json) => new EventBoardView(json);

        public static async Task<EventBoardView> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "event_board", "{}", cancel).ConfigureAwait(false)));

        private static EventBoardRow Row(JsonValue item) =>
            new EventBoardRow((int)NakamaJson.Number(item, "rank"), NakamaJson.Text(item, "userId"), NakamaJson.Text(item, "name"), (long)NakamaJson.Number(item, "score"));
    }
}
