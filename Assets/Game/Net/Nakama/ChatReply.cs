using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;
using Game.Core.Party;

namespace Game.Net.Nakama
{
    public sealed class ChatReply
    {
        private ChatReply(JsonValue json)
        {
            Channel = NakamaJson.Text(json, "channel");
            Room = NakamaJson.Text(json, "room");
            Seq = (long)NakamaJson.Number(json, "seq");
            Role = NakamaJson.Text(json, "role");
            CanModerate = NakamaJson.Flag(json, "canModerate");
            MutedUntilUnixMs = (long)NakamaJson.Number(json, "mutedUntil");
            var lines = new List<ChatLine>();
            foreach (var item in NakamaJson.Items(json, "messages"))
                lines.Add(new ChatLine((long)NakamaJson.Number(item, "seq"), NakamaJson.Text(item, "from"), NakamaJson.Text(item, "name"), NakamaJson.Text(item, "role"),
                    NakamaJson.Text(item, "text"), (long)NakamaJson.Number(item, "at")));
            Lines = lines;
            var removed = new List<long>();
            foreach (var item in NakamaJson.Items(json, "removed"))
            {
                if (item.Kind == JsonKind.Number)
                    removed.Add((long)item.Number);
            }
            Removed = removed;
        }

        public string Channel { get; }
        public string Room { get; }
        public long Seq { get; }
        public string Role { get; }
        public bool CanModerate { get; }
        public long MutedUntilUnixMs { get; }
        public IReadOnlyList<ChatLine> Lines { get; }
        public IReadOnlyList<long> Removed { get; }

        public static ChatReply From(JsonValue json) => new ChatReply(json);

        public void ApplyTo(ChatLog log) => log.Receive(Room, Seq, Lines, Removed, CanModerate, Role, MutedUntilUnixMs);

        public static string ReadPayload(string channel, string language, long after) =>
            NakamaJson.Object(("channel", NakamaJson.Quote(channel)), ("language", NakamaJson.Quote(language)), ("after", Number(after)));

        public static string SendPayload(string channel, string language, string text, long after) =>
            NakamaJson.Object(("channel", NakamaJson.Quote(channel)), ("language", NakamaJson.Quote(language)), ("text", NakamaJson.Quote(text)), ("after", Number(after)));

        public static string LinePayload(string channel, string language, long seq, long after) =>
            NakamaJson.Object(("channel", NakamaJson.Quote(channel)), ("language", NakamaJson.Quote(language)), ("seq", Number(seq)), ("after", Number(after)));

        public static string ReportPayload(string channel, string language, long seq, string reason) =>
            NakamaJson.Object(("channel", NakamaJson.Quote(channel)), ("language", NakamaJson.Quote(language)), ("seq", Number(seq)), ("reason", NakamaJson.Quote(reason)));

        public static string MutePayload(string userId, double hours, string scope) =>
            NakamaJson.Object(("userId", NakamaJson.Quote(userId)), ("hours", hours.ToString("0.##", CultureInfo.InvariantCulture)), ("scope", NakamaJson.Quote(scope)));

        public static string RolePayload(string userId, string role) =>
            NakamaJson.Object(("userId", NakamaJson.Quote(userId)), ("role", NakamaJson.Quote(role)));

        public static async Task<ChatReply> ReadAsync(NakamaClient client, NakamaSession session, string channel, string language, long after, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "chat_read", ReadPayload(channel, language, after), cancel).ConfigureAwait(false)));

        public static async Task<ChatReply> SendAsync(NakamaClient client, NakamaSession session, string channel, string language, string text, long after, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "chat_send", SendPayload(channel, language, text, after), cancel).ConfigureAwait(false)));

        public static async Task<ChatReply> DeleteAsync(NakamaClient client, NakamaSession session, string channel, string language, long seq, long after, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "chat_delete", LinePayload(channel, language, seq, after), cancel).ConfigureAwait(false)));

        public static async Task<bool> ReportAsync(NakamaClient client, NakamaSession session, string channel, string language, long seq, string reason, CancellationToken cancel = default) =>
            NakamaJson.Flag(NakamaJson.Parse(await client.RpcAsync(session, "chat_report", ReportPayload(channel, language, seq, reason), cancel).ConfigureAwait(false)), "ok");

        public static async Task<long> MuteAsync(NakamaClient client, NakamaSession session, string userId, double hours, string scope, CancellationToken cancel = default) =>
            (long)NakamaJson.Number(NakamaJson.Parse(await client.RpcAsync(session, "chat_mute", MutePayload(userId, hours, scope), cancel).ConfigureAwait(false)), "until");

        public static async Task<string> SetRoleAsync(NakamaClient client, NakamaSession session, string userId, string role, CancellationToken cancel = default) =>
            NakamaJson.Text(NakamaJson.Parse(await client.RpcAsync(session, "chat_role", RolePayload(userId, role), cancel).ConfigureAwait(false)), "role");

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }

    public sealed class TicketReply
    {
        private readonly Dictionary<string, int> _owned = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _prices = new Dictionary<string, int>();

        private TicketReply(JsonValue json)
        {
            foreach (var item in NakamaJson.Items(json, "tickets"))
            {
                var id = NakamaJson.Text(item, "id");
                _owned[id] = (int)NakamaJson.Number(item, "owned");
                _prices[id] = (int)NakamaJson.Number(item, "price");
            }
        }

        public int Owned(string id) => _owned.TryGetValue(id, out var owned) ? owned : 0;

        public int Price(string id) => _prices.TryGetValue(id, out var price) ? price : 0;

        public static TicketReply From(JsonValue json) => new TicketReply(json);

        public static string BuyPayload(string id) => NakamaJson.Object(("id", NakamaJson.Quote(id)));

        public static async Task<TicketReply> FetchAsync(NakamaClient client, NakamaSession session, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "ticket_mine", "{}", cancel).ConfigureAwait(false)));

        public static async Task<TicketReply> BuyAsync(NakamaClient client, NakamaSession session, string id, CancellationToken cancel = default) =>
            From(NakamaJson.Parse(await client.RpcAsync(session, "ticket_buy", BuyPayload(id), cancel).ConfigureAwait(false)));
    }
}
