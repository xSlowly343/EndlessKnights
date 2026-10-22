using System.Collections.Generic;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public static class OfflineReport
    {
        public const double MinimumAwaySeconds = 60;
        public const string Title = "Пока тебя не было";

        private const double CapToleranceSeconds = 1;

        public static bool ShouldShow(double awaySeconds, OfflineResult result) =>
            awaySeconds >= MinimumAwaySeconds && result.Kills > 0;

        public static IReadOnlyList<string> Lines(double awaySeconds, OfflineResult result, NumberFormatSettings format)
        {
            var lines = new List<string> { "Тебя не было: " + Duration(awaySeconds) };
            if (awaySeconds > result.Seconds + CapToleranceSeconds)
                lines.Add("Засчитано: " + Duration(result.Seconds) + " (это предел)");
            lines.Add("Золото: +" + NumberFormatter.Format(result.Gold, format));
            lines.Add("Побеждено врагов: " + result.Kills);
            lines.Add(result.StagesCleared > 0
                ? $"Этап: {result.StageBefore} → {result.StageAfter}"
                : $"Этап: {result.StageAfter}");
            if (result.StoppedAtGuardian)
                lines.Add("Дальше страж биома, его бьют вживую");
            else if (result.StuckAtBoss)
                lines.Add("Босс этапа пока сильнее, нужна прокачка");
            return lines;
        }

        public static string Duration(double seconds)
        {
            var totalMinutes = (long)System.Math.Floor(seconds / 60);
            if (totalMinutes < 1)
                return "меньше минуты";
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            if (hours == 0)
                return $"{minutes} мин";
            return minutes == 0 ? $"{hours} ч" : $"{hours} ч {minutes} мин";
        }
    }
}
