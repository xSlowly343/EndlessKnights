using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public static class PartyQuestClaim
    {
        public const string Rpc = "quest_party_claim";

        public static string Payload(int bosses) => "{\"bosses\":" + bosses.ToString(CultureInfo.InvariantCulture) + "}";

        public static async Task<PartyQuestClaimResult> ClaimAsync(NakamaClient client, NakamaSession session, int bosses, CancellationToken cancel = default) =>
            PartyQuestClaimResult.From(NakamaJson.Parse(await client.RpcAsync(session, Rpc, Payload(bosses), cancel).ConfigureAwait(false)));
    }

    public sealed class PartyQuestClaimResult
    {
        public static PartyQuestClaimResult From(JsonValue json)
        {
            var wallet = NakamaJson.Field(json, "wallet");
            return new PartyQuestClaimResult((long)NakamaJson.Number(json, "granted"), wallet == null ? null : CoopWalletView.From(wallet));
        }

        public PartyQuestClaimResult(long granted, CoopWalletView? wallet)
        {
            Granted = granted;
            Wallet = wallet;
        }

        public long Granted { get; }
        public CoopWalletView? Wallet { get; }
    }
}
