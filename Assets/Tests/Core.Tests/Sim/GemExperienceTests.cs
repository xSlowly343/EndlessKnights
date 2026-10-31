using System.Linq;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Content;
using NUnit.Framework;

namespace Game.Core.Tests.Sim
{
    public class GemExperienceTests
    {
        private static GemLevels Levels => TestWorld.Data.Gems.Levels;
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static (GameLoop Loop, FakeInputSource Input) Hero(bool armed, params Modifier[] modifiers)
        {
            var input = new FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            hero.Add(new Modifier(CombatStats.Damage, ModifierKind.More, 1e60, StatTags.Keyboard));
            foreach (var modifier in modifiers)
                hero.Add(modifier);
            var loop = TestWorld.Loop(input, TestWorld.WithAllWeaponPlacesOpen(new GameState { Class = HeroClass.Viking }), hero);
            while (loop.State.Skills.Count > 0)
                loop.Skills.Unequip(0);
            while (loop.State.Weapons.Left != null || loop.State.Weapons.Right != null)
                loop.Weapons.Unequip(loop.State.Weapons.Left != null ? WeaponHand.Left : WeaponHand.Right);
            var skill = Gem("iron_rim_bash");
            if (armed)
                TestWorld.ArmFor(loop, skill);
            loop.State.OwnedGems.AddRange(new[] { skill, Gem("rebound") });
            loop.Skills.Equip(skill);
            loop.Book.SetSupport(0, 0, Gem("rebound"));
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.FromDouble(10));
            return (loop, input);
        }

        private static void Kill(GameLoop loop, FakeInputSource input)
        {
            var kills = loop.State.Kills;
            input.PressKeys(1);
            loop.Tick();
            Assert.That(loop.State.Kills, Is.EqualTo(kills + 1));
        }

        [Test]
        public void KillsTeachTheSkillAndItsSupports()
        {
            var (loop, input) = Hero(armed: true);

            Kill(loop, input);

            foreach (var id in new[] { "iron_rim_bash", "rebound" })
                Assert.That(loop.State.GemProgress[id].Experience, Is.EqualTo(Levels.ExperiencePerKill).Within(1e-9), id);
        }

        [Test]
        public void SkillWithoutItsWeaponLearnsNothing()
        {
            var (loop, input) = Hero(armed: false);
            Assert.That(loop.Skills.Armed(0), Is.False);

            Kill(loop, input);

            Assert.That(loop.State.GemProgress["iron_rim_bash"].Experience, Is.EqualTo(0));
        }

        [Test]
        public void GemExperienceStatSpeedsLearningUp()
        {
            var (loop, input) = Hero(true, new Modifier(CombatStats.GemExperience, ModifierKind.Increased, 0.5));

            Kill(loop, input);

            Assert.That(loop.State.GemProgress["iron_rim_bash"].Experience, Is.EqualTo(Levels.ExperiencePerKill * 1.5).Within(1e-9));
        }

        [Test]
        public void EnoughExperienceRaisesTheLevel()
        {
            var (loop, _) = Hero(armed: true);
            loop.Events.Clear();

            loop.GemExperience.Grant(Gem("iron_rim_bash"), Levels.ToNext(1) + 1);

            var progress = loop.State.GemProgress["iron_rim_bash"];
            Assert.That(progress.Level, Is.EqualTo(2));
            Assert.That(progress.Experience, Is.EqualTo(1).Within(1e-6));
            Assert.That(loop.Events.CountOf(GameEventKind.GemLevelUp), Is.EqualTo(1));
        }

        [Test]
        public void LevelStopsAtTheMaximum()
        {
            var (loop, _) = Hero(armed: true);

            loop.GemExperience.Grant(Gem("iron_rim_bash"), 1e15);

            Assert.That(loop.State.GemProgress["iron_rim_bash"].Level, Is.EqualTo(Levels.Max));
            Assert.That(loop.State.GemProgress["iron_rim_bash"].Experience, Is.EqualTo(0));
        }

        [Test]
        public void HigherLevelMakesTheCastStronger()
        {
            var (loop, _) = Hero(armed: true);
            loop.State.GemProgress["iron_rim_bash"].Level = 11;
            loop.State.Resource = 100;

            loop.Skills.TryCast(0);

            Assert.That(loop.State.Skills[0].CastPower, Is.EqualTo(Levels.EffectMultiplier(11)).Within(1e-9));
            Assert.That(Levels.EffectMultiplier(11), Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void ProgressSurvivesSaveAndLoad()
        {
            var (loop, _) = Hero(armed: true);
            loop.State.GemProgress["iron_rim_bash"].Level = 7;
            loop.State.GemProgress["iron_rim_bash"].Experience = 42;

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.LevelOf(Gem("iron_rim_bash")), Is.EqualTo(7));
            Assert.That(loaded.GemProgress["iron_rim_bash"].Experience, Is.EqualTo(42).Within(1e-9));
        }

        [Test]
        public void OldSaveStartsAtLevelOneAndStillLearns()
        {
            var state = SaveCodec.Decode(TestData.ReadSaveFixture("save_v2.json"), TestWorld.Data).State;
            var loop = TestWorld.Loop(state: state);

            foreach (var slot in loop.State.Skills)
            {
                Assert.That(loop.State.LevelOf(slot.Skill), Is.EqualTo(1));
                Assert.That(loop.State.GemProgress.ContainsKey(slot.Skill.Id), Is.True);
            }
        }

        [Test]
        public void CaravaneerCargoNodeIsOpen()
        {
            var caravaneer = TestWorld.Data.Ascendancy.Subclasses.Single(s => s.Id == "caravaneer");

            Assert.That(caravaneer.Nodes.Where(n => n.Locked), Is.Empty);
        }
    }
}
