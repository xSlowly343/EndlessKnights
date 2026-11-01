using Game.Core.Build;
using Game.Core.Content.Gems;

namespace Game.Core.Sim.Headless
{
    public static class BotBranches
    {
        public static BranchResult Choose(GameLoop loop, BotLoadout loadout, GemDef skill, int tier, int option)
        {
            var result = loop.ChooseBranch(skill, tier, option);
            if (result != BranchResult.SkillBusy)
                return result;
            loop.Book.Unequip(loop.Book.RowOf(skill));
            result = loop.ChooseBranch(skill, tier, option);
            loadout.Arrange();
            return result;
        }
    }
}
