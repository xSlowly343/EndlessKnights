using System.Collections.Generic;
using Game.Core.Math;
using Game.Core.Text;

namespace Game.Core.Sim
{
    public static class OfflineReport
    {
        public const double MinimumAwaySeconds = 60;
        public static string Title => Texts.Get("offline.title");

        private const double CapToleranceSeconds = 1;

        public static bool ShouldShow(double awaySeconds, OfflineResult result) =>
            awaySeconds >= MinimumAwaySeconds && result.Kills > 0;

        public static IReadOnlyList<string> Lines(double awaySeconds, OfflineResult result, NumberFormatSettings format)
        {
            var lines = new List<string> { Texts.Format("offline.away", Duration(awaySeconds)) };
            if (awaySeconds > result.Seconds + CapToleranceSeconds)
                lines.Add(Texts.Format("offline.counted", Duration(result.Seconds)));
            lines.Add(Texts.Format("offline.gold", NumberFormatter.Format(result.Gold, format)));
            lines.Add(Texts.Format("offline.kills", result.Kills));
            lines.Add(result.StagesCleared > 0
                ? Texts.Format("offline.stageMoved", result.StageBefore, result.StageAfter)
                : Texts.Format("offline.stage", result.StageAfter));
            if (result.StoppedAtGuardian)
                lines.Add(Texts.Get("offline.guardian"));
            else if (result.StuckAtBoss)
                lines.Add(Texts.Get("offline.stuck"));
            return lines;
        }

        public static string Duration(double seconds)
        {
            var totalMinutes = (long)System.Math.Floor(seconds / 60);
            if (totalMinutes < 1)
                return Texts.Get("unit.lessThanMinute");
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            if (hours == 0)
                return Texts.Format("unit.minutes", minutes);
            return minutes == 0 ? Texts.Format("unit.hours", hours) : Texts.Format("unit.hoursMinutes", hours, minutes);
        }
    }
}
