using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public sealed class NakamaSocket : IDisposable
    {
        private const int PacketBytes = Game.Core.Party.Wire.PartyCodec.MaxPacketBytes;

        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly BlockingCollection<Outgoing> _outgoing = new BlockingCollection<Outgoing>();
        private readonly ConcurrentQueue<byte[]> _incoming = new ConcurrentQueue<byte[]>();
        private readonly ConcurrentBag<byte[]> _buffers = new ConcurrentBag<byte[]>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonValue>> _pending = new ConcurrentDictionary<string, TaskCompletionSource<JsonValue>>();
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private int _nextCid;
        private volatile bool _open;

        public bool IsOpen => _open && _socket.State == WebSocketState.Open;
        public int Dropped { get; private set; }

        public async Task ConnectAsync(string serverUrl, NakamaSession session, CancellationToken cancel = default)
        {
            var url = serverUrl.TrimEnd('/');
            url = (url.StartsWith("https://", StringComparison.Ordinal) ? "wss://" + url.Substring(8) : url.StartsWith("http://", StringComparison.Ordinal) ? "ws://" + url.Substring(7) : url)
                + "/ws?lang=en&status=true&token=" + Uri.EscapeDataString(session.Token);
            try
            {
                await _socket.ConnectAsync(new Uri(url), cancel).ConfigureAwait(false);
            }
            catch (WebSocketException error)
            {
                throw new NakamaException(0, "no connection to the server: " + error.Message);
            }
            _open = true;
            new Thread(SendLoop) { IsBackground = true, Name = "nakama-send" }.Start();
            _ = Task.Run(ReceiveLoop);
        }

        public async Task<JsonValue> JoinMatchAsync(string matchId, IReadOnlyDictionary<string, string> metadata, CancellationToken cancel = default)
        {
            var fields = new List<(string, string)>();
            foreach (var pair in metadata)
                fields.Add((pair.Key, NakamaJson.Quote(pair.Value)));
            var reply = await RequestAsync("match_join", NakamaJson.Object(("match_id", NakamaJson.Quote(matchId)), ("metadata", NakamaJson.Object(fields.ToArray()))), cancel)
                .ConfigureAwait(false);
            return NakamaJson.Field(reply, "match") ?? reply;
        }

        public Task LeaveMatchAsync(string matchId, CancellationToken cancel = default) =>
            RequestAsync("match_leave", NakamaJson.Object(("match_id", NakamaJson.Quote(matchId))), cancel);

        public byte[] RentBuffer() => _buffers.TryTake(out var buffer) ? buffer : new byte[PacketBytes];

        public void SendMatchData(string matchId, byte[] rented, int length)
        {
            if (!IsOpen || length <= 0 || length > rented.Length)
            {
                _buffers.Add(rented);
                return;
            }
            _outgoing.Add(new Outgoing(null, matchId, rented, length));
        }

        public bool TryTakePacket(out byte[] packet) => _incoming.TryDequeue(out packet);

        public void Dispose()
        {
            _open = false;
            _stop.Cancel();
            _outgoing.CompleteAdding();
            try
            {
                if (_socket.State == WebSocketState.Open)
                    _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
                _socket.Abort();
            }
            _socket.Dispose();
            FailPending("the socket is closed");
        }

        private async Task<JsonValue> RequestAsync(string kind, string body, CancellationToken cancel)
        {
            var cid = Interlocked.Increment(ref _nextCid).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var reply = new TaskCompletionSource<JsonValue>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[cid] = reply;
            _outgoing.Add(new Outgoing(NakamaJson.Object(("cid", NakamaJson.Quote(cid)), (kind, body)), null, null, 0));
            using (cancel.Register(() => reply.TrySetCanceled()))
                return await reply.Task.ConfigureAwait(false);
        }

        private void SendLoop()
        {
            try
            {
                foreach (var message in _outgoing.GetConsumingEnumerable(_stop.Token))
                {
                    var text = message.Text ?? MatchData(message);
                    if (message.Buffer != null)
                        _buffers.Add(message.Buffer);
                    var bytes = Encoding.UTF8.GetBytes(text);
                    _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _stop.Token).GetAwaiter().GetResult();
                }
            }
            catch (Exception)
            {
                _open = false;
            }
        }

        private static string MatchData(in Outgoing message) =>
            "{\"match_data_send\":{\"match_id\":" + NakamaJson.Quote(message.MatchId!) + ",\"op_code\":" + message.Buffer![0].ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ",\"data\":\"" + Convert.ToBase64String(message.Buffer, 0, message.Length) + "\"}}";

        private async Task ReceiveLoop()
        {
            var chunk = new byte[8192];
            using var message = new MemoryStream();
            try
            {
                while (_socket.State == WebSocketState.Open && !_stop.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(chunk), _stop.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    message.Write(chunk, 0, result.Count);
                    if (!result.EndOfMessage)
                        continue;
                    Handle(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                    message.SetLength(0);
                }
            }
            catch (Exception)
            {
            }
            _open = false;
            FailPending("the connection to the server was lost");
        }

        private void Handle(string text)
        {
            JsonValue envelope;
            try
            {
                envelope = NakamaJson.Parse(text);
            }
            catch (NakamaException)
            {
                return;
            }
            var data = NakamaJson.Field(envelope, "match_data");
            if (data != null)
            {
                try
                {
                    _incoming.Enqueue(Convert.FromBase64String(NakamaJson.Text(data, "data")));
                }
                catch (FormatException)
                {
                    Dropped++;
                }
                return;
            }
            var cid = NakamaJson.Text(envelope, "cid");
            if (cid.Length == 0 || !_pending.TryRemove(cid, out var reply))
                return;
            var error = NakamaJson.Field(envelope, "error");
            if (error != null)
                reply.TrySetException(new NakamaException((int)NakamaJson.Number(error, "code"), NakamaClient.Clean(NakamaJson.Text(error, "message"))));
            else
                reply.TrySetResult(envelope);
        }

        private void FailPending(string reason)
        {
            foreach (var cid in _pending.Keys)
            {
                if (_pending.TryRemove(cid, out var reply))
                    reply.TrySetException(new NakamaException(0, reason));
            }
        }

        private readonly struct Outgoing
        {
            public Outgoing(string? text, string? matchId, byte[]? buffer, int length)
            {
                Text = text;
                MatchId = matchId;
                Buffer = buffer;
                Length = length;
            }

            public string? Text { get; }
            public string? MatchId { get; }
            public byte[]? Buffer { get; }
            public int Length { get; }
        }
    }
}
