using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content.Json;

namespace Game.Core.Content
{
    public readonly struct ContentNode
    {
        private const double MaxExactInteger = 9007199254740992d;

        private readonly JsonValue _value;

        public ContentNode(JsonValue value, string fileName, string path)
        {
            _value = value;
            FileName = fileName;
            Path = path;
        }

        public string FileName { get; }
        public string Path { get; }

        public static ContentNode Root(string text, string fileName) => new ContentNode(JsonParser.Parse(text, fileName), fileName, string.Empty);

        public ContentNode Field(string name)
        {
            RequireKind(JsonKind.Object, "an object");
            if (!_value.Fields.TryGetValue(name, out var child))
                throw Error($"required field '{name}' is missing");
            return new ContentNode(child, FileName, Child(name));
        }

        public IReadOnlyCollection<string> FieldNames()
        {
            RequireKind(JsonKind.Object, "an object");
            return new List<string>(_value.Fields.Keys);
        }

        public bool HasField(string name) => _value.Kind == JsonKind.Object && _value.Fields.ContainsKey(name);

        public IReadOnlyList<ContentNode> Items()
        {
            RequireKind(JsonKind.Array, "an array");
            var items = new List<ContentNode>(_value.Items.Count);
            for (var i = 0; i < _value.Items.Count; i++)
                items.Add(new ContentNode(_value.Items[i], FileName, $"{Path}[{i}]"));
            return items;
        }

        public double AsNumber()
        {
            RequireKind(JsonKind.Number, "a number");
            return _value.Number;
        }

        public double AsNumber(double min, double max)
        {
            var number = AsNumber();
            if (number < min || number > max)
                throw Error($"{Invariant(number)} is outside the allowed range {Invariant(min)}..{Invariant(max)}");
            return number;
        }

        public int AsInt(int min = int.MinValue, int max = int.MaxValue)
        {
            var number = AsNumber(min, max);
            if (number != System.Math.Floor(number))
                throw Error($"expected a whole number, got {Invariant(number)}");
            return (int)number;
        }

        public long AsLong(long min = long.MinValue, long max = long.MaxValue)
        {
            var number = AsNumber();
            if (number != System.Math.Floor(number) || System.Math.Abs(number) > MaxExactInteger)
                throw Error($"expected a whole number, got {Invariant(number)}");
            if (number < min || number > max)
                throw Error($"{Invariant(number)} is outside the allowed range {Invariant(min)}..{Invariant(max)}");
            return (long)number;
        }

        public bool IsNull => _value.Kind == JsonKind.Null;

        public string AsString()
        {
            RequireKind(JsonKind.String, "a string");
            if (_value.Text.Length == 0)
                throw Error("must not be empty");
            return _value.Text;
        }

        public bool AsBool()
        {
            RequireKind(JsonKind.Boolean, "true or false");
            return _value.Boolean;
        }

        public ContentException Error(string problem) => new ContentException(FileName, Path, problem);

        private void RequireKind(JsonKind kind, string description)
        {
            if (_value.Kind != kind)
                throw Error($"expected {description}, got {Describe(_value.Kind)}");
        }

        private static string Invariant(double number) => number.ToString(CultureInfo.InvariantCulture);

        private string Child(string name) => string.IsNullOrEmpty(Path) ? name : $"{Path}.{name}";

        private static string Describe(JsonKind kind)
        {
            switch (kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Boolean: return "true/false";
                case JsonKind.Number: return "a number";
                case JsonKind.String: return "a string";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }
    }
}
