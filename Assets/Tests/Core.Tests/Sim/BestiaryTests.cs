using System.Linq;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Ui;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BestiaryTests
    {
        private static BestiaryCatalog Catalog => TestWorld.Real.Bestiary;

        private static int Kill(GameLoop loop, EnemyDef enemy, int times)
        {
            var opened = 0;
            for (var i = 0; i < times; i++)
            {
                loop.Events.Clear();
                loop.Events.Add(GameEvent.ForEnemy(GameEventKind.EnemyKilled, loop.State.Tick, 1, enemy));
                loop.Bestiary.Tick(0);
                for (var e = 0; e < loop.Events.Count; e++)
                {
                    if (loop.Events[e].Kind == GameEventKind.BestiaryEntryOpened)
                        opened++;
                }
            }
            return opened;
        }

        [Test]
        public void EveryEnemyHasAnEntryWithAWeakSpotAndAText()
        {
            foreach (var enemy in TestWorld.Real.Enemies.All)
            {
                Assert.That(Catalog.TryGet(enemy.Id, out var entry), Is.True, enemy.Id);
                Assert.That(entry.WeakSpotRu, Is.Not.Empty, enemy.Id);
                Assert.That(entry.TextEn, Is.Not.Empty, enemy.Id);
            }
            Assert.That(Catalog.All.All(e => TestWorld.Real.Enemies.TryGet(e.Enemy, out _)), Is.True, "no entry without an enemy");
        }

        [Test]
        public void AnEntryOpensOnceAfterEnoughVictoriesAndSurvivesASave()
        {
            var loop = TestWorld.Loop(new FakeInputSource());
            var guardian = TestWorld.Real.Enemies.All.First(e => e.Type == EnemyType.Guardian);
            var need = (int)Catalog.KillsToOpen(EnemyType.Guardian);

            Assert.That(Kill(loop, guardian, need - 1), Is.EqualTo(0));
            Assert.That(loop.Bestiary.IsOpen(guardian.Id), Is.False);
            Assert.That(BestiaryView.Lines(loop.Bestiary, TestWorld.Real.Enemies).Single(l => l.Enemy == guardian.Id).Text, Is.Empty);
            Assert.That(Kill(loop, guardian, 3), Is.EqualTo(1));
            Assert.That(loop.Bestiary.IsOpen(guardian.Id), Is.True);
            Assert.That(loop.Bestiary.KillsOf(guardian.Id), Is.EqualTo(need + 2));

            var copy = GameStateJson.Read(new ContentNode(GameStateJson.Write(loop.State), "save", string.Empty), TestWorld.Data.Enemies, TestWorld.Data.Gems, TestWorld.Data.Weapons, TestWorld.Data.Items, TestWorld.Data.Slates);
            Assert.That(copy.Bestiary.Kills[guardian.Id], Is.EqualTo(need + 2));
            Assert.That(copy.Bestiary.Opened, Does.Contain(guardian.Id));
            Assert.That(BestiaryView.Lines(loop.Bestiary, TestWorld.Real.Enemies).Single(l => l.Enemy == guardian.Id).Text, Is.Not.Empty);
        }

        [Test]
        public void NormalEnemiesNeedAThousandBossesFiftyGuardiansTen()
        {
            Assert.That(Catalog.KillsToOpen(EnemyType.Normal), Is.EqualTo(1000));
            Assert.That(Catalog.KillsToOpen(EnemyType.Boss), Is.EqualTo(50));
            Assert.That(Catalog.KillsToOpen(EnemyType.Guardian), Is.EqualTo(10));
        }
    }
}
