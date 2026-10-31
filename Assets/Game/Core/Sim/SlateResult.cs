namespace Game.Core.Sim
{
    public enum SlateResult
    {
        Done,
        Locked,
        NotInBag,
        NotOnBoard,
        OutOfBoard,
        Overlaps,
        AlreadyTempered,
        NotEnoughShards,
        TemperFailed
    }
}
