using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class OracleOmensTests
    {
        private static ClassResource Omens => TestWorld.Data.Combat.Resources.For("oracle");

        private static (GameLoop Loop, FakeInputSource Input) Oracle()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, TestWorld.Classless(new GameState { Class = HeroClass.Oracle }), new StatSheet());
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            return (loop, input);
        }

        private static void Move(GameLoop loop, FakeInputSource input, int ticks, float px)
        {
            for (var i = 0; i < ticks; i++)
            {
                input.MoveMouse(px);
                loop.Tick();
            }
        }

        [Test]
        public void OmensComeFromMouseNotKeys()
        {
            var (loop, input) = Oracle();

            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.Resource, Is.EqualTo(0));

            Move(loop, input, 6, (float)Omens.MousePxPerPoint / 6);
            Assert.That(loop.State.Resource, Is.EqualTo(1).Within(0.05));
        }

        [Test]
        public void ReadingWithoutKeysGivesAnOmenEverySecond()
        {
            var (loop, input) = Oracle();
            var readingTicks = (int)System.Math.Round(Omens.ReadingAfterKeylessSeconds * GameLoop.TicksPerSecond);

            Move(loop, input, readingTicks, 0.001f);
            var before = loop.State.Resource;
            Move(loop, input, 2 * GameLoop.TicksPerSecond, 0.001f);

            Assert.That(loop.State.Resource - before, Is.EqualTo(2 * Omens.ReadingPerSecond).Within(0.11));
        }

        [Test]
        public void NoReadingWhileTheMouseStands()
        {
            var (loop, _) = Oracle();

            for (var i = 0; i < 10 * GameLoop.TicksPerSecond; i++)
                loop.Tick();

            Assert.That(loop.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void ByTheGaitGivesThePartyCritChance()
        {
            var (loop, _) = Oracle();
            var gem = TestWorld.Real.Gems["by_the_gait"];
            loop.State.Resource = 100;
            TestWorld.ArmFor(loop, gem);
            var before = loop.Hero.Value(CombatStats.CritChance, 0);

            Assert.That(loop.Skills.TryCast(loop.Skills.Equip(gem)), Is.EqualTo(CastResult.Cast));

            Assert.That(loop.Hero.Value(CombatStats.CritChance, 0) - before, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(((AuraEffect)gem.Skill!.Effect).Party, Is.True);
            Assert.That(TestWorld.Real.SkillBook.StartingGems("oracle"), Has.Some.Property("Id").EqualTo("by_the_gait"));
        }

        [Test]
        public void StarChartWeakensTheNextFiveEnemies()
        {
            var (loop, _) = Oracle();
            var chart = (WeakenNextEffect)TestWorld.Real.Gems["star_chart"].Skill!.Effect;
            loop.State.Farming = true;
            loop.State.WeakenedSpawns = chart.Enemies;
            loop.State.WeakenShare = chart.HpShare;
            var full = TestWorld.Data.Stages.EnemyHp(loop.State.Stage).ToDouble();

            for (var i = 0; i < chart.Enemies; i++)
            {
                loop.State.EnemyHp = BigNumber.FromDouble(1e-9);
                loop.Stages.OnEnemyKilled();
                var multiplier = loop.State.Enemy?.HpMultiplier ?? 1;
                Assert.That(loop.State.EnemyMaxHp.ToDouble() / multiplier, Is.EqualTo(full * (1 - chart.HpShare)).Within(full * 1e-9), $"enemy {i}");
            }
            Assert.That(loop.State.WeakenedSpawns, Is.EqualTo(0));
            Assert.That(loop.State.WeakenShare, Is.EqualTo(0));
            Assert.That(TestWorld.Real.Gems["star_chart"].Locked, Is.False);
        }
    }
}
