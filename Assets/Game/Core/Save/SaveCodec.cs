using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Json;
using Game.Core.Sim;

namespace Game.Core.Save
{
    public static class SaveCodec
    {
        public const string FileName = "save.json";
        public static int CurrentVersion => SaveMigrations.Default.CurrentVersion;

        private const string PayloadMarker = "\"payload\": ";

        public static string Encode(GameState state, ClockStamp clock)
        {
            var payload = JsonWriter.Write(Payload(CurrentVersion, clock, state), 1);
            return "{\n  \"checksum\": \"" + Checksum(payload) + "\",\n  " + PayloadMarker + payload + "\n}\n";
        }

        public static SaveFile Decode(string text, GameData data) => Decode(text, data, SaveMigrations.Default);

        public static SaveFile Decode(string text, GameData data, SaveMigrations migrations)
        {
            var root = ContentNode.Root(text, FileName);
            var expected = root.Field("checksum").AsString();
            if (!string.Equals(Checksum(PayloadText(text, root)), expected, StringComparison.Ordinal))
                throw root.Field("checksum").Error("does not match the saved data, the file is damaged or was edited");

            var stored = root.Field("payload");
            var version = stored.Field("version").AsInt(1);
            if (version > migrations.CurrentVersion)
                throw stored.Field("version").Error($"version {version} is newer than this game ({migrations.CurrentVersion})");

            var payload = new ContentNode(migrations.Upgrade(JsonParser.Parse(PayloadText(text, root), FileName), version), FileName, "payload");
            return new SaveFile(version, ReadClock(payload.Field("clock")), GameStateJson.Read(payload.Field("state"), data.Enemies, data.Gems, data.Weapons, data.Items, data.Slates));
        }

        private static JsonValue Payload(int version, ClockStamp clock, GameState state) =>
            JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["version"] = JsonValue.Of(version),
                ["savedAt"] = JsonValue.Of(clock.Reading.WallUnixMs),
                ["clock"] = WriteClock(clock),
                ["state"] = GameStateJson.Write(state)
            });

        private static JsonValue WriteClock(ClockStamp clock)
        {
            var fields = new Dictionary<string, JsonValue>
            {
                ["wallUnixMs"] = JsonValue.Of(clock.Reading.WallUnixMs),
                ["monotonicMs"] = JsonValue.Of(clock.Reading.MonotonicMs),
                ["bootId"] = JsonValue.Of(clock.Reading.BootId),
                ["highestWallUnixMs"] = JsonValue.Of(clock.HighestWallUnixMs)
            };
            if (clock.Reading.ServerUnixMs.HasValue)
                fields["serverUnixMs"] = JsonValue.Of(clock.Reading.ServerUnixMs.Value);
            return JsonValue.Object(fields);
        }

        private static ClockStamp ReadClock(ContentNode node)
        {
            long? server = node.HasField("serverUnixMs") ? node.Field("serverUnixMs").AsLong() : (long?)null;
            var reading = new ClockReading(node.Field("wallUnixMs").AsLong(), node.Field("monotonicMs").AsLong(0), node.Field("bootId").AsLong(), server);
            return new ClockStamp(reading, node.Field("highestWallUnixMs").AsLong());
        }

        private static string PayloadText(string text, ContentNode root)
        {
            var start = text.IndexOf(PayloadMarker, StringComparison.Ordinal);
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
                throw root.Error("payload is missing");
            return text.Substring(start + PayloadMarker.Length, end - start - PayloadMarker.Length).TrimEnd();
        }

        private static string Checksum(string payload)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                hex.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }
}
