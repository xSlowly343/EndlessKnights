using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class ChronicleTests
    {
        private static ChronicleCatalog Catalog => TestWorld.Data.Chronicle;

        private static GameLoop AfterPrestige(int prestiges = 1) =>
            TestWorld.Loop(state: new GameState { Class = HeroClass.Viking, Prestiges = prestiges });

        private static void Reach(GameLoop loop, long stage)
        {
            loop.State.HighestStage = stage;
            loop.State.Stage = stage;
            loop.Tick();
        }

        [Test]
        public void TheChronicleHoldsFourteenTrialsWithAConditionAndAReward()
        {
            Assert.That(Catalog.All.Count, Is.EqualTo(14));
            Assert.That(Catalog.All.Select(trial => trial.Id).Distinct().Count(), Is.EqualTo(14));
            foreach (var trial in Catalog.All)
            {
                Assert.That(trial.Rules.Count > 0 || trial.NoSkills, Is.True, trial.Id);
                Assert.That(trial.Reward.Count, Is.GreaterThan(0), trial.Id);
                Assert.That(trial.GoalStage, Is.GreaterThan(1), trial.Id);
            }
        }

        [Test]
        public void TrialsOpenOnlyAfterTheFirstPrestige()
        {
            var loop = AfterPrestige(0);

            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.NeedsPrestige));
        }

        [Test]
        public void ATrialStartsOnlyAtTheBeginningOfARun()
        {
            var loop = AfterPrestige();
            loop.State.HighestStage = 5;

            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.RunNotFresh));
        }

        [Test]
        public void UnknownAndSecondTrialsAreRefused()
        {
            var loop = AfterPrestige();

            Assert.That(loop.StartTrial("no_such_trial"), Is.EqualTo(TrialResult.UnknownTrial));
            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.Started));
            Assert.That(loop.StartTrial("only_keys"), Is.EqualTo(TrialResult.AlreadyRunning));
        }

        [Test]
        public void TheTrialConditionHoldsUntilTheGoal()
        {
            var loop = AfterPrestige();
            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.Started));

            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.Zero);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse), Is.GreaterThan(0));
        }

        [Test]
        public void ReachingTheGoalCompletesTheTrialForeverAndGivesTheReward()
        {
            var loop = AfterPrestige();
            var mouseBefore = loop.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse);
            var pointsBefore = loop.State.TreePointsEarned;
            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.Started));
            var goal = Catalog.All.First(trial => trial.Id == "only_mouse").GoalStage;

            Reach(loop, goal - 1);
            Assert.That(loop.Chronicle.Active, Is.Not.Null);
            loop.Events.Clear();
            Reach(loop, goal);

            Assert.That(loop.Chronicle.Active, Is.Null);
            Assert.That(loop.Chronicle.IsCompleted("only_mouse"), Is.True);
            Assert.That(Enumerable.Range(0, loop.Events.Count).Count(i => loop.Events[i].Kind == GameEventKind.TrialCompleted), Is.EqualTo(1));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.GreaterThan(0));
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Mouse) / mouseBefore, Is.EqualTo(1.1).Within(1e-9));
            Assert.That(loop.State.TreePointsEarned - pointsBefore, Is.EqualTo(2));
        }

        [Test]
        public void TheThirdTrialAddsOnlyItsOwnPointAndTheFifthAddsAChroniclePoint()
        {
            var loop = AfterPrestige();
            var ids = Catalog.All.Select(trial => trial.Id).ToList();
            loop.State.Chronicle.Completed.AddRange(ids.Take(2));
            loop.Levels.SyncTreePoints();
            var two = loop.State.TreePointsEarned;

            loop.State.Chronicle.Completed.Add(ids[2]);
            loop.Levels.SyncTreePoints();
            Assert.That(loop.State.TreePointsEarned - two, Is.EqualTo(2));

            loop.State.Chronicle.Completed.Add(ids[3]);
            loop.Levels.SyncTreePoints();
            Assert.That(loop.State.TreePointsEarned - two, Is.EqualTo(3));
        }

        [Test]
        public void NoSkillsTrialForbidsCasting()
        {
            var hero = SkillTestHero.Of(HeroClass.Viking, "iron_rim_bash");
            hero.State.Prestiges = 1;
            hero.State.HighestStage = 1;

            Assert.That(hero.Loop.StartTrial("no_skills"), Is.EqualTo(TrialResult.Started));
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.TrialForbids));

            Assert.That(hero.Loop.AbandonTrial(), Is.True);
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Cast));
        }

        [Test]
        public void PrestigeDuringATrialEndsItWithoutTheReward()
        {
            var loop = AfterPrestige();
            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.Started));
            loop.State.Prestiges++;

            loop.Tick();

            Assert.That(loop.Chronicle.Active, Is.Null);
            Assert.That(loop.Chronicle.CompletedCount, Is.Zero);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.GreaterThan(0));
        }

        [Test]
        public void TheChronicleSurvivesSaving()
        {
            var loop = AfterPrestige();
            loop.State.Chronicle.Completed.Add("only_keys");
            Assert.That(loop.StartTrial("only_mouse"), Is.EqualTo(TrialResult.Started));
            var clock = ClockStamp.First(new ClockReading(1_760_000_000_000, 3_600_000, 12));

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, clock), TestWorld.Data).State;
            var again = TestWorld.Loop(state: loaded);

            Assert.That(loaded.Chronicle.Active, Is.EqualTo("only_mouse"));
            Assert.That(loaded.Chronicle.Completed, Is.EquivalentTo(new[] { "only_keys" }));
            Assert.That(again.Hero.Value(CombatStats.Damage, 1, StatTags.Keyboard), Is.Zero);
        }
    }
}
