using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class HideoutTier
    {
        public HideoutTier(string id, string nameRu, string nameEn, int members, long bosses, int orders, double sitHours, int fireLevel, IReadOnlyList<HideoutNeed> needs, IReadOnlyList<string> opens)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Members = members;
            Bosses = bosses;
            Orders = orders;
            SitHours = sitHours;
            FireLevel = fireLevel;
            Needs = needs;
            Opens = opens;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public int Members { get; }
        public long Bosses { get; }
        public int Orders { get; }
        public double SitHours { get; }
        public int FireLevel { get; }
        public IReadOnlyList<HideoutNeed> Needs { get; }
        public IReadOnlyList<string> Opens { get; }
    }

    public readonly struct HideoutCost
    {
        public HideoutCost(long points, double hours)
        {
            Points = points;
            Hours = hours;
        }

        public long Points { get; }
        public double Hours { get; }
    }

    public readonly struct HideoutNeed
    {
        public HideoutNeed(string counter, long target)
        {
            Counter = counter;
            Target = target;
        }

        public string Counter { get; }
        public long Target { get; }
    }

    public sealed class HideoutBuilding
    {
        public HideoutBuilding(string id, string nameRu, string nameEn, bool personal, string guild, IReadOnlyList<HideoutCost> levels)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Personal = personal;
            Guild = guild;
            Levels = levels;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public bool Personal { get; }
        public string Guild { get; }
        public IReadOnlyList<HideoutCost> Levels { get; }
    }

    public sealed class HideoutRules
    {
        public const string FileName = "party_hideout.json";
        public const int SupportedVersion = 1;

        private static readonly string[] Counters = { "gardenHarvests", "guardiansTogether", "riftDepth", "fishTogether", "partyTrips", "hoursTogether", "allBuildingsLevel" };

        private HideoutRules(int presenceSeconds, IReadOnlyList<HideoutTier> tiers, IReadOnlyList<HideoutBuilding> buildings, IReadOnlyList<int> levelTiers)
        {
            PresenceSeconds = presenceSeconds;
            Tiers = tiers;
            Buildings = buildings;
            LevelTiers = levelTiers;
        }

        public int PresenceSeconds { get; }
        public IReadOnlyList<int> LevelTiers { get; }
        public IReadOnlyList<HideoutTier> Tiers { get; }
        public IReadOnlyList<HideoutBuilding> Buildings { get; }

        public HideoutBuilding? Building(string id)
        {
            foreach (var building in Buildings)
            {
                if (building.Id == id)
                    return building;
            }
            return null;
        }

        public static HideoutRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var presence = root.Field("presenceSeconds").AsInt(10, 600);
            var here = root.Field("hereSeconds").AsInt(presence + 1, 3600);
            root.Field("maxSitStepSeconds").AsInt(presence, here * 10);
            var buildings = new List<HideoutBuilding>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("buildings").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"building '{id}' is listed twice");
                var name = node.Field("name");
                var personal = node.HasField("personal") && node.Field("personal").AsBool();
                var levels = new List<HideoutCost>();
                if (!personal)
                {
                    foreach (var level in node.Field("levels").Items())
                        levels.Add(new HideoutCost(level.Field("points").AsLong(1), level.Field("hours").AsNumber(0, 100_000)));
                    if (levels.Count != 3)
                        throw node.Field("levels").Error($"building '{id}' needs three levels");
                }
                buildings.Add(new HideoutBuilding(id, name.Field("ru").AsString(), name.Field("en").AsString(), personal,
                    node.HasField("guild") ? node.Field("guild").AsString() : string.Empty, levels));
            }
            var tiers = new List<HideoutTier>();
            foreach (var node in root.Field("tiers").Items())
            {
                var opens = new List<string>();
                foreach (var item in node.Field("opens").Items())
                {
                    var building = item.AsString();
                    if (!ids.Contains(building))
                        throw item.Error($"unknown building '{building}'");
                    opens.Add(building);
                }
                var bosses = node.Field("bosses").AsLong(0);
                if (tiers.Count > 0 && bosses < tiers[tiers.Count - 1].Bosses)
                    throw node.Field("bosses").Error("a higher tier never needs fewer bosses");
                var needs = new List<HideoutNeed>();
                if (node.HasField("needs"))
                {
                    foreach (var item in node.Field("needs").Items())
                    {
                        var counter = item.Field("counter").AsString();
                        if (Array.IndexOf(Counters, counter) < 0)
                            throw item.Field("counter").Error($"unknown counter '{counter}'");
                        needs.Add(new HideoutNeed(counter, item.Field("target").AsLong(1)));
                    }
                }
                var name = node.Field("name");
                tiers.Add(new HideoutTier(node.Field("id").AsString(), name.Field("ru").AsString(), name.Field("en").AsString(), node.Field("members").AsInt(1, 4),
                    bosses, node.Field("orders").AsInt(0, 1000), node.Field("sitHours").AsNumber(0, 10_000), node.Field("fireLevel").AsInt(0, 3), needs, opens));
            }
            if (tiers.Count == 0)
                throw root.Field("tiers").Error("the hideout needs tiers");
            var levelTiers = new List<int>();
            foreach (var item in root.Field("levelTiers").Items())
                levelTiers.Add(item.AsInt(0, tiers.Count));
            if (levelTiers.Count != 3)
                throw root.Field("levelTiers").Error("three building levels need three tiers");
            foreach (var tier in tiers)
            {
                if (tier.FireLevel > 1 && levelTiers[tier.FireLevel - 1] >= tiers.IndexOf(tier) + 1)
                    throw root.Field("levelTiers").Error($"tier '{tier.Id}' needs a fire level that only a later tier allows");
            }
            return new HideoutRules(presence, tiers, buildings, levelTiers);
        }
    }
}
