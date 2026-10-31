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
            var (loop, input) = Hero(HeroClass.Viking, "iron_rim_bash", "no_mercy");

            input.ClickLeft();
            loop.Tick();

            Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Ready, Is.False);
            Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Ready, Is.True);
            Assert.That(loop.State.Resource, Is.EqualTo(85));
        }

        [Test]
        public void RightAndMiddleClicksCastTheSecondSlot()
        {
            var (right, rightInput) = Hero(HeroClass.Viking, "iron_rim_bash", "no_mercy");
            rightInput.ClickRight();
            right.Tick();

            var (middle, middleInput) = Hero(HeroClass.Viking, "iron_rim_bash", "no_mercy");
            middleInput.ClickMiddle();
            middle.Tick();

            foreach (var loop in new[] { right, middle })
            {
                Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Ready, Is.True);
                Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Ready, Is.False);
            }
        }

        [Test]
        public void WheelCastsNothing()
        {
            var (loop, input) = Hero(HeroClass.Viking, "iron_rim_bash", "no_mercy");

            input.ScrollWheel(20);
            loop.Tick();

            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(0));
        }

        [Test]
        public void ClickWhileOnCooldownDoesNothingAndTheFirstClickAfterCasts()
        {
            var (loop, input) = Hero(HeroClass.Viking, "iron_rim_bash");
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
        public void IronRimBashHitsForFourKeyStrikes()
        {
            var (loop, input) = Hero(HeroClass.Viking, "iron_rim_bash");

            input.ClickLeft();
            loop.Tick();

            Assert.That(SkillDamage(loop), Is.EqualTo(TestWorld.Data.Combat.KeyDamage * 4).Within(1e-9));
        }

        [Test]
        public void NoMercyHitsForFiveKeyStrikes()
        {
            var (loop, input) = Hero(HeroClass.Viking, "iron_rim_bash", "no_mercy");
            loop.State.Resource = 50;

            input.ClickRight();
            loop.Tick();

            Assert.That(SkillDamage(loop), Is.EqualTo(TestWorld.Data.Combat.KeyDamage * 5).Within(1e-9));
            Assert.That(loop.State.Resource, Is.EqualTo(0));
        }

        [Test]
        public void SongRaisesDamageAndGold()
        {
            var (loop, input) = Hero(HeroClass.Metalhead, "hearth_song");

            input.ClickLeft();
            loop.Tick();

            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(115).Within(1e-9));
            Assert.That(loop.Hero.Value(CombatStats.Gold, 100, StatTags.Gold), Is.EqualTo(110).Within(1e-9));
        }

        [Test]
        public void LouderDoublesTheSongAndAddsLightningToEveryKeyHit()
        {
            var (loop, input) = Hero(HeroClass.Metalhead, "hearth_song", "louder");
            input.ClickLeft();
            loop.Tick();
            loop.State.Resource = 100;

            input.ClickRight();
            loop.Tick();
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(130).Within(1e-9));

            loop.Events.Clear();
            input.PressKeys(1);
            loop.Tick();
            Assert.That(SkillDamage(loop), Is.EqualTo(TestWorld.Data.Combat.KeyDamage * 0.2 * 1.3).Within(1e-9));
        }

        [Test]
        public void SongReturnsToNormalWhenTheSoloEnds()
        {
            var (loop, input) = Hero(HeroClass.Metalhead, "hearth_song", "louder");
            input.ClickLeft();
            loop.Tick();
            loop.State.Resource = 100;
            input.ClickRight();
            loop.Tick();

            for (var i = 0; i < Gem("louder").Skill!.DurationSeconds * GameLoop.TicksPerSecond; i++)
                loop.Tick();

            Assert.That(loop.State.Skills[0].Active, Is.True);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard), Is.EqualTo(115).Within(1e-9));
        }
    }
}
