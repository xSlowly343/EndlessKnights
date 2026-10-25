using Game.Core.Content.Tree;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class TreeTexts
    {
        public static string Hint => Texts.Get("tree.hint");
        public static string Unavailable => Texts.Get("tree.unavailable");

        public static string Points(int left) => Texts.Format("tree.points", left);

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
