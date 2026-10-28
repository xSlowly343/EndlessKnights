using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Net.Nakama
{
    public sealed class NakamaClient
    {
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        private readonly HttpClient _http;
        private readonly string _basic;

        public NakamaClient(string serverUrl, string serverKey, HttpClient? http = null)
        {
            ServerUrl = serverUrl.TrimEnd('/');
            _basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(serverKey + ":"));
            _http = http ?? SharedHttp;
        }

        public string ServerUrl { get; }

        public Task<NakamaSession> AuthenticateDeviceAsync(string deviceId, CancellationToken cancel = default) =>
            AuthenticateAsync("/v2/account/authenticate/device?create=true", NakamaJson.Object(("id", NakamaJson.Quote(deviceId))), cancel);

        public Task<NakamaSession> AuthenticateSteamAsync(string sessionTicket, bool importFriends, CancellationToken cancel = default) =>
            AuthenticateAsync("/v2/account/authenticate/steam?create=true" + (importFriends ? "&sync=true" : string.Empty),
                NakamaJson.Object(("token", NakamaJson.Quote(sessionTicket))), cancel);

        public Task<NakamaSession> RefreshAsync(NakamaSession session, CancellationToken cancel = default) =>
            AuthenticateAsync("/v2/account/session/refresh", NakamaJson.Object(("token", NakamaJson.Quote(session.RefreshToken))), cancel);

        public async Task<string> RpcAsync(NakamaSession session, string id, string payloadJson, CancellationToken cancel = default)
        {
            var reply = await SendAsync("/v2/rpc/" + Uri.EscapeDataString(id), new AuthenticationHeaderValue("Bearer", session.Token), NakamaJson.Quote(payloadJson), cancel)
                .ConfigureAwait(false);
            return NakamaJson.Text(NakamaJson.Parse(reply), "payload");
        }

        private async Task<NakamaSession> AuthenticateAsync(string path, string body, CancellationToken cancel)
        {
            var reply = NakamaJson.Parse(await SendAsync(path, new AuthenticationHeaderValue("Basic", _basic), body, cancel).ConfigureAwait(false));
            return NakamaSession.FromTokens(NakamaJson.Text(reply, "token"), NakamaJson.Text(reply, "refresh_token"));
        }

        private async Task<string> SendAsync(string path, AuthenticationHeaderValue authorization, string body, CancellationToken cancel)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ServerUrl + path);
            request.Headers.Authorization = authorization;
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancel).ConfigureAwait(false);
            }
            catch (HttpRequestException error)
            {
                throw new NakamaException(0, "no connection to the server: " + error.Message);
            }
            catch (TaskCanceledException) when (!cancel.IsCancellationRequested)
            {
                throw new NakamaException(0, "the server did not answer in time");
            }
            using var reply = response;
            var text = await reply.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!reply.IsSuccessStatusCode)
                throw new NakamaException((int)reply.StatusCode, ErrorMessage(text));
            return text;
        }

        public static string Clean(string message)
        {
            const string ErrorPrefix = "Error: ";
            const string StackMark = " at ";
            if (message.StartsWith(ErrorPrefix, StringComparison.Ordinal))
                message = message.Substring(ErrorPrefix.Length);
            var stack = message.IndexOf(StackMark, StringComparison.Ordinal);
            return stack > 0 ? message.Substring(0, stack) : message;
        }

        private static string ErrorMessage(string text)
        {
            try
            {
                var message = NakamaJson.Text(NakamaJson.Parse(text), "message");
                return message.Length > 0 ? Clean(message) : text;
            }
            catch (NakamaException)
            {
                return text;
            }
        }
    }
}
