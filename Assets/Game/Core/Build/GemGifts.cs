using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public enum GiftCheck
    {
        Ready,
        PathGem,
        InTheBook,
        Bound
    }

    public static class GemGifts
    {
        public static int Bound(GameState state, GemDef gem) => state.BoundGems.TryGetValue(gem.Id, out var count) ? count : 0;

        public static int Owned(GameState state, GemDef gem)
        {
            var owned = 0;
            foreach (var item in state.OwnedGems)
            {
                if (ReferenceEquals(item, gem))
                    owned++;
            }
            return owned;
        }

        public static GiftCheck Check(SkillBook book, GameState state, GemDef gem)
        {
            if (gem.Ascendancy != null)
                return GiftCheck.PathGem;
            if (book.FreeCopies(gem) <= 0)
                return GiftCheck.InTheBook;
            return Owned(state, gem) - Bound(state, gem) > 0 ? GiftCheck.Ready : GiftCheck.Bound;
        }

        public static bool Give(SkillBook book, GameState state, GemDef gem)
        {
            if (Check(book, state, gem) != GiftCheck.Ready)
                return false;
            state.OwnedGems.RemoveAt(state.OwnedGems.LastIndexOf(gem));
            return true;
        }

        public static void Receive(GameState state, GemDef gem)
        {
            state.OwnedGems.Add(gem);
            state.BoundGems[gem.Id] = Bound(state, gem) + 1;
        }
    }
}
