namespace Game.Core.Content
{
    public sealed class InputCaps
    {
        public const string FileName = "input_caps.json";
        public const int SupportedVersion = 1;

        private InputCaps(RateCap keys, RateCap clicks, double maxMousePxPerSecond, InputDetection detection)
        {
            Keys = keys;
            Clicks = clicks;
            MaxMousePxPerSecond = maxMousePxPerSecond;
            Detection = detection;
        }

        public RateCap Keys { get; }
        public RateCap Clicks { get; }
        public double MaxMousePxPerSecond { get; }
        public InputDetection Detection { get; }

        public static InputCaps Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");

            return new InputCaps(
                RateCap.Read(root.Field("keys")),
                RateCap.Read(root.Field("clicks")),
                root.Field("mouse").Field("maxPxPerSecond").AsNumber(1, 1e6),
                InputDetection.Read(root.Field("detection")));
        }
    }
}
