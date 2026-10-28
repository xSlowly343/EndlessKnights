using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public enum PartyAction
    {
        None,
        Create,
        JoinByCode,
        QuickMatch,
        CopyCode,
        Links,
        Leave
    }

    public static class PartyActions
    {
        public static PartyAction Left(bool inParty) => inParty ? PartyAction.CopyCode : PartyAction.Create;

        public static PartyAction Middle(bool inParty) => inParty ? PartyAction.Links : PartyAction.JoinByCode;

        public static PartyAction Right(bool inParty) => inParty ? PartyAction.Leave : PartyAction.QuickMatch;

        public static string Label(PartyAction action, string code) => action switch
        {
            PartyAction.Create => Texts.Get("party.create"),
            PartyAction.JoinByCode => Texts.Get("party.joinCode"),
            PartyAction.QuickMatch => Texts.Get("party.quick"),
            PartyAction.CopyCode => Texts.Format("party.codeButton", PartySearchRules.FormatCode(code)),
            PartyAction.Links => Texts.Get("party.links"),
            PartyAction.Leave => Texts.Get("party.leave"),
            _ => string.Empty
        };

        public static string Offline => Texts.Get("party.offline");
        public static string Working => Texts.Get("party.working");
        public static string NoCode => Texts.Get("party.noCode");
        public static string CodeCopied(string code) => Texts.Format("party.codeCopied", PartySearchRules.FormatCode(code));
        public static string Joined(int members, int max) => Texts.Format("party.joined", members, max);
        public static string LeftParty => Texts.Get("party.left");
        public static string Failed(string reason) => Texts.Format("party.failed", Reason(reason));

        public static bool TryReadCode(string? text, int length, out string code)
        {
            code = string.Empty;
            if (string.IsNullOrEmpty(text))
                return false;
            var found = new System.Text.StringBuilder(length);
            foreach (var raw in text!)
            {
                var c = char.ToUpperInvariant(raw);
                if (c == '-' || c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    continue;
                if (PartySearchRules.CodeAlphabet.IndexOf(c) < 0 || found.Length == length)
                    return false;
                found.Append(c);
            }
            if (found.Length != length)
                return false;
            code = found.ToString();
            return true;
        }

        private static string Reason(string reason) => reason switch
        {
            "party is full" => Texts.Get("party.reason.full"),
            "unknown code" => Texts.Get("party.reason.unknownCode"),
            "already in a party" => Texts.Get("party.reason.alreadyIn"),
            "not in a party" => Texts.Get("party.reason.notIn"),
            "party not found" => Texts.Get("party.reason.notFound"),
            _ => Texts.Get("party.reason.network")
        };
    }
}
