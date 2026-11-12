using System.Text;

namespace Game.Core.Mail
{
    public static class Coupon
    {
        public const int MinLength = 4;
        public const int MaxLength = 24;
        private const int MaxRawLength = 64;

        public static string Normalize(string? raw)
        {
            if (string.IsNullOrEmpty(raw) || raw!.Length > MaxRawLength)
                return string.Empty;
            var code = new StringBuilder(raw.Length);
            foreach (var symbol in raw)
            {
                if (char.IsWhiteSpace(symbol) || symbol == '-')
                    continue;
                var upper = char.ToUpperInvariant(symbol);
                if ((upper < 'A' || upper > 'Z') && (upper < '0' || upper > '9'))
                    return string.Empty;
                code.Append(upper);
            }
            return code.Length >= MinLength && code.Length <= MaxLength ? code.ToString() : string.Empty;
        }
    }
}
