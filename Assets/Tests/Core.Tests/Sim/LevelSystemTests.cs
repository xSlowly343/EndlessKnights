using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Tree;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LevelSystemTests
    {
        private static HeroRules Rules => TestWorld.Data.Combat.Hero;

        private static (GameLoop Loop, FakeInputSource Input) Strong(GameState? state = null)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            return (TestWorld.Loop(input, state, hero), input);
        }

        private static void Kill(GameLoop loop, FakeInputSource input, int kills)
        {
            for (var i = 0; i < kills; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        [Test]
        public void NewHeroIsLevelOneWithOnePoint()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.State.HeroLevel, Is.EqualTo(1));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(1));
        }

        [Test]
        public void KillsGiveExperienceAndLevels()
        {
            var (loop, input) = Strong(new GameState { HeroXp = Rules.XpToNext(1) - 2 * Rules.XpPerKill });

            Kill(loop, input, 1);
            Assert.That(loop.State.HeroLevel, Is.EqualTo(1));

            Kill(loop, input, 1);
            Assert.That(loop.State.HeroLevel, Is.EqualTo(2));
            Assert.That(loop.State.HeroXp, Is.EqualTo(0).Within(1e-9));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(2));
            Assert.That(loop.Events.CountOf(GameEventKind.LevelUp), Is.EqualTo(1));
        }

        [Test]
        public void BossesGiveMoreExperience()
        {
            var (loop, input) = Strong();
            Kill(loop, input, TestWorld.Data.Stages.EnemiesPerStage);
            var before = loop.State.HeroXp + LevelXpSpent(loop.State.HeroLevel);

            Kill(loop, input, 1);

            var after = loop.State.HeroXp + LevelXpSpent(loop.State.HeroLevel);
            Assert.That(after - before, Is.EqualTo(Rules.XpPerKill * Rules.BossXpMultiplier).Within(1e-9));
        }

        [Test]
        public void MaxLevelGivesNoMoreExperience()
        {
            var (loop, input) = Strong(new GameState { HeroLevel = Rules.MaxLevel });

            Kill(loop, input, 5);

            Assert.That(loop.State.HeroLevel, Is.EqualTo(Rules.MaxLevel));
            Assert.That(loop.State.HeroXp, Is.EqualTo(0));
            Assert.That(loop.Levels.XpToNextLevel, Is.EqualTo(0));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(Rules.MaxLevel));
        }

        [Test]
        public void BeatingABiomeGuardianTheFirstTimeGivesAPoint()
        {
            var length = TestWorld.Data.Stages.BiomeLength;
            var (loop, input) = Strong(new GameState { Stage = length, HighestStage = length, RecordStage = length });
            var before = loop.State.Tree.PointsEarned;

            Kill(loop, input, TestWorld.Data.Stages.EnemiesOnStage(length) + 1);

            Assert.That(loop.State.Stage, Is.EqualTo(length + 1));
            Assert.That(loop.State.RecordStage, Is.EqualTo(length + 1));
            Assert.That(loop.Levels.GuardianPoints, Is.EqualTo(1));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(before + 1 + (loop.State.HeroLevel - 1)));
        }

        [Test]
        public void GuardianPointsAreCapped()
        {
            var loop = TestWorld.Loop(state: new GameState { RecordStage = 5000, HighestStage = 1 });

            Assert.That(loop.Levels.GuardianPoints, Is.EqualTo(Rules.MaxGuardianPoints));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(1 + Rules.MaxGuardianPoints + loop.Levels.MilestonePoints));
        }

        [Test]
        public void InputMilestonesGivePointsStepByStep()
        {
            var keys = TestWorld.Data.TreePoints.Ladders.Single(l => l.Counter == PointCounter.Keys);
            var state = new GameState();
            state.TotalInput.Keys = (ulong)keys.Steps[1];

            var loop = TestWorld.Loop(state: state);

            Assert.That(loop.Levels.MilestonePoints, Is.EqualTo(2));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(1 + 2));
        }

        [Test]
        public void EveryLadderStepCountsOnce()
        {
            var state = new GameState { Prestiges = 1000, RecordStage = 100_000 };
            state.TotalInput.Keys = ulong.MaxValue / 2;
            state.TotalInput.LeftClicks = ulong.MaxValue / 4;
            state.TotalInput.MouseMeters = 1e12;
            foreach (var trial in TestWorld.Data.Chronicle.All)
                state.Chronicle.Completed.Add(trial.Id);

            var loop = TestWorld.Loop(state: state);

            Assert.That(loop.Levels.MilestonePoints, Is.EqualTo(TestWorld.Data.TreePoints.MaxPoints));
        }

        [Test]
        public void TrackedSourcesGiveTheirShareOfTheHundredAndEightyPoints()
        {
            var points = Rules.MaxLevel - 1 + Rules.MaxGuardianPoints + TestWorld.Data.TreePoints.MaxPoints;

            Assert.That(points, Is.EqualTo(166));
        }

        [TestCase("\"counter\": \"keys\"", "\"counter\": \"steps\"", "unknown counter 'steps'")]
        [TestCase("[1, 10, 50, 100, 500]", "[1, 10, 5]", "steps must grow")]
        [TestCase("[1, 10, 50, 100, 500]", "[]", "a ladder needs at least one step")]
        public void BrokenLaddersAreReported(string original, string broken, string message)
        {
            var json = TestData.ReadGameDataFile(TreePointRules.FileName).Replace(original, broken);

            var error = Assert.Throws<ContentException>(() => TreePointRules.Load(json))!;

            Assert.That(error.Message, Does.Contain(message));
        }

        [Test]
        public void LevelAndRecordAreSaved()
        {
            var state = new GameState { HeroLevel = 12, HeroXp = 7.5, RecordStage = 140, HighestStage = 30, Stage = 30 };

            var loaded = SaveCodec.Decode(SaveCodec.Encode(state, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.HeroLevel, Is.EqualTo(12));
            Assert.That(loaded.HeroXp, Is.EqualTo(7.5));
            Assert.That(loaded.RecordStage, Is.EqualTo(140));
        }

        [Test]
        public void OldSavesStartAtLevelOneWithRecordFromTheirBestStage()
        {
            var save = SaveCodec.Decode(Content.TestData.ReadSaveFixture("save_v2.json"), TestWorld.Data);

            Assert.That(save.State.HeroLevel, Is.EqualTo(1));
            Assert.That(save.State.RecordStage, Is.EqualTo(save.State.HighestStage));
        }

        private static double LevelXpSpent(int level)
        {
            var total = 0.0;
            for (var l = 1; l < level; l++)
                total += Rules.XpToNext(l);
            return total;
        }
    }
}
