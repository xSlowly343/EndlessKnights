using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Game.Core.Content.Json
{
    public static class JsonWriter
    {
        private const string Indent = "  ";

        public static string Write(JsonValue value, int depth = 0)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value, depth);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, JsonValue value, int depth)
        {
            switch (value.Kind)
            {
                case JsonKind.Null:
                    builder.Append("null");
                    break;
                case JsonKind.Boolean:
                    builder.Append(value.Boolean ? "true" : "false");
                    break;
                case JsonKind.Number:
                    WriteNumber(builder, value.Number);
                    break;
                case JsonKind.String:
                    WriteString(builder, value.Text);
                    break;
                case JsonKind.Array:
                    WriteArray(builder, value.Items, depth);
                    break;
                default:
                    WriteObject(builder, value.Fields, depth);
                    break;
            }
        }

        private static void WriteNumber(StringBuilder builder, double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException($"JSON has no {number}", nameof(number));
            builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (var character in text)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private static void WriteArray(StringBuilder builder, IReadOnlyList<JsonValue> items, int depth)
        {
            if (items.Count == 0)
            {
                builder.Append("[]");
                return;
            }
            builder.Append('[');
            for (var i = 0; i < items.Count; i++)
            {
                NewLine(builder, depth + 1);
                WriteValue(builder, items[i], depth + 1);
                if (i < items.Count - 1)
                    builder.Append(',');
            }
            NewLine(builder, depth);
            builder.Append(']');
        }

        private static void WriteObject(StringBuilder builder, IReadOnlyDictionary<string, JsonValue> fields, int depth)
        {
            if (fields.Count == 0)
            {
                builder.Append("{}");
                return;
            }
            var names = new List<string>(fields.Keys);
            names.Sort(StringComparer.Ordinal);
            builder.Append('{');
            for (var i = 0; i < names.Count; i++)
            {
                NewLine(builder, depth + 1);
                WriteString(builder, names[i]);
                builder.Append(": ");
                WriteValue(builder, fields[names[i]], depth + 1);
                if (i < names.Count - 1)
                    builder.Append(',');
            }
            NewLine(builder, depth);
            builder.Append('}');
        }

        private static void NewLine(StringBuilder builder, int depth)
        {
            builder.Append('\n');
            for (var i = 0; i < depth; i++)
                builder.Append(Indent);
        }
    }
}
