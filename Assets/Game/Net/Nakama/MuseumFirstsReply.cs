using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class MuseumFirst
    {
        public MuseumFirst(string id, string name, bool mine, long atUnixMs)
        {
            Id = id;
            Name = name;
            Mine = mine;
            AtUnixMs = atUnixMs;
        }

        public string Id { get; }
        public string Name { get; }
        public bool Mine { get; }
        public long AtUnixMs { get; }
    }

    public sealed class MuseumFirstsReply
    {
        private MuseumFirstsReply(JsonValue json)
        {
            var firsts = new List<MuseumFirst>();
            foreach (var item in NakamaJson.Items(json, "firsts"))
                firsts.Add(new MuseumFirst(NakamaJson.Text(item, "id"), NakamaJson.Text(item, "name"), NakamaJson.Flag(item, "mine"), (long)NakamaJson.Number(item, "at")));
            Firsts = firsts;
            Reported = CosmeticsMineView.Ids(json, "reported");
            Won = CosmeticsMineView.Ids(json, "won");
        }

        public IReadOnlyList<MuseumFirst> Firsts { get; }
        public IReadOnlyList<string> Reported { get; }
        public IReadOnlyList<string> Won { get; }

        public static MuseumFirstsReply From(JsonValue json) => new MuseumFirstsReply(json);

        public static string FoundPayload(IReadOnlyList<string> ids)
        {
            var quoted = new string[ids.Count];
            for (var i = 0; i < quoted.Length; i++)
                quoted[i] = NakamaJson.Quote(ids[i]);
            return NakamaJson.Object(("ids", "[" + string.Join(",", quoted) + "]"));
        }

        public static async Task<MuseumFirstsReply> FoundAsync(NakamaClient client, NakamaSession session, IReadOnlyList<string> ids, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "museum_found", FoundPayload(ids), cancel).ConfigureAwait(false)));

        public static async Task<MuseumFirstsReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "museum_firsts", string.Empty, cancel).ConfigureAwait(false)));
    }

    public sealed class TownStreetReply
    {
        private TownStreetReply(JsonValue json)
        {
            var walkers = new List<string>();
            foreach (var item in NakamaJson.Items(json, "walkers"))
                walkers.Add(NakamaJson.Text(item, "name"));
            Walkers = walkers;
        }

        public IReadOnlyList<string> Walkers { get; }

        public static TownStreetReply From(JsonValue json) => new TownStreetReply(json);

        public static string Payload(bool here) => NakamaJson.Object(("here", here ? "true" : "false"));

        public static async Task<TownStreetReply> MarkAsync(NakamaClient client, NakamaSession session, bool here, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_street", Payload(here), cancel).ConfigureAwait(false)));
    }
}
