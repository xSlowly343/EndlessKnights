using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class HideoutReply
    {
        private HideoutReply(JsonValue json)
        {
            Tier = (int)NakamaJson.Number(json, "tier");
            Members = (int)NakamaJson.Number(json, "members");
            Bosses = (long)NakamaJson.Number(json, "bosses");
            Orders = (int)NakamaJson.Number(json, "orders");
            SitSeconds = (long)NakamaJson.Number(json, "sitSeconds");
            FireLevel = (int)NakamaJson.Number(json, "fireLevel");
            var here = new List<string>();
            foreach (var entry in NakamaJson.Items(json, "here"))
                here.Add(NakamaJson.Text(entry, "name"));
            Here = here;
            HoursTogether = (long)NakamaJson.Number(json, "hoursTogether");
            Leader = NakamaJson.Flag(json, "leader");
            Buildings = Counts(NakamaJson.Field(json, "buildings"));
            Wants = Counts(NakamaJson.Field(json, "wants"));
            CanQueue = CosmeticsMineView.Ids(json, "canQueue");
            MyWants = CosmeticsMineView.Ids(json, "mine");
            var project = NakamaJson.Field(json, "project");
            if (project != null && project.Kind == JsonKind.Object)
            {
                Project = NakamaJson.Text(project, "building");
                ProjectLevel = (int)NakamaJson.Number(project, "level");
                ProjectPoints = (long)NakamaJson.Number(project, "points");
                ProjectTarget = (long)NakamaJson.Number(project, "target");
                ProjectHours = NakamaJson.Number(project, "hours");
                ProjectYours = (long)NakamaJson.Number(project, "yours");
            }
        }

        public long HoursTogether { get; }
        public bool Leader { get; }
        public IReadOnlyDictionary<string, int> Buildings { get; }
        public IReadOnlyDictionary<string, int> Wants { get; }
        public IReadOnlyList<string> CanQueue { get; }
        public IReadOnlyList<string> MyWants { get; }
        public string Project { get; } = string.Empty;
        public int ProjectLevel { get; }
        public long ProjectPoints { get; }
        public long ProjectTarget { get; }
        public double ProjectHours { get; }
        public long ProjectYours { get; }

        public static string QueuePayload(string building) => NakamaJson.Object(("building", NakamaJson.Quote(building)));

        public static string GivePayload(string item, int amount) => ClanCampReply.GivePayload(item, amount);

        public static string WantPayload(string building, bool on) => NakamaJson.Object(("building", NakamaJson.Quote(building)), ("on", on ? "true" : "false"));

        public static async Task<HideoutReply> QueueAsync(NakamaClient client, NakamaSession session, string building, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_queue", QueuePayload(building), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> GiveAsync(NakamaClient client, NakamaSession session, string item, int amount, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_give", GivePayload(item, amount), cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> WantAsync(NakamaClient client, NakamaSession session, string building, bool on, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_want", WantPayload(building, on), cancel).ConfigureAwait(false)));

        private static IReadOnlyDictionary<string, int> Counts(JsonValue? json)
        {
            var counts = new Dictionary<string, int>();
            if (json != null && json.Kind == JsonKind.Object)
            {
                foreach (var pair in json.Fields)
                    counts[pair.Key] = pair.Value.Kind == JsonKind.Number ? (int)pair.Value.Number : 0;
            }
            return counts;
        }

        public int Tier { get; }
        public int Members { get; }
        public long Bosses { get; }
        public int Orders { get; }
        public long SitSeconds { get; }
        public int FireLevel { get; }
        public IReadOnlyList<string> Here { get; }

        public static HideoutReply From(JsonValue json) => new HideoutReply(json);

        public static string PresencePayload(bool here) => NakamaJson.Object(("here", here ? "true" : "false"));

        public static async Task<HideoutReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_view", "{}", cancel).ConfigureAwait(false)));

        public static async Task<HideoutReply> PresenceAsync(NakamaClient client, NakamaSession session, bool here, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "hideout_presence", PresencePayload(here), cancel).ConfigureAwait(false)));
    }
}
