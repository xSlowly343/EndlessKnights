namespace Game.Core.Content
{
    public sealed class ColossusSeedRules
    {
        public const string FileName = "colossus_seed.json";
        public const int SupportedVersion = 1;
        private const double SecondsPerDay = 86400;

        public string Seed { get; private set; } = string.Empty;
        public string Bloom { get; private set; } = string.Empty;
        public double CooldownSeconds { get; private set; }
        public double GrowSeconds { get; private set; }
        public double NeedKeys { get; private set; }
        public double NeedClicks { get; private set; }
        public double NeedMouseMeters { get; private set; }
        public double NeedIdleSeconds { get; private set; }
        public double BloomEverySeconds { get; private set; }
        public int MaxBlooms { get; private set; }

        public static ColossusSeedRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var needs = root.Field("needs");
            return new ColossusSeedRules
            {
                Seed = root.Field("seed").AsString(),
                Bloom = root.Field("bloom").AsString(),
                CooldownSeconds = root.Field("cooldownDays").AsNumber(0, 365) * SecondsPerDay,
                GrowSeconds = root.Field("growDays").AsNumber(0.01, 365) * SecondsPerDay,
                NeedKeys = needs.Field("keys").AsNumber(0, 10_000_000),
                NeedClicks = needs.Field("clicks").AsNumber(0, 10_000_000),
                NeedMouseMeters = needs.Field("mouseMeters").AsNumber(0, 10_000_000),
                NeedIdleSeconds = needs.Field("idleSeconds").AsNumber(0, 10_000_000),
                BloomEverySeconds = root.Field("bloomEveryHours").AsNumber(1, 10_000) * 3600,
                MaxBlooms = root.Field("maxBlooms").AsInt(1, 1000)
            };
        }
    }
}
