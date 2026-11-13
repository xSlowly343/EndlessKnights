using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class GuildPerk
    {
        public GuildPerk(int level, int hall, MuseumPerk perk, double value)
        {
            Level = level;
            Hall = hall;
            Perk = perk;
            Value = value;
        }

        public int Level { get; }
        public int Hall { get; }
        public MuseumPerk Perk { get; }
        public double Value { get; }
    }

    public sealed class ObservatoryBonus
    {
        public ObservatoryBonus(double fishWeight, double plantGrow, double plantMutation)
        {
            FishWeight = fishWeight;
            PlantGrow = plantGrow;
            PlantMutation = plantMutation;
        }

        public double FishWeight { get; }
        public double PlantGrow { get; }
        public double PlantMutation { get; }
    }

    public sealed class TownDecorDef
    {
        public TownDecorDef(string id, string nameRu, string nameEn, string frame)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Frame = frame;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Frame { get; }
    }

    public sealed class MasterTrialRules
    {
        public MasterTrialRules(double seconds, int stage, int heroLevel)
        {
            Seconds = seconds;
            Stage = stage;
            HeroLevel = heroLevel;
        }

        public double Seconds { get; }
        public int Stage { get; }
        public int HeroLevel { get; }
    }

    public sealed class GuildDef
    {
        public GuildDef(string id, string nameRu, string nameEn, string master, string district, IReadOnlyList<GuildPerk> perks)
        {
            Perks = perks;
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
        public IReadOnlyList<GuildPerk> Perks { get; }
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

    public sealed class TownBuildLevel
    {
        public TownBuildLevel(int afterBuilt, IReadOnlyDictionary<string, long> needs)
        {
            AfterBuilt = afterBuilt;
            Needs = needs;
        }

        public int AfterBuilt { get; }
        public IReadOnlyDictionary<string, long> Needs { get; }
    }

    public sealed class TownMasterLine
    {
        public TownMasterLine(string ru, string en)
        {
            Ru = ru;
            En = en;
        }

        public string Ru { get; }
        public string En { get; }
    }

    public sealed class TownBuildingDef
    {
        public TownBuildingDef(string id, string guild, string nameRu, string nameEn, string master, string district, IReadOnlyList<TownBuildLevel> levels,
            IReadOnlyList<TownMasterLine> lines)
        {
            Id = id;
            Guild = guild;
            NameRu = nameRu;
            NameEn = nameEn;
            Master = master;
            District = district;
            Levels = levels;
            Lines = lines;
            var ids = new string[levels.Count];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = GuildRules.ProjectId(id, i + 1);
            ProjectIds = ids;
        }

        public string Id { get; }
        public string Guild { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public string Master { get; }
        public string District { get; }
        public IReadOnlyList<TownBuildLevel> Levels { get; }
        public IReadOnlyList<TownMasterLine> Lines { get; }
        public IReadOnlyList<string> ProjectIds { get; }
        public int MaxLevel => Levels.Count;
    }

    public sealed class TownStageDef
    {
        public TownStageDef(string id, string nameRu, string nameEn, int built)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Built = built;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int Built { get; }
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
        public const string Marble = "marble";
        public const string Amber = "amber";
        public const string StarMetal = "starmetal";

        public static readonly IReadOnlyList<string> Counted = new[]
        {
            Labour, Herbs, Finds, Potions, Fish, Metalwork, Dishes, Relics, Tinctures, Wood, Stone, Ore, Clay, Salt, Coal, Skyglass, RiftDust, Marble, Amber, StarMetal
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

        public const char LevelMark = ':';

        private GuildRules(IReadOnlyList<long> levels, IReadOnlyList<GuildDef> guilds, IReadOnlyList<DistrictDef> districts, IReadOnlyList<TownBuildingDef> buildings,
            IReadOnlyList<TownStageDef> stages, int voteDays, int openHours, IReadOnlyList<BuildResourceDef> resources, IReadOnlyList<TownSiteDef> sites,
            IReadOnlyList<TownRewardTier> rewards, int topSize, int topShards, int stageShards, ObservatoryBonus observatory, MasterTrialRules trial, IReadOnlyList<TownDecorDef> decorations, long weeklyMinLabour)
        {
            Decorations = decorations;
            WeeklyMinLabour = weeklyMinLabour;
            Trial = trial;
            Observatory = observatory;
            Buildings = buildings;
            Stages = stages;
            StageShards = stageShards;
            Sites = sites;
            Rewards = rewards;
            TopSize = topSize;
            TopShards = topShards;
            Levels = levels;
            Guilds = guilds;
            Districts = districts;
            VoteDays = voteDays;
            OpenHours = openHours;
            Resources = resources;
        }

        public IReadOnlyList<long> Levels { get; }
        public IReadOnlyList<GuildDef> Guilds { get; }
        public IReadOnlyList<DistrictDef> Districts { get; }
        public IReadOnlyList<TownBuildingDef> Buildings { get; }
        public IReadOnlyList<TownStageDef> Stages { get; }
        public int StageShards { get; }
        public ObservatoryBonus Observatory { get; }
        public MasterTrialRules Trial { get; }
        public IReadOnlyList<TownDecorDef> Decorations { get; }
        public long WeeklyMinLabour { get; }

        public TownDecorDef? Decor(string id)
        {
            for (var i = 0; i < Decorations.Count; i++)
            {
                if (Decorations[i].Id == id)
                    return Decorations[i];
            }
            return null;
        }
        public int VoteDays { get; }
        public int OpenHours { get; }
        public IReadOnlyList<BuildResourceDef> Resources { get; }
        public IReadOnlyList<TownSiteDef> Sites { get; }
        public IReadOnlyList<TownRewardTier> Rewards { get; }
        public int TopSize { get; }
        public int TopShards { get; }
        public int MaxLevel => Levels.Count;

        public int ProjectCount
        {
            get
            {
                var count = 0;
                foreach (var building in Buildings)
                    count += building.MaxLevel;
                return count;
            }
        }

        public static string ProjectId(string building, int level) => level <= 1 ? building : building + LevelMark + level;

        public TownBuildingDef? Building(string id)
        {
            for (var i = 0; i < Buildings.Count; i++)
            {
                if (Buildings[i].Id == id)
                    return Buildings[i];
            }
            return null;
        }

        public TownBuildingDef? ProjectOf(string project, out int level)
        {
            var mark = project.IndexOf(LevelMark);
            level = 1;
            if (mark < 0)
                return Building(project);
            if (!int.TryParse(project.Substring(mark + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out level) || level < 2)
                return null;
            var building = Building(project.Substring(0, mark));
            return building != null && level <= building.MaxLevel ? building : null;
        }

        public int LevelOf(TownBuildingDef building, ICollection<string> built)
        {
            var level = 0;
            while (level < building.MaxLevel && built.Contains(building.ProjectIds[level]))
                level++;
            return level;
        }

        public int CountProjects(IEnumerable<string> built)
        {
            var count = 0;
            foreach (var id in built)
            {
                if (ProjectOf(id, out _) != null)
                    count++;
            }
            return count;
        }

        public TownStageDef StageAt(int built)
        {
            var reached = Stages[0];
            foreach (var stage in Stages)
            {
                if (built >= stage.Built)
                    reached = stage;
            }
            return reached;
        }

        public TownStageDef? StageAfter(int built)
        {
            foreach (var stage in Stages)
            {
                if (built < stage.Built)
                    return stage;
            }
            return null;
        }

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
            var guilds = GuildsOf(root.Field("guilds"), districts, characters, levels.Count);
            var build = root.Field("build");
            var resources = ResourcesOf(build.Field("resources"), guilds);
            var buildings = BuildingsOf(build.Field("buildings"), guilds, districts, characters, resources);
            var stages = StagesOf(build.Field("stages"), buildings);
            var sites = SitesOf(build.Field("sites"), resources);
            var rewards = RewardsOf(build.Field("rewards"));
            var top = build.Field("top");
            build.Field("gatherDays").AsInt(1);
            build.Field("buildDays").AsInt(1);
            build.Field("activeDays").AsInt(1);
            build.Field("minPlayers").AsInt(1);
            return new GuildRules(levels, guilds, districts, buildings, stages, build.Field("voteDays").AsInt(1), build.Field("openHours").AsInt(0), resources, sites,
                rewards, top.Field("size").AsInt(0), top.Field("shards").AsInt(0), build.Field("stageShards").AsInt(0), ObservatoryOf(root.Field("observatory")), TrialOf(root.Field("trial")), DecorationsOf(root.Field("weekly")), root.Field("weekly").Field("minLabour").AsLong(1));
        }

        private static IReadOnlyList<TownDecorDef> DecorationsOf(ContentNode node)
        {
            node.Field("perPlayer").AsLong(1);
            node.Field("minGoal").AsLong(1);
            var decorations = new List<TownDecorDef>();
            foreach (var item in node.Field("rewards").Items())
            {
                var id = item.Field("decor").AsString();
                foreach (var known in decorations)
                {
                    if (known.Id == id)
                        throw item.Field("decor").Error($"decoration '{id}' is listed twice");
                }
                var name = item.Field("name");
                decorations.Add(new TownDecorDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), item.Field("frame").AsString()));
            }
            if (decorations.Count == 0)
                throw node.Field("rewards").Error("the weekly rebuild needs rewards");
            return decorations;
        }

        private static MasterTrialRules TrialOf(ContentNode node) =>
            new MasterTrialRules(node.Field("seconds").AsNumber(10, 600), node.Field("stage").AsInt(1, 1000), node.Field("heroLevel").AsInt(1, 200));

        private static ObservatoryBonus ObservatoryOf(ContentNode node) =>
            new ObservatoryBonus(node.Field("fishWeight").AsNumber(1, 20), node.Field("plantGrow").AsNumber(0, MuseumRules.MaxPerk), node.Field("plantMutation").AsNumber(0, MuseumRules.MaxPerk));

        private static IReadOnlyList<GuildPerk> PerksOf(ContentNode node, string guild, int maxLevel)
        {
            var perks = new List<GuildPerk>();
            var sum = new Dictionary<MuseumPerk, double>();
            foreach (var item in node.Items())
            {
                var level = item.HasField("level") ? item.Field("level").AsInt(2, maxLevel) : 0;
                var hall = item.HasField("hall") ? item.Field("hall").AsInt(2, 10) : 0;
                if ((level > 0) == (hall > 0))
                    throw item.Error($"a perk of guild '{guild}' opens either at a guild level or at a hall level");
                var name = item.Field("perk").AsString();
                if (!MuseumRules.TryPerk(name, out var perk))
                    throw item.Field("perk").Error($"unknown perk '{name}'");
                var value = item.Field("value").AsNumber(0.001, MuseumRules.MaxOf(perk));
                sum[perk] = (sum.TryGetValue(perk, out var before) ? before : 0) + value;
                if (sum[perk] > MuseumRules.MaxOf(perk) / 2)
                    throw item.Field("value").Error($"guild '{guild}' gives too much '{name}'");
                perks.Add(new GuildPerk(level, hall, perk, value));
            }
            return perks;
        }

        private static IReadOnlyList<TownBuildingDef> BuildingsOf(ContentNode node, IReadOnlyList<GuildDef> guilds, IReadOnlyList<DistrictDef> districts,
            IReadOnlyCollection<string> characters, IReadOnlyList<BuildResourceDef> resources)
        {
            var buildings = new List<TownBuildingDef>();
            var total = 0;
            foreach (var item in node.Items())
            {
                var id = item.Field("id").AsString();
                if (id.Length == 0 || id.IndexOf(LevelMark) >= 0)
                    throw item.Field("id").Error($"building id '{id}' must be a plain name");
                foreach (var known in buildings)
                {
                    if (known.Id == id)
                        throw item.Field("id").Error($"building '{id}' is listed twice");
                }
                var guild = item.HasField("guild") ? item.Field("guild").AsString() : string.Empty;
                if (guild.Length > 0 && guild != id)
                    throw item.Field("guild").Error($"the hall of guild '{guild}' must have the guild's id");
                if (guild.Length > 0 && FindGuild(guilds, guild) == null)
                    throw item.Field("guild").Error($"unknown guild '{guild}'");
                var master = item.Field("master").AsString();
                if (!Contains(characters, master))
                    throw item.Field("master").Error($"master '{master}' is not a character");
                var district = item.Field("district").AsString();
                var placed = false;
                foreach (var entry in districts)
                    placed |= entry.Id == district;
                if (!placed)
                    throw item.Field("district").Error($"unknown district '{district}'");
                var levels = new List<TownBuildLevel>();
                foreach (var level in item.Field("levels").Items())
                    levels.Add(LevelOf(level, id, guild, levels, resources));
                if (levels.Count == 0)
                    throw item.Field("levels").Error($"building '{id}' needs at least one level");
                foreach (var perk in guild.Length > 0 ? FindGuild(guilds, guild)!.Perks : System.Array.Empty<GuildPerk>())
                {
                    if (perk.Hall > levels.Count)
                        throw item.Field("levels").Error($"a perk of '{guild}' waits for hall level {perk.Hall}, the hall has {levels.Count}");
                }
                var lines = new List<TownMasterLine>();
                foreach (var line in item.Field("lines").Items())
                    lines.Add(new TownMasterLine(line.Field("ru").AsString(), line.Field("en").AsString()));
                if (lines.Count == 0)
                    throw item.Field("lines").Error($"the master of '{id}' needs something to say at the board");
                var name = item.Field("name");
                buildings.Add(new TownBuildingDef(id, guild, name.Field("ru").AsString(), name.Field("en").AsString(), master, district, levels, lines));
                total += levels.Count;
            }
            foreach (var guild in guilds)
            {
                var hall = false;
                foreach (var building in buildings)
                    hall |= building.Guild == guild.Id;
                if (!hall)
                    throw node.Error($"guild '{guild.Id}' has no hall to build");
            }
            var start = false;
            foreach (var building in buildings)
            {
                start |= building.Levels[0].AfterBuilt == 0;
                foreach (var level in building.Levels)
                {
                    if (level.AfterBuilt >= total)
                        throw node.Error($"building '{building.Id}' waits for more buildings than the town can have");
                }
            }
            if (!start)
                throw node.Error("the town needs a building to start from");
            return buildings;
        }

        private static TownBuildLevel LevelOf(ContentNode node, string building, string guild, IReadOnlyList<TownBuildLevel> before, IReadOnlyList<BuildResourceDef> resources)
        {
            var afterBuilt = node.Field("afterBuilt").AsInt(0);
            if (before.Count > 0 && afterBuilt < before[before.Count - 1].AfterBuilt)
                throw node.Field("afterBuilt").Error($"a level of '{building}' cannot open before the level below it");
            var perPlayerDay = node.Field("perPlayerDay");
            var needs = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var field in perPlayerDay.FieldNames())
            {
                var resource = Find(resources, field);
                if (resource == null)
                    throw perPlayerDay.Field(field).Error($"building '{building}' needs unknown resource '{field}'");
                if (guild.Length > 0 && resource.Guild == guild)
                    throw perPlayerDay.Field(field).Error($"hall '{guild}' cannot be built from what only it makes");
                needs[field] = perPlayerDay.Field(field).AsLong(1);
            }
            if (needs.Count == 0)
                throw perPlayerDay.Error($"building '{building}' needs at least one resource");
            return new TownBuildLevel(afterBuilt, needs);
        }

        private static IReadOnlyList<TownStageDef> StagesOf(ContentNode node, IReadOnlyList<TownBuildingDef> buildings)
        {
            var total = 0;
            foreach (var building in buildings)
                total += building.MaxLevel;
            var stages = new List<TownStageDef>();
            foreach (var item in node.Items())
            {
                var built = item.Field("built").AsInt(0);
                if (stages.Count == 0 && built != 0)
                    throw item.Field("built").Error("the first look of the town is the one it starts with");
                if (stages.Count > 0 && built <= stages[stages.Count - 1].Built)
                    throw item.Field("built").Error("each look of the town needs more buildings than the one before");
                if (built > total)
                    throw item.Field("built").Error($"the town has only {total} buildings and levels");
                var name = item.Field("name");
                stages.Add(new TownStageDef(item.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), built));
            }
            if (stages.Count == 0)
                throw node.Error("the town needs at least one look");
            return stages;
        }

        private static GuildDef? FindGuild(IReadOnlyList<GuildDef> guilds, string id)
        {
            foreach (var guild in guilds)
            {
                if (guild.Id == id)
                    return guild;
            }
            return null;
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

        private static IReadOnlyList<GuildDef> GuildsOf(ContentNode node, IReadOnlyList<DistrictDef> districts, IReadOnlyCollection<string> characters, int maxLevel)
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
                var perks = item.HasField("perks") ? PerksOf(item.Field("perks"), id, maxLevel) : System.Array.Empty<GuildPerk>();
                guilds.Add(new GuildDef(id, name.Field("ru").AsString(), name.Field("en").AsString(), master, district, perks));
            }
            return guilds;
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
