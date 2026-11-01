using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public static class BookPlacing
    {
        public static BookResult PutInto(SkillBook book, GemDef skill, SkillSlotRole role)
        {
            var row = book.RowOf(skill);
            if (row != SkillLoadout.NoSlot)
                return book.Assign(row, role);
            if (!SkillLoadout.Accepts(role, skill))
                return BookResult.WrongSlot;
            var equipped = book.Equip(skill);
            if (equipped != BookResult.Done)
                return equipped;
            row = book.RowOf(skill);
            var assigned = book.Assign(row, role);
            if (assigned != BookResult.Done)
                book.Unequip(row);
            return assigned;
        }
    }
}
