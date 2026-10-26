using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StageSystemTests
    {
        private static StatSheet Overpowered()
        {
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e12));
            return hero;
        }

        private static void TypeFor(GameLoop loop, FakeInputSource input, int keys)
        {
            for (var key = 0; key < keys; key++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
        }

        [Test]
        public void LoopStartsWithFirstEnemyOfStageOne()
        {
            var loop = TestWorld.Loop();
            var stages = TestWorld.Data.Stages;

            Assert.That(loop.State.Stage, Is.EqualTo(1));
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Normal));
            Assert.That(loop.State.Enemy, Is.Not.Null);
            Assert.That(loop.State.Enemy!.Biome, Is.EqualTo(stages.Biome(1)));
            Assert.That(loop.State.EnemyMaxHp.ToDouble(), Is.EqualTo(stages.EnemyHp(1).ToDouble() * loop.State.Enemy.HpMultiplier).Within(1e-9));
        }

        [Test]
        public void TenEnemiesThenBossThenNextStage()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: Overpowered());
            var perStage = TestWorld.Data.Stages.EnemiesPerStage;

            TypeFor(loop, input, perStage);
            Assert.That(loop.State.KillsOnStage, Is.EqualTo(perStage));
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Boss));
            Assert.That(loop.Events.CountOf(GameEventKind.BossStarted), Is.EqualTo(1));

            TypeFor(loop, input, 1);
            Assert.That(loop.State.Stage, Is.EqualTo(2));
            Assert.That(loop.State.KillsOnStage, Is.EqualTo(0));
            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Normal));
        }

        [Test]
        public void ThreeStagesInARow()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: Overpowered());
            var perStage = TestWorld.Data.Stages.EnemiesPerStage;

            TypeFor(loop, input, 3 * (perStage + 1));

            Assert.That(loop.State.Stage, Is.EqualTo(4));
            Assert.That(loop.State.HighestStage, Is.EqualTo(4));
            Assert.That(loop.Events.CountOf(GameEventKind.EnemyKilled), Is.EqualTo(3 * (perStage + 1)));
            Assert.That(loop.Events.CountOf(GameEventKind.BossDefeated), Is.EqualTo(3));
            Assert.That(loop.Events.CountOf(GameEventKind.EnemySpawned), Is.EqualTo(3 * (perStage + 1) + 1));
        }

        [Test]
        public void BossHpFollowsStageTable()
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, hero: Overpowered());
            var data = TestWorld.Data;

            TypeFor(loop, input, data.Stages.EnemiesPerStage);

            var boss = data.Enemies.Boss(data.Stages.Biome(1))!;
            Assert.That(loop.State.Enemy, Is.SameAs(boss));
            Assert.That(loop.State.EnemyMaxHp.ToDouble(), Is.EqualTo(data.Stages.BossHp(1).ToDouble() * boss.HpMultiplier).Within(1e-6));
        }

        [Test]
        public void GuardianStandsAtBiomeEndAndItsModifiersOnlyApplyDuringTheFight()
        {
            var input = new FakeInputSource();
            var hero = Overpowered();
            var loop = TestWorld.Loop(input, new GameState { Stage = TestWorld.Data.Stages.BiomeLength }, hero);
            var normalDamage = hero.Compute(CombatStats.Damage, 10, StatTags.Keyboard).ToDouble();

            TypeFor(loop, input, TestWorld.Data.Stages.EnemiesOnStage(TestWorld.Data.Stages.BiomeLength));

            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Guardian));
            Assert.That(loop.State.Enemy!.Id, Is.EqualTo("ancient_ent"));
            Assert.That(hero.Compute(CombatStats.Damage, 10, StatTags.Keyboard).ToDouble(), Is.EqualTo(normalDamage * 0.5).Within(normalDamage * 1e-9));
            Assert.That(hero.Compute(CombatStats.Damage, 10, StatTags.Click).ToDouble(), Is.EqualTo(normalDamage).Within(normalDamage * 1e-9));

            TypeFor(loop, input, 1);

            Assert.That(loop.State.EnemyType, Is.EqualTo(EnemyType.Normal));
            Assert.That(hero.Compute(CombatStats.Damage, 10, StatTags.Keyboard).ToDouble(), Is.EqualTo(normalDamage).Within(normalDamage * 1e-9));
        }

        [Test]
        public void BiomeWithoutEnemyDataStillSpawnsUnnamedEnemies()
        {
            var stages = TestWorld.Data.Stages;
            var loop = TestWorld.Loop(state: new GameState { Stage = stages.BiomeLength + 1 });

            Assert.That(TestWorld.Data.Enemies.Normals(stages.Biome(stages.BiomeLength + 1)), Is.Empty);
            Assert.That(loop.State.Enemy, Is.Null);
            Assert.That(loop.State.EnemyMaxHp.ToDouble(), Is.EqualTo(stages.EnemyHp(stages.BiomeLength + 1).ToDouble()).Within(1e-6));
        }

        [Test]
        public void SameSeedPicksSameEnemies()
        {
            string Sequence(ulong seed)
            {
                var input = new FakeInputSource();
                var loop = TestWorld.Loop(input, new GameState(seed), Overpowered());
                var ids = new System.Text.StringBuilder();
                for (var i = 0; i < 30; i++)
                {
                    ids.Append(loop.State.Enemy?.Id).Append(',');
                    TypeFor(loop, input, 1);
                }
                return ids.ToString();
            }

            Assert.That(Sequence(7), Is.EqualTo(Sequence(7)));
            Assert.That(Sequence(7), Is.Not.EqualTo(Sequence(8)));
        }
    }
}
