using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public static class GemBag
    {
        private static readonly List<GemDef> ClassSkills = new List<GemDef>();

        public static void Fill(SkillBook book, GameState state, GemBagFilter filter, List<GemDef> into)
        {
            into.Clear();
            if (filter != GemBagFilter.Supports)
            {
                book.OpenClassSkills(ClassSkills);
                foreach (var skill in ClassSkills)
                {
                    if (book.FreeCopies(skill) > 0)
                        into.Add(skill);
                }
            }
            foreach (var gem in state.OwnedGems)
            {
                if (!Matches(gem, filter) || into.Contains(gem) || book.FreeCopies(gem) <= 0)
                    continue;
                into.Add(gem);
            }
        }

        public static bool Matches(GemDef gem, GemBagFilter filter) => filter switch
        {
            GemBagFilter.Skills => gem.Kind == GemKind.Skill,
            GemBagFilter.Supports => gem.Kind == GemKind.Support,
            _ => true
        };
    }
}
