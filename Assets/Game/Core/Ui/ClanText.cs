using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ClanText
    {
        private const long HourMs = 3600000;
        private const long DayMs = 24 * HourMs;

        public static string Name(string adjective, string noun) => Texts.Format("clan.name", Texts.Get("clan.adjective." + adjective), Texts.Get("clan.noun." + noun));

        public static string Tag(string adjective, string noun) =>
            (Initial(Texts.Get("clan.adjective." + adjective)) + Initial(Texts.Get("clan.noun." + noun))).ToUpperInvariant();

        public static string Role(string role) => Texts.Get("clan.role." + role);

        public static string Members(int members, int maxMembers) => Texts.Format("clan.members", members, maxMembers);

        public static string ActiveNow(int count) => Texts.Format("clan.activeNow", count);

        public static string Code(string code) => Texts.Format("clan.code", PartySearchRules.FormatCode(code));

        public static string Seen(long seenUnixMs, long nowUnixMs, int activeMinutes)
        {
            var ago = nowUnixMs - seenUnixMs;
            if (seenUnixMs <= 0)
                return Texts.Get("clan.seen.never");
            if (ago <= activeMinutes * 60000L)
                return Texts.Get("clan.seen.now");
            if (ago < DayMs)
                return Texts.Format("clan.seen.hours", System.Math.Max(1, ago / HourMs));
            return Texts.Format("clan.seen.days", ago / DayMs);
        }

        public static string Failed(string reason) => Texts.Format("party.failed", Reason(reason));

        private static string Reason(string reason) => reason switch
        {
            "already in a clan" => Texts.Get("clan.reason.alreadyIn"),
            "not in a clan" => Texts.Get("clan.reason.notIn"),
            "clan is full" => Texts.Get("clan.reason.full"),
            "unknown code" => Texts.Get("party.reason.unknownCode"),
            "clan not found" => Texts.Get("clan.reason.notFound"),
            "too many officers" => Texts.Get("clan.reason.officers"),
            "only the leader can do that" => Texts.Get("clan.reason.leaderOnly"),
            _ => Texts.Get("party.reason.network")
        };

        private static string Initial(string word) => word.Length == 0 ? string.Empty : word.Substring(0, 1);
    }
}
