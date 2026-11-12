using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class MarketLot
    {
        public MarketLot(string id, string seller, string sellerName, string item, int amount, int price, long atUnixMs, long untilUnixMs)
        {
            Id = id;
            Seller = seller;
            SellerName = sellerName;
            Item = item;
            Amount = amount;
            Price = price;
            AtUnixMs = atUnixMs;
            UntilUnixMs = untilUnixMs;
        }

        public string Id { get; }
        public string Seller { get; }
        public string SellerName { get; }
        public string Item { get; }
        public int Amount { get; }
        public int Price { get; }
        public long AtUnixMs { get; }
        public long UntilUnixMs { get; }
        public long Total => (long)Amount * Price;
    }

    public sealed class MarketDelivery
    {
        public MarketDelivery(string id, string item, int amount, string reason)
        {
            Id = id;
            Item = item;
            Amount = amount;
            Reason = reason;
        }

        public string Id { get; }
        public string Item { get; }
        public int Amount { get; }
        public string Reason { get; }
        public bool Bought => Reason == "bought";
    }

    public sealed class MarketReply
    {
        private MarketReply(JsonValue json)
        {
            NowUnixMs = (long)NakamaJson.Number(json, "now");
            Currency = NakamaJson.Text(json, "currency");
            Balance = (long)NakamaJson.Number(json, "balance");
            Fee = NakamaJson.Number(json, "fee");
            MaxLots = (int)NakamaJson.Number(json, "maxLots");
            Paid = (long)NakamaJson.Number(json, "paid");
            Lots = LotsOf(json, "lots");
            Mine = LotsOf(json, "mine");
            var inbox = new List<MarketDelivery>();
            foreach (var item in NakamaJson.Items(json, "inbox"))
                inbox.Add(new MarketDelivery(NakamaJson.Text(item, "id"), NakamaJson.Text(item, "item"), (int)NakamaJson.Number(item, "amount"), NakamaJson.Text(item, "reason")));
            Inbox = inbox;
        }

        public long NowUnixMs { get; }
        public string Currency { get; }
        public long Balance { get; }
        public double Fee { get; }
        public int MaxLots { get; }
        public long Paid { get; }
        public IReadOnlyList<MarketLot> Lots { get; }
        public IReadOnlyList<MarketLot> Mine { get; }
        public IReadOnlyList<MarketDelivery> Inbox { get; }

        public bool Expired(MarketLot lot) => lot.UntilUnixMs <= NowUnixMs;

        public static MarketReply From(JsonValue json) => new MarketReply(json);

        public static string ListPayload(string item) => item.Length == 0 ? "{}" : NakamaJson.Object(("item", NakamaJson.Quote(item)));

        public static string SellPayload(string nonce, string item, int amount, int price) =>
            NakamaJson.Object(("nonce", NakamaJson.Quote(nonce)), ("item", NakamaJson.Quote(item)), ("amount", amount.ToString(CultureInfo.InvariantCulture)),
                ("price", price.ToString(CultureInfo.InvariantCulture)));

        public static string LotPayload(string lot) => NakamaJson.Object(("lot", NakamaJson.Quote(lot)));

        public static async Task<MarketReply> ListAsync(NakamaClient client, NakamaSession session, string item = "", CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "market_list", ListPayload(item), cancel).ConfigureAwait(false)));

        public static async Task<MarketReply> SellAsync(NakamaClient client, NakamaSession session, string nonce, string item, int amount, int price, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "market_sell", SellPayload(nonce, item, amount, price), cancel).ConfigureAwait(false)));

        public static async Task<MarketReply> BuyAsync(NakamaClient client, NakamaSession session, string lot, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "market_buy", LotPayload(lot), cancel).ConfigureAwait(false)));

        public static async Task<MarketReply> CancelAsync(NakamaClient client, NakamaSession session, string lot, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "market_cancel", LotPayload(lot), cancel).ConfigureAwait(false)));

        public static async Task<MarketReply> ClaimAsync(NakamaClient client, NakamaSession session, IReadOnlyList<string> received, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "market_claim", TownReply.ClaimPayload(received), cancel).ConfigureAwait(false)));

        private static IReadOnlyList<MarketLot> LotsOf(JsonValue json, string name)
        {
            var lots = new List<MarketLot>();
            foreach (var item in NakamaJson.Items(json, name))
                lots.Add(new MarketLot(NakamaJson.Text(item, "id"), NakamaJson.Text(item, "seller"), NakamaJson.Text(item, "sellerName"), NakamaJson.Text(item, "item"),
                    (int)NakamaJson.Number(item, "amount"), (int)NakamaJson.Number(item, "price"), (long)NakamaJson.Number(item, "at"), (long)NakamaJson.Number(item, "until")));
            return lots;
        }
    }
}
