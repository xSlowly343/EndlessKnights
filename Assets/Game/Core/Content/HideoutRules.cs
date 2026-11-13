using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class HideoutTier
    {
        public HideoutTier(string id, string nameRu, string nameEn, int members, long bosses, int orders, double sitHours, int fireLevel, int cornerDecor, IReadOnlyList<HideoutNeed> needs, IReadOnlyList<string> opens)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Members = members;
            Bosses = bosses;
            Orders = orders;
            SitHours = sitHours;
            FireLevel = fireLevel;
            CornerDecor = cornerDecor;
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
        public int CornerDecor { get; }
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

    public sealed class HideoutCrafts
    {
        public HideoutCrafts(string forgeBuilding, double hammersShare, double stepBoost, double masterpieceChance, double masterpieceBoost,
            string cauldronBuilding, double stirsShare, double fineChance)
        {
            ForgeBuilding = forgeBuilding;
            HammersShare = hammersShare;
            StepBoost = stepBoost;
            MasterpieceChance = masterpieceChance;
            MasterpieceBoost = masterpieceBoost;
            CauldronBuilding = cauldronBuilding;
            StirsShare = stirsShare;
            FineChance = fineChance;
        }

        public string ForgeBuilding { get; }
        public double HammersShare { get; }
        public double StepBoost { get; }
        public double MasterpieceChance { get; }
        public double MasterpieceBoost { get; }
        public string CauldronBuilding { get; }
        public double StirsShare { get; }
        public double FineChance { get; }
    }

    public sealed class HideoutStorage
    {
        public HideoutStorage(IReadOnlyList<string> prefixes, IReadOnlyList<string> items, IReadOnlyList<string> never, int takePerDay, int shareKeep)
        {
            Prefixes = prefixes;
            Items = items;
            Never = never;
            TakePerDay = takePerDay;
            ShareKeep = shareKeep;
        }

        public IReadOnlyList<string> Prefixes { get; }
        public IReadOnlyList<string> Items { get; }
        public IReadOnlyList<string> Never { get; }
        public int TakePerDay { get; }
        public int ShareKeep { get; }

        public bool Takes(string item)
        {
            if (item.Length == 0 || Contains(Never, item))
                return false;
            if (Contains(Items, item))
                return true;
            foreach (var prefix in Prefixes)
            {
                if (item.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool Contains(IReadOnlyList<string> list, string item)
        {
            foreach (var entry in list)
            {
                if (entry == item)
                    return true;
            }
            return false;
        }
    }

    public sealed class HideoutRules
    {
        public const string FileName = "party_hideout.json";
        public const int SupportedVersion = 1;

        private static readonly string[] Counters = { "gardenHarvests", "guardiansTogether", "riftDepth", "fishTogether", "partyTrips", "hoursTogether", "allBuildingsLevel" };

        public const string CornerBuilding = "corner";

        private HideoutRules(int presenceSeconds, int cornerDecor, int frozenDays, HideoutCrafts crafts, string dummy, string pier, string trophyHall, double pierRareBoost, IReadOnlyList<HideoutTier> tiers, IReadOnlyList<HideoutBuilding> buildings, IReadOnlyList<int> levelTiers, HideoutStorage storage)
        {
            Storage = storage;
            PresenceSeconds = presenceSeconds;
            CornerDecor = cornerDecor;
            FrozenDays = frozenDays;
            Crafts = crafts;
            Dummy = dummy;
            Pier = pier;
            TrophyHall = trophyHall;
            PierRareBoost = pierRareBoost;
            Tiers = tiers;
            Buildings = buildings;
            LevelTiers = levelTiers;
        }

        public HideoutStorage Storage { get; }
        public string Pot { get; private set; } = string.Empty;
        public int PresenceSeconds { get; }
        public int CornerDecor { get; }
        public int FrozenDays { get; }
        public HideoutCrafts Crafts { get; }
        public string Dummy { get; }
        public string Pier { get; }
        public string TrophyHall { get; }
        public double PierRareBoost { get; }
        public IReadOnlyList<int> LevelTiers { get; }
        public IReadOnlyList<HideoutTier> Tiers { get; }
        public IReadOnlyList<HideoutBuilding> Buildings { get; }

        public int CornerDecorAt(int tier)
        {
            var spots = CornerDecor;
            for (var i = 0; i < tier && i < Tiers.Count; i++)
                spots = System.Math.Max(spots, Tiers[i].CornerDecor);
            return spots;
        }

        public HideoutBuilding? Building(string id)
        {
            foreach (var building in Buildings)
            {
                if (building.Id == id)
                    return building;
            }
            return null;
        }

        private static string Known(ContentNode node, HashSet<string> ids)
        {
            var id = node.AsString();
            if (!ids.Contains(id))
                throw node.Error($"unknown building '{id}'");
            return id;
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
            var cornerDecor = root.Field("cornerDecor").AsInt(1, 10);
            var frozenDays = root.Field("frozenDays").AsInt(1, 365);
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
                    bosses, node.Field("orders").AsInt(0, 1000), node.Field("sitHours").AsNumber(0, 10_000), node.Field("fireLevel").AsInt(0, 3),
                    node.HasField("cornerDecor") ? node.Field("cornerDecor").AsInt(cornerDecor, 10) : 0, needs, opens));
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
            var craftsNode = root.Field("crafts");
            var forge = craftsNode.Field("forge");
            var cauldron = craftsNode.Field("cauldron");
            craftsNode.Field("brewsPerDay").AsInt(1, 100);
            var crafts = new HideoutCrafts(Known(forge.Field("building"), ids), forge.Field("hammersShare").AsNumber(0, 0.9), forge.Field("stepBoost").AsNumber(0, 2),
                forge.Field("masterpieceChance").AsNumber(0, 1), forge.Field("masterpieceBoost").AsNumber(1, 5),
                Known(cauldron.Field("building"), ids), cauldron.Field("stirsShare").AsNumber(0, 0.9), cauldron.Field("fineChance").AsNumber(0, 1));
            var places = root.Field("places");
            var dummy = Known(places.Field("dummy"), ids);
            var pier = Known(places.Field("pier"), ids);
            var trophyHall = Known(places.Field("trophyHall"), ids);
            var pierRareBoost = places.Field("pierRareBoost").AsNumber(0, 1);
            if (!ids.Contains(CornerBuilding))
                throw root.Field("buildings").Error($"the hideout needs the '{CornerBuilding}' building");
            var pot = craftsNode.Field("pot");
            pot.Field("dishesPerDay").AsInt(1, 100);
            return new HideoutRules(presence, cornerDecor, frozenDays, crafts, dummy, pier, trophyHall, pierRareBoost, tiers, buildings, levelTiers, LoadStorage(root.Field("storage")))
            {
                Pot = Known(pot.Field("building"), ids)
            };
        }

        private static HideoutStorage LoadStorage(ContentNode node)
        {
            List<string> Strings(string field)
            {
                var list = new List<string>();
                foreach (var item in node.Field(field).Items())
                    list.Add(item.AsString());
                return list;
            }
            return new HideoutStorage(Strings("prefixes"), Strings("items"), Strings("never"), node.Field("takePerDay").AsInt(1, 1000), node.Field("shareKeep").AsInt(0, 1000));
        }
    }
}
