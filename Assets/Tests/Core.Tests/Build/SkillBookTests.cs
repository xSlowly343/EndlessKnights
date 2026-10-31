using Game.Core.Build;
using Game.Core.Clock;
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
                loop.State.OwnedGems.Add(Gem(id));
            return loop;
        }

        [Test]
        public void BookStartsWithThreeSlots()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Book.SlotCount, Is.EqualTo(3));
            Assert.That(TestWorld.Data.SkillBook.MaxSlots, Is.EqualTo(5));
        }

        [Test]
        public void FourthAndFifthSlotsOpenAfterGuardiansOfStageHundredAndTwoHundred()
        {
            var loop = TestWorld.Loop();

            loop.State.RecordStage = 100;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(3));
            loop.State.RecordStage = 101;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(4));
            loop.State.RecordStage = 201;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(5));
            loop.State.RecordStage = 100000;
            Assert.That(loop.Book.SlotCount, Is.EqualTo(5));
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
        public void OnlyOwnedSkillsGoIn()
        {
            var loop = WithGems("through_stride");

            Assert.That(loop.Book.Equip(Gem("march")), Is.EqualTo(BookResult.NotOwned));
            Assert.That(loop.Book.Equip(Gem("through_stride")), Is.EqualTo(BookResult.Done));
            Assert.That(loop.Book.Equip(Gem("through_stride")), Is.EqualTo(BookResult.NotOwned));
        }

        [Test]
        public void NoMoreSkillsThanSlots()
        {
            var loop = WithGems("through_stride", "key_drum", "lull", "space_hammer");

            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("key_drum"));
            loop.Book.Equip(Gem("lull"));

            Assert.That(loop.Book.Equip(Gem("space_hammer")), Is.EqualTo(BookResult.NoFreeSlot));
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
        public void VikingStartsWithShieldBashOnLeftOdinsFuryOnRightAndLull()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking });

            Assert.That(loop.Book.GrantStartingGems(), Is.True);

            Assert.That(loop.State.OwnedGems.Count, Is.EqualTo(3));
            Assert.That(loop.State.Skills.Count, Is.EqualTo(3));
            Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Skill, Is.SameAs(Gem("iron_rim_bash")));
            Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Skill, Is.SameAs(Gem("no_mercy")));
            Assert.That(loop.State.Skills[2].Skill, Is.SameAs(Gem("lull")));
            Assert.That(loop.Book.GrantStartingGems(), Is.False);
        }

        [Test]
        public void MetalheadStartsWithSongOnLeftAndGuitarSoloOnRight()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Metalhead });

            loop.Book.GrantStartingGems();

            Assert.That(loop.State.Skills[SkillSystem.LeftButtonSlot].Skill, Is.SameAs(Gem("hearth_song")));
            Assert.That(loop.State.Skills[SkillSystem.RightButtonSlot].Skill, Is.SameAs(Gem("louder")));
        }

        [Test]
        public void EveryCharacterHasStartingSkillsOnBothButtons()
        {
            foreach (HeroClass heroClass in System.Enum.GetValues(typeof(HeroClass)))
            {
                var loop = TestWorld.Loop(state: new GameState { Class = heroClass });

                loop.Book.GrantStartingGems();

                Assert.That(loop.State.Skills.Count, Is.EqualTo(3), heroClass.ToString());
            }
        }

        [Test]
        public void OwnedGemsAreSaved()
        {
            var loop = TestWorld.Loop(state: new GameState { Class = HeroClass.Viking });
            loop.Book.GrantStartingGems();

            var loaded = SaveCodec.Decode(SaveCodec.Encode(loop.State, ClockStamp.First(new ClockReading(1, 1, 1))), TestWorld.Data).State;

            Assert.That(loaded.OwnedGems.Count, Is.EqualTo(3));
            Assert.That(loaded.Skills[1].Skill, Is.SameAs(Gem("no_mercy")));
        }

        [Test]
        public void OldSaveOwnsWhatItHadEquipped()
        {
            var save = SaveCodec.Decode(TestData.ReadSaveFixture("save_v2.json"), TestWorld.Data);

            Assert.That(save.State.OwnedGems.Count, Is.EqualTo(3));
        }
    }
}
