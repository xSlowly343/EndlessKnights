using Game.Core.Build;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class GiftText
    {
        public static string Check(GiftCheck check) => Texts.Get(check switch
        {
            GiftCheck.Ready => "gift.check.ready",
            GiftCheck.PathGem => "gift.check.path",
            GiftCheck.InTheBook => "gift.check.book",
            _ => "gift.check.bound"
        });

        public static string Sent(string gemName, string friendName, int left) => Texts.Format("gift.sent", gemName, friendName, left);

        public static string Status(int left, int dailyLimit, int cost) => Texts.Format("gift.status", left, dailyLimit, cost);

        public static string Refused(string reason) => Texts.Get(reason switch
        {
            "this gem cannot be gifted" => "gift.check.path",
            "no gifts left today" => "gift.reason.limit",
            "already gifted this friend today" => "gift.reason.friend",
            "this friend got enough gifts today" => "gift.reason.full",
            "not enough friendship" => "gift.reason.tokens",
            "this player does not take gifts from you" => "gift.reason.refused",
            _ => "party.reason.network"
        });
    }
}
