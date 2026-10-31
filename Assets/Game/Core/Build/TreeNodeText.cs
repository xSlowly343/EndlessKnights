using Game.Core.Content.Tree;
using Game.Core.Stats;
using Game.Core.Text;

namespace Game.Core.Build
{
    public static class TreeNodeText
    {
        public static string TypeLabel(TreeNodeDef node) => node.Type switch
        {
            TreeNodeType.Small => Texts.Get("tree.type.small"),
            TreeNodeType.Notable => Texts.Get("tree.type.notable"),
            TreeNodeType.Keystone => Texts.Get("tree.type.keystone"),
            _ => Texts.Get("tree.type.start")
        };

        public static string AttributeName(string attribute) => ModifierText.Label(new StatId(attribute));
    }
}
