using Game.Core.Content.Gems;

namespace Game.Core.Ui
{
    public readonly struct MasterPlace
    {
        public readonly int Index;
        public readonly GemDef Gem;
        public readonly bool Open;
        public readonly int BookRow;

        public MasterPlace(int index, GemDef gem, bool open, int bookRow)
        {
            Index = index;
            Gem = gem;
            Open = open;
            BookRow = bookRow;
        }

        public bool InBook => BookRow >= 0;
        public bool CanPlace => Open && !InBook && !Gem.Locked;
    }
}
