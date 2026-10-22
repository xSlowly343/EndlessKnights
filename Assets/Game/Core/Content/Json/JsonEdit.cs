using System;
using System.Collections.Generic;

namespace Game.Core.Content.Json
{
    public static class JsonEdit
    {
        public static JsonValue Set(JsonValue target, string name, JsonValue value)
        {
            var fields = Copy(target);
            fields[name] = value;
            return JsonValue.Object(fields);
        }

        public static JsonValue Remove(JsonValue target, string name)
        {
            var fields = Copy(target);
            fields.Remove(name);
            return JsonValue.Object(fields);
        }

        public static JsonValue Rename(JsonValue target, string from, string to)
        {
            var fields = Copy(target);
            if (!fields.TryGetValue(from, out var value))
                return target;
            fields.Remove(from);
            fields[to] = value;
            return JsonValue.Object(fields);
        }

        public static JsonValue Update(JsonValue target, string name, Func<JsonValue, JsonValue> change)
        {
            if (!target.Fields.TryGetValue(name, out var value))
                return target;
            return Set(target, name, change(value));
        }

        private static Dictionary<string, JsonValue> Copy(JsonValue target)
        {
            if (target.Kind != JsonKind.Object)
                throw new ArgumentException("Only objects can be edited.", nameof(target));
            var fields = new Dictionary<string, JsonValue>(target.Fields.Count + 1, StringComparer.Ordinal);
            foreach (var pair in target.Fields)
                fields.Add(pair.Key, pair.Value);
            return fields;
        }
    }
}
