using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ChestShopReply
    {
        private ChestShopReply(JsonValue json)
        {
            Week = (long)NakamaJson.Number(json, "week");
            Crystals = (long)NakamaJson.Number(json, "crystals");
            Letter = NakamaJson.Text(json, "letter");
            var bought = new Dictionary<string, int>();
            var counts = NakamaJson.Field(json, "bought");
            if (counts != null && counts.Kind == JsonKind.Object)
            {
                foreach (var pair in counts.Fields)
                    bought[pair.Key] = pair.Value.Kind == JsonKind.Number ? (int)pair.Value.Number : 0;
            }
            Bought = bought;
        }

        public long Week { get; }
        public long Crystals { get; }
        public string Letter { get; }
        public IReadOnlyDictionary<string, int> Bought { get; }

        public static ChestShopReply From(JsonValue json) => new ChestShopReply(json);

        public static string BuyPayload(string id) => NakamaJson.Object(("id", NakamaJson.Quote(id)));

        public static async Task<ChestShopReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "chest_shop", "{}", cancel).ConfigureAwait(false)));

        public static async Task<ChestShopReply> BuyAsync(NakamaClient client, NakamaSession session, string id, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "chest_buy", BuyPayload(id), cancel).ConfigureAwait(false)));
    }
}
