using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class CommonKeySkillTests
    {
        private static SkillTestHero Hero(params string[] skills) => SkillTestHero.Of(HeroClass.Ronin, skills);

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
        public void SpaceHammerMakesEverySpaceHitThreeTimesAsHard()
        {
            var hero = Hero("space_hammer");
            hero.Cast(0);

            hero.Input.PressSpace(2);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("space_hammer", 2 * 2)).Within(1e-9));
        }

        [Test]
        public void SpaceHammerDoesNothingBeforeTheCast()
        {
            var hero = Hero("space_hammer");

            hero.Input.PressSpace(2);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void CapsLockRageCountsOnlyUnbrokenHits()
        {
            var steady = Hero("caps_rage");
            steady.Cast(0);
            for (var i = 0; i < 20; i++)
            {
                steady.Input.PressKeys(1);
                steady.Ticks(2);
            }

            var broken = Hero("caps_rage");
            broken.Cast(0);
            for (var i = 0; i < 20; i++)
            {
                broken.Input.PressKeys(1);
                broken.Ticks(i == 10 ? 8 : 2);
            }

            Assert.That(steady.DamageFrom(DamageSource.Skill), Is.EqualTo(steady.SkillDamage("caps_rage", 5)).Within(1e-9));
            Assert.That(broken.DamageFrom(DamageSource.Skill), Is.EqualTo(0));
        }

        [Test]
        public void TypoTurnsEveryBackspaceIntoADoubleHit()
        {
            var hero = Hero("typo");
            Assert.That(hero.Cast(0), Is.EqualTo(CastResult.Passive));

            hero.Input.PressBackspace(1);
            hero.Ticks(1);

            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("typo", 1)).Within(1e-9));
        }

        [Test]
        public void TypistDoublesResourceFromKeys()
        {
            var hero = Hero("typist");
            hero.State.Resource = 50;
            hero.Cast(0);
            var perKey = TestWorld.Data.Combat.Resources.For("ronin").PerKey;

            hero.Ticks(1, keysPerTick: 4);

            Assert.That(hero.State.Resource, Is.EqualTo(4 * perKey * 2).Within(1e-9));
        }

        [Test]
        public void HotkeysChargeTenCombos()
        {
            var hero = Hero("hotkeys");
            hero.Cast(0);
            Assert.That(hero.State.Skills[0].HitsLeft, Is.EqualTo(10));

            for (var i = 0; i < 6; i++)
            {
                hero.Input.PressCombo(2);
                hero.Ticks(2);
            }

            Assert.That(hero.State.Skills[0].HitsLeft, Is.EqualTo(0));
            Assert.That(hero.DamageFrom(DamageSource.Skill), Is.EqualTo(hero.SkillDamage("hotkeys", 3 * 10)).Within(1e-6));
        }

        [Test]
        public void TouchTypingRewardsAMinuteOfSteadyTyping()
        {
            var hero = Hero("touch_typing");

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
