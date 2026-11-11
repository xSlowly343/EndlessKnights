using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PartyCardText
    {
        public static bool IsOwnTitle(string titleId) => titleId.StartsWith(SocialRules.OwnTitleMark, System.StringComparison.Ordinal);

        public static string Title(string titleId) => IsOwnTitle(titleId) ? titleId.Substring(SocialRules.OwnTitleMark.Length) : Texts.Get("party.title." + titleId);

        public static string Title(string titleId, string ownName) => ownName.Length > 0 ? ownName : Title(titleId);

        public static string Role(string roleId) => Texts.Get("party.role." + roleId);

        public static string Wanted(string roleId) => Texts.Format("party.wanted", Role(roleId));

        public static string UsuallyOnline(string slotId) => Texts.Format("party.usuallyOnline", Texts.Get("party.time." + slotId));

        public static string Code(string code) => Texts.Format("party.code", PartySearchRules.FormatCode(code));

        public static string Report(string reasonId) => Texts.Get("party.report." + reasonId);

        public static string Language(string languageId) => Texts.Get("settings.language." + languageId);
    }
}
