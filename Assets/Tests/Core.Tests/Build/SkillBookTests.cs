using System.Linq;
using Game.Core.Build;
using Game.Core.Clock;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Save;
using Game.Core.Sim;
using Game.Core.Tests.Content;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class SkillBookTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop WithGems(params string[] ids)
        {
            var loop = TestWorld.Loop();
            foreach (var id in ids)
            {
                loop.State.OwnedGems.Add(Gem(id));
                loop.State.HeroLevel = System.Math.Max(loop.State.HeroLevel, Gem(id).UnlockLevel);
            }
            return loop;
        }

        private static readonly string BookJson = TestData.ReadGameDataFile(SkillBookRules.FileName);

        private static string WithUnlockLevels(string levels) =>
            BookJson.Replace("\"skillUnlockLevels\": { \"common\": 1, \"rare\": 15, \"epic\": 30, \"legendary\": 50, \"mythic\": 75 }", "\"skillUnlockLevels\": " + levels);

        [Test]
        public void SkillRanksOpenAtOneFifteenThirtyFiftySeventyFive()
        {
            var rules = TestWorld.Data.SkillBook;

            Assert.That(rules.SkillUnlockLevel(SkillTier.Common), Is.EqualTo(1));
            Assert.That(rules.SkillUnlockLevel(SkillTier.Rare), Is.EqualTo(15));
            Assert.That(rules.SkillUnlockLevel(SkillTier.Epic), Is.EqualTo(30));
            Assert.That(rules.SkillUnlockLevel(SkillTier.Legendary), Is.EqualTo(50));
            Assert.That(rules.SkillUnlockLevel(SkillTier.Mythic), Is.EqualTo(75));
        }

        [TestCase("runedancer_strong_1", 30)]
        [TestCase("duelist_strong_1", 30)]
        [TestCase("dreamcatcher_strong_1", 30)]
        [TestCase("golemancer_strong_1", 30)]
        public void ClassSkillOpensAtTheHeroLevelOfItsRank(string id, int level)
        {
            var heroClass = System.Enum.Parse<HeroClass>(id.Substring(0, id.IndexOf('_')), true);
            var loop = TestWorld.Loop(state: new GameState { Class = heroClass, HeroLevel = level - 1 });

            Assert.That(loop.Book.SkillOpen(Gem(id)), Is.False);
            Assert.That(loop.Book.Equip(Gem(id)), Is.EqualTo(BookResult.LevelTooLow));

            loop.State.HeroLevel = level;

            Assert.That(loop.Book.SkillOpen(Gem(id)), Is.True);
            Assert.That(loop.Book.Equip(Gem(id)), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.OwnedGems, Does.Not.Contain(Gem(id)), "an open class skill needs no gem copy");
        }

        [Test]
        public void SkillsOfAnotherClassAndPathSkillsStayClosed()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = 100 });

            Assert.That(loop.Book.Equip(Gem("duelist_hit_1")), Is.EqualTo(BookResult.WrongClass));
            Assert.That(loop.Book.SkillOpen(Gem("runedancer_path_1")), Is.False);
        }

        [Test]
        public void OpenClassSkillsGrowWithTheHeroLevel()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer, HeroLevel = 1 });
            var open = new System.Collections.Generic.List<GemDef>();

            loop.Book.OpenClassSkills(open);
            var atStart = open.Count;
            loop.State.HeroLevel = 75;
            loop.Book.OpenClassSkills(open);

            Assert.That(atStart, Is.EqualTo(4));
            Assert.That(open.Count, Is.EqualTo(SkillRoster.FamiliesPerClass + SkillRoster.AurasPerClass));
            Assert.That(open, Does.Contain(Gem("runedancer_hit_3")).And.Not.Contain(Gem("runedancer_hit_1")), "the bag shows the form of the hero level");
        }

        [Test]
        public void UnlockLevelsMustCoverEveryRankAndNotGoDown()
        {
            Assert.That(Assert.Throws<ContentException>(() => SkillBookRules.Load(WithUnlockLevels("{ \"common\": 1, \"rare\": 15, \"epic\": 30, \"legendary\": 50 }"), TestWorld.Data.Gems))!.Message, Does.Contain("mythic"));
            Assert.That(Assert.Throws<ContentException>(() => SkillBookRules.Load(WithUnlockLevels("{ \"common\": 1, \"rare\": 40, \"epic\": 30, \"legendary\": 50, \"mythic\": 75 }"), TestWorld.Data.Gems))!.Message, Does.Contain("must not open earlier"));
            Assert.That(Assert.Throws<ContentException>(() => SkillBookRules.Load(WithUnlockLevels("{ \"common\": 2, \"rare\": 15, \"epic\": 30, \"legendary\": 50, \"mythic\": 75 }"), TestWorld.Data.Gems))!.Message, Does.Contain("level 1"));
            Assert.That(Assert.Throws<ContentException>(() => SkillBookRules.Load(WithUnlockLevels("{ \"common\": 1, \"rare\": 15, \"epic\": 30, \"legendary\": 50, \"mythic\": 75, \"ancient\": 90 }"), TestWorld.Data.Gems))!.Message, Does.Contain("ancient"));
        }

        [Test]
        public void BookStartsWithThreeSlots()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Book.SlotCount, Is.EqualTo(3));
            Assert.That(TestWorld.Data.SkillBook.MaxSlots, Is.EqualTo(4));
        }

        [Test]
        public void TheFourthSlotOpensAfterTheGuardianOfStageHundred()
        {
            var loop = TestWorld.Loop();

            loop.State.RecordStage = 100;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(3));
            loop.State.RecordStage = 101;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(4));
            loop.State.RecordStage = 100000;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(4));
        }

        [Test]
        public void SocketRulesAreReadyForSupportGems()
        {
            var rules = TestWorld.Data.SkillBook;

            Assert.That(rules.SocketsFor(1, 0), Is.EqualTo(1));
            Assert.That(rules.SocketsFor(50, 0), Is.EqualTo(2));
            Assert.That(rules.SocketsFor(250, 0), Is.EqualTo(3));
            Assert.That(rules.SocketsFor(250, 2), Is.EqualTo(5));
            Assert.That(rules.SocketsFor(250, 9), Is.EqualTo(5));
        }

        [Test]
        public void ACommonSkillOpensAtItsHeroLevelAndGoesInOnce()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Book.Equip(Gem("march")), Is.EqualTo(BookResult.LevelTooLow));
            Assert.That(loop.Book.Equip(Gem("through_stride")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.Book.Equip(Gem("through_stride")), Is.EqualTo(BookResult.AlreadyInBook));
            Assert.That(loop.Book.FreeCopies(Gem("march")), Is.Zero);

            loop.State.HeroLevel = Gem("march").UnlockLevel;

            Assert.That(loop.Book.FreeCopies(Gem("march")), Is.EqualTo(1));
            Assert.That(loop.Book.Equip(Gem("march")), Is.EqualTo(BookResult.Done));
        }

        [Test]
        public void EveryCommonSkillHasAnOpeningLevel()
        {
            var common = TestWorld.Data.Gems.All.Where(SkillBook.IsCommonSkill).ToList();

            Assert.That(common.Count, Is.EqualTo(29));
            Assert.That(common.Where(gem => gem.UnlockLevel == 1).Select(gem => gem.Id), Is.EquivalentTo(new[] { "through_stride", "lull" }));
            Assert.That(common.Max(gem => gem.UnlockLevel), Is.LessThanOrEqualTo(60));
        }

        [Test]
        public void NoMoreSkillsThanSlots()
        {
            var loop = WithGems("through_stride", "key_drum", "lull", "no_breather");

            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("key_drum"));
            loop.Book.Equip(Gem("lull"));

            Assert.That(loop.Book.Equip(Gem("no_breather")), Is.EqualTo(BookResult.NoFreeSlot));
        }

        [Test]
        public void SkillsCanBeRearrangedFreely()
        {
            var loop = WithGems("through_stride", "march", "lull");
            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("march"));
            loop.Book.Equip(Gem("lull"));

            Assert.That(loop.Book.Move(0, 2), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills[0].Skill, Is.SameAs(Gem("march")));
            Assert.That(loop.State.Skills[2].Skill, Is.SameAs(Gem("through_stride")));
            Assert.That(loop.Book.Move(0, 3), Is.EqualTo(BookResult.NoSuchSlot));
        }

        [Test]
        public void UnequippingAnAuraTakesItsBonusAway()
        {
            var loop = WithGems("march");
            loop.Book.Equip(Gem("march"));
            loop.State.Resource = 100;
            loop.Skills.TryCast(0);
            Assert.That(loop.State.Skills[0].Active, Is.True);

            Assert.That(loop.Book.Unequip(0), Is.EqualTo(BookResult.Done));

            Assert.That(loop.State.Skills, Is.Empty);
            Assert.That(loop.Hero.Value(CombatStats.Damage, 100), Is.EqualTo(100));
            Assert.That(loop.Book.FreeCopies(Gem("march")), Is.EqualTo(1));
        }

        [Test]
        public void RunedancerStartsWithAHitOnLeftAQueueSkillOnRightAndLull()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });

            Assert.That(loop.Book.GrantStartingGems(), Is.True);

            Assert.That(loop.State.OwnedGems, Is.EqualTo(new[] { Gem("lull") }), "class skills are not owned, they open with the hero level");
            Assert.That(loop.State.Skills.Count, Is.EqualTo(3));
            Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Skill, Is.SameAs(Gem("runedancer_hit_1")));
            Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Skill, Is.SameAs(Gem("runedancer_queue_1")));
            Assert.That(loop.State.Skills[2].Skill, Is.SameAs(Gem("lull")));
            Assert.That(loop.Book.GrantStartingGems(), Is.False);
        }

        [Test]
        public void MinstrelStartsWithItsOwnHitAndQueueSkill()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Minstrel });

            loop.Book.GrantStartingGems();

            Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Skill, Is.SameAs(Gem("minstrel_hit_1")));
            Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Skill, Is.SameAs(Gem("minstrel_queue_1")));
        }

        [Test]
        public void EveryCharacterHasStartingSkillsOnBothButtons()
        {
            foreach (var heroClass in HeroClassNames.ByName.Values)
            {
                var loop = TestWorld.Loop(state: new GameState { Class = heroClass });

                loop.Book.GrantStartingGems();

                Assert.That(loop.State.Skills.Count, Is.EqualTo(3), heroClass.ToString());
            }
        }

        [Test]
        public void OwnedGemsAreSaved()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Runedancer });
            loop.Book.GrantStartingGems();

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.OwnedGems.Count, Is.EqualTo(1));
            Assert.That(loaded.Skills[1].Skill, Is.SameAs(Gem("runedancer_queue_1")));
        }

        [Test]
        public void OldSaveOwnsWhatItHadEquipped()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v2.json"), TestWorld.Data);

            Assert.That(save.State.OwnedGems.Count, Is.EqualTo(3));
        }
    }
}
