using System.Collections.Generic;
using Game.Core.Settings;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ConsentTexts
    {
        private const int CountedLines = 5;
        private const int NotCountedLines = 3;

        public static string Title => Texts.Get("consent.title");
        public static string Accept => Texts.Get("consent.accept");
        public static string Decline => Texts.Get("consent.decline");
        public static string Declined => Texts.Get("consent.declined");
        public static string NetworkNote => Texts.Get("consent.networkNote");
        public static string CountedHeading => Texts.Get("consent.countedHeading");
        public static string NotCountedHeading => Texts.Get("consent.notCountedHeading");
        public static string PolicyNote => Texts.Format("consent.policyNote", PrivacyPolicy.CurrentVersion);
        public static string Note => NetworkNote + " " + PolicyNote;

        public static IReadOnlyList<string> Counted => Lines("consent.counted.", CountedLines);

        public static IReadOnlyList<string> NotCounted => Lines("consent.notCounted.", NotCountedLines);

        private static IReadOnlyList<string> Lines(string prefix, int count)
        {
            var lines = new string[count];
            for (var i = 0; i < count; i++)
                lines[i] = Texts.Get(prefix + i);
            return lines;
        }
    }
}
