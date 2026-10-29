using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class CoopWalletView
    {
        private CoopWalletView(JsonValue json)
        {
            var balances = NakamaJson.Field(json, "balances");
            Friendship = balances == null ? 0 : (long)NakamaJson.Number(balances, "friendship");
            Seals = balances == null ? 0 : (long)NakamaJson.Number(balances, "seals");
            var today = new List<CoopWalletToday>();
            foreach (var item in NakamaJson.Items(json, "today"))
                today.Add(new CoopWalletToday(NakamaJson.Text(item, "source"), NakamaJson.Text(item, "currency"), (int)NakamaJson.Number(item, "earned"), (int)NakamaJson.Number(item, "cap")));
            Today = today;
            var ledger = new List<CoopWalletEntry>();
            foreach (var item in NakamaJson.Items(json, "ledger"))
                ledger.Add(new CoopWalletEntry((long)NakamaJson.Number(item, "at"), NakamaJson.Text(item, "source"), NakamaJson.Text(item, "currency"), (long)NakamaJson.Number(item, "amount")));
            Ledger = ledger;
        }

        public long Friendship { get; }
        public long Seals { get; }
        public IReadOnlyList<CoopWalletToday> Today { get; }
        public IReadOnlyList<CoopWalletEntry> Ledger { get; }

        public static CoopWalletView From(JsonValue json) => new CoopWalletView(json);

        public static async Task<CoopWalletView> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "coop_wallet", "{}", cancel).ConfigureAwait(false)));
    }

    public readonly struct CoopWalletToday
    {
        public CoopWalletToday(string source, string currency, int earned, int cap)
        {
            Source = source;
            Currency = currency;
            Earned = earned;
            Cap = cap;
        }

        public string Source { get; }
        public string Currency { get; }
        public int Earned { get; }
        public int Cap { get; }
    }

    public readonly struct CoopWalletEntry
    {
        public CoopWalletEntry(long atUnixMs, string source, string currency, long amount)
        {
            AtUnixMs = atUnixMs;
            Source = source;
            Currency = currency;
            Amount = amount;
        }

        public long AtUnixMs { get; }
        public string Source { get; }
        public string Currency { get; }
        public long Amount { get; }
    }
}
