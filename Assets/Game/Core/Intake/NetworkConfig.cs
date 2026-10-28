using Game.Core.Content;

namespace Game.Core.Intake
{
    public sealed class NetworkConfig
    {
        public const string FileName = "network.json";
        public const string TelemetryPath = "/intake/telemetry";
        public const string ErrorsPath = "/intake/errors";

        public static readonly NetworkConfig Offline = new NetworkConfig(string.Empty, string.Empty);

        private NetworkConfig(string serverUrl, string serverKey)
        {
            ServerUrl = serverUrl;
            ServerKey = serverKey;
        }

        public string ServerUrl { get; }
        public string ServerKey { get; }
        public bool HasServer => ServerUrl.Length > 0;
        public bool HasParty => HasServer && ServerKey.Length > 0;
        public string TelemetryUrl => ServerUrl + TelemetryPath;
        public string ErrorsUrl => ServerUrl + ErrorsPath;

        public static NetworkConfig Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != 1)
                throw root.Field("version").Error($"version {version} is not supported, expected 1");
            var node = root.Field("serverUrl");
            var url = node.IsNull ? string.Empty : node.AsString().TrimEnd('/');
            if (url.Length > 0 && !url.StartsWith("https://", System.StringComparison.Ordinal))
                throw node.Error("must be null or start with https://");
            var keyNode = root.Field("serverKey");
            var key = keyNode.IsNull ? string.Empty : keyNode.AsString();
            return new NetworkConfig(url, key);
        }
    }
}
