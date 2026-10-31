using Game.Core.Content.Gems;

namespace Game.Core.Build
{
    public readonly struct BookPlace
    {
        private BookPlace(BookSpot spot, int row, GemDef? gem)
        {
            Spot = spot;
            Row = row;
            Gem = gem;
        }

        public BookSpot Spot { get; }
        public int Row { get; }
        public GemDef? Gem { get; }

        public static BookPlace Nowhere => default;
        public static BookPlace Skill(int row) => new BookPlace(BookSpot.Skill, row, null);
        public static BookPlace Bag(GemDef? gem = null) => new BookPlace(BookSpot.Bag, -1, gem);
    }
}
