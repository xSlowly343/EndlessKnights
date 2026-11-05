using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class WeeklyBoardView
    {
        private WeeklyBoardView(JsonValue json)
        {
            Week = (long)NakamaJson.Number(json, "week");
            var trials = new List<string>();
            foreach (var item in NakamaJson.Items(json, "trials"))
                trials.Add(item.Kind == JsonKind.String ? item.Text : string.Empty);
            Trials = trials;
            Trial = NakamaJson.Text(json, "trial");
            Top = Rows(json, "top");
            Friends = Rows(json, "friends");
            var mine = NakamaJson.Field(json, "mine");
            Mine = mine == null || mine.Kind == JsonKind.Null ? null : WeeklyRow.From(mine);
        }

        public long Week { get; }
        public IReadOnlyList<string> Trials { get; }
        public string Trial { get; }
        public IReadOnlyList<WeeklyRow> Top { get; }
        public IReadOnlyList<WeeklyRow> Friends { get; }
        public WeeklyRow? Mine { get; }

        public static WeeklyBoardView From(JsonValue json) => new WeeklyBoardView(json);

        public static string SubmitPayload(long week, string trial, long stage, int heroClass) => NakamaJson.Object(
            ("week", week.ToString(CultureInfo.InvariantCulture)),
            ("trial", NakamaJson.Quote(trial)),
            ("stage", stage.ToString(CultureInfo.InvariantCulture)),
            ("heroClass", heroClass.ToString(CultureInfo.InvariantCulture)));

        public static string BoardPayload(string trial, IReadOnlyList<string> friends)
        {
            var ids = new string[friends.Count];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = NakamaJson.Quote(friends[i]);
            return NakamaJson.Object(("trial", NakamaJson.Quote(trial)), ("friends", "[" + string.Join(",", ids) + "]"));
        }

        public static async Task<WeeklyRow> SubmitAsync(NakamaClient client, NakamaSession session, long week, string trial, long stage, int heroClass, CancellationToken cancel = default) =>
            WeeklyRow.From(NakamaJson.Parse(await client.RpcAsync(session, "weekly_submit", SubmitPayload(week, trial, stage, heroClass), cancel).ConfigureAwait(false)));

        public static async Task<WeeklyBoardView> FetchAsync(NakamaClient client, NakamaSession session, string trial, IReadOnlyList<string> friends, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "weekly_board", BoardPayload(trial, friends), cancel).ConfigureAwait(false)));

        private static IReadOnlyList<WeeklyRow> Rows(JsonValue json, string name)
        {
            var rows = new List<WeeklyRow>();
            foreach (var item in NakamaJson.Items(json, name))
                rows.Add(WeeklyRow.From(item));
            return rows;
        }
    }

    public sealed class WeeklyRow
    {
        private WeeklyRow(int rank, string userId, string name, long stage, int heroClass)
        {
            Rank = rank;
            UserId = userId;
            Name = name;
            Stage = stage;
            HeroClass = heroClass;
        }

        public int Rank { get; }
        public string UserId { get; }
        public string Name { get; }
        public long Stage { get; }
        public int HeroClass { get; }

        public static WeeklyRow From(JsonValue json) => new WeeklyRow((int)NakamaJson.Number(json, "rank"), NakamaJson.Text(json, "userId"),
            NakamaJson.Text(json, "name"), (long)NakamaJson.Number(json, "stage"), (int)NakamaJson.Number(json, "heroClass"));
    }
}
