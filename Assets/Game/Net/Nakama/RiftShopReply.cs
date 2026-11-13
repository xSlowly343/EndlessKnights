using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class RiftShopReply
    {
        private RiftShopReply(JsonValue json)
        {
            RiftId = (long)NakamaJson.Number(json, "riftId");
            Sparks = (long)NakamaJson.Number(json, "sparks");
            Crystals = (long)NakamaJson.Number(json, "crystals");
            var bought = new Dictionary<string, int>();
            var counts = NakamaJson.Field(json, "bought");
            if (counts != null && counts.Kind == JsonKind.Object)
            {
                foreach (var pair in counts.Fields)
                    bought[pair.Key] = pair.Value.Kind == JsonKind.Number ? (int)pair.Value.Number : 0;
            }
            Bought = bought;
        }

        public long RiftId { get; }
        public long Sparks { get; }
        public long Crystals { get; }
        public IReadOnlyDictionary<string, int> Bought { get; }

        public static RiftShopReply From(JsonValue json) => new RiftShopReply(json);

        public static string ViewPayload(long riftId) => NakamaJson.Object(("riftId", riftId.ToString(CultureInfo.InvariantCulture)));

        public static string BuyPayload(long riftId, string id) =>
            NakamaJson.Object(("riftId", riftId.ToString(CultureInfo.InvariantCulture)), ("id", NakamaJson.Quote(id)));

        public static async Task<RiftShopReply> FetchAsync(NakamaClient client, NakamaSession session, long riftId, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_shop", ViewPayload(riftId), cancel).ConfigureAwait(false)));

        public static async Task<RiftShopReply> BuyAsync(NakamaClient client, NakamaSession session, long riftId, string id, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "rift_shop_buy", BuyPayload(riftId, id), cancel).ConfigureAwait(false)));
    }
}
