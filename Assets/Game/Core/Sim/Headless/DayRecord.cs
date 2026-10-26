using System.Globalization;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public readonly struct DayRecord
    {
        public const string CsvHeader = "day,stage,highest_stage,record_stage,hero_level,tree_points_spent,subclass,subclass_points,prestiges,artifacts,artifact_levels,echo_earned,gold_earned_today,items_today,daily_items_today,gems_owned";

        public readonly int Day;
        public readonly long Stage;
        public readonly long HighestStage;
        public readonly long RecordStage;
        public readonly int HeroLevel;
        public readonly int TreePointsSpent;
        public readonly string Subclass;
        public readonly int SubclassPoints;
        public readonly int Prestiges;
        public readonly int Artifacts;
        public readonly int ArtifactLevels;
        public readonly BigNumber EchoEarned;
        public readonly BigNumber GoldToday;
        public readonly long ItemsToday;
        public readonly int DailyItemsToday;
        public readonly int GemsOwned;

        public DayRecord(int day, long stage, long highestStage, long recordStage, int heroLevel, int treePointsSpent, string subclass, int subclassPoints,
            int prestiges, int artifacts, int artifactLevels, BigNumber echoEarned, BigNumber goldToday,
            long itemsToday, int dailyItemsToday, int gemsOwned)
        {
            Day = day;
            Stage = stage;
            HighestStage = highestStage;
            RecordStage = recordStage;
            HeroLevel = heroLevel;
            TreePointsSpent = treePointsSpent;
            Subclass = subclass;
            SubclassPoints = subclassPoints;
            Prestiges = prestiges;
            Artifacts = artifacts;
            ArtifactLevels = artifactLevels;
            EchoEarned = echoEarned;
            GoldToday = goldToday;
            ItemsToday = itemsToday;
            DailyItemsToday = dailyItemsToday;
            GemsOwned = gemsOwned;
        }

        public string ToCsv() => string.Join(",",
            Day.ToString(CultureInfo.InvariantCulture),
            Stage.ToString(CultureInfo.InvariantCulture),
            HighestStage.ToString(CultureInfo.InvariantCulture),
            RecordStage.ToString(CultureInfo.InvariantCulture),
            HeroLevel.ToString(CultureInfo.InvariantCulture),
            TreePointsSpent.ToString(CultureInfo.InvariantCulture),
            Subclass,
            SubclassPoints.ToString(CultureInfo.InvariantCulture),
            Prestiges.ToString(CultureInfo.InvariantCulture),
            Artifacts.ToString(CultureInfo.InvariantCulture),
            ArtifactLevels.ToString(CultureInfo.InvariantCulture),
            StageRecord.Number(EchoEarned),
            StageRecord.Number(GoldToday),
            ItemsToday.ToString(CultureInfo.InvariantCulture),
            DailyItemsToday.ToString(CultureInfo.InvariantCulture),
            GemsOwned.ToString(CultureInfo.InvariantCulture));
    }
}
