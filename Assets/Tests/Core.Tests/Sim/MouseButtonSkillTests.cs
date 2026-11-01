using Game.Core.Content.Gems;
using Game.Core.Content;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class MouseButtonSkillTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static (GameLoop Loop, FakeInputSource Input) Hero(HeroClass heroClass, params string[] skills)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = TestWorld.Loop(input, new GameState { Class = heroClass }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            foreach (var skill in skills)
                loop.Skills.Equip(Gem(skill));
            loop.State.Resource = 100;
            loop.State.OverdriveSpent = true;
            loop.Events.Clear();
            return (loop, input);
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
        public void LeftClickCastsTheFirstSlot()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, "runedancer_hit_1", "runedancer_strong_1");

            input.ClickLeft();
            loop.Tick();

            Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Ready, Is.False);
            Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Ready, Is.True);
            Assert.That(loop.State.Resource, Is.EqualTo(90));
        }

        [Test]
        public void RightClickCastsTheSecondSlotAndMiddleClickOnlyHits()
        {
            var (right, rightInput) = Hero(HeroClass.Runedancer, "runedancer_hit_1", "runedancer_strong_1");
            rightInput.ClickRight();
            right.Tick();

            var (middle, middleInput) = Hero(HeroClass.Runedancer, "runedancer_hit_1", "runedancer_strong_1");
            middleInput.ClickMiddle();
            middle.Tick();

            Assert.That(right.State.Skills[SkillSystem.LeftButtonSlot].Ready, Is.True);
            Assert.That(right.State.Skills[SkillSystem.RightButtonSlot].Ready, Is.False);
            Assert.That(middle.State.Skills[SkillSystem.RightButtonSlot].Ready, Is.True);
            Assert.That(middle.State.KeyHits, Is.EqualTo(1));
        }

        [Test]
        public void WheelCastsNothing()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, "runedancer_hit_1", "runedancer_strong_1");

            input.ScrollWheel(20);
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
        }

        [Test]
        public void ClickWhileOnCooldownDoesNothingAndTheFirstClickAfterCasts()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, "runedancer_hit_1");
            input.ClickLeft();
            loop.Tick();
            var cooldown = loop.State.Skills[0].CooldownTicks;

            for (var i = 0; i < cooldown - 1; i++)
            {
                input.ClickLeft(3);
                loop.Tick();
            }
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));

            input.ClickLeft();
            loop.Tick();
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(2));
        }

        [Test]
        public void AHitSkillHitsForItsMultiplierInKeyStrikes()
        {
            var (loop, input) = Hero(HeroClass.Runedancer, "runedancer_hit_1");

            input.ClickLeft();
            loop.Tick();

            Assert.That(SkillDamage(loop), Is.EqualTo(TestWorld.Data.Combat.KeyDamage * 3).Within(1e-9));
        }
    }
}
