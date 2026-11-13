using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class TownGift
    {
        public TownGift(string id, string from, string fromName, string item, int amount, long atUnixMs)
        {
            Id = id;
            From = from;
            FromName = fromName;
            Item = item;
            Amount = amount;
            AtUnixMs = atUnixMs;
        }

        public string Id { get; }
        public string From { get; }
        public string FromName { get; }
        public string Item { get; }
        public int Amount { get; }
        public long AtUnixMs { get; }
    }

    public sealed class TownReply
    {
        private TownReply(JsonValue json)
        {
            Guilds = CosmeticsMineView.Ids(json, "guilds");
            SentToday = (int)NakamaJson.Number(json, "sentToday");
            GiftsPerDay = (int)NakamaJson.Number(json, "giftsPerDay");
            Inbox = Gifts(json, "inbox");
            Claimed = Gifts(json, "claimed");
            Available = HideoutReply.Amounts(NakamaJson.Field(json, "available"));
        }

        public IReadOnlyList<string> Guilds { get; }
        public int SentToday { get; }
        public int GiftsPerDay { get; }
        public IReadOnlyList<TownGift> Inbox { get; }
        public IReadOnlyList<TownGift> Claimed { get; }
        public IReadOnlyDictionary<string, long> Available { get; }

        public static TownReply From(JsonValue json) => new TownReply(json);

        public static string GiftPayload(string to, string item, int amount) =>
            NakamaJson.Object(("to", NakamaJson.Quote(to)), ("item", NakamaJson.Quote(item)), ("amount", amount.ToString(CultureInfo.InvariantCulture)));

        public static async Task<TownReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_mine", "{}", cancel).ConfigureAwait(false)));

        public static async Task<TownReply> SendGiftAsync(NakamaClient client, NakamaSession session, string to, string item, int amount, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_gift_send", GiftPayload(to, item, amount), cancel).ConfigureAwait(false)));

        public static string ClaimPayload(IReadOnlyList<string> received)
        {
            var ids = new string[received.Count];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = NakamaJson.Quote(received[i]);
            return NakamaJson.Object(("ack", "[" + string.Join(",", ids) + "]"));
        }

        public static async Task<TownReply> ClaimGiftsAsync(NakamaClient client, NakamaSession session, IReadOnlyList<string> received, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "town_gift_claim", ClaimPayload(received), cancel).ConfigureAwait(false)));

        private static IReadOnlyList<TownGift> Gifts(JsonValue json, string name)
        {
            var gifts = new List<TownGift>();
            foreach (var item in NakamaJson.Items(json, name))
                gifts.Add(new TownGift(NakamaJson.Text(item, "id"), NakamaJson.Text(item, "from"), NakamaJson.Text(item, "fromName"), NakamaJson.Text(item, "item"),
                    (int)NakamaJson.Number(item, "amount"), (long)NakamaJson.Number(item, "at")));
            return gifts;
        }
    }
}
