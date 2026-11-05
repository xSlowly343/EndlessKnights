using System.Collections.Generic;
using System.Globalization;
using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public sealed class QuestLine
    {
        public bool Weekly { get; internal set; }
        public int Index { get; internal set; }
        public string Text { get; internal set; } = string.Empty;
        public double Progress { get; internal set; }
        public double Target { get; internal set; }
        public string Reward { get; internal set; } = string.Empty;
        public bool CanClaim { get; internal set; }
        public bool Claimed { get; internal set; }
        public bool CanSwap { get; internal set; }
    }

    public static class QuestsView
    {
        public static List<QuestLine> Lines(QuestSystem quests)
        {
            var lines = new List<QuestLine>();
            Add(lines, quests, false);
            Add(lines, quests, true);
            return lines;
        }

        public static string Streak(QuestSystem quests) =>
            Texts.Format("quest.streak", quests.State.Streak, Percent(quests.StreakBonus));

        public static string Swaps(QuestSystem quests) => Texts.Format("quest.swaps", quests.State.SwapsLeft);

        public static string Text(QuestKind kind, double target) =>
            Texts.Format("quest.kind." + QuestRules.IdOf(kind), target.ToString("0", CultureInfo.InvariantCulture));

        public static string Reward(QuestReward reward, double bonus)
        {
            var parts = new List<string>(3);
            var times = 1 + bonus;
            if (reward.Shards > 0)
                parts.Add(Texts.Format("quest.reward.shards", System.Math.Round(reward.Shards * times).ToString("0", CultureInfo.InvariantCulture)));
            if (reward.Dust > 0)
                parts.Add(Texts.Format("quest.reward.dust", System.Math.Round(reward.Dust * times).ToString("0", CultureInfo.InvariantCulture)));
            if (reward.Ingots > 0)
                parts.Add(Texts.Format("quest.reward.ingots", System.Math.Round(reward.Ingots * times).ToString("0", CultureInfo.InvariantCulture)));
            if (reward.Friendship > 0)
                parts.Add(Texts.Format("quest.reward.friendship", reward.Friendship.ToString(CultureInfo.InvariantCulture)));
            return string.Join(" · ", parts);
        }

        public static string Result(QuestResult result) => result switch
        {
            QuestResult.Done => string.Empty,
            QuestResult.NotFinished => Texts.Get("quest.result.notFinished"),
            QuestResult.AlreadyClaimed => Texts.Get("quest.result.claimed"),
            QuestResult.NoSwapsLeft => Texts.Get("quest.result.noSwaps"),
            _ => Texts.Get("quest.result.noQuest")
        };

        private static void Add(List<QuestLine> lines, QuestSystem quests, bool weekly)
        {
            var list = weekly ? quests.State.Weekly : quests.State.Daily;
            for (var i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                lines.Add(new QuestLine
                {
                    Weekly = weekly,
                    Index = i,
                    Text = Text(entry.Kind, entry.Target),
                    Progress = entry.Progress,
                    Target = entry.Target,
                    Reward = Reward(quests.RewardOf(weekly, entry.Kind), weekly ? 0 : quests.StreakBonus),
                    CanClaim = entry.Done && !entry.Claimed,
                    Claimed = entry.Claimed,
                    CanSwap = !weekly && !entry.Done && !entry.Claimed && quests.State.SwapsLeft > 0
                });
            }
        }

        private static string Percent(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture);
    }
}
