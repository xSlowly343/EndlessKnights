using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public readonly struct PartyMemberOption
    {
        public PartyMemberOption(string label, string? reportReason)
        {
            Label = label;
            ReportReason = reportReason;
        }

        public string Label { get; }
        public string? ReportReason { get; }
        public bool IsBlock => ReportReason == null;
    }

    public static class PartyMemberMenu
    {
        public static IReadOnlyList<PartyMemberOption> Options(PartySearchRules rules)
        {
            var options = new List<PartyMemberOption> { new PartyMemberOption(Texts.Get("party.member.block"), null) };
            foreach (var reason in rules.ReportReasons)
                options.Add(new PartyMemberOption(Texts.Format("party.member.report", PartyCardText.Report(reason)), reason));
            return options;
        }

        public static string Title(string playerName) => Texts.Format("party.member.title", playerName);
        public static string Blocked => Texts.Get("party.member.blocked");
        public static string Reported => Texts.Get("party.member.reported");
        public static string NotFound => Texts.Get("party.member.notFound");
        public static string Close => Texts.Get("party.member.close");
    }
}
