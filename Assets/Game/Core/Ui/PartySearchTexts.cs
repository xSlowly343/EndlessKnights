using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PartySearchTexts
    {
        public static string Quick => Texts.Get("party.search.quick");
        public static string QuickHint => Texts.Get("party.search.quickHint");
        public static string Refresh => Texts.Get("party.search.refresh");
        public static string Join => Texts.Get("party.search.join");
        public static string Loading => Texts.Get("party.search.loading");
        public static string Empty => Texts.Get("party.search.empty");
        public static string InParty => Texts.Get("party.search.inParty");
        public static string Offline => Texts.Get("party.search.offline");
        public static string Found(int count) => Texts.Format("party.search.found", count);
    }
}
