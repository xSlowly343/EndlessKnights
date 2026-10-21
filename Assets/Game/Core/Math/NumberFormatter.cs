using System;
using System.Globalization;
using System.Text;

namespace Game.Core.Math
{
    public static class NumberFormatter
    {
        public const long FullFormatLimitExponent = 15;

        private const int LettersAlphabet = 26;
        private const int FirstLetterGroup = 5;
        private const int MinLetterCount = 2;
        private static readonly string[] NamedSuffixes = { string.Empty, "K", "M", "B", "T" };

        public static string Format(BigNumber value, NumberFormatSettings settings)
        {
            if (value.IsNegative)
                return "-" + Format(-value, settings);
            if (value.IsZero)
                return "0";

            switch (settings.Format)
            {
                case NumberFormat.Scientific:
                    return FormatScientific(value, settings);
                case NumberFormat.Engineering:
                    return FormatEngineering(value, settings);
                case NumberFormat.Full:
                    return value.Exponent < FullFormatLimitExponent ? FormatFull(value, settings) : FormatLetters(value, settings);
                default:
                    return FormatLetters(value, settings);
            }
        }

        public static string LetterSuffix(long thousandsGroup)
        {
            if (thousandsGroup < 0)
                throw new ArgumentOutOfRangeException(nameof(thousandsGroup), thousandsGroup, "Group must not be negative.");
            if (thousandsGroup < FirstLetterGroup)
                return NamedSuffixes[thousandsGroup];

            var index = thousandsGroup - FirstLetterGroup;
            var length = MinLetterCount;
            var blockSize = (long)LettersAlphabet * LettersAlphabet;
            while (index >= blockSize)
            {
                index -= blockSize;
                length++;
                blockSize *= LettersAlphabet;
            }

            var letters = new char[length];
            for (var position = length - 1; position >= 0; position--)
            {
                letters[position] = (char)('a' + index % LettersAlphabet);
                index /= LettersAlphabet;
            }
            return new string(letters);
        }

        private static string FormatLetters(BigNumber value, NumberFormatSettings settings)
        {
            var (mantissa, group) = Grouped(value, settings.Decimals);
            if (group == 0)
                return Decimal(mantissa, settings);
            return Decimal(mantissa, settings) + LetterSuffix(group);
        }

        private static string FormatEngineering(BigNumber value, NumberFormatSettings settings)
        {
            var (mantissa, group) = Grouped(value, settings.Decimals);
            if (group == 0)
                return Decimal(mantissa, settings);
            return Decimal(mantissa, settings) + "e" + (group * 3).ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatScientific(BigNumber value, NumberFormatSettings settings)
        {
            if (value.Exponent < 3)
                return Decimal(value.ToDouble(), settings);

            var mantissa = RoundDown(value.Mantissa, settings.Decimals);
            var exponent = value.Exponent;
            return Decimal(mantissa, settings) + "e" + exponent.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatFull(BigNumber value, NumberFormatSettings settings)
        {
            var whole = value.Exponent < 0 ? 0m : decimal.Floor((decimal)value.Mantissa * PowerOfTen(value.Exponent));
            var digits = whole.ToString(CultureInfo.InvariantCulture);
            var builder = new StringBuilder(digits.Length + digits.Length / 3 * settings.ThousandsSeparator.Length);
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    builder.Append(settings.ThousandsSeparator);
                builder.Append(digits[i]);
            }
            return builder.ToString();
        }

        private static decimal PowerOfTen(long exponent)
        {
            var result = 1m;
            for (var i = 0L; i < exponent; i++)
                result *= 10m;
            return result;
        }

        private static (double Mantissa, long Group) Grouped(BigNumber value, int decimals)
        {
            var group = value.Exponent / 3;
            var mantissa = RoundDown(value.Mantissa * System.Math.Pow(10d, value.Exponent - group * 3), decimals);
            return (mantissa, group);
        }

        private static double RoundDown(double value, int decimals)
        {
            var scale = System.Math.Pow(10d, decimals);
            return System.Math.Floor(value * scale + 1e-9) / scale;
        }

        private static string Decimal(double value, NumberFormatSettings settings)
        {
            var text = RoundDown(value, settings.Decimals).ToString("0." + new string('#', settings.Decimals), CultureInfo.InvariantCulture);
            return settings.DecimalSeparator == "." ? text : text.Replace(".", settings.DecimalSeparator);
        }
    }
}
