using Game.Core.Content;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class TalentView
    {
        public static string Name(TalentDef talent) => Texts.Pick(talent.NameRu, talent.NameEn);

        public static string About(TalentDef talent) => Texts.Pick(talent.AboutRu, talent.AboutEn);

        public static string Status(TalentDef talent) => talent.Works ? Texts.Get("talent.works") : Texts.Get("talent.waits." + talent.Waits);
    }
}
