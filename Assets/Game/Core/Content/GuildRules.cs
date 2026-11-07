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

    public sealed class GuildRules
    {
        public const string FileName = "guilds.json";
        public const int SupportedVersion = 1;

        private GuildRules(IReadOnlyList<long> levels, IReadOnlyList<GuildDef> guilds, IReadOnlyList<DistrictDef> districts, IReadOnlyList<string> startBuilt,
            IReadOnlyList<string> buildOrder, long dailyCap)
        {
            Levels = levels;
            Guilds = guilds;
            Districts = districts;
            StartBuilt = startBuilt;
            BuildOrder = buildOrder;
            DailyCap = dailyCap;
        }

        public IReadOnlyList<long> Levels { get; }
        public IReadOnlyList<GuildDef> Guilds { get; }
        public IReadOnlyList<DistrictDef> Districts { get; }
        public IReadOnlyList<string> StartBuilt { get; }
        public IReadOnlyList<string> BuildOrder { get; }
        public long DailyCap { get; }
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
            return new GuildRules(levels, guilds, districts, startBuilt, order, build.Field("dailyCap").AsLong(1));
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
