using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Content;
using Game.Core.Content.Json;

namespace Game.Bootstrap
{
    public sealed class OnlineIdentity
    {
        public const string FileName = "online.json";
        private const ulong PlayerNumberBase = 76561190000000000UL;

        private OnlineIdentity(string deviceId, ulong playerNumber)
        {
            DeviceId = deviceId;
            PlayerNumber = playerNumber;
        }

        public string DeviceId { get; }
        public ulong PlayerNumber { get; }

        public static OnlineIdentity LoadOrCreate(string folder)
        {
            var path = Path.Combine(folder, FileName);
            try
            {
                if (File.Exists(path))
                {
                    var root = ContentNode.Root(File.ReadAllText(path), FileName);
                    return new OnlineIdentity(root.Field("deviceId").AsString(), ulong.Parse(root.Field("playerNumber").AsString(), System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            catch (Exception error) when (error is ContentException || error is FormatException || error is OverflowException || error is IOException)
            {
            }
            return Write(folder, new OnlineIdentity(NewDeviceId(), PlayerNumberBase + (ulong)new Random().Next(1, 999999999)));
        }

        public static string NewDeviceId() => "rw-" + Guid.NewGuid().ToString("N");

        public static OnlineIdentity MoveTo(string folder, OnlineIdentity current, string deviceId) => Write(folder, new OnlineIdentity(deviceId, current.PlayerNumber));

        private static OnlineIdentity Write(string folder, OnlineIdentity created)
        {
            var path = Path.Combine(folder, FileName);
            Directory.CreateDirectory(folder);
            var json = JsonWriter.Write(JsonValue.Object(new Dictionary<string, JsonValue>
            {
                ["deviceId"] = JsonValue.Of(created.DeviceId),
                ["playerNumber"] = JsonValue.Of(created.PlayerNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
            }));
            File.WriteAllText(path, json + "\n");
            return created;
        }
    }
}
