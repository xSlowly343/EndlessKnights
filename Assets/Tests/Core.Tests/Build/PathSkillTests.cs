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

        private static GameLoop Ronin()
        {
            var loop = new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), new GameState { Class = HeroClass.Ronin }, new StatSheet());
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
            Assert.That(Gems.All.Count(gem => gem.Ascendancy != null), Is.EqualTo(42));
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
            var loop = Ronin();
            Assert.That(Owns(loop, "second_shadow"), Is.False);

            Assert.That(loop.ChooseSubclass("ninja"), Is.EqualTo(SubclassResult.Chosen));

            Assert.That(Owns(loop, "second_shadow"), Is.True);
            Assert.That(loop.State.OwnedGems.Count(gem => gem.Ascendancy != null), Is.EqualTo(1));
        }

        [Test]
        public void ChangingThePathTakesTheOldSkillAndGivesTheNewOne()
        {
            var loop = Ronin();
            loop.ChooseSubclass("ninja");
            TestWorld.ArmFor(loop, Gems["second_shadow"]);
            Assert.That(loop.Book.Equip(Gems["second_shadow"]), Is.EqualTo(BookResult.Done));

            Assert.That(loop.ChangeSubclassPaid("kensei"), Is.EqualTo(SubclassResult.Chosen));

            Assert.That(Owns(loop, "second_shadow"), Is.False);
            Assert.That(Owns(loop, "lift_the_anchor"), Is.True);
            Assert.That(loop.State.Skills.Any(slot => slot.Skill.Id == "second_shadow"), Is.False);
        }

        [Test]
        public void PathSkillOfAnotherPathCannotBeEquipped()
        {
            var loop = Ronin();
            loop.ChooseSubclass("ninja");
            loop.State.OwnedGems.Add(Gems["lift_the_anchor"]);
            TestWorld.ArmFor(loop, Gems["lift_the_anchor"]);

            Assert.That(loop.Book.Equip(Gems["lift_the_anchor"]), Is.EqualTo(BookResult.WrongPath));
        }

        [Test]
        public void SavedPathSkillOfAnotherPathIsTakenAwayOnLoad()
        {
            var state = new GameState { Class = HeroClass.Ronin };
            state.OwnedGems.Add(Gems["lift_the_anchor"]);

            var loop = new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), state, new StatSheet());

            Assert.That(Owns(loop, "lift_the_anchor"), Is.False);
        }

        [Test]
        public void PathSkillWithoutItsMechanicWaits()
        {
            var loop = new GameLoop(TestWorld.Data, new Game.Core.Input.FakeInputSource(), new GameState { Class = HeroClass.Viking }, new StatSheet());
            Assert.That(loop.CompleteActivity("garden"), Is.True);
            loop.ChooseSubclass("butcher");
            var gem = Gems["hook_and_cleaver"];
            TestWorld.ArmFor(loop, gem);

            Assert.That(gem.Locked, Is.True);
            Assert.That(Owns(loop, gem.Id), Is.True);
            Assert.That(loop.Book.Equip(gem), Is.EqualTo(BookResult.WaitsForMechanic));
        }

        [Test]
        public void SecondShadowRepeatsEveryFifthKeyHitTwice()
        {
            var input = new Game.Core.Input.FakeInputSource();
            var hero = new StatSheet();
            hero.Add(new Modifier(CombatStats.CritChance, ModifierKind.More, -1));
            var loop = new GameLoop(TestWorld.Data, input, new GameState { Class = HeroClass.Ronin }, hero);
            loop.State.SpawnEnemy(null, EnemyType.Normal, BigNumber.Create(1, 50));
            loop.CompleteActivity("garden");
            loop.ChooseSubclass("ninja");
            TestWorld.ArmFor(loop, Gems["second_shadow"]);
            Assert.That(loop.Book.Equip(Gems["second_shadow"]), Is.EqualTo(BookResult.Done));
            loop.State.Resource = 100;
            Assert.That(loop.Skills.TryCast(loop.State.Skills.Count - 1), Is.EqualTo(CastResult.Cast));
            loop.Events.Clear();

            for (var i = 0; i < 5; i++)
            {
                input.PressKeys(1);
                loop.Tick();
            }

            var skillHits = 0;
            for (var i = 0; i < loop.Events.Count; i++)
            {
                if (loop.Events[i].Kind == GameEventKind.Damage && loop.Events[i].Source == DamageSource.Skill)
                    skillHits++;
            }
            Assert.That(skillHits, Is.EqualTo(1));
        }

        [Test]
        public void LockedSkillNeedsAWeapon()
        {
            var json = Content.TestData.ReadGameDataFile(GemCatalog.FileName).Replace("\"gems\": [",
                "\"gems\": [\n    { \"id\": \"x\", \"kind\": \"active\", \"color\": \"red\", \"name\": { \"ru\": \"x\", \"en\": \"x\" }, \"cost\": {}, \"cooldownSeconds\": 1, \"locked\": true },");

            Assert.That(() => GemCatalog.Load(json), Throws.TypeOf<ContentException>().With.Message.Contains("class skill"));
        }
    }
}
