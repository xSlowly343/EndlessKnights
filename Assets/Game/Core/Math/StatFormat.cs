using System.Globalization;

namespace Game.Core.Math
{
    public static class StatFormat
    {
        public static string Count(ulong value, string thousandsSeparator = " ")
        {
            var digits = value.ToString(CultureInfo.InvariantCulture);
            return Group(digits, thousandsSeparator);
        }

        public static string Distance(double meters, bool russian = true, string thousandsSeparator = " ")
        {
            var safe = double.IsNaN(meters) || meters < 0 ? 0 : meters;
            if (safe < 1000)
                return ((long)System.Math.Floor(safe)).ToString(CultureInfo.InvariantCulture) + (russian ? " м" : " m");

            var hundredths = (long)System.Math.Floor(safe / 10 + 1e-9);
            var whole = hundredths / 100;
            var fraction = hundredths % 100;
            return Group(whole.ToString(CultureInfo.InvariantCulture), thousandsSeparator) + "." + fraction.ToString("00", CultureInfo.InvariantCulture)
                + (russian ? " км" : " km");
        }

        private static string Group(string digits, string separator)
        {
            if (digits.Length <= 3)
                return digits;
            var builder = new System.Text.StringBuilder(digits.Length + digits.Length / 3 * separator.Length);
            var head = digits.Length % 3;
            if (head > 0)
                builder.Append(digits, 0, head);
            for (var i = head; i < digits.Length; i += 3)
            {
                if (builder.Length > 0)
                    builder.Append(separator);
                builder.Append(digits, i, 3);
            }
            return builder.ToString();
        }
    }
}
