using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class MentorText
    {
        private const long DayMs = 86_400_000;

        public static string StudentDay(long studentUntilUnixMs, long nowUnixMs, int studentDays)
        {
            var left = System.Math.Max(0, studentUntilUnixMs - nowUnixMs);
            var day = System.Math.Min(studentDays, System.Math.Max(1, studentDays - (int)(left / DayMs)));
            return Texts.Format("mentor.day", day, studentDays);
        }

        public static string WithStudent(string name, long studentUntilUnixMs, long nowUnixMs, int studentDays) =>
            Texts.Format("mentor.withStudent", name, StudentDay(studentUntilUnixMs, nowUnixMs, studentDays));

        public static string WithMentor(string name, long studentUntilUnixMs, long nowUnixMs, int studentDays) =>
            Texts.Format("mentor.withMentor", name, StudentDay(studentUntilUnixMs, nowUnixMs, studentDays));

        public static string Together(double seconds, MentorRules rules, int level)
        {
            var spent = Duration(seconds);
            return level >= rules.Steps.Count ? Texts.Format("mentor.togetherAll", spent)
                : Texts.Format("mentor.together", spent, Duration(rules.Steps[level].Hours * 3600));
        }

        public static string Step(MentorStep step, bool reached, bool forMentor)
        {
            var hours = Texts.Format("mentor.hours", step.Hours);
            var perk = forMentor ? step.MentorPerk : step.StudentPerk;
            var prize = perk.Length > 0 ? Texts.Get("mentor.perk." + perk) : step.CoopPoints > 0 ? Texts.Get("mentor.coopPoint") : string.Empty;
            var text = prize.Length > 0 ? Texts.Format("mentor.stepWith", hours, prize) : hours;
            return reached ? Texts.Format("mentor.stepDone", text) : text;
        }

        public static string Marks(int count, int max) => Texts.Format("mentor.marks", count, max);

        public static string Bonus(double share) => Texts.Format("mentor.bonus", System.Math.Round(share * 100));

        public static string Perk(string perk) => Texts.Get("mentor.perk." + perk);

        public static string Duration(double seconds)
        {
            var minutes = (long)System.Math.Floor(seconds / 60);
            var hours = minutes / 60;
            minutes %= 60;
            if (hours == 0)
                return Texts.Format("mentor.minutes", minutes);
            return minutes == 0 ? Texts.Format("mentor.hours", hours) : Texts.Format("mentor.hoursMinutes", hours, minutes);
        }
    }
}
