using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class PresetTests
    {
        private static GameLoop TypistWithGems()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Typist });
            loop.Book.GrantStartingGems();
            loop.State.Tree.PointsEarned = 10;
            loop.AllocateNode("t_pen_1");
            loop.AllocateNode("t_pen_2");
            return loop;
        }

        private static double KeyDamage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

        [Test]
        public void SecondPresetStartsEmptyWithTheSamePoints()
        {
            var loop = TypistWithGems();

            loop.SwitchPreset();

            Assert.That(loop.State.ActivePreset, Is.EqualTo(1));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(0));
            Assert.That(loop.State.Tree.PointsEarned, Is.EqualTo(10));
            Assert.That(loop.State.Skills, Is.Empty);
            Assert.That(KeyDamage(loop), Is.EqualTo(100));
        }

        [Test]
        public void EachPresetKeepsItsOwnTreeAndSkills()
        {
            var loop = TypistWithGems();
            loop.SwitchPreset();
            loop.AllocateNode("t_heavy_1");
            loop.Book.Equip(TestWorld.Data.Gems["lull"]);

            loop.SwitchPreset();
            Assert.That(loop.State.ActivePreset, Is.EqualTo(0));
            Assert.That(loop.State.Tree.PointsSpent, Is.EqualTo(2));
            Assert.That(KeyDamage(loop), Is.EqualTo(110).Within(1e-9));
            Assert.That(loop.State.Skills.Count, Is.EqualTo(2));
            Assert.That(loop.State.Skills[0].Trigger, Is.SameAs(TestWorld.Data.Gems["cast_on_enter"]));

            loop.SwitchPreset();
            Assert.That(loop.State.Tree.IsAllocated(TestWorld.Data.Tree, "t_heavy_1"), Is.True);
            Assert.That(loop.State.Skills.Count, Is.EqualTo(1));
            Assert.That(loop.State.Skills[0].Skill, Is.SameAs(TestWorld.Data.Gems["lull"]));
        }

        [Test]
        public void SwitchingPutsSkillsOnCooldown()
        {
            var loop = TypistWithGems();

            loop.SwitchPreset();
            loop.SwitchPreset();

            Assert.That(loop.State.Skills[0].Ready, Is.False);
            Assert.That(loop.Skills.TryCast(0), Is.EqualTo(CastResult.OnCooldown));
        }

        [Test]
        public void PresetsAreSaved()
        {
            var loop = TypistWithGems();
            loop.SwitchPreset();
            loop.AllocateNode("t_heavy_1");
            loop.Book.Equip(TestWorld.Data.Gems["lull"]);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.State.ActivePreset, Is.EqualTo(1));
            Assert.That(loaded.State.OtherPreset.Tree.PointsSpent, Is.EqualTo(2));
            Assert.That(loaded.State.OtherPreset.Skills.Count, Is.EqualTo(2));
            loaded.SwitchPreset();
            Assert.That(loaded.State.Skills[0].Trigger, Is.SameAs(TestWorld.Data.Gems["cast_on_enter"]));
        }
    }
}
