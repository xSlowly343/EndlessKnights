using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Json;

namespace Game.Core.Diagnostics
{
    public static class ErrorReportJson
    {
        public static string Write(ErrorReport report)
        {
            var lines = new JsonValue[report.Lines.Count];
            for (var i = 0; i < lines.Length; i++)
                lines[i] = JsonValue.Of(report.Lines[i]);
            var fields = new Dictionary<string, JsonValue>
            {
                ["schema"] = JsonValue.Of(report.Schema),
                ["kind"] = JsonValue.Of(report.Kind),
                ["game"] = JsonValue.Of(report.GameVersion),
                ["os"] = JsonValue.Of(report.Os),
                ["createdUtc"] = JsonValue.Of(report.CreatedUtc),
                ["message"] = JsonValue.Of(report.Message),
                ["stackTrace"] = JsonValue.Of(report.StackTrace),
                ["lines"] = JsonValue.Array(lines)
            };
            return JsonWriter.Write(JsonValue.Object(fields)) + "\n";
        }

        public static ErrorReport Read(string text, string fileName)
        {
            var root = JsonParser.Parse(text, fileName);
            var lines = new List<string>();
            foreach (var line in Field(root, "lines", JsonKind.Array, fileName).Items)
                lines.Add(Require(line, JsonKind.String, fileName, "lines").Text);
            return new ErrorReport
            {
                Schema = (int)Field(root, "schema", JsonKind.Number, fileName).Number,
                Kind = Text(root, "kind", fileName),
                GameVersion = Text(root, "game", fileName),
                Os = Text(root, "os", fileName),
                CreatedUtc = Text(root, "createdUtc", fileName),
                Message = Text(root, "message", fileName),
                StackTrace = Text(root, "stackTrace", fileName),
                Lines = lines
            };
        }

        private static string Text(JsonValue root, string name, string fileName) => Field(root, name, JsonKind.String, fileName).Text;

        private static JsonValue Field(JsonValue root, string name, JsonKind kind, string fileName)
        {
            if (root.Kind != JsonKind.Object || !root.Fields.TryGetValue(name, out var value))
                throw new ContentException(fileName, name, "is missing");
            return Require(value, kind, fileName, name);
        }

        private static JsonValue Require(JsonValue value, JsonKind kind, string fileName, string name)
        {
            if (value.Kind != kind)
                throw new ContentException(fileName, name, $"expected {kind}");
            return value;
        }
    }
}
