using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Sim.Headless;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class OfflineCalculatorTests
    {
        private static OfflineCalculator Calculator(StatSheet? hero = null) => new OfflineCalculator(TestWorld.Data, hero ?? new StatSheet());

        private static GameState AtStage(long stage) => new GameState { Stage = stage, HighestStage = stage };

        [TestCase(6, 0, 1)]
        [TestCase(8, 2, 1)]
        [TestCase(3, 0, 0.5)]
        [TestCase(4, 2, 2)]
        public void MatchesHeadlessSimulationWithinTenPercent(double keys, double clicks, double hours)
        {
            var simulated = new Simulation(TestWorld.Data, new BotProfile(keys, clicks)).Run(hours * 3600);

            var offline = Calculator().Simulate(new GameState(), hours * 3600, simulated.AverageDps, fightGuardians: true);

            Assert.That((offline.Gold / simulated.GoldEarned).ToDouble(), Is.EqualTo(1).Within(0.10));
            Assert.That((double)offline.StageAfter / simulated.FinalStage, Is.EqualTo(1).Within(0.10));
        }

        [Test]
        public void ApplyingOfflineToARunningLoopBringsTheNextEnemy()
        {
            var loop = TestWorld.Loop(state: new GameState());
            var away = new OfflineCalculator(TestWorld.Data, loop.Hero).Simulate(loop.State, 600, BigNumber.FromDouble(50), fightGuardians: false);

            loop.ApplyOffline(away);

            Assert.That(loop.State.EnemyAlive, Is.True);
            Assert.That(loop.State.Stage, Is.EqualTo(away.StageAfter));
        }

        [Test]
        public void OfflineTimeIsCapped()
        {
            var rules = TestWorld.Data.Combat.Offline;

            var result = Calculator().Simulate(new GameState(), 48 * 3600, BigNumber.FromDouble(50), fightGuardians: false);

            Assert.That(result.Seconds, Is.EqualTo(rules.CapSeconds));
        }

        [Test]
        public void OfflineDoesNotPassBiomeGuardian()
        {
            var guardianStage = TestWorld.Data.Stages.BiomeLength;

            var result = Calculator().Simulate(AtStage(guardianStage - 3), 3600, BigNumber.FromDouble(1e9), fightGuardians: false);

            Assert.That(result.StageAfter, Is.EqualTo(guardianStage));
            Assert.That(result.StoppedAtGuardian, Is.True);
            Assert.That(result.Farming, Is.True);
            Assert.That(result.Kills, Is.GreaterThan(1000));
        }

        [Test]
        public void WeakHeroGetsStuckAtBossAndFarms()
        {
            var stage = 30;
            var bossHp = TestWorld.Data.Stages.BossHp(stage).ToDouble() * TestWorld.Data.Enemies.Boss(TestWorld.Data.Stages.Biome(stage))!.HpMultiplier;
            var dps = bossHp / TestWorld.Data.Stages.BossTimerSeconds / 2;

            var result = Calculator().Simulate(AtStage(stage), 3600, BigNumber.FromDouble(dps), fightGuardians: false);

            Assert.That(result.StuckAtBoss, Is.True);
            Assert.That(result.StageAfter, Is.EqualTo(stage));
            Assert.That(result.Gold.Sign, Is.GreaterThan(0));
        }

        [Test]
        public void ShortAbsenceKillsSomeEnemiesOnTheSameStage()
        {
            var stage = 20;
            var hp = TestWorld.Data.Stages.EnemyHp(stage).ToDouble();

            var result = Calculator().Simulate(AtStage(stage), 3.5 * hp / 100, BigNumber.FromDouble(100), fightGuardians: false);

            Assert.That(result.StageAfter, Is.EqualTo(stage));
            Assert.That(result.KillsOnStage, Is.InRange(2, 4));
            Assert.That(result.Farming, Is.False);
        }

        [Test]
        public void NoDamageMeansNoProgress()
        {
            var state = AtStage(10);

            var result = Calculator().Simulate(state, 3600);

            Assert.That(state.RecentDps, Is.EqualTo(BigNumber.Zero));
            Assert.That(result.Kills, Is.EqualTo(0));
            Assert.That(result.Gold, Is.EqualTo(BigNumber.Zero));
        }

        [Test]
        public void OfflineDpsIsAShareOfRecentDps()
        {
            var state = new GameState { RecentDps = BigNumber.FromDouble(200) };

            var dps = Calculator().OfflineDps(state);

            Assert.That(dps.ToDouble(), Is.EqualTo(200 * TestWorld.Data.Combat.Offline.DpsShare).Within(1e-9));
        }

        [Test]
        public void GoldModifiersApplyOffline()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Gold, ModifierKind.More, 1.0, StatTags.Gold));

            var plain = Calculator().Simulate(AtStage(5), 600, BigNumber.FromDouble(100), fightGuardians: false);
            var doubled = Calculator(hero).Simulate(AtStage(5), 600, BigNumber.FromDouble(100), fightGuardians: false);

            Assert.That((doubled.Gold / plain.Gold).ToDouble(), Is.EqualTo(2).Within(1e-9));
        }

        [Test]
        public void OfflineGoldNodesOnlyBoostOfflineGold()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.OfflineGold, ModifierKind.Increased, 0.08));

            var plain = Calculator().Simulate(AtStage(5), 600, BigNumber.FromDouble(100), fightGuardians: false);
            var boosted = Calculator(hero).Simulate(AtStage(5), 600, BigNumber.FromDouble(100), fightGuardians: false);

            Assert.That((boosted.Gold / plain.Gold).ToDouble(), Is.EqualTo(1.08).Within(1e-9));
            Assert.That(TestWorld.Loop(hero: hero).Loot.GoldFor(5, EnemyType.Normal, null), Is.EqualTo(TestWorld.Loop().Loot.GoldFor(5, EnemyType.Normal, null)));
        }

        [Test]
        public void ApplyMovesTheGameForwardAndLoopContinues()
        {
            var state = AtStage(3);
            var result = Calculator().Simulate(state, 600, BigNumber.FromDouble(300), fightGuardians: false);

            OfflineCalculator.Apply(state, result);
            var loop = TestWorld.Loop(state: state);

            Assert.That(state.Stage, Is.EqualTo(result.StageAfter));
            Assert.That(state.Stage, Is.GreaterThan(3));
            Assert.That(state.HighestStage, Is.EqualTo(state.Stage));
            Assert.That(state.Gold, Is.EqualTo(result.Gold));
            Assert.That(state.RunGoldEarned, Is.EqualTo(result.Gold));
            Assert.That(loop.State.EnemyAlive, Is.True);
        }

        [Test]
        public void RecentDpsFollowsActivePlayAndIgnoresIdleTime()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input);
            for (var tick = 0; tick < 6000; tick++)
            {
                if (tick % 2 == 0)
                    input.PressKeys(1);
                loop.Tick();
            }
            var active = loop.State.RecentDps.ToDouble();

            for (var tick = 0; tick < 3000; tick++)
                loop.Tick();

            var expected = 5 * TestWorld.Data.Combat.KeyDamage * TestWorld.Data.Combat.Focus.Max;
            Assert.That(active, Is.EqualTo(expected).Within(expected * 0.15));
            Assert.That(loop.State.RecentDps.ToDouble(), Is.EqualTo(active).Within(active * 0.02));
        }
    }
}
