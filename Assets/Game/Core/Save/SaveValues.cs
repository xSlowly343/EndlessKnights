using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Math;

namespace Game.Core.Save
{
    public static class SaveValues
    {
        public static JsonValue Of(BigNumber value) =>
            JsonValue.Of(value.Mantissa.ToString("R", CultureInfo.InvariantCulture) + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture));

        public static JsonValue Of(ulong value) => JsonValue.Of(value.ToString("x16", CultureInfo.InvariantCulture));

        public static BigNumber ReadBigNumber(ContentNode node)
        {
            var text = node.AsString();
            var split = text.IndexOf('e');
            if (split <= 0
                || !double.TryParse(text.Substring(0, split), NumberStyles.Float, CultureInfo.InvariantCulture, out var mantissa)
                || !long.TryParse(text.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exponent)
                || double.IsNaN(mantissa) || double.IsInfinity(mantissa))
                throw node.Error($"'{text}' is not a number like 1.5e3");
            return BigNumber.Create(mantissa, exponent);
        }

        public static ulong ReadULong(ContentNode node)
        {
            var text = node.AsString();
            if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                throw node.Error($"'{text}' is not a hexadecimal number");
            return value;
        }

        public static TEnum ReadEnum<TEnum>(ContentNode node, IReadOnlyDictionary<string, TEnum> names)
        {
            var text = node.AsString();
            if (!names.TryGetValue(text, out var value))
                throw node.Error($"unknown value '{text}', expected {string.Join(", ", names.Keys)}");
            return value;
        }

        public static string NameOf<TEnum>(TEnum value, IReadOnlyDictionary<string, TEnum> names) where TEnum : struct, Enum
        {
            foreach (var pair in names)
            {
                if (pair.Value.Equals(value))
                    return pair.Key;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "No save name for this value.");
        }
    }
}
