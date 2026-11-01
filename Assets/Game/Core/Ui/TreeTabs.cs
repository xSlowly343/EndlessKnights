using System.Collections.Generic;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class TreeTabs
    {
        public static void Fill(GameLoop loop, List<TreeTab> into)
        {
            into.Clear();
            var state = loop.State;
            into.Add(new TreeTab
            {
                Kind = TreeTabKind.Class,
                Slot = 0,
                Open = loop.ClassTreeDef != null,
                TreeId = loop.ClassTreeDef != null ? state.ClassName : null,
                PointsLeft = loop.ClassTreeDef != null ? state.ClassTree.PointsLeft : 0
            });
            for (var slot = 0; slot < loop.CommonTreeSlots.Count; slot++)
            {
                into.Add(new TreeTab
                {
                    Kind = TreeTabKind.Common,
                    Slot = slot,
                    Open = loop.CommonTreeSlotOpen(slot),
                    OpenLevel = loop.CommonTreeSlots[slot].Level,
                    TreeId = slot < state.CommonTrees.Count ? state.CommonTrees[slot]?.TreeId : null,
                    TreeName = loop.CommonTreeDef(slot) is { } tree ? Texts.Pick(tree.NameRu, tree.NameEn) : null,
                    PointsLeft = loop.CommonTreeAllocation(slot)?.PointsLeft ?? 0
                });
            }
            var catalog = loop.Data.Ascendancy;
            into.Add(new TreeTab
            {
                Kind = TreeTabKind.Path,
                Slot = 0,
                Open = state.Ascendancy.Unlocked(catalog),
                TreeId = state.Ascendancy.Subclass,
                PointsLeft = state.Ascendancy.Unlocked(catalog) ? state.Ascendancy.PointsLeft(catalog) : 0
            });
        }

        public static string Locked(in TreeTab tab) => tab.Open ? string.Empty
            : tab.Kind == TreeTabKind.Common ? Texts.Format("trees.lockedLevel", tab.OpenLevel)
            : tab.Kind == TreeTabKind.Path ? Texts.Get("trees.lockedPath")
            : Texts.Get("trees.lockedClass");

        public static string Title(in TreeTab tab) => tab.Kind switch
        {
            TreeTabKind.Class => Texts.Get("trees.tab.class"),
            TreeTabKind.Path => Texts.Get("trees.tab.path"),
            _ => tab.TreeName == null ? Texts.Get("trees.tab.commonEmpty") : Texts.Format("trees.tab.common", tab.TreeName)
        };
    }
}
