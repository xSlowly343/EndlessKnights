using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content.Gems;
using Game.Core.Content.Tree;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class BuildCodeText
    {
        public static string Copied => Texts.Get("build.copied");

        public static string NothingToCopy => Texts.Get("build.noClass");

        public static string Result(BuildImportResult result) => Texts.Get(result switch
        {
            BuildImportResult.BadCode => "build.badCode",
            BuildImportResult.NoClass => "build.noClass",
            BuildImportResult.OtherClass => "build.otherClass",
            _ => "build.done"
        });

        public static string Imported(SharedBuild build, ClassTree tree, GemCatalog gems)
        {
            var skills = new List<string>();
            foreach (var entry in build.Skills)
            {
                if (gems.TryGet(entry[0], out var gem))
                    skills.Add(Texts.Pick(gem.NameRu, gem.NameEn));
            }
            var name = Texts.Pick(tree.NameRu, tree.NameEn);
            return skills.Count == 0
                ? Texts.Format("build.imported", name, build.Points)
                : Texts.Format("build.importedSkills", name, build.Points, string.Join(", ", skills));
        }
    }
}
