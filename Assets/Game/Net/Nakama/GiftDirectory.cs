using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public readonly struct GiftSent
    {
        public GiftSent(string gemId, bool mentor, int left)
        {
            GemId = gemId;
            Mentor = mentor;
            Left = left;
        }

        public string GemId { get; }
        public bool Mentor { get; }
        public int Left { get; }
    }

    public readonly struct GiftStatus
    {
        public GiftStatus(int left, int dailyLimit, int perFriendDaily, int cost)
        {
            Left = left;
            DailyLimit = dailyLimit;
            PerFriendDaily = perFriendDaily;
            Cost = cost;
        }

        public int Left { get; }
        public int DailyLimit { get; }
        public int PerFriendDaily { get; }
        public int Cost { get; }
    }

    public sealed class GiftDirectory
    {
        private readonly NakamaClient _client;
        private readonly NakamaSession _session;

        public GiftDirectory(NakamaClient client, NakamaSession session)
        {
            _client = client;
            _session = session;
        }

        public async Task<GiftSent> SendAsync(string userId, string gemId, CancellationToken cancel = default) =>
            Sent(NakamaJson.Parse(await _client.RpcAsync(_session, "gift_send", NakamaJson.Object(("userId", NakamaJson.Quote(userId)), ("gemId", NakamaJson.Quote(gemId))), cancel)
                .ConfigureAwait(false)));

        public async Task<GiftStatus> StatusAsync(CancellationToken cancel = default) =>
            Status(NakamaJson.Parse(await _client.RpcAsync(_session, "gift_status", "{}", cancel).ConfigureAwait(false)));

        public static GiftSent Sent(JsonValue reply) =>
            new GiftSent(NakamaJson.Text(reply, "gemId"), NakamaJson.Flag(reply, "mentor"), (int)NakamaJson.Number(reply, "left"));

        public static GiftStatus Status(JsonValue reply) =>
            new GiftStatus((int)NakamaJson.Number(reply, "left"), (int)NakamaJson.Number(reply, "dailyLimit"), (int)NakamaJson.Number(reply, "perFriendDaily"), (int)NakamaJson.Number(reply, "cost"));
    }
}
