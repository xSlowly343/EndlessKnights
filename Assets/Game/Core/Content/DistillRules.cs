namespace Game.Core.Content
{
    public sealed class DistillRules
    {
        public const string FileName = "distill.json";
        public const int SupportedVersion = 1;

        public string Building { get; private set; } = string.Empty;
        public string Guild { get; private set; } = string.Empty;
        public ModuleMaterial Fuel { get; private set; }
        public double HeatMeters { get; private set; }
        public double PressurePerSecond { get; private set; }
        public double Vent { get; private set; }
        public double Edge { get; private set; }
        public double MiracleChance { get; private set; }
        public double EdgeMiracleChance { get; private set; }
        public double BurstMiracleChance { get; private set; }
        public double TinctureLasting { get; private set; }
        public double MiracleLasting { get; private set; }

        public static DistillRules Load(string json, CampRules camp)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var building = root.Field("building").AsString();
            if (camp.Building(building) == null)
                throw root.Field("building").Error($"unknown camp building '{building}'");
            var fuel = root.Field("fuel");
            var tincture = root.Field("tinctureLasting").AsNumber(1, 10);
            var miracle = root.Field("miracleLasting").AsNumber(tincture, 10);
            return new DistillRules
            {
                Building = building,
                Guild = root.Field("guild").AsString(),
                Fuel = new ModuleMaterial(fuel.Field("item").AsString(), fuel.Field("count").AsInt(0, 1000)),
                HeatMeters = root.Field("heatMeters").AsNumber(0.1, 10_000),
                PressurePerSecond = root.Field("pressurePerSecond").AsNumber(0.001, 1),
                Vent = root.Field("vent").AsNumber(0.01, 1),
                Edge = root.Field("edge").AsNumber(0, 1),
                MiracleChance = root.Field("miracleChance").AsNumber(0, 1),
                EdgeMiracleChance = root.Field("edgeMiracleChance").AsNumber(0, 1),
                BurstMiracleChance = root.Field("burstMiracleChance").AsNumber(0, 1),
                TinctureLasting = tincture,
                MiracleLasting = miracle
            };
        }
    }
}
