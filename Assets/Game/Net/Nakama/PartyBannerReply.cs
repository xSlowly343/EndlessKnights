using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class PartyBannerReply
    {
        private PartyBannerReply(JsonValue json)
        {
            PartyId = NakamaJson.Text(json, "partyId");
            Opened = NakamaJson.Flag(json, "opened");
            Leader = NakamaJson.Flag(json, "leader");
            Slots = PartyTreasuryReply.Texts(NakamaJson.Items(json, "slots"));
            Earned = PartyTreasuryReply.Texts(NakamaJson.Items(json, "earned"));
            var placed = new List<string>();
            foreach (var crest in Slots)
            {
                if (crest.Length > 0)
                    placed.Add(crest);
            }
            Placed = placed;
        }

        public string PartyId { get; }
        public bool Opened { get; }
        public bool Leader { get; }
        public IReadOnlyList<string> Slots { get; }
        public IReadOnlyList<string> Earned { get; }
        public IReadOnlyList<string> Placed { get; }

        public static PartyBannerReply From(JsonValue json) => new PartyBannerReply(json);

        public static string SetPayload(int slot, string crest) =>
            NakamaJson.Object(("slot", slot.ToString(CultureInfo.InvariantCulture)), ("crest", NakamaJson.Quote(crest)));

        public static Task<PartyBannerReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_banner", "{}", cancel);

        public static Task<PartyBannerReply> SetAsync(NakamaClient client, NakamaSession session, int slot, string crest, CancellationToken cancel = default) =>
            CallAsync(client, session, "party_banner_set", SetPayload(slot, crest), cancel);

        private static async Task<PartyBannerReply> CallAsync(NakamaClient client, NakamaSession session, string rpc, string payload, CancellationToken cancel) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, rpc, payload, cancel).ConfigureAwait(false)));
    }
}
