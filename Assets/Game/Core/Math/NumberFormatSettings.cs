using System;

namespace Game.Core.Math
{
    public readonly struct NumberFormatSettings
    {
        public const int MaxDecimals = 3;

        public static readonly NumberFormatSettings Default = new NumberFormatSettings(NumberFormat.Letters, 2, " ", ".");

        public readonly NumberFormat Format;
        public readonly int Decimals;
        public readonly string ThousandsSeparator;
        public readonly string DecimalSeparator;

        public NumberFormatSettings(NumberFormat format, int decimals, string thousandsSeparator, string decimalSeparator)
        {
            if (decimals < 0 || decimals > MaxDecimals)
                throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "Decimals must be between 0 and 3.");
            Format = format;
            Decimals = decimals;
            ThousandsSeparator = thousandsSeparator;
            DecimalSeparator = decimalSeparator;
        }
    }
}
