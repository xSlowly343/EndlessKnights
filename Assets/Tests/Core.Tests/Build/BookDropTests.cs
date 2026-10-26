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
            var loop = WithGems("stenography");

            Assert.That(Drop(loop, BookPlace.Bag(Gem("stenography")), BookPlace.Skill(2)), Is.EqualTo(BookResult.Done));

            Assert.That(loop.State.Skills.Count, Is.EqualTo(1));
            Assert.That(SkillAt(loop, 0), Is.EqualTo("stenography"));
        }

        [Test]
        public void NewlyPlacedSkillStartsOnCooldown()
        {
            var loop = WithGems("stenography");

            Drop(loop, BookPlace.Bag(Gem("stenography")), BookPlace.Skill(0));

            Assert.That(loop.State.Skills[0].Ready, Is.False);
        }

        [Test]
        public void SkillDroppedOnAFullRowReplacesItAndKeepsTheTrigger()
        {
            var loop = WithGems("stenography", "march", "cast_on_timer");
            loop.Book.Equip(Gem("stenography"));
            loop.Book.Equip(Gem("march"));
            loop.Book.SetTrigger(0, Gem("cast_on_timer"));
            loop.State.OwnedGems.Add(Gem("lull"));

            Assert.That(Drop(loop, BookPlace.Bag(Gem("lull")), BookPlace.Skill(0)), Is.EqualTo(BookResult.Done));

            Assert.That(SkillAt(loop, 0), Is.EqualTo("lull"));
            Assert.That(SkillAt(loop, 1), Is.EqualTo("march"));
            Assert.That(loop.State.Skills[0].Trigger, Is.SameAs(Gem("cast_on_timer")));
            Assert.That(loop.Book.FreeCopies(Gem("stenography")), Is.EqualTo(1));
        }

        [Test]
        public void ReplacingWithAGemYouDoNotHaveKeepsTheOldSkill()
        {
            var loop = WithGems("stenography");
            loop.Book.Equip(Gem("stenography"));

            Assert.That(Drop(loop, BookPlace.Bag(Gem("march")), BookPlace.Skill(0)), Is.EqualTo(BookResult.NotOwned));
            Assert.That(SkillAt(loop, 0), Is.EqualTo("stenography"));
        }

        [Test]
        public void LockedRowRefusesASkill()
        {
            var loop = WithGems("stenography");

            Assert.That(Drop(loop, BookPlace.Bag(Gem("stenography")), BookPlace.Skill(loop.Book.SlotCount)), Is.EqualTo(BookResult.NoSuchSlot));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void SkillDraggedToTheBagIsUnequipped()
        {
            var loop = WithGems("stenography");
            loop.Book.Equip(Gem("stenography"));

            Assert.That(Drop(loop, BookPlace.Skill(0), BookPlace.Bag()), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void SkillDraggedToAnotherRowMovesThere()
        {
            var loop = WithGems("stenography", "march", "lull");
            loop.Book.Equip(Gem("stenography"));
            loop.Book.Equip(Gem("march"));
            loop.Book.Equip(Gem("lull"));

            Drop(loop, BookPlace.Skill(2), BookPlace.Skill(0));

            Assert.That(SkillAt(loop, 0), Is.EqualTo("lull"));
            Assert.That(SkillAt(loop, 1), Is.EqualTo("stenography"));
        }

        [Test]
        public void TriggerGoesOnARowAndBackToTheBag()
        {
            var loop = WithGems("stenography", "cast_on_timer");
            loop.Book.Equip(Gem("stenography"));

            Assert.That(Drop(loop, BookPlace.Bag(Gem("cast_on_timer")), BookPlace.Trigger(0)), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills[0].Trigger, Is.SameAs(Gem("cast_on_timer")));

            Assert.That(Drop(loop, BookPlace.Trigger(0), BookPlace.Bag()), Is.EqualTo(BookResult.Done));
            Assert.That(loop.State.Skills[0].Trigger, Is.Null);
        }

        [Test]
        public void TriggerIsNotASkill()
        {
            var loop = WithGems("cast_on_timer");

            Assert.That(Drop(loop, BookPlace.Bag(Gem("cast_on_timer")), BookPlace.Skill(0)), Is.EqualTo(BookResult.NoSuchSlot));
            Assert.That(loop.State.Skills, Is.Empty);
        }

        [Test]
        public void TriggerMovesBetweenRows()
        {
            var loop = WithGems("stenography", "march", "cast_on_timer");
            loop.Book.Equip(Gem("stenography"));
            loop.Book.Equip(Gem("march"));
            loop.Book.SetTrigger(0, Gem("cast_on_timer"));

            Assert.That(Drop(loop, BookPlace.Trigger(0), BookPlace.Trigger(1)), Is.EqualTo(BookResult.Done));

            Assert.That(loop.State.Skills[0].Trigger, Is.Null);
            Assert.That(loop.State.Skills[1].Trigger, Is.SameAs(Gem("cast_on_timer")));
        }

        [Test]
        public void BagShowsEachFreeGemOnceAndHonoursTheFilter()
        {
            var loop = WithGems("stenography", "stenography", "march", "cast_on_timer");
            loop.Book.Equip(Gem("march"));
            var bag = new List<GemDef>();

            GemBag.Fill(loop.Book, loop.State, GemBagFilter.All, bag);
            Assert.That(bag, Is.EqualTo(new[] { Gem("stenography"), Gem("cast_on_timer") }));

            GemBag.Fill(loop.Book, loop.State, GemBagFilter.Triggers, bag);
            Assert.That(bag, Is.EqualTo(new[] { Gem("cast_on_timer") }));
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
