using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Content.Json;

namespace Game.Core.Telemetry
{
    public static class TelemetryJson
    {
        public const string StateFileName = "state.json";
        public const int StateVersion = 1;
        public const int InstallIdLength = 32;

        public static string Write(TelemetryPacket packet) => JsonWriter.Write(Encode(packet)) + "\n";

        public static TelemetryPacket ReadPacket(string text, string fileName) => Decode(ContentNode.Root(text, fileName));

        public static string Write(TelemetryState state)
        {
            var fields = new Dictionary<string, JsonValue>
            {
                ["version"] = JsonValue.Of(StateVersion),
                ["installId"] = JsonValue.Of(state.InstallId),
                ["firstLocalDay"] = JsonValue.Of(state.FirstLocalDay),
                ["returnDays"] = Days(state.ReturnDays),
                ["sessions"] = JsonValue.Of(state.Sessions),
                ["firstPrestigeSeconds"] = Optional(state.FirstPrestigeSeconds),
                ["sawNoPrestige"] = JsonValue.Of(state.SawNoPrestige),
                ["openSession"] = state.OpenSession == null ? JsonValue.Null() : Encode(state.OpenSession)
            };
            return JsonWriter.Write(JsonValue.Object(fields)) + "\n";
        }

        public static TelemetryState ReadState(string text)
        {
            var root = ContentNode.Root(text, "telemetry/" + StateFileName);
            var version = root.Field("version").AsInt(1);
            if (version > StateVersion)
                throw root.Field("version").Error($"version {version} is newer than {StateVersion}");
            var state = new TelemetryState(InstallId(root.Field("installId")), root.Field("firstLocalDay").AsLong());
            foreach (var day in root.Field("returnDays").Items())
                state.MarkDay(state.FirstLocalDay + day.AsInt(0, TelemetryState.MaxTrackedDay));
            state.Sessions = root.Field("sessions").AsInt(0);
            state.FirstPrestigeSeconds = OptionalLong(root.Field("firstPrestigeSeconds"));
            state.SawNoPrestige = root.Field("sawNoPrestige").AsBool();
            var open = root.Field("openSession");
            state.OpenSession = open.IsNull ? null : Decode(open);
            return state;
        }

        public static bool IsInstallId(string text)
        {
            if (text.Length != InstallIdLength)
                return false;
            foreach (var character in text)
            {
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f'))
                    return false;
            }
            return true;
        }

        private static JsonValue Encode(TelemetryPacket packet) => JsonValue.Object(new Dictionary<string, JsonValue>
        {
            ["schema"] = JsonValue.Of(packet.Schema),
            ["installId"] = JsonValue.Of(packet.InstallId),
            ["game"] = JsonValue.Of(packet.GameVersion),
            ["session"] = JsonValue.Of(packet.Session),
            ["closed"] = JsonValue.Of(packet.Closed),
            ["sessionSeconds"] = JsonValue.Of(packet.SessionSeconds),
            ["keysPerMinute"] = JsonValue.Of(packet.KeysPerMinute),
            ["clicksPerMinute"] = JsonValue.Of(packet.ClicksPerMinute),
            ["mouseMeters"] = JsonValue.Of(packet.MouseMeters),
            ["totalPlaySeconds"] = JsonValue.Of(packet.TotalPlaySeconds),
            ["firstPrestigeSeconds"] = Optional(packet.FirstPrestigeSeconds),
            ["prestiges"] = JsonValue.Of(packet.Prestiges),
            ["recordStage"] = JsonValue.Of(packet.RecordStage),
            ["character"] = JsonValue.Of(packet.Character),
            ["daysSinceFirst"] = JsonValue.Of(packet.DaysSinceFirst),
            ["returnDays"] = Days(packet.ReturnDays)
        });

        private static TelemetryPacket Decode(ContentNode node)
        {
            var days = new List<int>();
            foreach (var day in node.Field("returnDays").Items())
                days.Add(day.AsInt(0, TelemetryState.MaxTrackedDay));
            return new TelemetryPacket
            {
                Schema = node.Field("schema").AsInt(1),
                InstallId = InstallId(node.Field("installId")),
                GameVersion = node.Field("game").AsString(),
                Session = node.Field("session").AsInt(0),
                Closed = node.Field("closed").AsBool(),
                SessionSeconds = node.Field("sessionSeconds").AsLong(0),
                KeysPerMinute = node.Field("keysPerMinute").AsNumber(0, double.MaxValue),
                ClicksPerMinute = node.Field("clicksPerMinute").AsNumber(0, double.MaxValue),
                MouseMeters = node.Field("mouseMeters").AsNumber(0, double.MaxValue),
                TotalPlaySeconds = node.Field("totalPlaySeconds").AsLong(0),
                FirstPrestigeSeconds = OptionalLong(node.Field("firstPrestigeSeconds")),
                Prestiges = node.Field("prestiges").AsInt(0),
                RecordStage = node.Field("recordStage").AsLong(1),
                Character = node.Field("character").AsString(),
                DaysSinceFirst = node.Field("daysSinceFirst").AsInt(0, TelemetryState.MaxTrackedDay),
                ReturnDays = days
            };
        }

        private static string InstallId(ContentNode node)
        {
            var text = node.AsString();
            if (!IsInstallId(text))
                throw node.Error("an install id is 32 lowercase hex digits");
            return text;
        }

        private static JsonValue Days(IReadOnlyList<int> days)
        {
            var items = new JsonValue[days.Count];
            for (var i = 0; i < days.Count; i++)
                items[i] = JsonValue.Of(days[i]);
            return JsonValue.Array(items);
        }

        private static JsonValue Optional(long? value) => value.HasValue ? JsonValue.Of(value.Value) : JsonValue.Null();

        private static long? OptionalLong(ContentNode node) => node.IsNull ? (long?)null : node.AsLong(0);
    }
}
