using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ClanTreeView
    {
        private ClanTreeView(JsonValue json)
        {
            Treasury = (long)NakamaJson.Number(json, "treasury");
            Yours = (long)NakamaJson.Number(json, "yours");
            var taken = new List<string>();
            foreach (var item in NakamaJson.Items(json, "taken"))
                taken.Add(item.Text);
            Taken = taken;
            var donors = new List<ClanDonorView>();
            foreach (var item in NakamaJson.Items(json, "donors"))
                donors.Add(new ClanDonorView(NakamaJson.Text(item, "userId"), NakamaJson.Text(item, "name"), (long)NakamaJson.Number(item, "amount")));
            Donors = donors;
            var log = new List<ClanTreeLogView>();
            foreach (var item in NakamaJson.Items(json, "log"))
                log.Add(new ClanTreeLogView((long)NakamaJson.Number(item, "at"), NakamaJson.Text(item, "name"), NakamaJson.Text(item, "kind"), (long)NakamaJson.Number(item, "amount"),
                    NakamaJson.Text(item, "node")));
            Log = log;
        }

        public long Treasury { get; }
        public long Yours { get; }
        public IReadOnlyList<string> Taken { get; }
        public IReadOnlyList<ClanDonorView> Donors { get; }
        public IReadOnlyList<ClanTreeLogView> Log { get; }

        public static ClanTreeView From(JsonValue json) => new ClanTreeView(json);

        public static string DonatePayload(long amount) => NakamaJson.Object(("amount", amount.ToString(CultureInfo.InvariantCulture)));

        public static string BuyPayload(string nodeId) => NakamaJson.Object(("node", NakamaJson.Quote(nodeId)));
    }

    public readonly struct ClanDonorView
    {
        public ClanDonorView(string userId, string name, long amount)
        {
            UserId = userId;
            Name = name;
            Amount = amount;
        }

        public string UserId { get; }
        public string Name { get; }
        public long Amount { get; }
    }

    public readonly struct ClanTreeLogView
    {
        public ClanTreeLogView(long atUnixMs, string name, string kind, long amount, string node)
        {
            AtUnixMs = atUnixMs;
            Name = name;
            Kind = kind;
            Amount = amount;
            Node = node;
        }

        public long AtUnixMs { get; }
        public string Name { get; }
        public string Kind { get; }
        public long Amount { get; }
        public string Node { get; }
    }
}
