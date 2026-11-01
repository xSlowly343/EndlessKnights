using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CommonKeySkillTests
    {
        private static SkillTestHero Hero(params string[] skills) => SkillTestHero.Of(HeroClass.Duelist, skills);

        [Test]
        public void KeyDrumrollMakesEveryFifthKeyHitTwice()
        {
            var hero = Hero("key_drum");
            hero.Cast(0);

            for (var i = 0; i < 12; i++)
            {
                hero.Input.PressKeys(1);
                hero.Ticks(2);
            }

            var everyFifth = 0.0;
            var keyHits = 0;
            for (var i = 0; i < hero.Loop.Events.Count; i++)
            {
                var hit = hero.Loop.Events[i];
                if (hit.Kind == GameEventKind.Damage && hit.Source == DamageSource.Keyboard && ++keyHits % 5 == 0)
                    everyFifth += hit.Amount.ToDouble();
            }
            Assert.That(keyHits, Is.EqualTo(12));
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(everyFifth).Within(everyFifth * 1e-9));
        }

        [Test]
        public void OverheatTriplesFocusThenLeavesNoFocusForThirtySeconds()
        {
            var hero = Hero("overheat");
            hero.State.Focus = 2;
            hero.Cast(0);

            hero.Ticks(1, keysPerTick: 1);
            Assert.That(hero.State.FocusMultiplier, Is.EqualTo(hero.State.Focus * 3).Within(1e-9));

            for (var i = 0; i < 150; i++)
                hero.Ticks(1, keysPerTick: 1);
            Assert.That(hero.State.FocusMultiplier, Is.EqualTo(1));

            for (var i = 0; i < 300; i++)
                hero.Ticks(1, keysPerTick: 1);
            Assert.That(hero.State.FocusMultiplier, Is.EqualTo(hero.State.Focus).Within(1e-9));
            Assert.That(hero.State.FocusMultiplier, Is.GreaterThan(1));
        }

        [Test]
        public void CapsLockRageCountsOnlyUnbrokenHits()
        {
            var steady = Hero("no_breather");
            steady.Cast(0);
            for (var i = 0; i < 20; i++)
            {
                steady.Input.PressKeys(1);
                steady.Ticks(2);
            }

            var broken = Hero("no_breather");
            broken.Cast(0);
            for (var i = 0; i < 20; i++)
            {
                broken.Input.PressKeys(1);
                broken.Ticks(i == 10 ? 8 : 2);
            }

            Assert.That(steady.DamageFrom(DamageSource.Skill), Is.EqualTo(steady.SkillDamage("no_breather", 5)).Within(1e-9));
            Assert.That(broken.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void QuickStrideDoublesResourceFromKeys()
        {
            var hero = Hero("quick_stride");
            hero.State.Resource = 50;
            hero.Cast(0);
            var perKey = TestWorld.Data.Combat.Resources.For("duelist").PerKey;

            hero.Ticks(1, keysPerTick: 4);

            Assert.That(hero.State.Resource, Is.EqualTo(4 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void SteadyPaceRewardsAMinuteOfSteadyTyping()
        {
            var hero = Hero("steady_pace");

            for (var i = 0; i < 599; i++)
                hero.Ticks(1, keysPerTick: i % 10 == 0 ? 1 : 0);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100).Within(1e-9));

            hero.Ticks(5, keysPerTick: 1);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(120).Within(1e-9));

            hero.Ticks(25);
            Assert.That(hero.Loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(100).Within(1e-9));
        }
    }
}
