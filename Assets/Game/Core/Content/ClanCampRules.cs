using System;
using System.Collections.Generic;
using Game.Core.Stats;

namespace Game.Core.Content
{
    public sealed class ClanCampLevel
    {
        public ClanCampLevel(long points, string nameRu, string nameEn, IReadOnlyList<Modifier> modifiers)
        {
            Points = points;
            NameRu = nameRu;
            NameEn = nameEn;
            Modifiers = modifiers;
        }

        public long Points { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }
    }

    public sealed class ClanFeastRules
    {
        public ClanFeastRules(int fromLevel, int dishesPerMember, int minDishes, int maxDishes, int perMemberWeekly, IReadOnlyList<Modifier> modifiers)
        {
            FromLevel = fromLevel;
            DishesPerMember = dishesPerMember;
            MinDishes = minDishes;
            MaxDishes = maxDishes;
            PerMemberWeekly = perMemberWeekly;
            Modifiers = modifiers;
        }

        public int FromLevel { get; }
        public int DishesPerMember { get; }
        public int MinDishes { get; }
        public int MaxDishes { get; }
        public int PerMemberWeekly { get; }
        public IReadOnlyList<Modifier> Modifiers { get; }

        public int Target(int members) => System.Math.Max(MinDishes, System.Math.Min(MaxDishes, members * DishesPerMember));
    }

    public sealed class ClanCampRules
    {
        public const string FileName = "clan_camp.json";
        public const int SupportedVersion = 1;

        private readonly Dictionary<string, int> _points;

        private ClanCampRules(int offlineHours, int maxGive, int dailyPointsPerMember, Dictionary<string, int> points, IReadOnlyList<string> materials,
            IReadOnlyList<ClanCampLevel> levels, ClanFeastRules feast)
        {
            OfflineHours = offlineHours;
            MaxGive = maxGive;
            DailyPointsPerMember = dailyPointsPerMember;
            _points = points;
            Materials = materials;
            Levels = levels;
            Feast = feast;
        }

        public int OfflineHours { get; }
        public int MaxGive { get; }
        public int DailyPointsPerMember { get; }
        public IReadOnlyList<string> Materials { get; }
        public IReadOnlyList<ClanCampLevel> Levels { get; }
        public ClanFeastRules Feast { get; }
        public int MaxLevel => Levels.Count;

        public int PointsOf(string item) => _points.TryGetValue(item, out var points) ? points : 0;

        public int LevelAt(long points)
        {
            var level = 0;
            while (level < Levels.Count && points >= Levels[level].Points)
                level++;
            return level;
        }

        public static ClanCampRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var points = new Dictionary<string, int>(StringComparer.Ordinal);
            var materials = new List<string>();
            foreach (var node in root.Field("materials").Items())
            {
                var item = node.Field("item").AsString();
                if (points.ContainsKey(item))
                    throw node.Field("item").Error($"material '{item}' is listed twice");
                points[item] = node.Field("points").AsInt(1, 1000);
                materials.Add(item);
            }
            if (materials.Count == 0)
                throw root.Field("materials").Error("the clan camp needs materials");
            var levels = new List<ClanCampLevel>();
            foreach (var node in root.Field("levels").Items())
            {
                var need = node.Field("points").AsLong(1);
                if (levels.Count > 0 && need <= levels[levels.Count - 1].Points)
                    throw node.Field("points").Error("each camp level needs more points than the one before");
                var name = node.Field("name");
                levels.Add(new ClanCampLevel(need, name.Field("ru").AsString(), name.Field("en").AsString(), IncreasedOnly(node.Field("modifiers"))));
            }
            if (levels.Count == 0)
                throw root.Field("levels").Error("the clan camp needs levels");
            var feast = root.Field("feast");
            var minDishes = feast.Field("minDishes").AsInt(1, 1000);
            var feastRules = new ClanFeastRules(feast.Field("fromLevel").AsInt(1, levels.Count), feast.Field("dishesPerMember").AsInt(1, 100), minDishes,
                feast.Field("maxDishes").AsInt(minDishes, 1000), feast.Field("perMemberWeekly").AsInt(1, 100), IncreasedOnly(feast.Field("modifiers")));
            return new ClanCampRules(root.Field("offlineHours").AsInt(1, 24 * 14), root.Field("maxGive").AsInt(1, 10000),
                root.Field("dailyPointsPerMember").AsInt(1, 1_000_000), points, materials, levels, feastRules);
        }

        private static IReadOnlyList<Modifier> IncreasedOnly(ContentNode node)
        {
            var modifiers = ModifierReader.ReadList(node);
            foreach (var modifier in modifiers)
            {
                if (modifier.Kind != ModifierKind.Increased)
                    throw node.Error($"the clan camp gives only increased bonuses, not {modifier.Kind}");
            }
            return modifiers;
        }
    }
}
