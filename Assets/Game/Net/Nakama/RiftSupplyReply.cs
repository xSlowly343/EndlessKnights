using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class RiftSupplyReply
    {
        private RiftSupplyReply(JsonValue json)
        {
            Window = (long)NakamaJson.Number(json, "window");
            SentThisWindow = (int)NakamaJson.Number(json, "sentThisWindow");
            Friendship = (long)NakamaJson.Number(json, "friendship");
            var supply = NakamaJson.Field(json, "supply");
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

        public static RiftSupplyReply From(JsonValue json) => new RiftSupplyReply(json);

        public static string SendPayload(string to, string item) =>
            NakamaJson.Object(("to", NakamaJson.Quote(to)), ("item", NakamaJson.Quote(item)));

        public static async Task<RiftSupplyReply> SendAsync(NakamaClient client, NakamaSession session, string to, string item, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_supply_send", SendPayload(to, item), cancel).ConfigureAwait(false)));

        public static async Task<RiftSupplyReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_supply", string.Empty, cancel).ConfigureAwait(false)));
    }
}
