using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ChatText
    {
        public static string Channel(string channel) => Texts.Get("chat.channel." + channel);

        public static string Role(string role) => role.Length == 0 ? string.Empty : Texts.Get("chat.role." + role);

        public static string Line(ChatLine line) =>
            line.Role.Length == 0 ? Texts.Format("chat.line", line.Name, line.Text) : Texts.Format("chat.line.staff", Role(line.Role), line.Name, line.Text);

        public static string Unread(int count) => count <= 0 ? string.Empty : Texts.Format("chat.unread", count);

        public static string ReportReason(string reason) => Texts.Get("chat.report." + reason);

        public static string Ticket(string ticket) => Texts.Get("ticket." + ticket);

        public static string TicketPrice(string ticket, int price) => Texts.Format("ticket.price", Ticket(ticket), price);

        public static string TicketsOwned(int count) => Texts.Format("ticket.owned", count);

        public static string RenameHint(bool free, int tickets) =>
            free ? Texts.Get("name.hint.free") : tickets > 0 ? Texts.Format("name.hint.ticket", tickets) : Texts.Get("name.hint.noTicket");

        public static string NameProblem(SocialNameProblem problem, SocialRules rules) => problem switch
        {
            SocialNameProblem.None => string.Empty,
            SocialNameProblem.TooShort => Texts.Format("name.problem.short", rules.NameMin),
            SocialNameProblem.TooLong => Texts.Format("name.problem.long", rules.NameMax),
            SocialNameProblem.BadSymbols => Texts.Get("name.problem.symbols"),
            _ => Texts.Get("name.problem.banned")
        };

        public static string Muted(long untilUnixMs, long nowUnixMs)
        {
            var minutes = System.Math.Max(1, (untilUnixMs - nowUnixMs + 59999) / 60000);
            return minutes >= 120 ? Texts.Format("chat.muted.hours", minutes / 60) : Texts.Format("chat.muted.minutes", minutes);
        }

        public static string Failed(string reason) => Texts.Format("party.failed", Reason(reason));

        private static string Reason(string reason) => reason switch
        {
            "too fast" => Texts.Get("chat.reason.tooFast"),
            "muted" => Texts.Get("chat.reason.muted"),
            "not in a party" => Texts.Get("party.reason.notIn"),
            "not in a clan" => Texts.Get("clan.reason.notIn"),
            "no such message" => Texts.Get("chat.reason.noMessage"),
            "not yours" => Texts.Get("chat.reason.notYours"),
            "bad name" => Texts.Get("name.problem.symbols"),
            "name not allowed" => Texts.Get("name.problem.banned"),
            "no ticket" => Texts.Get("name.hint.noTicket"),
            "not enough crystals" => Texts.Get("ticket.reason.crystals"),
            "only the leader can do that" => Texts.Get("clan.reason.leaderOnly"),
            _ => Texts.Get("party.reason.network")
        };
    }
}
