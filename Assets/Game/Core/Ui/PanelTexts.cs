using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PanelTexts
    {
        public static string LockedShort(long stage) => Texts.Format("panel.lock.short", stage);
        public static string Locked(PanelSection section, long stage) => Texts.Format("panel.lock.notice", PanelSections.Title(section), stage);
        public static string Unlocked(PanelSection section) => Texts.Format("panel.unlocked", PanelSections.Title(section));
        public static string LevelUp(long level) => Texts.Format("panel.levelUp", level);
    }
}
