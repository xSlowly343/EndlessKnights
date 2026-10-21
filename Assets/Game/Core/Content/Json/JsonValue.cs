using System.Collections.Generic;

namespace Game.Core.Content.Json
{
    public sealed class JsonValue
    {
        private static readonly IReadOnlyList<JsonValue> NoItems = new JsonValue[0];
        private static readonly IReadOnlyDictionary<string, JsonValue> NoFields = new Dictionary<string, JsonValue>();

        private JsonValue(JsonKind kind, bool boolean = false, double number = 0, string? text = null,
            IReadOnlyList<JsonValue>? items = null, IReadOnlyDictionary<string, JsonValue>? fields = null)
        {
            Kind = kind;
            Boolean = boolean;
            Number = number;
            Text = text ?? string.Empty;
            Items = items ?? NoItems;
            Fields = fields ?? NoFields;
        }

        public JsonKind Kind { get; }
        public bool Boolean { get; }
        public double Number { get; }
        public string Text { get; }
        public IReadOnlyList<JsonValue> Items { get; }
        public IReadOnlyDictionary<string, JsonValue> Fields { get; }

        public static JsonValue Null() => new JsonValue(JsonKind.Null);
        public static JsonValue Of(bool value) => new JsonValue(JsonKind.Boolean, boolean: value);
        public static JsonValue Of(double value) => new JsonValue(JsonKind.Number, number: value);
        public static JsonValue Of(string value) => new JsonValue(JsonKind.String, text: value);
        public static JsonValue Array(IReadOnlyList<JsonValue> items) => new JsonValue(JsonKind.Array, items: items);
        public static JsonValue Object(IReadOnlyDictionary<string, JsonValue> fields) => new JsonValue(JsonKind.Object, fields: fields);
    }
}
