using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Sim;

namespace Game.Core.Build
{
    public static class GemBag
    {
        private static readonly List<GemDef> OpenSkills = new List<GemDef>();

        public static void Fill(SkillBook book, GameState state, IReadOnlyList<GemDef> catalog, GemBagFilter filter, List<GemDef> into)
        {
            into.Clear();
            if (filter != GemBagFilter.Supports)
            {
                book.OpenClassSkills(OpenSkills);
                AddFree(book, into);
                book.OpenCommonSkills(catalog, OpenSkills);
                AddFree(book, into);
            }
            foreach (var gem in state.OwnedGems)
            {
                if (gem.Kind != GemKind.Skill || !Matches(gem, filter) || into.Contains(gem) || book.FreeCopies(gem) <= 0)
                    continue;
                into.Add(gem);
            }
            if (filter == GemBagFilter.Skills)
                return;
            book.OpenSupports(catalog, OpenSkills);
            into.AddRange(OpenSkills);
        }

        private static void AddFree(SkillBook book, List<GemDef> into)
        {
            foreach (var skill in OpenSkills)
            {
                if (book.FreeCopies(skill) > 0 && !into.Contains(skill))
                    into.Add(skill);
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
