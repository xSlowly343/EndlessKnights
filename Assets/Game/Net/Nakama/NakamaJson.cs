using System;
using System.Collections.Generic;
using System.Text;
using Game.Core.Content;
using Game.Core.Content.Json;

namespace Game.Net.Nakama
{
    public static class NakamaJson
    {
        public static string Quote(string text) => JsonWriter.Write(JsonValue.Of(text));

        public static JsonValue Parse(string text)
        {
            try
            {
                return JsonParser.Parse(text, "nakama");
            }
            catch (ContentException error)
            {
                throw new NakamaException(0, "bad reply from the server: " + error.Message);
            }
        }

        public static JsonValue? Field(JsonValue value, string name) =>
            value.Kind == JsonKind.Object && value.Fields.TryGetValue(name, out var field) ? field : null;

        public static string Text(JsonValue value, string name)
        {
            var field = Field(value, name);
            return field != null && field.Kind == JsonKind.String ? field.Text : string.Empty;
        }

        public static double Number(JsonValue value, string name)
        {
            var field = Field(value, name);
            if (field == null)
                return 0;
            if (field.Kind == JsonKind.Number)
                return field.Number;
            return field.Kind == JsonKind.String && double.TryParse(field.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        public static bool Flag(JsonValue value, string name)
        {
            var field = Field(value, name);
            return field != null && field.Kind == JsonKind.Boolean && field.Boolean;
        }

        public static IReadOnlyList<JsonValue> Items(JsonValue value, string name)
        {
            var field = Field(value, name);
            return field != null && field.Kind == JsonKind.Array ? field.Items : Array.Empty<JsonValue>();
        }

        public static string Object(params (string Name, string Json)[] fields)
        {
            var builder = new StringBuilder("{");
            for (var i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(Quote(fields[i].Name)).Append(':').Append(fields[i].Json);
            }
            return builder.Append('}').ToString();
        }

        public static byte[] FromBase64Url(string text)
        {
            var padded = text.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            return Convert.FromBase64String(padded);
        }
    }
}
