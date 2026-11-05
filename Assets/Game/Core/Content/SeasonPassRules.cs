using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public readonly struct SeasonPassReward
    {
        public SeasonPassReward(int level, string cosmetic, long crystals)
        {
            Level = level;
            Cosmetic = cosmetic;
            Crystals = crystals;
        }

        public int Level { get; }
        public string Cosmetic { get; }
        public long Crystals { get; }
    }

    public sealed class SeasonPassSeason
    {
        public SeasonPassSeason(string id, string nameRu, string nameEn, IReadOnlyList<SeasonPassReward> free, IReadOnlyList<SeasonPassReward> premium)
        {
            Id = id;
            NameRu = nameRu;
            NameEn = nameEn;
            Free = free;
            Premium = premium;
        }

        public string Id { get; }
        public string NameRu { get; }
        public string NameEn { get; }
        public IReadOnlyList<SeasonPassReward> Free { get; }
        public IReadOnlyList<SeasonPassReward> Premium { get; }

        public SeasonPassReward? RewardAt(int level, bool premium)
        {
            foreach (var reward in premium ? Premium : Free)
            {
                if (reward.Level == level)
                    return reward;
            }
            return null;
        }
    }

    public sealed class SeasonPassRules
    {
        public const string FileName = "season_pass.json";
        public const int SupportedVersion = 1;

        private SeasonPassRules(int seasonWeeks, long firstWeek, int levels, int pointsPerLevel, int pointsPerActiveMinute, int dailyCap, long premiumPrice, IReadOnlyList<SeasonPassSeason> seasons)
        {
            SeasonWeeks = seasonWeeks;
            FirstWeek = firstWeek;
            Levels = levels;
            PointsPerLevel = pointsPerLevel;
            PointsPerActiveMinute = pointsPerActiveMinute;
            DailyCap = dailyCap;
            PremiumPrice = premiumPrice;
            Seasons = seasons;
        }

        public int SeasonWeeks { get; }
        public long FirstWeek { get; }
        public int Levels { get; }
        public int PointsPerLevel { get; }
        public int PointsPerActiveMinute { get; }
        public int DailyCap { get; }
        public long PremiumPrice { get; }
        public IReadOnlyList<SeasonPassSeason> Seasons { get; }
        public long MaxPoints => (long)Levels * PointsPerLevel;

        public int LevelAt(long points) => (int)System.Math.Min(Levels, System.Math.Max(0, points) / PointsPerLevel);

        public SeasonPassSeason? Season(int index) => index >= 0 && index < Seasons.Count ? Seasons[index] : null;

        public int SeasonOfWeek(long week) => week < FirstWeek ? -1 : (int)((week - FirstWeek) / SeasonWeeks);

        public static SeasonPassRules Load(string json, CosmeticCatalog cosmetics)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var seasonWeeks = root.Field("seasonWeeks").AsInt(1, 52);
            var firstWeek = root.Field("firstWeek").AsLong(0);
            if (firstWeek % seasonWeeks != 0)
                throw root.Field("firstWeek").Error($"first week {firstWeek} must be a multiple of {seasonWeeks} so that seasons start together with clan war seasons");
            var levels = root.Field("levels").AsInt(1, 1000);
            var seasons = new List<SeasonPassSeason>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var given = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in root.Field("seasons").Items())
            {
                var id = node.Field("id").AsString();
                if (!ids.Add(id))
                    throw node.Field("id").Error($"season '{id}' is listed twice");
                var name = node.Field("name");
                seasons.Add(new SeasonPassSeason(id, name.Field("ru").AsString(), name.Field("en").AsString(),
                    Track(node.Field("free"), levels, cosmetics, given), Track(node.Field("premium"), levels, cosmetics, given)));
            }
            return new SeasonPassRules(seasonWeeks, firstWeek, levels, root.Field("pointsPerLevel").AsInt(1, 1_000_000),
                root.Field("pointsPerActiveMinute").AsInt(0, 1000), root.Field("dailyCap").AsInt(1, 1_000_000),
                root.Field("premiumPrice").AsLong(1, 1_000_000), seasons);
        }

        private static IReadOnlyList<SeasonPassReward> Track(ContentNode track, int levels, CosmeticCatalog cosmetics, HashSet<string> given)
        {
            var rewards = new List<SeasonPassReward>();
            var used = new HashSet<int>();
            foreach (var node in track.Items())
            {
                var level = node.Field("level").AsInt(1, levels);
                if (!used.Add(level))
                    throw node.Field("level").Error($"level {level} has two rewards on one track");
                var cosmetic = node.HasField("cosmetic") ? node.Field("cosmetic").AsString() : string.Empty;
                var crystals = node.HasField("crystals") ? node.Field("crystals").AsLong(1, 1_000_000) : 0;
                if ((cosmetic.Length > 0) == (crystals > 0))
                    throw node.Error("a reward is either a cosmetic or crystals");
                if (cosmetic.Length > 0)
                {
                    if (!cosmetics.TryGet(cosmetic, out var item))
                        throw node.Field("cosmetic").Error($"unknown cosmetic '{cosmetic}'");
                    if (item.Source != "pass")
                        throw node.Field("cosmetic").Error($"cosmetic '{cosmetic}' is not a season pass item");
                    if (item.Clan)
                        throw node.Field("cosmetic").Error($"clan cosmetic '{cosmetic}' cannot be a personal reward");
                    if (!given.Add(cosmetic))
                        throw node.Field("cosmetic").Error($"cosmetic '{cosmetic}' is given twice");
                }
                rewards.Add(new SeasonPassReward(level, cosmetic, crystals));
            }
            return rewards;
        }
    }
}
