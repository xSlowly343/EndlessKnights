using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class PrestigeTests
    {
        private static PrestigeRules Rules => TestWorld.Data.Prestige;

        private static GameLoop AtStage(long stage, double runGold)
        {
            var loop = TestWorld.Loop(state: new GameState
            {
                Class = HeroClass.Swordsman,
                Stage = stage,
                HighestStage = stage,
                RecordStage = stage,
                Gold = BigNumber.FromDouble(runGold / 2),
                RunGoldEarned = BigNumber.FromDouble(runGold),
                HeroLevel = 12
            });
            loop.State.Tree.PointsEarned = 20;
            return loop;
        }

        [Test]
        public void EchoIsTheRootOfRunGold()
        {
            Assert.That(Rules.EchoFor(BigNumber.FromDouble(Rules.GoldBase)).ToDouble(), Is.EqualTo(Rules.K));
            Assert.That(Rules.EchoFor(BigNumber.FromDouble(Rules.GoldBase * 4)).ToDouble(), Is.EqualTo(2 * Rules.K));
            Assert.That(Rules.EchoFor(BigNumber.FromDouble(Rules.GoldBase * 3)).ToDouble(), Is.EqualTo(System.Math.Floor(Rules.K * System.Math.Sqrt(3))));
            Assert.That(Rules.EchoFor(BigNumber.Zero), Is.EqualTo(BigNumber.Zero));
        }

        [Test]
        public void EchoGrowsSlowerThanGold()
        {
            var at50 = Rules.EchoFor(TestWorld.Data.Stages.EnemyGold(50) * 100).ToDouble();
            var at60 = Rules.EchoFor(TestWorld.Data.Stages.EnemyGold(60) * 100).ToDouble();

            Assert.That(at60 / at50, Is.EqualTo(System.Math.Pow(1.07, 5)).Within(0.05));
        }

        [Test]
        public void PrestigeOpensAtStageThirty()
        {
            Assert.That(AtStage(Rules.MinStage - 1, 1e6).Prestige.Available, Is.False);
            Assert.That(AtStage(Rules.MinStage, 1e6).Prestige.Available, Is.True);
            Assert.That(AtStage(Rules.MinStage - 1, 1e6).Prestige.TryPrestige(), Is.False);
        }

        [Test]
        public void PrestigeResetsStageAndGoldButKeepsTheBuild()
        {
            var loop = AtStage(45, 80000);
            loop.State.OwnedGems.Add(TestWorld.Data.Gems["lull"]);
            loop.AllocateNode("swordsman_ring_1");
            var gain = loop.Prestige.EchoGain;

            Assert.That(loop.Prestige.TryPrestige(), Is.True);

            var state = loop.State;
            Assert.That(state.Stage, Is.EqualTo(1));
            Assert.That(state.HighestStage, Is.EqualTo(1));
            Assert.That(state.Gold, Is.EqualTo(BigNumber.Zero));
            Assert.That(state.RunGoldEarned, Is.EqualTo(BigNumber.Zero));
            Assert.That(state.Echo, Is.EqualTo(gain));
            Assert.That(state.Prestiges, Is.EqualTo(1));
            Assert.That(state.RecordStage, Is.EqualTo(45));
            Assert.That(state.HeroLevel, Is.EqualTo(12));
            Assert.That(state.Tree.PointsSpent, Is.EqualTo(1));
            Assert.That(state.OwnedGems.Count, Is.EqualTo(1));
            Assert.That(state.EnemyAlive, Is.True);
            Assert.That(state.EnemyMaxHp, Is.EqualTo(TestWorld.Data.Stages.EnemyHp(1) * state.Enemy!.HpMultiplier));
            Assert.That(loop.Events.CountOf(GameEventKind.Prestige), Is.EqualTo(1));
        }

        [Test]
        public void ButtonLightsUpWhenGainDoublesTheEcho()
        {
            var loop = AtStage(60, 80000);
            var gain = loop.Prestige.EchoGain;

            loop.State.EchoEarned = gain * 1.2;
            Assert.That(loop.Prestige.Highlighted, Is.False);

            loop.State.EchoEarned = gain;
            Assert.That(loop.Prestige.Highlighted, Is.True);

            loop.State.EchoEarned = BigNumber.Zero;
            Assert.That(loop.Prestige.Highlighted, Is.True);
        }

        [Test]
        public void HalfAnHourOfTypingReachesThePrestigeStage()
        {
            var fresh = new Game.Core.Sim.Headless.Simulation(TestWorld.Data, new Game.Core.Sim.Headless.BotProfile(6)).Run(1800);
            Assert.That(fresh.HighestStage, Is.GreaterThan(Rules.MinStage));
        }

        [Test]
        public void PrestigeIsSaved()
        {
            var loop = AtStage(45, 80000);
            loop.Prestige.TryPrestige();

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.Echo, Is.EqualTo(loop.State.Echo));
            Assert.That(loaded.EchoEarned, Is.EqualTo(loop.State.EchoEarned));
            Assert.That(loaded.Prestiges, Is.EqualTo(1));
        }
    }
}
