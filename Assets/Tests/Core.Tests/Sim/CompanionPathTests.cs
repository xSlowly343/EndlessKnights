using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CompanionPathTests
    {
        private static GameLoop Awake(int circle)
        {
            var loop = TestWorld.Loop(new FakeInputSource(), new GameState(1));
            loop.Tick();
            loop.SetLocalDay(100);
            var progress = loop.Companions.Progress("camp_dog");
            progress.Stage = TestWorld.Data.Companions.Stages.Count - 1;
            progress.Circle = circle;
            return loop;
        }

        [Test]
        public void ThreePathsWithFiveLevelsAndThreeForks()
        {
            var paths = TestWorld.Data.Companions.Paths.Paths;
            Assert.That(paths.Count, Is.EqualTo(3));
            foreach (var path in paths)
            {
                Assert.That(path.Levels.Count, Is.EqualTo(5), path.Id);
                Assert.That(path.Forks.Count, Is.EqualTo(3), path.Id);
                foreach (var fork in path.Forks)
                    Assert.That(fork.Count, Is.EqualTo(2), path.Id);
            }
        }

        [Test]
        public void APathOpensOnTheThirdCircle()
        {
            var loop = Awake(2);
            Assert.That(loop.Companions.ChoosePath("camp_dog", "tracker"), Is.EqualTo(CompanionResult.Locked));
            loop.Companions.Progress("camp_dog").Circle = 3;
            Assert.That(loop.Companions.ChoosePath("camp_dog", "nowhere"), Is.EqualTo(CompanionResult.UnknownTrait));
            Assert.That(loop.Companions.ChoosePath("camp_dog", "tracker"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.Companions.PathLevel("camp_dog"), Is.EqualTo(0));
        }

        [Test]
        public void CirclesFourToEightRaiseThePath()
        {
            var loop = Awake(3);
            var companions = loop.Companions;
            companions.ChoosePath("camp_dog", "tracker");
            companions.Progress("camp_dog").Circle = 4;
            Assert.That(companions.PathLevel("camp_dog"), Is.EqualTo(1));
            Assert.That(companions.Bonus("camp_dog", CompanionStat.TripLoot), Is.EqualTo(0.1).Within(1e-9));
            companions.Progress("camp_dog").Circle = 12;
            Assert.That(companions.PathLevel("camp_dog"), Is.EqualTo(5));
            Assert.That(companions.Bonus("camp_dog", CompanionStat.TripLoot), Is.EqualTo(0.4).Within(1e-9));
        }

        [Test]
        public void AForkIsChosenOnceAndAddsItsRule()
        {
            var loop = Awake(5);
            var companions = loop.Companions;
            companions.ChoosePath("camp_dog", "tracker");
            Assert.That(companions.ChooseFork("camp_dog", 1, "swift"), Is.EqualTo(CompanionResult.Locked));
            companions.Progress("camp_dog").Circle = 6;
            var before = companions.DurationFactor("camp_dog", 0);
            Assert.That(companions.ChooseFork("camp_dog", 1, "hunter"), Is.EqualTo(CompanionResult.UnknownTrait));
            Assert.That(companions.ChooseFork("camp_dog", 1, "swift"), Is.EqualTo(CompanionResult.Done));
            Assert.That(companions.ChooseFork("camp_dog", 1, "heavy"), Is.EqualTo(CompanionResult.Repeat));
            Assert.That(companions.DurationFactor("camp_dog", 0), Is.EqualTo(before * 0.8).Within(1e-9));
        }

        [Test]
        public void ChangingThePathCostsEssenceAndClearsTheForks()
        {
            var loop = Awake(6);
            var companions = loop.Companions;
            companions.ChoosePath("camp_dog", "tracker");
            companions.ChooseFork("camp_dog", 1, "swift");
            Assert.That(companions.ChoosePath("camp_dog", "hearth"), Is.EqualTo(CompanionResult.NoEssence));
            loop.State.Companions.Essence = TestWorld.Data.Companions.Paths.ChangeEssence;
            Assert.That(companions.ChoosePath("camp_dog", "hearth"), Is.EqualTo(CompanionResult.Done));
            Assert.That(loop.State.Companions.Essence, Is.EqualTo(0));
            Assert.That(companions.ForkOf("camp_dog", 1), Is.Empty);
            Assert.That(companions.PathLevel("camp_dog"), Is.EqualTo(3), "levels stay with the circles");
        }

        [Test]
        public void ThePathSurvivesASave()
        {
            var loop = Awake(6);
            loop.Companions.ChoosePath("camp_dog", "spirit");
            loop.Companions.ChooseFork("camp_dog", 1, "depth");
            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty),
                TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Companions.Progress["camp_dog"].Path, Is.EqualTo("spirit"));
            Assert.That(copy.Companions.Progress["camp_dog"].Forks, Is.EqualTo(new[] { "depth" }));
        }
    }
}
