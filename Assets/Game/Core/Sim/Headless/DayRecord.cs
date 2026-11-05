using System.Globalization;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public readonly struct DayRecord
    {
        public const string CsvHeader = "day,stage,highest_stage,record_stage,hero_level,tree_points_spent,common_trees,subclass,trait_level,prestiges,artifacts,artifact_levels,echo_earned,gold_earned_today,items_today,daily_items_today,gems_owned,shards,shards_found";

        public readonly int Day;
        public readonly long Stage;
        public readonly long HighestStage;
        public readonly long RecordStage;
        public readonly int HeroLevel;
        public readonly int TreePointsSpent;
        public readonly string CommonTrees;
        public readonly string Subclass;
        public readonly int TraitLevel;
        public readonly int Prestiges;
        public readonly int Artifacts;
        public readonly int ArtifactLevels;
        public readonly BigNumber EchoEarned;
        public readonly BigNumber GoldToday;
        public readonly long ItemsToday;
        public readonly int DailyItemsToday;
        public readonly int GemsOwned;
        public readonly long Shards;
        public readonly long ShardsFound;

        public DayRecord(int day, long stage, long highestStage, long recordStage, int heroLevel, int treePointsSpent, string commonTrees, string subclass, int traitLevel,
            int prestiges, int artifacts, int artifactLevels, BigNumber echoEarned, BigNumber goldToday,
            long itemsToday, int dailyItemsToday, int gemsOwned, long shards, long shardsFound)
        {
            Day = day;
            Stage = stage;
            HighestStage = highestStage;
            RecordStage = recordStage;
            HeroLevel = heroLevel;
            TreePointsSpent = treePointsSpent;
            CommonTrees = commonTrees;
            Subclass = subclass;
            TraitLevel = traitLevel;
            Prestiges = prestiges;
            Artifacts = artifacts;
            ArtifactLevels = artifactLevels;
            EchoEarned = echoEarned;
            GoldToday = goldToday;
            ItemsToday = itemsToday;
            DailyItemsToday = dailyItemsToday;
            GemsOwned = gemsOwned;
            Shards = shards;
            ShardsFound = shardsFound;
        }

        public string ToCsv() => string.Join(",",
            Day.ToString(CultureInfo.InvariantCulture),
            Stage.ToString(CultureInfo.InvariantCulture),
            HighestStage.ToString(CultureInfo.InvariantCulture),
            RecordStage.ToString(CultureInfo.InvariantCulture),
            HeroLevel.ToString(CultureInfo.InvariantCulture),
            TreePointsSpent.ToString(CultureInfo.InvariantCulture),
            CommonTrees,
            Subclass,
            TraitLevel.ToString(CultureInfo.InvariantCulture),
            Prestiges.ToString(CultureInfo.InvariantCulture),
            Artifacts.ToString(CultureInfo.InvariantCulture),
            ArtifactLevels.ToString(CultureInfo.InvariantCulture),
            StageRecord.Number(EchoEarned),
            StageRecord.Number(GoldToday),
            ItemsToday.ToString(CultureInfo.InvariantCulture),
            DailyItemsToday.ToString(CultureInfo.InvariantCulture),
            GemsOwned.ToString(CultureInfo.InvariantCulture),
            Shards.ToString(CultureInfo.InvariantCulture),
            ShardsFound.ToString(CultureInfo.InvariantCulture));
    }
}
