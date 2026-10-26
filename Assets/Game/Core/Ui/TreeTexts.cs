using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Tree;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class TreeTexts
    {
        public static string Hint => Texts.Get("tree.hint");
        public static string Unavailable => Texts.Get("tree.unavailable");

        public static string Points(int left) => Texts.Format("tree.points", left);

        public static string Reset => Texts.Get("tree.reset");

        public static string ResetConfirm(string cost) => cost.Length == 0 ? Texts.Get("tree.resetConfirmFree") : Texts.Format("tree.resetConfirm", cost);

        public static string ResetResult(RefundResult result, int points) => result switch
        {
            RefundResult.Refunded => Texts.Format("tree.resetDone", points),
            RefundResult.NotEnoughGold => Texts.Get("tree.resetNoGold"),
            _ => Texts.Get("tree.resetNothing")
        };

        public static string ClickTip(IReadOnlyList<TreeAction> actions)
        {
            if (actions.Count > 1)
                return Texts.Get("tree.tip.choose");
            if (actions.Count == 0)
                return string.Empty;
            return actions[0].Kind == TreeActionKind.Refund ? Texts.Get("tree.tip.refund") : Texts.Get("tree.tip.take");
        }

        public static string Taken(int spent, int total) => Texts.Format("tree.taken", spent, total);

        public static int TakeableCount(PassiveTree tree)
        {
            var count = 0;
            foreach (var node in tree.Nodes)
                if (node.Type != TreeNodeType.Start)
                    count++;
            return count;
        }
    }
}
