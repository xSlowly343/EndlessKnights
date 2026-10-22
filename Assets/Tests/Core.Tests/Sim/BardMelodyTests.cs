using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class BardMelodyTests
    {
        private static SkillDef March => TestWorld.Data.Gems["march"].Skill!;

        private static (GameLoop Loop, FakeInputSource Input, StatSheet Hero) Bard(GameState? state = null)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            state ??= new GameState { Class = HeroClass.Bard };
            var loop = TestWorld.Loop(input, state, hero);
            if (loop.State.Skills.Count == 0)
                loop.Skills.Equip(TestWorld.Data.Gems["march"]);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(1e12));
            loop.Events.Clear();
            return (loop, input, hero);
        }

        private static void Click(GameLoop loop, FakeInputSource input, int clicks)
        {
            for (var i = 0; i < clicks; i++)
            {
                input.ClickLeft();
                loop.Tick();
                loop.Tick();
            }
        }

        private static double Damage(StatSheet hero) => hero.Compute(CombatStats.Damage, BigNumber.FromDouble(100), StatTags.Keyboard).ToDouble();

        [Test]
        public void FourClicksPlayAMelodyThatStartsTheAura()
        {
            var (loop, input, hero) = Bard();

            Click(loop, input, 3);
            Assert.That(loop.State.Skills[0].Active, Is.False);
            Assert.That(Damage(hero), Is.EqualTo(100));

            Click(loop, input, 1);
            Assert.That(loop.State.Skills[0].Active, Is.True);
            Assert.That(loop.State.Notes, Is.EqualTo(0));
            Assert.That(loop.Events.CountOf(GameEventKind.SkillCast), Is.EqualTo(1));
            Assert.That(loop.Events.CountOf(GameEventKind.AuraChanged), Is.EqualTo(1));
        }

        [Test]
        public void MarchGivesTwentyFivePercentDamage()
        {
            var (loop, input, hero) = Bard();

            Click(loop, input, 4);

            Assert.That(Damage(hero), Is.EqualTo(125).Within(1e-9));
        }

        [Test]
        public void AuraEndsAfterItsDuration()
        {
            var (loop, input, hero) = Bard();
            Click(loop, input, 4);
            var left = loop.State.Skills[0].ActiveTicks;

            for (var i = 0; i < left; i++)
                loop.Tick();

            Assert.That(loop.State.Skills[0].Active, Is.False);
            Assert.That(Damage(hero), Is.EqualTo(100));
            Assert.That(loop.Events.CountOf(GameEventKind.AuraChanged), Is.EqualTo(2));
        }

        [Test]
        public void NewMelodyRefreshesTheAuraWithoutStacking()
        {
            var (loop, input, hero) = Bard();
            Click(loop, input, 4);
            for (var i = 0; i < 100; i++)
                loop.Tick();

            Click(loop, input, 4);

            Assert.That(loop.State.Skills[0].ActiveTicks, Is.GreaterThan(March.DurationSeconds * GameLoop.TicksPerSecond - 10));
            Assert.That(Damage(hero), Is.EqualTo(125).Within(1e-9));
        }

        [Test]
        public void TypistHasNoNotesSoMarchNeverPlays()
        {
            var (loop, input, hero) = Bard(new GameState { Class = HeroClass.Swordsman });

            Click(loop, input, 8);

            Assert.That(loop.State.Skills[0].Active, Is.False);
            Assert.That(Damage(hero), Is.EqualTo(100));
        }

        [Test]
        public void ActiveAuraIsBackAfterLoading()
        {
            var (loop, input, _) = Bard();
            Click(loop, input, 4);
            var text = SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1)));

            var (loaded, _, hero) = Bard(SaveCodec.Decode(text, TestWorld.Data).State);

            Assert.That(loaded.State.Skills[0].Active, Is.True);
            Assert.That(Damage(hero), Is.EqualTo(125).Within(1e-9));
        }
    }
}
