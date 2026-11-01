namespace Game.Core.Content.Tree
{
    public sealed class TreeNodeKindRule
    {
        public TreeNodeKindRule(TreeNodeKind kind, int count, int fromRow)
        {
            Kind = kind;
            Count = count;
            FromRow = fromRow;
        }

        public TreeNodeKind Kind { get; }
        public int Count { get; }
        public int FromRow { get; }
    }
}
