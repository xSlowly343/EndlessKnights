namespace Game.Core.Ui
{
    public struct TreeTab
    {
        public TreeTabKind Kind;
        public int Slot;
        public bool Open;
        public int OpenLevel;
        public string? TreeId;
        public string? TreeName;
        public int PointsLeft;
    }
}
