using System.Collections.Generic;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Text;

namespace Game.Core.Build
{
    public static class SubclassView
    {
        public static string Name(SubclassDef subclass) => Texts.Pick(subclass.NameRu, subclass.NameEn);

        public static string Blurb(SubclassDef subclass) => Texts.Pick(subclass.BlurbRu, subclass.BlurbEn);

        public static string Level(AscendancyState state, AscendancyCatalog catalog) =>
            Texts.Format("subclass.level", state.TraitLevel(catalog), TraitDef.MaxLevel);

        public static string? PathSkill(GemCatalog gems, SubclassDef subclass)
        {
            foreach (var gem in gems.All)
                if (gem.Ascendancy == subclass.Id)
                    return Texts.Format("subclass.skill", Texts.Pick(gem.NameRu, gem.NameEn));
            return null;
        }

        public static string Locked(AscendancyCatalog catalog)
        {
            var names = new List<string>();
            foreach (var activity in catalog.Activities)
                names.Add(Texts.Pick(activity.NameRu, activity.NameEn).ToLowerInvariant());
            return Texts.Format("subclass.locked", string.Join(", ", names));
        }
    }
}
