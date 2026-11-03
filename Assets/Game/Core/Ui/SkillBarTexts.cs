using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class SkillBarTexts
    {
        public static string Key(SkillSlotRole role) => role switch
        {
            SkillSlotRole.Left => Texts.Get("skillbar.key.left"),
            SkillSlotRole.Right => Texts.Get("skillbar.key.right"),
            _ => string.Empty
        };

        public static string Self => Texts.Get("skillbar.self");

        public static string Locked(in SkillBarPlace place) =>
            place.Open ? string.Empty
            : place.UnlockLevel > 0 ? Texts.Format("skillbar.lockedLevel", place.UnlockLevel)
            : place.UnlockStage > 0 ? Texts.Format("skillbar.lockedStage", place.UnlockStage)
            : Texts.Get("skillbar.lockedByItem");

        public static string Role(SkillSlotRole role) => role switch
        {
            SkillSlotRole.Left => Texts.Get("book.row.left"),
            SkillSlotRole.Right => Texts.Get("book.row.right"),
            SkillSlotRole.Auto => Texts.Get("book.row.auto"),
            _ => Texts.Get("book.row.aura")
        };
    }
}
