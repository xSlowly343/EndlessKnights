using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class ClanCampReply
    {
        private ClanCampReply(JsonValue json)
        {
            Points = (long)NakamaJson.Number(json, "points");
            Level = (int)NakamaJson.Number(json, "level");
            Next = (long)NakamaJson.Number(json, "next");
            Yours = (long)NakamaJson.Number(json, "yours");
            Today = (long)NakamaJson.Number(json, "today");
            DailyCap = (int)NakamaJson.Number(json, "dailyCap");
            var feast = NakamaJson.Field(json, "feast");
            if (feast != null && feast.Kind == JsonKind.Object)
            {
                FeastDishes = (int)NakamaJson.Number(feast, "dishes");
                FeastTarget = (int)NakamaJson.Number(feast, "target");
                FeastLaid = NakamaJson.Flag(feast, "laid");
                FeastOpen = NakamaJson.Flag(feast, "open");
                FeastYours = (int)NakamaJson.Number(feast, "yours");
                FeastUntilUnixMs = (long)NakamaJson.Number(feast, "until");
            }
        }

        public long Points { get; }
        public int Level { get; }
        public long Next { get; }
        public long Yours { get; }
        public long Today { get; }
        public int DailyCap { get; }
        public int FeastDishes { get; }
        public int FeastTarget { get; }
        public bool FeastLaid { get; }
        public bool FeastOpen { get; }
        public int FeastYours { get; }
        public long FeastUntilUnixMs { get; }

        public static ClanCampReply From(JsonValue json) => new ClanCampReply(json);

        public static string GivePayload(string item, int amount) =>
            NakamaJson.Object(("item", NakamaJson.Quote(item)), ("amount", amount.ToString(CultureInfo.InvariantCulture)));

        public static string BringPayload(string dish) => NakamaJson.Object(("dish", NakamaJson.Quote(dish)));

        public static async Task<ClanCampReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_camp", "{}", cancel).ConfigureAwait(false)));

        public static async Task<ClanCampReply> GiveAsync(NakamaClient client, NakamaSession session, string item, int amount, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_camp_give", GivePayload(item, amount), cancel).ConfigureAwait(false)));

        public static async Task<ClanCampReply> BringAsync(NakamaClient client, NakamaSession session, string dish, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "clan_feast_bring", BringPayload(dish), cancel).ConfigureAwait(false)));
    }
}
