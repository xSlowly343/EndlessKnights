using System.Globalization;
using Game.Core.Math;

namespace Game.Core.Sim.Headless
{
    public readonly struct StageRecord
    {
        public const string CsvHeader = "stage,cleared_at_s,stage_s,to_boss_s,boss_s,boss_fails,enemy_hp,gold_earned";

        public readonly long Stage;
        public readonly double ClearedAtSeconds;
        public readonly double StageSeconds;
        public readonly double SecondsToBoss;
        public readonly double BossSeconds;
        public readonly int BossFails;
        public readonly BigNumber EnemyHp;
        public readonly BigNumber GoldEarned;

        public StageRecord(long stage, double clearedAtSeconds, double stageSeconds, double secondsToBoss, double bossSeconds, int bossFails,
            BigNumber enemyHp, BigNumber goldEarned)
        {
            Stage = stage;
            ClearedAtSeconds = clearedAtSeconds;
            StageSeconds = stageSeconds;
            SecondsToBoss = secondsToBoss;
            BossSeconds = bossSeconds;
            BossFails = bossFails;
            EnemyHp = enemyHp;
            GoldEarned = goldEarned;
        }

        public string ToCsv() => string.Join(",",
            Stage.ToString(CultureInfo.InvariantCulture),
            Seconds(ClearedAtSeconds),
            Seconds(StageSeconds),
            Seconds(SecondsToBoss),
            Seconds(BossSeconds),
            BossFails.ToString(CultureInfo.InvariantCulture),
            Number(EnemyHp),
            Number(GoldEarned));

        private static string Seconds(double seconds) => seconds.ToString("0.0", CultureInfo.InvariantCulture);

        public static string Number(BigNumber value) =>
            value.Mantissa.ToString("0.###", CultureInfo.InvariantCulture) + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
    }
}
