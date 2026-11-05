using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class AchievementLine
    {
        public string Id { get; internal set; } = string.Empty;
        public string Name { get; internal set; } = string.Empty;
        public string Text { get; internal set; } = string.Empty;
        public double Progress { get; internal set; }
        public double Target { get; internal set; }
        public bool Unlocked { get; internal set; }
    }

    public static class AchievementsView
    {
        public static List<AchievementLine> Lines(AchievementSystem achievements, bool russian = true)
        {
            var lines = new List<AchievementLine>();
            foreach (var achievement in achievements.Catalog.All)
            {
                lines.Add(new AchievementLine
                {
                    Id = achievement.Id,
                    Name = russian ? achievement.NameRu : achievement.NameEn,
                    Text = Text(achievement),
                    Progress = achievements.Progress(achievement),
                    Target = achievement.Target,
                    Unlocked = achievements.IsUnlocked(achievement.Id)
                });
            }
            return lines;
        }

        public static string Text(AchievementDef achievement) =>
            Texts.Format("achievement.counter." + AchievementCatalog.IdOf(achievement.Counter), achievement.Target.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " "));
    }
}
