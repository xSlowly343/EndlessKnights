using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class CosmeticsMineView
    {
        private CosmeticsMineView(JsonValue json)
        {
            Owned = Ids(json, "owned");
            Fresh = Ids(json, "fresh");
            Crystals = (long)NakamaJson.Number(json, "crystals");
        }

        public IReadOnlyList<string> Owned { get; }
        public IReadOnlyList<string> Fresh { get; }
        public long Crystals { get; }

        public static CosmeticsMineView From(JsonValue json) => new CosmeticsMineView(json);

        public static string BuyPayload(string id) => NakamaJson.Object(("id", NakamaJson.Quote(id)));

        public static async Task<CosmeticsMineView> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "cosmetics_mine", "{}", cancel).ConfigureAwait(false)));

        public static async Task<CosmeticsMineView> BuyAsync(NakamaClient client, NakamaSession session, string id, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "cosmetic_buy", BuyPayload(id), cancel).ConfigureAwait(false)));

        internal static IReadOnlyList<string> Ids(JsonValue json, string name)
        {
            var ids = new List<string>();
            foreach (var item in NakamaJson.Items(json, name))
            {
                if (item.Kind == JsonKind.String)
                    ids.Add(item.Text);
            }
            return ids;
        }
    }

    public sealed class ClanCosmeticsView
    {
        private ClanCosmeticsView(JsonValue json)
        {
            Owned = CosmeticsMineView.Ids(json, "owned");
            Banner = NakamaJson.Text(json, "banner");
            Emblem = NakamaJson.Text(json, "emblem");
            Victor = NakamaJson.Flag(json, "victor");
            VictorUntilUnixMs = (long)NakamaJson.Number(json, "victorUntil");
            VictorBanner = NakamaJson.Text(json, "victorBanner");
        }

        public IReadOnlyList<string> Owned { get; }
        public string Banner { get; }
        public string Emblem { get; }
        public bool Victor { get; }
        public long VictorUntilUnixMs { get; }
        public string VictorBanner { get; }
        public string ShownBanner => Victor ? VictorBanner : Banner;

        public static ClanCosmeticsView From(JsonValue json) => new ClanCosmeticsView(json);

        public static string SetPayload(string? banner, string? emblem)
        {
            var fields = new List<(string, string)>();
            if (banner != null)
                fields.Add(("banner", NakamaJson.Quote(banner)));
            if (emblem != null)
                fields.Add(("emblem", NakamaJson.Quote(emblem)));
            return NakamaJson.Object(fields.ToArray());
        }

        public static async Task<ClanCosmeticsView> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_cosmetics", "{}", cancel).ConfigureAwait(false)));

        public static async Task<ClanCosmeticsView> BuyAsync(NakamaClient client, NakamaSession session, string id, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_cosmetic_buy", CosmeticsMineView.BuyPayload(id), cancel).ConfigureAwait(false)));

        public static async Task<ClanCosmeticsView> SetAsync(NakamaClient client, NakamaSession session, string? banner, string? emblem, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_cosmetic_set", SetPayload(banner, emblem), cancel).ConfigureAwait(false)));
    }
}
