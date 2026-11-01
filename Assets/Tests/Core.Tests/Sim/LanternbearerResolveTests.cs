using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class LanternbearerResolveTests
    {
        private static BannerRules Rules => TestWorld.Data.Combat.Banner;
        private static ClassResource Resolve => TestWorld.Data.Combat.Resources.For("lanternbearer");

        private static GemDef Aura => new GemDef("test_aura", GemKind.Skill, GemColor.Red, "a", "a", StatTags.Aura,
            skill: new SkillDef(0, 1, 600, new AuraEffect(new[] { new Modifier(CombatStats.Damage, ModifierKind.Increased, 0.2) })));

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass)
        {
            var input = new FakeInputSource();
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, new StatSheet());
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e6));
            return (loop, input);
        }

        private static void Move(GameLoop loop, FakeInputSource input, double px)
        {
            var ticks = 50;
            for (var i = 0; i < ticks; i++)
            {
                input.MoveMouse((float)(px / ticks));
                loop.Tick();
            }
        }

        private static double AuraDamage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 1);

        [Test]
        public void ResolveComesFromKeys()
        {
            var (loop, input) = Hero(HeroClass.Lanternbearer);

            for (var i = 0; i < 3; i++)
            {
                input.PressKeys(1);
                loop.Tick();
                loop.Tick();
                loop.Tick();
            }

            Assert.That(loop.State.Resource, Is.EqualTo(3 * Resolve.PerKey).Within(1e-9));
        }

        [Test]
        public void TheMouseNoLongerRaisesABanner()
        {
            var (loop, input) = Hero(HeroClass.Lanternbearer);

            Move(loop, input, Rules.MousePx * 20);

            Assert.That(Rules.Step, Is.Zero);
            Assert.That(loop.State.BannerBonus, Is.Zero);
        }

        [Test]
        public void OtherCharactersRaiseNoBanner()
        {
            var (loop, input) = Hero(HeroClass.Minstrel);

            Move(loop, input, Rules.MousePx * 2);

            Assert.That(loop.State.BannerBonus, Is.EqualTo(0));
            Assert.That(loop.Hero.Value(CombatStats.AuraEffect, 1, StatTags.Aura), Is.EqualTo(1));
        }

        [Test]
        public void ForemansHammerIsTheStartingSkill()
        {
            Assert.That(TestWorld.Real.SkillBook.StartingGems("lanternbearer"), Has.Some.Property("Id").EqualTo("lanternbearer_hit_1"));
            Assert.That(TestWorld.Real.Gems["lanternbearer_hit_1"].Skill!.Weapon, Is.EqualTo("lantern_staff"));
        }
    }
}
