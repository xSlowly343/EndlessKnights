using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class HelpCallText
    {
        public static string Accept(string accept) => Texts.Get("help.accept." + accept);

        public static string Called(string playerName) => Texts.Format("help.called", playerName);

        public static string Invite(string playerName, long untilUnixMs, long nowUnixMs) =>
            Texts.Format("help.invite", playerName, Seconds((untilUnixMs - nowUnixMs) / 1000.0));

        public static string Helping(double secondsLeft) => Texts.Format("help.helping", Seconds(secondsLeft));

        public static string Guest(string playerName) => Texts.Format("help.guest", playerName);

        public static string Refused(string reason) => Texts.Get(reason switch
        {
            "this player does not take calls from you" => "help.reason.refused",
            "wait before calling again" => "help.reason.wait",
            "no boss to help with" => "help.reason.noBoss",
            "no calls from the rift" => "help.reason.rift",
            "already in your party" => "help.reason.inParty",
            "party is full" => "party.reason.full",
            "not in a party" => "party.reason.notIn",
            "the call is over" => "help.reason.over",
            _ => "party.reason.network"
        });

        private static string Seconds(double seconds) => System.Math.Max(0, System.Math.Ceiling(seconds)).ToString("0", CultureInfo.InvariantCulture);
    }
}
