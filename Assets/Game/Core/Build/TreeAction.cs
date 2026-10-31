namespace Game.Core.Build
{
    public enum TreeActionKind
    {
        Allocate,
        Refund
    }

    public readonly struct TreeAction
    {
        public readonly TreeActionKind Kind;
        public readonly string Label;

        public TreeAction(TreeActionKind kind, string label)
        {
            Kind = kind;
            Label = label;
        }
    }
}
