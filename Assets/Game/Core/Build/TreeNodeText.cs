using Game.Core.Content.Tree;
using Game.Core.Stats;

namespace Game.Core.Build
{
    public static class TreeNodeText
    {
        public static string AttributeName(ClassAttributeDef attribute) =>
            ModifierText.Label(attribute.Name.Length == 0 ? ClassAttributeDef.Stat : new StatId(attribute.Name));
    }
}
