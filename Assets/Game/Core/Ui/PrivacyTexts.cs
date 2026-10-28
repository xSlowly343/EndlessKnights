using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PrivacyTexts
    {
        public const int PolicyLines = 7;
        public const int EulaLines = 7;

        public static string PolicyTitle => Texts.Format("privacy.title", PrivacyPolicy.CurrentVersion);
        public static string EulaTitle => Texts.Format("eula.title", PrivacyPolicy.CurrentVersion);
        public static string GotIt => Texts.Get("report.gotIt");

        public static IReadOnlyList<string> Policy => Lines("privacy.line.", PolicyLines);

        public static IReadOnlyList<string> Eula => Lines("eula.line.", EulaLines);

        public static string Title(SettingId id) => id == SettingId.Eula ? EulaTitle : PolicyTitle;

        public static IReadOnlyList<string> Lines(SettingId id) => id == SettingId.Eula ? Eula : Policy;

        private static IReadOnlyList<string> Lines(string prefix, int count)
        {
            var lines = new string[count];
            for (var i = 0; i < count; i++)
                lines[i] = Texts.Get(prefix + i);
            return lines;
        }
    }
}
