using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LullTests
    {
        private static IdleStrikeEffect Effect => (IdleStrikeEffect)TestWorld.Data.Gems["lull"].Skill!.Effect;

        private static (GameLoop Loop, FakeInputSource Input) WithLull()
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, hero: hero);
            loop.Skills.Equip(TestWorld.Data.Gems["lull"]);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e12));
            loop.Events.Clear();
            return (loop, input);
        }

        private static void IdleFor(GameLoop loop, double seconds)
        {
            for (var i = 0; i < seconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();
        }

        private static double SkillDamage(GameLoop loop)
        {
            var total = 0.0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                    total += loop.Events[i].Amount.ToDouble();
            }
            return total;
        }

        [Test]
        public void FirstActionAfterLongPauseReleasesTheCharge()
        {
            var (loop, input) = WithLull();
            IdleFor(loop, 45);

            input.PressKeys(1);
            loop.Tick();

            var expected = TestWorld.Data.Combat.KeyDamage * Effect.ChargePerSecond * 45;
            Assert.That(SkillDamage(loop), Is.EqualTo(expected).Within(expected * 0.01));
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
            Assert.That(loop.State.Skills[0].Ready, Is.False);
        }

        [Test]
        public void ChargeStopsGrowingAtTheCap()
        {
            var (loop, input) = WithLull();
            IdleFor(loop, Effect.MaxChargeSeconds + 120);

            input.ClickLeft();
            loop.Tick();

            var expected = TestWorld.Data.Combat.KeyDamage * Effect.ChargePerSecond * Effect.MaxChargeSeconds;
            Assert.That(SkillDamage(loop), Is.EqualTo(expected).Within(1e-6));
        }

        [Test]
        public void ShortPauseReleasesNothing()
        {
            var (loop, input) = WithLull();
            IdleFor(loop, Effect.MinIdleSeconds - 1);

            Assert.That(loop.Skills.IdleStrikeCharge(0), Is.EqualTo(0));
            input.PressKeys(1);
            loop.Tick();

            Assert.That(SkillDamage(loop), Is.EqualTo(0));
            Assert.That(loop.State.Skills[0].Ready, Is.True);
        }

        [Test]
        public void ChargeIsVisibleWhileWaiting()
        {
            var (loop, _) = WithLull();

            IdleFor(loop, 40);

            Assert.That(loop.Skills.IdleStrikeCharge(0), Is.EqualTo(Effect.ChargePerSecond * 40).Within(0.01));
        }

        [Test]
        public void CooldownBlocksTheNextRelease()
        {
            var (loop, input) = WithLull();
            IdleFor(loop, 40);
            input.PressKeys(1);
            loop.Tick();
            loop.Events.Clear();

            IdleFor(loop, 40);
            input.PressKeys(1);
            loop.Tick();

            Assert.That(SkillDamage(loop), Is.EqualTo(0));
        }

        [Test]
        public void IdleModifiersBoostTheStrike()
        {
            var (loop, input) = WithLull();
            loop.Hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1.0, StatTags.Idle));
            IdleFor(loop, 40);

            input.PressKeys(1);
            loop.Tick();

            var expected = 2 * TestWorld.Data.Combat.KeyDamage * Effect.ChargePerSecond * 40;
            Assert.That(SkillDamage(loop), Is.EqualTo(expected).Within(expected * 0.01));
        }
    }
}
