using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class ForgeRules
    {
        public const string FileName = "forge.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, string> _ores = new Dictionary<string, string>(StringComparer.Ordinal);

        public string Building { get; private set; } = string.Empty;
        public int SmeltBase { get; private set; }
        public int SmeltPerRarity { get; private set; }
        public int TemperOre { get; private set; }
        public int Hammers { get; private set; }
        public double MaxTemper { get; private set; }
        public double StepInZone { get; private set; }
        public double StepOutside { get; private set; }
        public double HeatPerMeter { get; private set; }
        public double HeatDecayPerSecond { get; private set; }
        public double ZoneFrom { get; private set; }
        public double ZoneTo { get; private set; }

        public string OreOf(string biome) => _ores.TryGetValue(biome, out var ore) ? ore : string.Empty;

        public bool InZone(double heat) => heat >= ZoneFrom && heat <= ZoneTo;

        public static ForgeRules Load(string json, CampRules camp, IReadOnlyList<string> biomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var building = root.Field("building").AsString();
            if (camp.Building(building) == null)
                throw root.Field("building").Error($"unknown building '{building}'");
            var smelt = root.Field("smelt");
            var temper = root.Field("temper");
            var zoneFrom = temper.Field("zoneFrom").AsNumber(0, 1);
            var max = temper.Field("max").AsNumber(0, 0.05);
            var rules = new ForgeRules
            {
                Building = building,
                SmeltBase = smelt.Field("base").AsInt(0, 1000),
                SmeltPerRarity = smelt.Field("perRarity").AsInt(0, 1000),
                TemperOre = temper.Field("ore").AsInt(0, 1000),
                Hammers = temper.Field("hammers").AsInt(1, 100000),
                MaxTemper = max,
                StepInZone = temper.Field("stepInZone").AsNumber(0, max),
                StepOutside = temper.Field("stepOutside").AsNumber(0, max),
                HeatPerMeter = temper.Field("heatPerMeter").AsNumber(0, 100),
                HeatDecayPerSecond = temper.Field("heatDecayPerSecond").AsNumber(0, 10),
                ZoneFrom = zoneFrom,
                ZoneTo = temper.Field("zoneTo").AsNumber(zoneFrom, 1)
            };
            var ores = root.Field("ores");
            foreach (var biome in biomes)
            {
                if (!ores.HasField(biome))
                    continue;
                rules._ores[biome] = ores.Field(biome).AsString();
            }
            return rules;
        }
    }
}
