using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LootSystemTests
    {
        private static StatSheet Overpowered()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            return hero;
        }

        private static void KillOne(GameLoop loop, FakeInputSource input)
        {
            input.PressKeys(1);
            loop.Tick();
            loop.Tick();
        }

        private static double GoldEvents(GameLoop loop)
        {
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.GoldGained)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        [Test]
        public void NormalKillGivesStageGoldTimesEnemyMultiplier()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: Overpowered());
            var enemy = loop.State.Enemy!;

            KillOne(loop, input);

            var expected = TestWorld.Data.Stages.EnemyGold(1).ToDouble() * enemy.GoldMultiplier;
            Assert.That(loop.State.Gold.ToDouble(), Is.EqualTo(expected).Within(1e-9));
            Assert.That(loop.State.RunGoldEarned, Is.EqualTo(loop.State.Gold));
            Assert.That(GoldEvents(loop), Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void BossGivesBossGold()
        {
            var loop = TestWorld.Loop(hero: Overpowered());
            var boss = TestWorld.Data.Enemies.Boss(TestWorld.Data.Stages.Biome(3))!;

            var gold = loop.Loot.GoldFor(3, EnemyType.Boss, boss);

            Assert.That(gold.ToDouble(), Is.EqualTo(TestWorld.Data.Stages.BossGold(3).ToDouble() * boss.GoldMultiplier).Within(1e-9));
        }

        [Test]
        public void GoldOfAWholeStageAddsUp()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: Overpowered());

            for (var i = 0; i <= TestWorld.Data.Stages.EnemiesPerStage; i++)
                KillOne(loop, input);

            Assert.That(loop.State.Stage, Is.EqualTo(2));
            Assert.That(loop.Events.CountOf(GameEventKind.GoldGained), Is.EqualTo(TestWorld.Data.Stages.EnemiesPerStage + 1));
            Assert.That(loop.State.Gold.ToDouble(), Is.EqualTo(GoldEvents(loop)).Within(1e-6));
            Assert.That(loop.State.Gold.ToDouble(), Is.GreaterThan(TestWorld.Data.Stages.BossGold(1).ToDouble()));
        }

        [Test]
        public void GoldModifiersApply()
        {
            var hero = Overpowered();
            hero.Add(new Modifier(CombatStats.Gold, ModifierKind.Increased, 0.5, StatTags.Gold));
            var loop = TestWorld.Loop(hero: hero);

            var gold = loop.Loot.GoldFor(1, EnemyType.Normal, null);

            Assert.That(gold.ToDouble(), Is.EqualTo(TestWorld.Data.Stages.EnemyGold(1).ToDouble() * 1.5).Within(1e-9));
        }

        [Test]
        public void SpendingGoldDoesNotLowerRunEarnings()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: Overpowered());
            KillOne(loop, input);
            var earned = loop.State.RunGoldEarned;

            loop.State.Gold = BigNumber.Zero;
            KillOne(loop, input);

            Assert.That(loop.State.RunGoldEarned, Is.GreaterThan(earned));
            Assert.That(loop.State.Gold, Is.LessThan(loop.State.RunGoldEarned));
        }
    }
}
