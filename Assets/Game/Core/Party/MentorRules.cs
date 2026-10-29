using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public readonly struct MentorStep
    {
        public MentorStep(double hours, string mentorPerk, string studentPerk, int coopPoints)
        {
            Hours = hours;
            MentorPerk = mentorPerk;
            StudentPerk = studentPerk;
            CoopPoints = coopPoints;
        }

        public double Hours { get; }
        public string MentorPerk { get; }
        public string StudentPerk { get; }
        public int CoopPoints { get; }
    }

    public sealed class MentorRules
    {
        public const string FileName = "mentor.json";
        public const int SupportedVersion = 1;

        private MentorRules(int studentDays, int maxMarks, double goldBonus, double xpBonus, IReadOnlyList<MentorStep> steps)
        {
            StudentDays = studentDays;
            MaxMarks = maxMarks;
            GoldBonus = goldBonus;
            XpBonus = xpBonus;
            Steps = steps;
        }

        public int StudentDays { get; }
        public int MaxMarks { get; }
        public double GoldBonus { get; }
        public double XpBonus { get; }
        public IReadOnlyList<MentorStep> Steps { get; }

        public static MentorRules Load(string json)
        {
            var root = ContentNode.Root(json, FileName);
            var version = root.Field("version").AsInt(1);
            if (version != SupportedVersion)
                throw root.Field("version").Error($"version {version} is not supported, expected {SupportedVersion}");
            var studentDays = root.Field("studentDays").AsInt(1, 60);
            var mentorDays = root.Field("mentorMinDays").AsInt(1, 3650);
            if (mentorDays <= studentDays)
                throw root.Field("mentorMinDays").Error($"must be longer than studentDays ({studentDays})");
            root.Field("maxMentorCoopPoints").AsInt(0, 100);
            var steps = new List<MentorStep>();
            var stepsNode = root.Field("rewards");
            foreach (var item in stepsNode.Items())
            {
                var hours = item.Field("hours").AsNumber(0.1, 1000);
                if (steps.Count > 0 && hours <= steps[steps.Count - 1].Hours)
                    throw item.Field("hours").Error("hours must grow");
                var mentor = item.Field("mentor");
                var student = item.Field("student");
                steps.Add(new MentorStep(hours, Perk(mentor), Perk(student), student.HasField("coopPoints") ? student.Field("coopPoints").AsInt(0, 100) : 0));
            }
            if (steps.Count == 0)
                throw stepsNode.Error("must list at least one reward");
            var bonus = root.Field("bonus");
            return new MentorRules(studentDays, root.Field("maxMarks").AsInt(0, 100), bonus.Field("gold").AsNumber(0, 10), bonus.Field("xp").AsNumber(0, 10), steps);
        }

        private static string Perk(ContentNode side) => side.HasField("perk") ? side.Field("perk").AsString() : string.Empty;
    }
}
