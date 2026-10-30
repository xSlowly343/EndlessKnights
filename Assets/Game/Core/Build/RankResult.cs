namespace Game.Core.Build
{
    public enum RankResult
    {
        Done,
        UnknownNode,
        MaxRank,
        NoPoints,
        RowClosed,
        NeedsParent,
        NotTaken,
        HoldsChild,
        HoldsRow,
        UnknownMajor,
        MarkNotReached
    }
}
