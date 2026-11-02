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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Duelist, HeroLevel = 10 });
            loop.Book.GrantStartingGems();
            loop.Levels.SyncTreePoints();
            loop.RaiseNode("duelist_r1c0");
            loop.RaiseNode("duelist_r1c0");
            loop.RaiseNode("duelist_r1c0");
            return loop;
        }

        private static double KeyboardDamage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 100, StatTags.Strike);

        [Test]
        public void SecondPresetStartsEmptyWithTheSamePoints()
        {
            var loop = TypistWithGems();

            var plain = TestWorld.Loop(state: new GameState { Class = HeroClass.Duelist });
            var earned = loop.State.TreePointsEarned;

            loop.SwitchPreset();

            Assert.That(loop.State.ActivePreset, Is.EqualTo(1));
            Assert.That(loop.State.TreePointsSpent, Is.EqualTo(0));
            Assert.That(loop.State.TreePointsEarned, Is.EqualTo(earned));
            Assert.That(earned, Is.GreaterThanOrEqualTo(10));
            Assert.That(loop.State.Skills, Is.Empty);
            Assert.That(KeyboardDamage(loop), Is.EqualTo(KeyboardDamage(plain)).Within(1e-9));
        }

        [Test]
        public void EachPresetKeepsItsOwnTreeAndSkills()
        {
            var loop = TypistWithGems();
            var raised = KeyboardDamage(loop);
            loop.SwitchPreset();
            loop.RaiseNode("duelist_r1c1");
            loop.Book.Equip(TestWorld.Data.Gems["lull"]);

            loop.SwitchPreset();
            Assert.That(loop.State.ActivePreset, Is.EqualTo(0));
            Assert.That(loop.State.TreePointsSpent, Is.EqualTo(3));
            Assert.That(KeyboardDamage(loop), Is.EqualTo(raised).Within(1e-9));
            Assert.That(loop.State.ClassTree.RankOf("duelist_r1c0"), Is.EqualTo(3));
            Assert.That(loop.State.Skills.Count, Is.EqualTo(3));
            Assert.That(loop.State.Skills[0].Skill, Is.SameAs(TestWorld.Data.Gems["duelist_hit_1"]));

            loop.SwitchPreset();
            Assert.That(loop.State.ClassTree.RankOf("duelist_r1c1"), Is.EqualTo(1));
            Assert.That(loop.State.ClassTree.RankOf("duelist_r1c0"), Is.Zero);
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
            loop.RaiseNode("duelist_r1c1");
            loop.Book.Equip(TestWorld.Data.Gems["lull"]);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.State.ActivePreset, Is.EqualTo(1));
            Assert.That(loaded.State.OtherPreset.ClassTree.PointsSpent, Is.EqualTo(3));
            Assert.That(loaded.State.OtherPreset.Skills.Count, Is.EqualTo(3));
            loaded.SwitchPreset();
            Assert.That(loaded.State.Skills[0].Skill, Is.SameAs(TestWorld.Data.Gems["duelist_hit_1"]));
        }
    }
}
