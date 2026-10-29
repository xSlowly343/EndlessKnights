using System.Globalization;
using Game.Core.Party.Wire;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class FocusRelayText
    {
        public static string Notice(int ownSlot, in FocusRelayMessage relay, string fromName, string toName, double seconds)
        {
            var bonus = relay.Focus.ToString("0.0#", CultureInfo.InvariantCulture);
            if (relay.To == ownSlot)
                return Texts.Format("party.relay.received", fromName, bonus, seconds.ToString("0", CultureInfo.InvariantCulture));
            if (relay.From == ownSlot)
                return Texts.Format("party.relay.sent", toName, bonus);
            return Texts.Format("party.relay.between", fromName, toName);
        }

        public static string Active(double bonus, double secondsLeft) =>
            Texts.Format("hero.stats.focusRelayValue", bonus.ToString("0.0#", CultureInfo.InvariantCulture), System.Math.Ceiling(secondsLeft).ToString("0", CultureInfo.InvariantCulture));
    }
}
