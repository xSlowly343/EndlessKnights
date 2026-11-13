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

    public enum TownResourceKind
    {
        Labour,
        Raw,
        Produce,
        Craft
    }

    public sealed class BuildResourceDef
    {
        public BuildResourceDef(string id, string nameRu, string nameEn, long dailyCap, TownResourceKind kind, string guild)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            DailyCap = dailyCap;
            Kind = kind;
            Guild = guild;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long DailyCap { get; }
        public TownResourceKind Kind { get; }
        public string Guild { get; }
        public double PointsPerUnit => TownResource.PointsPerDay / DailyCap;
    }

    public sealed class TownSiteDef
    {
        public TownSiteDef(string id, string nameRu, string nameEn, string resource, int fromStage, double perKill)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Resource = resource;
            FromStage = fromStage;
            PerKill = perKill;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Resource { get; }
        public int FromStage { get; }
        public double PerKill { get; }
    }

    public sealed class TownRewardTier
    {
        public TownRewardTier(string id, string nameRu, string nameEn, long points, int shards, int friendship)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Points = points;
            Shards = shards;
            Friendship = friendship;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public long Points { get; }
        public int Shards { get; }
        public int Friendship { get; }
    }

    public static class TownResource
    {
        public const double PointsPerDay = 1000;
        public const string Labour = "labour";
        public const string Herbs = "herbs";
        public const string Finds = "finds";
        public const string Potions = "potions";
        public const string Fish = "fish";
        public const string Metalwork = "metalwork";
        public const string Dishes = "dishes";
        public const string Relics = "relics";
        public const string Tinctures = "tinctures";
        public const string Wood = "wood";
        public const string Stone = "stone";
        public const string Ore = "ore";
        public const string Clay = "clay";
        public const string Salt = "salt";
        public const string Coal = "coal";
        public const string Skyglass = "skyglass";
        public const string RiftDust = "riftdust";

        public static readonly IReadOnlyList<string> Counted = new[]
        {
            Labour, Herbs, Finds, Potions, Fish, Metalwork, Dishes, Relics, Tinctures, Wood, Stone, Ore, Clay, Salt, Coal, Skyglass, RiftDust
        };

        public static TownResourceKind KindOf(string name) => name switch
        {
            "labour" => TownResourceKind.Labour,
            "raw" => TownResourceKind.Raw,
            "produce" => TownResourceKind.Produce,
            "craft" => TownResourceKind.Craft,
            _ => throw new ArgumentException($"unknown resource kind '{name}'")
        };
    }

    public sealed class GuildRules
    {
        public const string FileName = "guilds.json";
        public const int SupportedVersion = 1;

        private GuildRules(IReadOnlyList<long> levels, IReadOnlyList<GuildDef> guilds, IReadOnlyList<DistrictDef> districts, IReadOnlyList<string> startBuilt,
            IReadOnlyList<string> buildOrder, int voteDays, int openHours, IReadOnlyList<BuildResourceDef> resources, IReadOnlyList<TownSiteDef> sites,
            IReadOnlyList<TownRewardTier> rewards, int topSize, int topShards)
        {
            Sites = sites;
            Rewards = rewards;
            TopSize = topSize;
            TopShards = topShards;
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
        public IReadOnlyList<TownSiteDef> Sites { get; }
        public IReadOnlyList<TownRewardTier> Rewards { get; }
        public int TopSize { get; }
        public int TopShards { get; }
        public int MaxLevel => Levels.Count;

        public TownRewardTier? TierAt(long points)
        {
            TownRewardTier? reached = null;
            foreach (var tier in Rewards)
            {
                if (points >= tier.Points)
                    reached = tier;
            }
            return reached;
        }

        public TownRewardTier? TierAfter(long points)
        {
            foreach (var tier in Rewards)
            {
                if (points < tier.Points)
                    return tier;
            }
            return null;
        }

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
            var resources = ResourcesOf(build.Field("resources"), guilds);
            CheckNeeds(build.Field("needs"), order, resources);
            var sites = SitesOf(build.Field("sites"), resources);
            var rewards = RewardsOf(build.Field("rewards"));
            var top = build.Field("top");
            build.Field("gatherDays").AsInt(1);
            build.Field("activeDays").AsInt(1);
            build.Field("minPlayers").AsInt(1);
            return new GuildRules(levels, guilds, districts, startBuilt, order, build.Field("voteDays").AsInt(1), build.Field("openHours").AsInt(0), resources, sites,
                rewards, top.Field("size").AsInt(0), top.Field("shards").AsInt(0));
        }

        private static IReadOnlyList<TownSiteDef> SitesOf(ContentNode node, IReadOnlyList<BuildResourceDef> resources)
        {
            var sites = new List<TownSiteDef>();
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                foreach (var known in sites)
                {
                    if (known.Id == id)
                        throw item.Field("id").Error($"site '{id}' is listed twice");
                }
                var resource = item.Field("resource").AsString();
                var def = Find(resources, resource);
                if (def == null)
                    throw item.Field("resource").Error($"site '{id}' gathers unknown resource '{resource}'");
                if (def.Guild.Length > 0)
                    throw item.Field("resource").Error($"resource '{resource}' belongs to a guild hall, a site cannot gather it");
                var fromStage = item.Field("fromStage").AsInt(1);
                if (sites.Count > 0 && fromStage <= sites[sites.Count - 1].FromStage)
                    throw item.Field("fromStage").Error("each site opens later than the one before");
                var name = item.Field("name");
                sites.Add(new TownSiteDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), resource, fromStage, item.Field("perKill").AsNumber(0.0001, 100)));
            }
            foreach (var resource in resources)
            {
                if (resource.Kind != TownResourceKind.Raw)
                    continue;
                var gathered = false;
                foreach (var site in sites)
                    gathered |= site.Resource == resource.Id;
                if (!gathered)
                    throw node.Error($"raw resource '{resource.Id}' has no site to gather it");
            }
            return sites;
        }

        private static IReadOnlyList<TownRewardTier> RewardsOf(ContentNode node)
        {
            var tiers = new List<TownRewardTier>();
            foreach (var item in node.Items())
            {
                var points = item.Field("points").AsLong(1);
                if (tiers.Count > 0 && points <= tiers[tiers.Count - 1].Points)
                    throw item.Field("points").Error("each reward tier needs more points than the one before");
                var name = item.Field("name");
                tiers.Add(new TownRewardTier(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), points,
                    item.Field("shards").AsInt(0), item.Field("friendship").AsInt(0)));
            }
            return tiers;
        }

        private static BuildResourceDef? Find(IReadOnlyList<BuildResourceDef> resources, string id)
        {
            foreach (var resource in resources)
            {
                if (resource.Id == id)
                    return resource;
            }
            return null;
        }

        private static IReadOnlyList<BuildResourceDef> ResourcesOf(ContentNode node, IReadOnlyList<GuildDef> guilds)
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
                var kindName = item.Field("kind").AsString();
                if (kindName != "labour" && kindName != "raw" && kindName != "produce" && kindName != "craft")
                    throw item.Field("kind").Error($"unknown resource kind '{kindName}', expected labour, raw, produce or craft");
                var guild = item.HasField("guild") ? item.Field("guild").AsString() : string.Empty;
                if (guild.Length > 0)
                {
                    var known = false;
                    foreach (var entry in guilds)
                        known |= entry.Id == guild;
                    if (!known)
                        throw item.Field("guild").Error($"unknown guild '{guild}'");
                }
                resources.Add(new BuildResourceDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), item.Field("dailyCap").AsLong(1), TownResource.KindOf(kindName), guild));
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
                foreach (var resource in resources)
                {
                    if (resource.Guild == guild && perPlayerDay.HasField(resource.Id))
                        throw perPlayerDay.Field(resource.Id).Error($"hall '{guild}' cannot be built from what only it makes");
                }
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
