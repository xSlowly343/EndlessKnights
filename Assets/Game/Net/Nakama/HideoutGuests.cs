using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Ui;

namespace Game.Net.Nakama
{
    public sealed class HideoutGuestAction
    {
        public HideoutGuestAction(string from, string fromName, string act, int bed, long atUnixMs)
        {
            From = from;
            FromName = fromName;
            Act = act;
            Bed = bed;
            AtUnixMs = atUnixMs;
        }

        public string From { get; }
        public string FromName { get; }
        public string Act { get; }
        public int Bed { get; }
        public long AtUnixMs { get; }
    }

    public sealed class HideoutVisitView
    {
        private HideoutVisitView(JsonValue json)
        {
            Host = NakamaJson.Text(json, "host");
            var camp = NakamaJson.Field(json, "hideout") ?? JsonValue.Null();
            Theme = camp.Kind == JsonKind.Object ? NakamaJson.Text(camp, "theme") : string.Empty;
            Decor = camp.Kind == JsonKind.Object ? CosmeticsMineView.Ids(camp, "decor") : System.Array.Empty<string>();
            Title = camp.Kind == JsonKind.Object ? NakamaJson.Text(camp, "title") : string.Empty;
            Trophies = camp.Kind == JsonKind.Object ? CosmeticsMineView.Ids(camp, "trophies") : System.Array.Empty<string>();
            Brand = camp.Kind == JsonKind.Object ? NakamaJson.Text(camp, "brand") : string.Empty;
            var monuments = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var raw = camp.Kind == JsonKind.Object ? NakamaJson.Field(camp, "monuments") : null;
            if (raw != null && raw.Kind == JsonKind.Object)
            {
                foreach (var pair in raw.Fields)
                    monuments[pair.Key] = (int)pair.Value.Number;
            }
            Monuments = monuments;
            Left = CosmeticsMineView.Ids(json, "left");
        }

        public string Host { get; }
        public string Theme { get; }
        public IReadOnlyList<string> Decor { get; }
        public IReadOnlyDictionary<string, int> Monuments { get; }
        public string Title { get; }
        public IReadOnlyList<string> Trophies { get; }
        public string Brand { get; }
        public IReadOnlyList<string> Left { get; }

        public static HideoutVisitView From(JsonValue json) => new HideoutVisitView(json);
    }

    public static class HideoutGuests
    {
        public static string PublishPayload(CampSnapshot snapshot)
        {
            var monuments = new List<string>();
            foreach (var (id, level) in snapshot.Monuments)
                monuments.Add(NakamaJson.Quote(id) + ":" + level.ToString(CultureInfo.InvariantCulture));
            return NakamaJson.Object(
                ("theme", NakamaJson.Quote(snapshot.Theme)),
                ("decor", List(snapshot.Decor)),
                ("monuments", "{" + string.Join(",", monuments) + "}"),
                ("title", NakamaJson.Quote(snapshot.Title)),
                ("trophies", List(snapshot.Trophies)),
                ("brand", NakamaJson.Quote(snapshot.Brand)));
        }

        public static string GuestPayload(string host, string act, int bed = -1) =>
            NakamaJson.Object(("host", NakamaJson.Quote(host)), ("act", NakamaJson.Quote(act)), ("bed", bed.ToString(CultureInfo.InvariantCulture)));

        public static IReadOnlyList<HideoutGuestAction> Guests(JsonValue json)
        {
            var actions = new List<HideoutGuestAction>();
            foreach (var item in NakamaJson.Items(json, "guests"))
                actions.Add(new HideoutGuestAction(NakamaJson.Text(item, "from"), NakamaJson.Text(item, "fromName"), NakamaJson.Text(item, "act"),
                    (int)NakamaJson.Number(item, "bed"), (long)NakamaJson.Number(item, "at")));
            return actions;
        }

        public static Task<string> PublishAsync(NakamaClient client, NakamaSession session, CampSnapshot snapshot, CancellationToken cancel = default) =>
            client.RpcAsync(session, "hideout_publish", PublishPayload(snapshot), cancel);

        public static async Task<HideoutVisitView> VisitAsync(NakamaClient client, NakamaSession session, string host, CancellationToken cancel = default) =>
            HideoutVisitView.From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_visit", NakamaJson.Object(("host", NakamaJson.Quote(host))), cancel).ConfigureAwait(false)));

        public static async Task<IReadOnlyList<string>> GuestAsync(NakamaClient client, NakamaSession session, string host, string act, int bed = -1, CancellationToken cancel = default) =>
            CosmeticsMineView.Ids(NakamaJson.Parse(await client.RpcAsync(session, "hideout_guest", GuestPayload(host, act, bed), cancel).ConfigureAwait(false)), "left");

        public static async Task<IReadOnlyList<HideoutGuestAction>> ClaimGuestsAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            Guests(NakamaJson.Parse(await client.RpcAsync(session, "hideout_guests_claim", "{}", cancel).ConfigureAwait(false)));

        private static string List(IReadOnlyList<string> items)
        {
            var quoted = new string[items.Count];
            for (var i = 0; i < quoted.Length; i++)
                quoted[i] = NakamaJson.Quote(items[i]);
            return "[" + string.Join(",", quoted) + "]";
        }
    }
}
