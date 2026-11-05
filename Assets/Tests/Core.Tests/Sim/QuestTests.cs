using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class QuestTests
    {
        private static QuestRules Rules => TestWorld.Data.Quests;

        private static GameLoop Loop(long day = 100)
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            loop.SetLocalDay(day);
            loop.Tick();
            return loop;
        }

        private static void Finish(GameLoop loop, int index)
        {
            var entry = loop.Quests.State.Daily[index];
            entry.Progress = entry.Target;
        }

        [Test]
        public void ADayBringsThreeDailyAndAWeekThreeWeeklyQuests()
        {
            var loop = Loop();

            Assert.That(loop.Quests.State.Daily, Has.Count.EqualTo(Rules.PerDay));
            Assert.That(loop.Quests.State.Weekly, Has.Count.EqualTo(Rules.PerWeek));
            Assert.That(loop.Quests.State.Daily.Select(entry => entry.Kind).Distinct().Count(), Is.EqualTo(Rules.PerDay));
            Assert.That(loop.Quests.State.SwapsLeft, Is.EqualTo(Rules.SwapsPerDay));
        }

        [Test]
        public void TheSameDayGivesTheSameQuestsToEveryone()
        {
            Assert.That(Loop(42).Quests.State.Daily.Select(entry => entry.Kind), Is.EqualTo(Loop(42).Quests.State.Daily.Select(entry => entry.Kind)));
        }

        [Test]
        public void MissedDaysAddUpToThreeDaysAndNothingIsLost()
        {
            var loop = Loop(100);
            loop.SetLocalDay(110);

            Assert.That(loop.Quests.State.Daily, Has.Count.EqualTo(Rules.PerDay * Rules.KeepDays));
            Assert.That(loop.Quests.State.Daily.Min(entry => entry.Day), Is.EqualTo(110 - Rules.KeepDays + 1));
        }

        [Test]
        public void KillsAndKeysCountTowardsTheirQuests()
        {
            var loop = Loop();
            var kills = new QuestEntry { Kind = QuestKind.Kills, Day = 100, Target = 5 };
            var keys = new QuestEntry { Kind = QuestKind.Keys, Day = 100, Target = 1000 };
            loop.Quests.State.Daily.Add(kills);
            loop.Quests.State.Daily.Add(keys);

            loop.State.Kills += 3;
            loop.State.TotalInput.Keys += 40;
            loop.Tick();

            Assert.That(kills.Progress, Is.EqualTo(3));
            Assert.That(keys.Progress, Is.GreaterThanOrEqualTo(40));
        }

        [Test]
        public void AFinishedQuestPaysOnceWithTheStreakBonus()
        {
            var loop = Loop();
            loop.Quests.State.Streak = 3;
            var shards = loop.State.Drops.Shards;
            var dust = loop.State.Drops.Dust;
            Assert.That(loop.ClaimQuest(false, 0), Is.EqualTo(QuestResult.NotFinished));

            Finish(loop, 0);
            Assert.That(loop.ClaimQuest(false, 0), Is.EqualTo(QuestResult.Done));
            Assert.That(loop.ClaimQuest(false, 0), Is.EqualTo(QuestResult.AlreadyClaimed));

            var reward = loop.Quests.RewardOf(false, loop.Quests.State.Daily[0].Kind);
            Assert.That(loop.State.Drops.Shards - shards, Is.EqualTo(System.Math.Round(reward.Shards * 1.3)));
            Assert.That(loop.State.Drops.Dust - dust, Is.EqualTo(reward.Dust * 1.3).Within(1e-9));
        }

        [Test]
        public void TheStreakGrowsWhenAllDailiesAreDoneAndHalvesOnAMiss()
        {
            var loop = Loop(100);
            loop.Quests.State.Streak = 4;
            for (var i = 0; i < Rules.PerDay; i++)
                Finish(loop, i);
            loop.SetLocalDay(101);
            Assert.That(loop.Quests.State.Streak, Is.EqualTo(5));

            loop.SetLocalDay(102);
            Assert.That(loop.Quests.State.Streak, Is.EqualTo(2), "a day without all dailies halves the streak");
        }

        [Test]
        public void ASwapGivesAnotherKindOncePerDay()
        {
            var loop = Loop();
            var before = loop.Quests.State.Daily[0].Kind;

            Assert.That(loop.SwapQuest(0), Is.EqualTo(QuestResult.Done));
            Assert.That(loop.Quests.State.Daily[0].Kind, Is.Not.EqualTo(before));
            Assert.That(loop.Quests.State.Daily.Take(Rules.PerDay).Select(entry => entry.Kind).Distinct().Count(), Is.EqualTo(Rules.PerDay));
            Assert.That(loop.SwapQuest(1), Is.EqualTo(QuestResult.NoSwapsLeft));
        }

        [Test]
        public void QuestsSurviveASave()
        {
            var loop = Loop();
            loop.Quests.State.Daily[0].Progress = 2;
            loop.Quests.State.Streak = 2;

            var copy = GameStateJson.Read(new Game.Core.Content.ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);

            Assert.That(copy.Quests.Daily.Select(entry => entry.Kind), Is.EqualTo(loop.Quests.State.Daily.Select(entry => entry.Kind)));
            Assert.That(copy.Quests.Daily[0].Progress, Is.EqualTo(2));
            Assert.That(copy.Quests.Streak, Is.EqualTo(2));
            Assert.That(copy.Quests.Day, Is.EqualTo(100));
        }

        [Test]
        public void TheQuestWindowHasTextsRewardsAndTheStreak()
        {
            var loop = Loop();
            var lines = QuestsView.Lines(loop.Quests);

            Assert.That(lines, Has.Count.EqualTo(Rules.PerDay + Rules.PerWeek));
            Assert.That(lines.All(line => !line.Text.StartsWith("quest.") && line.Reward.Length > 0), Is.True);
            Assert.That(QuestsView.Streak(loop.Quests), Does.Not.StartWith("quest."));
            foreach (QuestKind kind in System.Enum.GetValues(typeof(QuestKind)))
                Assert.That(QuestsView.Text(kind, 5), Does.Not.StartWith("quest."), kind.ToString());
        }

        [Test]
        public void TickingQuestsDoesNotAllocate()
        {
            var loop = Loop();
            for (var i = 0; i < 20; i++)
                loop.Tick();
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 200; i++)
                loop.Quests.Tick(loop.Events.Count);

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
