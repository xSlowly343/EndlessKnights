using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class AchievementTests
    {
        private static AchievementCatalog Catalog => TestWorld.Data.Achievements;

        private static GameLoop Loop()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            for (var i = 0; i < AchievementSystem.CheckEveryTicks; i++)
                loop.Tick();
            return loop;
        }

        private static int Check(GameLoop loop)
        {
            var unlocked = 0;
            for (var i = 0; i < AchievementSystem.CheckEveryTicks; i++)
            {
                loop.Events.Clear();
                loop.Tick();
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Kind == GameEventKind.AchievementUnlocked)
                        unlocked++;
                }
            }
            return unlocked;
        }

        [Test]
        public void EveryCounterHasAnAchievementAndATextAndIdsAreUnique()
        {
            Assert.That(Catalog.All.Count, Is.GreaterThanOrEqualTo(30));
            Assert.That(Catalog.All.Select(a => a.Id).Distinct().Count(), Is.EqualTo(Catalog.All.Count));
            foreach (AchievementCounter counter in System.Enum.GetValues(typeof(AchievementCounter)))
                Assert.That(Catalog.All.Any(a => a.Counter == counter), Is.True, counter.ToString());
            foreach (var achievement in Catalog.All)
                Assert.That(AchievementsView.Text(achievement), Does.Not.StartWith("achievement."), achievement.Id);
        }

        [Test]
        public void KillsAddUpAcrossASaveAndUnlockOnceWithAnEvent()
        {
            var loop = Loop();
            var first = Catalog.All.First(a => a.Counter == AchievementCounter.Kills);
            loop.State.Kills += (long)first.Target - 1;
            Check(loop);
            Assert.That(loop.Achievements.IsUnlocked(first.Id), Is.False);

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Achievements.Totals[(int)AchievementCounter.Kills], Is.EqualTo(first.Target - 1));

            loop.State.Kills += 1;

            Assert.That(Check(loop), Is.EqualTo(1));
            Assert.That(loop.Achievements.IsUnlocked(first.Id), Is.True);
            Assert.That(loop.State.Achievements.Unsent, Does.Contain(first.Id));
            Assert.That(Check(loop), Is.EqualTo(0));
            loop.Achievements.Sent(first.Id);
            Assert.That(loop.State.Achievements.Unsent, Does.Not.Contain(first.Id));
        }

        [Test]
        public void ARunResetDoesNotLoseOrDoubleCountKills()
        {
            var loop = Loop();
            loop.State.Kills += 500;
            Check(loop);
            loop.State.Kills = 0;
            Check(loop);
            loop.State.Kills += 200;
            Check(loop);

            Assert.That(loop.Achievements.Value(AchievementCounter.Kills), Is.EqualTo(700));
        }

        [Test]
        public void RecordAndInputAchievementsReadTheLifetimeCounters()
        {
            var loop = Loop();
            loop.State.RecordStage = 60;
            loop.State.TotalInput.MouseMeters = 1500;
            Check(loop);

            Assert.That(loop.Achievements.IsUnlocked("record_50"), Is.True);
            Assert.That(loop.Achievements.IsUnlocked("mouse_1km"), Is.True);
            Assert.That(loop.Achievements.IsUnlocked("record_250"), Is.False);
            Assert.That(AchievementsView.Lines(loop.Achievements).Single(l => l.Id == "record_250").Progress, Is.EqualTo(60));
        }

        [Test]
        public void TickingAchievementsDoesNotAllocate()
        {
            var loop = Loop();
            Check(loop);
            var before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 200; i++)
            {
                loop.State.Tick++;
                loop.Achievements.Tick();
            }

            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }
    }
}
