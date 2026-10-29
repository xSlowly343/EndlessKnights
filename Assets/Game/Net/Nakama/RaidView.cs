using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Party;

namespace Game.Net.Nakama
{
    public sealed class RaidView
    {
        private RaidView(JsonValue json)
        {
            Boss = NakamaJson.Text(json, "boss");
            Week = (long)NakamaJson.Number(json, "week");
            EndsAtUnixMs = (long)NakamaJson.Number(json, "endsAt");
            KilledAtUnixMs = (long)NakamaJson.Number(json, "killedAt");
            Members = (int)NakamaJson.Number(json, "members");
            Left = NakamaJson.Number(json, "left");
            var layers = new List<RaidLayerView>();
            foreach (var layer in NakamaJson.Items(json, "layers"))
                layers.Add(new RaidLayerView(NakamaJson.Text(layer, "id"), NakamaJson.Number(layer, "hp"), NakamaJson.Number(layer, "left")));
            Layers = layers;
            var own = NakamaJson.Field(json, "own");
            OwnShare = own == null ? 0 : NakamaJson.Number(own, "share");
            OwnPlace = own == null ? 0 : (int)NakamaJson.Number(own, "place");
            Players = own == null ? 0 : (int)NakamaJson.Number(own, "players");
            OwnSeals = own == null ? 0 : (long)NakamaJson.Number(own, "seals");
            var top = new List<RaidRowView>();
            foreach (var row in NakamaJson.Items(json, "top"))
                top.Add(new RaidRowView((int)NakamaJson.Number(row, "place"), NakamaJson.Text(row, "userId"), NakamaJson.Text(row, "name"), NakamaJson.Number(row, "share")));
            Top = top;
            var daily = NakamaJson.Field(json, "daily");
            Daily = daily != null && daily.Kind == JsonKind.Object ? ClanDailyView.From(daily) : null;
            var last = NakamaJson.Field(json, "last");
            HasLast = last != null && last.Kind == JsonKind.Object;
            if (last != null && HasLast)
            {
                LastKilled = NakamaJson.Flag(last, "killed");
                LastPlace = (int)NakamaJson.Number(last, "place");
                LastPlayers = (int)NakamaJson.Number(last, "players");
                LastSeals = (long)NakamaJson.Number(last, "seals");
            }
        }

        public string Boss { get; }
        public long Week { get; }
        public long EndsAtUnixMs { get; }
        public long KilledAtUnixMs { get; }
        public int Members { get; }
        public double Left { get; }
        public IReadOnlyList<RaidLayerView> Layers { get; }
        public double OwnShare { get; }
        public int OwnPlace { get; }
        public int Players { get; }
        public long OwnSeals { get; }
        public IReadOnlyList<RaidRowView> Top { get; }
        public ClanDailyView? Daily { get; }
        public bool HasLast { get; }
        public bool LastKilled { get; }
        public int LastPlace { get; }
        public int LastPlayers { get; }
        public long LastSeals { get; }

        public static RaidView From(JsonValue json) => new RaidView(json);

        public static async Task<RaidView> ShowAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "raid_view", "{}", cancel).ConfigureAwait(false)));

        public static async Task<RaidView> PushAsync(NakamaClient client, NakamaSession session, RaidRules rules, RaidPush push, CancellationToken cancel = default)
        {
            var fields = new List<(string, string)>();
            foreach (var layer in rules.Layers)
                fields.Add((layer.Id, Number(push.Of(layer.Input))));
            fields.Add(("seconds", Number(push.Seconds)));
            var counts = push.Counts;
            fields.Add(("counts", NakamaJson.Object(("keys", Number(counts.KeyPresses)), ("clicks", Number(counts.Clicks)), ("mouseMeters", Number(counts.MouseMeters)),
                ("bosses", Number(counts.Bosses)))));
            return From(NakamaJson.Parse(await client.RpcAsync(session, "raid_push", NakamaJson.Object(fields.ToArray()), cancel).ConfigureAwait(false)));
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }

    public sealed class ClanDailyView
    {
        private ClanDailyView(JsonValue json)
        {
            Day = (long)NakamaJson.Number(json, "day");
            EndsAtUnixMs = (long)NakamaJson.Number(json, "endsAt");
            var challenges = new List<ClanChallengeView>();
            foreach (var item in NakamaJson.Items(json, "challenges"))
                challenges.Add(new ClanChallengeView(NakamaJson.Text(item, "id"), NakamaJson.Text(item, "counter"), NakamaJson.Number(item, "target"), NakamaJson.Number(item, "progress"),
                    (long)NakamaJson.Number(item, "done"), NakamaJson.Number(item, "yours"), (int)NakamaJson.Number(item, "seals")));
            Challenges = challenges;
        }

        public long Day { get; }
        public long EndsAtUnixMs { get; }
        public IReadOnlyList<ClanChallengeView> Challenges { get; }

        public static ClanDailyView From(JsonValue json) => new ClanDailyView(json);

        public static async Task<ClanDailyView> ShowAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_daily", "{}", cancel).ConfigureAwait(false)));
    }

    public readonly struct ClanChallengeView
    {
        public ClanChallengeView(string id, string counter, double target, double progress, long doneAtUnixMs, double yours, int seals)
        {
            Id = id;
            Counter = counter;
            Target = target;
            Progress = progress;
            DoneAtUnixMs = doneAtUnixMs;
            Yours = yours;
            Seals = seals;
        }

        public string Id { get; }
        public string Counter { get; }
        public double Target { get; }
        public double Progress { get; }
        public long DoneAtUnixMs { get; }
        public bool Done => DoneAtUnixMs > 0;
        public double Yours { get; }
        public int Seals { get; }
    }

    public readonly struct RaidLayerView
    {
        public RaidLayerView(string id, double hp, double left)
        {
            Id = id;
            Hp = hp;
            Left = left;
        }

        public string Id { get; }
        public double Hp { get; }
        public double Left { get; }
    }

    public readonly struct RaidRowView
    {
        public RaidRowView(int place, string userId, string name, double share)
        {
            Place = place;
            UserId = userId;
            Name = name;
            Share = share;
        }

        public int Place { get; }
        public string UserId { get; }
        public string Name { get; }
        public double Share { get; }
    }
}
