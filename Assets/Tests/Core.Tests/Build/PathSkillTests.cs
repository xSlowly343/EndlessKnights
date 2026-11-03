using System.Linq;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Sim;
using Game.Core.Stats;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class PathSkillTests
    {
        private static GemCatalog Gems => TestWorld.Data.Gems;

        private static GameLoop Duelist()
        {
            var loop = new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), new GameState { Class = HeroClass.Duelist }, new StatSheet());
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            Assert.That(loop.CompleteActivity("garden"), Is.True);
            return loop;
        }

        private static bool Owns(GameLoop loop, string gemId) => loop.State.OwnedGems.Any(gem => gem.Id == gemId);

        [Test]
        public void EveryPathHasOneSkillOnTheWeaponOfItsCharacter()
        {
            foreach (var path in TestWorld.Data.Ascendancy.Subclasses)
            {
                var gem = Gems.ForPath(path.Id);
                Assert.That(gem, Is.Not.Null, path.Id);
                Assert.That(gem!.Kind, Is.EqualTo(GemKind.Skill), path.Id);
                Assert.That(TestWorld.Data.Weapons.TryGetType(gem.Skill!.Weapon!, out var weapon), Is.True, path.Id);
                Assert.That(HeroClassNames.Of(weapon.HeroClass), Is.EqualTo(path.HeroClass), path.Id);
            }
            Assert.That(Gems.All.Count(gem => gem.Ascendancy != null), Is.EqualTo(54));
        }

        [Test]
        public void PathSkillsAreNotStartingGems()
        {
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
                Assert.That(TestWorld.Data.SkillBook.StartingGems(HeroClassNames.Of(heroClass)).Any(gem => gem.Ascendancy != null), Is.False, heroClass.ToString());
        }

        [Test]
        public void ChoosingAPathGivesItsSkill()
        {
            var loop = Duelist();
            Assert.That(Owns(loop, "duelist_path_2"), Is.False);

            Assert.That(loop.ChooseSubclass("duelist_trait_2"), Is.EqualTo(SubclassResult.Chosen));

            Assert.That(Owns(loop, "duelist_path_2"), Is.True);
            Assert.That(loop.State.OwnedGems.Count(gem => gem.Ascendancy != null), Is.EqualTo(1));
        }

        [Test]
        public void ChangingThePathTakesTheOldSkillAndGivesTheNewOne()
        {
            var loop = Duelist();
            loop.ChooseSubclass("duelist_trait_2");
            Assert.That(loop.Book.Equip(Gems["duelist_path_2"]), Is.EqualTo(BookResult.Done));

            Assert.That(loop.ChangeSubclassPaid("duelist_trait_1"), Is.EqualTo(SubclassResult.Chosen));

            Assert.That(Owns(loop, "duelist_path_2"), Is.False);
            Assert.That(Owns(loop, "duelist_path_1"), Is.True);
            Assert.That(loop.State.Skills.Any(slot => slot.Skill.Id == "duelist_path_2"), Is.False);
        }

        [Test]
        public void PathSkillOfAnotherPathCannotBeEquipped()
        {
            var loop = Duelist();
            loop.ChooseSubclass("duelist_trait_2");
            loop.State.OwnedGems.Add(Gems["duelist_path_1"]);

            Assert.That(loop.Book.Equip(Gems["duelist_path_1"]), Is.EqualTo(BookResult.WrongPath));
        }

        [Test]
        public void SavedPathSkillOfAnotherPathIsTakenAwayOnLoad()
        {
            var state = new GameState { Class = HeroClass.Duelist };
            state.OwnedGems.Add(Gems["duelist_path_1"]);

            var loop = new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), state, new StatSheet());

            Assert.That(Owns(loop, "duelist_path_1"), Is.False);
        }

        [Test]
        public void LockedSkillNeedsAWeapon()
        {
            var json = Content.TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"gems\": [",
                "\"gems\": [\n    { \"id\": \"x\", \"level\": 1, \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"locked\": true },");

            Assert.That(() => GemCatalog.Load(json), Throws.TypeOf<ContentException>().With.Message.Contains("class skill"));
        }
    }
}
