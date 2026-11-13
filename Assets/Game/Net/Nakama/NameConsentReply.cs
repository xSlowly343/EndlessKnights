using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class NameConsentReply
    {
        public const string TownTop = "town_top";
        public const string Museum = "museum";

        private NameConsentReply(JsonValue json)
        {
            Shown = CosmeticsMineView.Ids(json, "shown");
        }

        public IReadOnlyList<string> Shown { get; }

        public bool Shows(string place)
        {
            foreach (var entry in Shown)
            {
                if (entry == place)
                    return true;
            }
            return false;
        }

        public static NameConsentReply From(JsonValue json) => new NameConsentReply(json);

        public static string SetPayload(string place, bool shown) =>
            NakamaJson.Object(("place", NakamaJson.Quote(place)), ("shown", shown ? "true" : "false"));

        public static async Task<NameConsentReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "name_consent", string.Empty, cancel).ConfigureAwait(false)));

        public static async Task<NameConsentReply> SetAsync(NakamaClient client, NakamaSession session, string place, bool shown, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "name_consent", SetPayload(place, shown), cancel).ConfigureAwait(false)));
    }
}
