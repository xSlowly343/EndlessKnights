using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;
using Game.Core.Tests.Sim;
using NUnit.Framework;

namespace Game.Core.Tests.Build
{
    public class BookDropTests
    {
        private static GemDef Gem(string id) => TestWorld.Data.Gems[id];

        private static GameLoop WithGems(params string[] ids)
        {
            var loop = TestWorld.Loop();
            foreach (var id in ids)
                loop.State.OwnedGems.Add(Gem(id));
            return loop;
        }

        private static BookResult Drop(GameLoop loop, BookPlace from, BookPlace to) => BookDrop.Apply(loop.Book, loop.State, from, to);

        private static string SkillAt(GameLoop loop, int row) => loop.State.Skills[row].Skill.Id;

        [Test]
        public void SkillFromTheBagGoesIntoAnEmptyRow()
        {
            var loop = WithGems("through_stride");

            Assert.That(Drop(loop, BookPlace.Bag(Gem("through_stride")), BookPlace.Skill(2)), Is.EqualTo(BookResult.Done));

            Assert.That(loop.State.Skills.Count, Is.EqualTo(1));
            Assert.That(SkillAt(loop, 0), Is.EqualTo("through_stride"));
        }

        [Test]
        public void NewlyPlacedSkillStartsOnCooldown()
        {
            var loop = WithGems("through_stride");

            Drop(loop, BookPlace.Bag(Gem("through_stride")), BookPlace.Skill(0));

            Assert.That(loop.State.Skills[0].Ready, Is.False);
        }

        [Test]
        public void SkillDroppedOnAFullRowReplacesIt()
        {
            var loop = WithGems("through_stride", "march");
            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("march"));
            loop.State.OwnedGems.Add(Gem("lull"));

            Assert.That(Drop(loop, BookPlace.Bag(Gem("lull")), BookPlace.Skill(0)), Is.EqualTo(BookResult.Done));

            Assert.That(SkillAt(loop, 0), Is.EqualTo("lull"));
            Assert.That(SkillAt(loop, 1), Is.EqualTo("march"));
            Assert.That(loop.Book.FreeCopies(Gem("through_stride")), Is.EqualTo(1));
        }

        [Test]
        public void ReplacingWithAGemYouDoNotHaveKeepsTheOldSkill()
        {
            var loop = WithGems("through_stride");
            loop.Book.Equip(Gem("through_stride"));

            Assert.That(Drop(loop, BookPlace.Bag(Gem("march")), BookPlace.Skill(0)), Is.EqualTo(BookResult.NotOwned));
            Assert.That(SkillAt(loop, 0), Is.EqualTo("through_stride"));
        }

        [Test]
        public void LockedRowRefusesASkill()
        {
            var loop = WithGems("through_stride");

            Assert.That(Drop(loop, BookPlace.Bag(Gem("through_stride")), BookPlace.Skill(loop.Book.SlotCount)), Is.EqualTo(BookResult.NoSuchSlot));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void SkillDraggedToTheBagIsUnequipped()
        {
            var loop = WithGems("through_stride");
            loop.Book.Equip(Gem("through_stride"));

            Assert.That(Drop(loop, BookPlace.Skill(0), BookPlace.Bag()), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void SkillDraggedToAnotherRowMovesThere()
        {
            var loop = WithGems("through_stride", "march", "lull");
            loop.Book.Equip(Gem("through_stride"));
            loop.Book.Equip(Gem("march"));
            loop.Book.Equip(Gem("lull"));

            Drop(loop, BookPlace.Skill(2), BookPlace.Skill(0));

            Assert.That(SkillAt(loop, 0), Is.EqualTo("lull"));
            Assert.That(SkillAt(loop, 1), Is.EqualTo("through_stride"));
        }

        [Test]
        public void BagShowsEachFreeGemOnceAndHonoursTheFilter()
        {
            var loop = WithGems("through_stride", "through_stride", "march", "rebound");
            loop.Book.Equip(Gem("march"));
            var bag = new List<GemDef>();

            GemBag.Fill(loop.Book, loop.State, GemBagFilter.All, bag);
            Assert.That(bag, Is.EqualTo(new[] { Gem("through_stride"), Gem("rebound") }));

            GemBag.Fill(loop.Book, loop.State, GemBagFilter.Supports, bag);
            Assert.That(bag, Is.EqualTo(new[] { Gem("rebound") }));
        }

        [Test]
        public void LockedRowsKnowTheirUnlockStage()
        {
            var loop = TestWorld.Loop();

            Assert.That(loop.Book.UnlockStageOf(0), Is.EqualTo(0));
            Assert.That(loop.Book.UnlockStageOf(3), Is.EqualTo(100));
            Assert.That(loop.Book.UnlockStageOf(4), Is.EqualTo(200));
        }
    }
}
