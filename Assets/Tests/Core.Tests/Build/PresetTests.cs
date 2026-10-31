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
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin, HeroLevel = 10 });
            loop.Book.GrantStartingGems();
            loop.Levels.SyncTreePoints();
            loop.RaiseNode("ronin_katana");
            loop.RaiseNode("ronin_katana");
            loop.RaiseNode("ronin_katana");
            return loop;
        }

        private static double KeyboardDamage(GameLoop loop) => loop.Hero.Value(CombatStats.Damage, 100, StatTags.Keyboard);

        [Test]
        public void SecondPresetStartsEmptyWithTheSamePoints()
        {
            var loop = TypistWithGems();

            var plain = TestWorld.Loop(state: new GameState { Class = HeroClass.Ronin });
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
            loop.RaiseNode("ronin_breath");
            loop.Book.Equip(TestWorld.Data.Gems["lull"]);

            loop.SwitchPreset();
            Assert.That(loop.State.ActivePreset, Is.EqualTo(0));
            Assert.That(loop.State.TreePointsSpent, Is.EqualTo(3));
            Assert.That(KeyboardDamage(loop), Is.EqualTo(raised).Within(1e-9));
            Assert.That(loop.State.ClassTree.RankOf("ronin_katana"), Is.EqualTo(3));
            Assert.That(loop.State.Skills.Count, Is.EqualTo(3));
            Assert.That(loop.State.Skills[0].Skill, Is.SameAs(TestWorld.Data.Gems["thin_cut"]));

            loop.SwitchPreset();
            Assert.That(loop.State.ClassTree.RankOf("ronin_breath"), Is.EqualTo(1));
            Assert.That(loop.State.ClassTree.RankOf("ronin_katana"), Is.Zero);
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
            loop.RaiseNode("ronin_breath");
            loop.Book.Equip(TestWorld.Data.Gems["lull"]);

            var loaded = TestWorld.Loop(state: SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State);

            Assert.That(loaded.State.ActivePreset, Is.EqualTo(1));
            Assert.That(loaded.State.OtherPreset.ClassTree.PointsSpent, Is.EqualTo(3));
            Assert.That(loaded.State.OtherPreset.Skills.Count, Is.EqualTo(3));
            loaded.SwitchPreset();
            Assert.That(loaded.State.Skills[0].Skill, Is.SameAs(TestWorld.Data.Gems["thin_cut"]));
        }
    }
}
