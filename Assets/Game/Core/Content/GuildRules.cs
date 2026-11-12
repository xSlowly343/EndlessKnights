using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class GuildDef
    {
        public GuildDef(string id, string nameRu, string nameEn, string master, string district)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Master = master;
            District = district;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Master { get; }
        public string District { get; }
    }

    public sealed class DistrictDef
    {
        public DistrictDef(string id, string nameRu, string nameEn, string biome, bool observatory)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Biome = biome;
            Observatory = observatory;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Biome { get; }
        public bool Observatory { get; }
    }

    public sealed class BuildResourceDef
    {
        public BuildResourceDef(string id, string nameRu, string nameEn, long dailyCap)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            DailyCap = dailyCap;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long DailyCap { get; }
    }

    public static class TownResource
    {
        public const string Labour = "labour";
        public const string Herbs = "herbs";
        public const string Finds = "finds";

        public static readonly IReadOnlyList<string> Counted = new[] { Labour, Herbs, Finds };
    }

    public sealed class GuildRules
    {
        public const string FileName = "guilds.json";
        public const int SupportedVersion = 1;

        private GuildRules(IReadOnlyList<long> levels, IReadOnlyList<GuildDef> guilds, IReadOnlyList<DistrictDef> districts, IReadOnlyList<string> startBuilt,
            IReadOnlyList<string> buildOrder, int voteDays, int openHours, IReadOnlyList<BuildResourceDef> resources)
        {
            Levels = levels;
            Guilds = guilds;
            Districts = districts;
            StartBuilt = startBuilt;
            BuildOrder = buildOrder;
            VoteDays = voteDays;
            OpenHours = openHours;
            Resources = resources;
        }

        public IReadOnlyList<long> Levels { get; }
        public IReadOnlyList<GuildDef> Guilds { get; }
        public IReadOnlyList<DistrictDef> Districts { get; }
        public IReadOnlyList<string> StartBuilt { get; }
        public IReadOnlyList<string> BuildOrder { get; }
        public int VoteDays { get; }
        public int OpenHours { get; }
        public IReadOnlyList<BuildResourceDef> Resources { get; }
        public int MaxLevel => Levels.Count;

        public int LevelAt(long reputation)
        {
            var level = 0;
            while (level < Levels.Count && reputation >= Levels[level])
                level++;
            return level;
        }

        public GuildDef? Guild(string id)
        {
            foreach (var guild in Guilds)
            {
                if (guild.Id == id)
                    return guild;
            }
            return null;
        }

        public BuildResourceDef? Resource(string id)
        {
            foreach (var resource in Resources)
            {
                if (resource.Id == id)
                    return resource;
            }
            return null;
        }

        public static GuildRules Load(string json, IReadOnlyCollection<string> biomes, IReadOnlyCollection<string> characters)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var levels = new List<long>();
            foreach (var item in root.Field("levels").Items())
            {
                var need = item.AsLong(0);
                if (levels.Count == 0 && need != 0)
                    throw item.Error("guild level 1 starts at 0 reputation");
                if (levels.Count > 0 && need <= levels[levels.Count - 1])
                    throw item.Error("each guild level needs more reputation than the one before");
                levels.Add(need);
            }
            var districts = DistrictsOf(root.Field("districts"), biomes);
            var guilds = GuildsOf(root.Field("guilds"), districts, characters);
            var build = root.Field("build");
            var startBuilt = IdsOf(build.Field("startBuilt"), guilds);
            var order = IdsOf(build.Field("order"), guilds);
            foreach (var guild in guilds)
            {
                var placed = (Contains(startBuilt, guild.Id) ? 1 : 0) + (Contains(order, guild.Id) ? 1 : 0);
                if (placed != 1)
                    throw build.Error($"guild '{guild.Id}' must be either built from the start or in the build order, once");
            }
            var resources = ResourcesOf(build.Field("resources"));
            CheckNeeds(build.Field("needs"), order, resources);
            build.Field("gatherDays").AsInt(1);
            build.Field("activeDays").AsInt(1);
            build.Field("minPlayers").AsInt(1);
            return new GuildRules(levels, guilds, districts, startBuilt, order, build.Field("voteDays").AsInt(1), build.Field("openHours").AsInt(0), resources);
        }

        private static IReadOnlyList<BuildResourceDef> ResourcesOf(ContentNode node)
        {
            var resources = new List<BuildResourceDef>();
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!Contains(TownResource.Counted, id))
                    throw item.Field("id").Error($"the game does not count resource '{id}'");
                foreach (var known in resources)
                {
                    if (known.Id == id)
                        throw item.Field("id").Error($"resource '{id}' is listed twice");
                }
                var name = item.Field("name");
                resources.Add(new BuildResourceDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), item.Field("dailyCap").AsLong(1)));
            }
            if (resources.Count == 0)
                throw node.Error("the town needs resources to build from");
            return resources;
        }

        private static void CheckNeeds(ContentNode node, IReadOnlyList<string> order, IReadOnlyList<BuildResourceDef> resources)
        {
            var seen = new List<string>();
            foreach (var item in node.Items())
            {
                var guild = item.Field("guild").AsString();
                if (!Contains(order, guild))
                    throw item.Field("guild").Error($"guild '{guild}' is not in the build order");
                if (Contains(seen, guild))
                    throw item.Field("guild").Error($"guild '{guild}' is listed twice");
                seen.Add(guild);
                var perPlayerDay = item.Field("perPlayerDay");
                var needed = 0;
                foreach (var resource in resources)
                {
                    if (!perPlayerDay.HasField(resource.Id))
                        continue;
                    perPlayerDay.Field(resource.Id).AsLong(1);
                    needed++;
                }
                if (needed == 0)
                    throw perPlayerDay.Error($"guild '{guild}' needs at least one known resource");
            }
            foreach (var guild in order)
            {
                if (!Contains(seen, guild))
                    throw node.Error($"guild '{guild}' has no resources to gather");
            }
        }

        private static IReadOnlyList<DistrictDef> DistrictsOf(ContentNode node, IReadOnlyCollection<string> biomes)
        {
            var districts = new List<DistrictDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"district '{id}' is listed twice");
                var biome = item.HasField("biome") ? item.Field("biome").AsString() : string.Empty;
                if (biome.Length > 0 && !Contains(biomes, biome))
                    throw item.Field("biome").Error($"unknown biome '{biome}'");
                if (districts.Count == 0 && biome.Length > 0)
                    throw item.Error("the first district is open from the start and has no biome");
                var name = item.Field("name");
                districts.Add(new DistrictDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), biome,
                    item.HasField("observatory") && item.Field("observatory").AsBool()));
            }
            if (districts.Count == 0)
                throw node.Error("the town needs districts");
            return districts;
        }

        private static IReadOnlyList<GuildDef> GuildsOf(ContentNode node, IReadOnlyList<DistrictDef> districts, IReadOnlyCollection<string> characters)
        {
            var guilds = new List<GuildDef>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (!ids.Add(id))
                    throw item.Field("id").Error($"guild '{id}' is listed twice");
                var master = item.Field("master").AsString();
                if (!Contains(characters, master))
                    throw item.Field("master").Error($"master '{master}' is not a character");
                var district = item.Field("district").AsString();
                var known = false;
                foreach (var entry in districts)
                    known |= entry.Id == district;
                if (!known)
                    throw item.Field("district").Error($"unknown district '{district}'");
                var name = item.Field("name");
                guilds.Add(new GuildDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), master, district));
            }
            return guilds;
        }

        private static IReadOnlyList<string> IdsOf(ContentNode node, IReadOnlyList<GuildDef> guilds)
        {
            var ids = new List<string>();
            foreach (var item in node.Items())
            {
                var id = item.AsString();
                var known = false;
                foreach (var guild in guilds)
                    known |= guild.Id == id;
                if (!known)
                    throw item.Error($"unknown guild '{id}'");
                if (Contains(ids, id))
                    throw item.Error($"guild '{id}' is listed twice");
                ids.Add(id);
            }
            return ids;
        }

        private static bool Contains(IEnumerable<string> list, string id)
        {
            foreach (var entry in list)
            {
                if (entry == id)
                    return true;
            }
            return false;
        }
    }
}
