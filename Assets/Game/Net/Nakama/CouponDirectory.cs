using System.Threading;
using System.Threading.Tasks;

namespace Game.Net.Nakama
{
    public sealed class CouponDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public CouponDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<string> RedeemAsync(string code, CancellationToken cancel = default) =>
            Status(await _client.RpcAsync(_session, "coupon_redeem", NakamaJson.Object(("code", NakamaJson.Quote(code))), cancel).ConfigureAwait(false));

        public static string Status(string reply) => NakamaJson.Text(NakamaJson.Parse(reply), "status");
    }
}
