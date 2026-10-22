using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class StenographyTests
    {
        private static ChainHitsEffect Effect => (ChainHitsEffect)TestWorld.Data.Gems["stenography"].Skill!.Effect;

        private static (GameLoop Loop, FakeInputSource Input, int Slot) Typist(double enemyHp)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = HeroClass.Bard }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(enemyHp));
            var slot = loop.Skills.Equip(TestWorld.Data.Gems["stenography"]);
            loop.State.Energy = 100;
            loop.Events.Clear();
            return (loop, input, slot);
        }

        private static double Keys(GameLoop loop, FakeInputSource input, int count)
        {
            var dealt = 0.0;
            for (var i = 0; i < count; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
            }
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Keyboard)
                    dealt += loop.Events[i].Amount.ToDouble();
            }
            loop.Events.Clear();
            return dealt;
        }

        [Test]
        public void CastChargesSixtyChainHits()
        {
            var (loop, _, slot) = Typist(1e12);

            loop.Skills.TryCast(slot);

            Assert.That(loop.State.Skills[slot].HitsLeft, Is.EqualTo(Effect.Hits));
        }

        [Test]
        public void KeyHitsStoreHalfTheirDamageForTheNextEnemy()
        {
            var (loop, input, slot) = Typist(1e12);
            loop.Skills.TryCast(slot);

            var dealt = Keys(loop, input, 10);

            Assert.That(loop.State.Skills[slot].HitsLeft, Is.EqualTo(Effect.Hits - 10));
            Assert.That(loop.State.ChainDamage.ToDouble(), Is.EqualTo(dealt * Effect.NextEnemyShare).Within(1e-6));
        }

        [Test]
        public void OnlySixtyHitsAreChained()
        {
            var (loop, input, slot) = Typist(1e12);
            loop.Skills.TryCast(slot);

            var chained = Keys(loop, input, Effect.Hits);
            Keys(loop, input, 20);

            Assert.That(loop.State.Skills[slot].HitsLeft, Is.EqualTo(0));
            Assert.That(loop.State.ChainDamage.ToDouble(), Is.EqualTo(chained * Effect.NextEnemyShare).Within(1e-6));
        }

        [Test]
        public void NextEnemySpawnsAlreadyHurt()
        {
            var (loop, input, slot) = Typist(1e12);
            loop.Skills.TryCast(slot);
            loop.State.ChainDamage = BigNumber.FromDouble(5);
            loop.State.EnemyHp = BigNumber.FromDouble(1);

            var killingHit = Keys(loop, input, 1);

            var expected = 5 + killingHit * Effect.NextEnemyShare;
            Assert.That(loop.State.KillsOnStage, Is.EqualTo(1));
            Assert.That((loop.State.EnemyMaxHp - loop.State.EnemyHp).ToDouble(), Is.EqualTo(expected).Within(1e-6));
            Assert.That(loop.State.ChainDamage.IsZero, Is.True);
        }

        [Test]
        public void ChainCanKillTheNextEnemyOutright()
        {
            var (loop, input, slot) = Typist(1e12);
            loop.Skills.TryCast(slot);
            loop.State.ChainDamage = BigNumber.FromDouble(1e9);
            loop.State.EnemyHp = BigNumber.FromDouble(1);

            Keys(loop, input, 1);

            Assert.That(loop.State.KillsOnStage, Is.EqualTo(2));
            Assert.That(loop.State.EnemyAlive, Is.True);
            Assert.That(loop.State.ChainDamage.IsZero, Is.True);
        }

        [Test]
        public void ClicksAreNotChained()
        {
            var (loop, input, slot) = Typist(1e12);
            loop.Skills.TryCast(slot);

            input.ClickLeft(3);
            loop.Tick();

            Assert.That(loop.State.ChainDamage.IsZero, Is.True);
            Assert.That(loop.State.Skills[slot].HitsLeft, Is.EqualTo(Effect.Hits));
        }
    }
}
