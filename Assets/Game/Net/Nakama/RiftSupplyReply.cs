using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class RearFighter
    {
        public RearFighter(string userId, string name, bool party, bool supplied)
        {
            UserId = userId;
            Name = name;
            Party = party;
            Supplied = supplied;
        }

        public string UserId { get; }
        public string Name { get; }
        public bool Party { get; }
        public bool Supplied { get; }
    }

    public sealed class RiftSupplyReply
    {
        private RiftSupplyReply(JsonValue json)
        {
            Window = (long)NakamaJson.Number(json, "window");
            SentThisWindow = (int)NakamaJson.Number(json, "sentThisWindow");
            Friendship = (long)NakamaJson.Number(json, "friendship");
            EndsAt = (long)NakamaJson.Number(json, "endsAt");
            SendsPerWindow = (int)NakamaJson.Number(json, "sendsPerWindow");
            var fighters = new List<RearFighter>();
            foreach (var item in NakamaJson.Items(json, "fighters"))
                fighters.Add(new RearFighter(NakamaJson.Text(item, "userId"), NakamaJson.Text(item, "name"), NakamaJson.Flag(item, "party"), NakamaJson.Flag(item, "supplied")));
            Fighters = fighters;
            var supply = NakamaJson.Field(json, "supply") ?? NakamaJson.Field(json, "mine");
            if (supply != null && supply.Kind == JsonKind.Object)
            {
                Item = NakamaJson.Text(supply, "item");
                FromName = NakamaJson.Text(supply, "fromName");
            }
        }

        public long Window { get; }
        public int SentThisWindow { get; }
        public long Friendship { get; }
        public string Item { get; } = string.Empty;
        public string FromName { get; } = string.Empty;
        public bool HasSupply => Item.Length > 0;
        public long EndsAt { get; }
        public int SendsPerWindow { get; }
        public IReadOnlyList<RearFighter> Fighters { get; }

        public static RiftSupplyReply From(JsonValue json) => new RiftSupplyReply(json);

        public static string SendPayload(string to, string item) =>
            NakamaJson.Object(("to", NakamaJson.Quote(to)), ("item", NakamaJson.Quote(item)));

        public static async Task<RiftSupplyReply> SendAsync(NakamaClient client, NakamaSession session, string to, string item, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_supply_send", SendPayload(to, item), cancel).ConfigureAwait(false)));

        public static async Task<RiftSupplyReply> RearAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_rear", string.Empty, cancel).ConfigureAwait(false)));

        public static async Task<RiftSupplyReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_supply", string.Empty, cancel).ConfigureAwait(false)));
    }
}
