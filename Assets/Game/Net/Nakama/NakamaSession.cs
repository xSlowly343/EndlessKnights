using System.Text;

namespace Game.Net.Nakama
{
    public sealed class NakamaSession
    {
        private NakamaSession(string token, string refreshToken, string userId, long expiresAtUnix)
        {
            Token = token;
            RefreshToken = refreshToken;
            UserId = userId;
            ExpiresAtUnix = expiresAtUnix;
        }

        public string Token { get; }
        public string RefreshToken { get; }
        public string UserId { get; }
        public long ExpiresAtUnix { get; }

        public bool ExpiresWithin(long nowUnix, long seconds) => ExpiresAtUnix - nowUnix <= seconds;

        public static NakamaSession FromTokens(string token, string refreshToken)
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                throw new NakamaException(0, "the server sent a broken session token");
            var claims = NakamaJson.Parse(Encoding.UTF8.GetString(NakamaJson.FromBase64Url(parts[1])));
            return new NakamaSession(token, refreshToken, NakamaJson.Text(claims, "uid"), (long)NakamaJson.Number(claims, "exp"));
        }
    }
}
