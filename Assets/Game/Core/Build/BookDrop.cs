using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public static class BookDrop
    {
        public static BookResult Apply(SkillBook book, GameState state, BookPlace from, BookPlace to)
        {
            switch (from.Spot)
            {
                case BookSpot.Bag when from.Gem != null:
                    return FromBag(book, state, from.Gem, to);
                case BookSpot.Skill:
                    return FromSkill(book, state, from.Row, to);
                case BookSpot.Trigger:
                    return FromTrigger(book, state, from.Row, to);
                default:
                    return BookResult.WrongKind;
            }
        }

        private static BookResult FromBag(SkillBook book, GameState state, GemDef gem, BookPlace to)
        {
            if (gem.Kind == GemKind.Trigger && (to.Spot == BookSpot.Trigger || to.Spot == BookSpot.Skill))
                return book.SetTrigger(to.Row, gem);
            if (gem.Kind != GemKind.Skill || to.Spot != BookSpot.Skill)
                return BookResult.WrongKind;
            if (to.Row < 0 || to.Row >= book.SlotCount)
                return BookResult.NoSuchSlot;
            return to.Row < state.Skills.Count ? Replace(book, state, to.Row, gem) : EquipAt(book, state, gem, state.Skills.Count);
        }

        private static BookResult FromSkill(SkillBook book, GameState state, int row, BookPlace to)
        {
            if (row < 0 || row >= state.Skills.Count)
                return BookResult.NoSuchSlot;
            switch (to.Spot)
            {
                case BookSpot.Bag:
                    return book.Unequip(row);
                case BookSpot.Skill when to.Row >= 0 && to.Row < book.SlotCount:
                    return book.Move(row, System.Math.Min(to.Row, state.Skills.Count - 1));
                default:
                    return BookResult.NoSuchSlot;
            }
        }

        private static BookResult FromTrigger(SkillBook book, GameState state, int row, BookPlace to)
        {
            if (row < 0 || row >= state.Skills.Count || state.Skills[row].Trigger == null)
                return BookResult.NoSuchSlot;
            var trigger = state.Skills[row].Trigger;
            if (to.Spot == BookSpot.Bag)
                return book.SetTrigger(row, null);
            if ((to.Spot != BookSpot.Trigger && to.Spot != BookSpot.Skill) || to.Row < 0 || to.Row >= state.Skills.Count)
                return BookResult.NoSuchSlot;
            if (to.Row == row)
                return BookResult.Done;
            var other = state.Skills[to.Row].Trigger;
            state.Skills[row].Trigger = other;
            state.Skills[to.Row].Trigger = trigger;
            return BookResult.Done;
        }

        private static BookResult Replace(SkillBook book, GameState state, int row, GemDef gem)
        {
            var old = state.Skills[row];
            if (ReferenceEquals(old.Skill, gem))
                return BookResult.Done;
            if (book.FreeCopies(gem) <= 0)
                return BookResult.NotOwned;
            var trigger = old.Trigger;
            book.Unequip(row);
            var result = EquipAt(book, state, gem, row);
            if (result != BookResult.Done)
            {
                EquipAt(book, state, old.Skill, row);
                if (trigger != null)
                    book.SetTrigger(row, trigger);
                return result;
            }
            if (trigger != null)
                book.SetTrigger(row, trigger);
            return BookResult.Done;
        }

        private static BookResult EquipAt(SkillBook book, GameState state, GemDef gem, int row)
        {
            var result = book.Equip(gem);
            if (result != BookResult.Done)
                return result;
            var index = state.Skills.Count - 1;
            state.Skills[index].StartCooldown((int)System.Math.Round(gem.Skill!.CooldownSeconds * GameLoop.TicksPerSecond));
            if (index != row)
                book.Move(index, row);
            return BookResult.Done;
        }
    }
}
