using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class DigSiteDef
    {
        public DigSiteDef(string biome, string bone, string nameRu, string nameEn)
        {
            Biome = biome;
            Bone = bone;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Biome { get; }
        public string Bone { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class DigRules
    {
        public const string FileName = "digs.json";
        public const int SupportedVersion = 1;
        public const int SiteCapacity = 16;

        public string Building { get; private set; } = string.Empty;
        public int MaxSites { get; private set; }
        public double BrushMeters { get; private set; }
        public double CrackMetersPerSecond { get; private set; }
        public double CrackLoss { get; private set; }
        public double MinQuality { get; private set; }
        public int BonesMin { get; private set; }
        public int BonesMax { get; private set; }
        public int SkeletonBones { get; private set; }
        public double ShardChance { get; private set; }
        public long ShardCount { get; private set; }
        public double DustChance { get; private set; }
        public long DustCount { get; private set; }
        public IReadOnlyList<DigSiteDef> Sites { get; private set; } = Array.Empty<DigSiteDef>();

        public DigSiteDef? Site(string biome)
        {
            for (var i = 0; i < Sites.Count; i++)
            {
                if (Sites[i].Biome == biome)
                    return Sites[i];
            }
            return null;
        }

        public static DigRules Load(string json, CampRules camp, IReadOnlyList<string> biomes)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var building = root.Field("building").AsString();
            if (camp.Building(building) == null)
                throw root.Field("building").Error($"unknown camp building '{building}'");
            var sites = new List<DigSiteDef>();
            foreach (var node in root.Field("sites").Items())
            {
                var biome = node.Field("biome").AsString();
                if (!Contains(biomes, biome))
                    throw node.Field("biome").Error($"unknown biome '{biome}'");
                foreach (var other in sites)
                {
                    if (other.Biome == biome)
                        throw node.Field("biome").Error($"biome '{biome}' has two dig sites");
                }
                var name = node.Field("name");
                sites.Add(new DigSiteDef(biome, node.Field("bone").AsString(), name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            if (sites.Count == 0)
                throw root.Field("sites").Error("digs need at least one site");
            var bones = root.Field("bones");
            var bonesMin = bones.Field("min").AsInt(1, 100);
            var bonesMax = bones.Field("max").AsInt(bonesMin, 100);
            var shards = root.Field("shards");
            var dust = root.Field("dust");
            return new DigRules
            {
                Building = building,
                MaxSites = root.Field("maxSites").AsInt(1, SiteCapacity),
                BrushMeters = root.Field("brushMeters").AsNumber(0.1, 10_000),
                CrackMetersPerSecond = root.Field("crackMetersPerSecond").AsNumber(0.1, 1000),
                CrackLoss = root.Field("crackLoss").AsNumber(0, 1),
                MinQuality = root.Field("minQuality").AsNumber(0, 1),
                BonesMin = bonesMin,
                BonesMax = bonesMax,
                SkeletonBones = root.Field("skeletonBones").AsInt(1, 10_000),
                ShardChance = shards.Field("chance").AsNumber(0, 1),
                ShardCount = shards.Field("count").AsInt(0, 10_000),
                DustChance = dust.Field("chance").AsNumber(0, 1),
                DustCount = dust.Field("count").AsInt(0, 10_000),
                Sites = sites
            };
        }

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (var item in list)
            {
                if (item == value)
                    return true;
            }
            return false;
        }
    }
}
