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
