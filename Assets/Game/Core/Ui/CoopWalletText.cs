using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class CoopWalletText
    {
        public static string Currency(string currency) => Texts.Get("wallet.currency." + currency);

        public static string Balance(string currency, long amount) => Texts.Format("wallet.balance", Currency(currency), amount.ToString(CultureInfo.InvariantCulture));

        public static string Source(string source) => Texts.Get("wallet.source." + source);

        public static string Today(string source, int earned, int cap) => Texts.Format("wallet.today", Source(source), earned, cap);

        public static string Entry(string currency, long amount, string source)
        {
            var signed = (amount > 0 ? "+" : string.Empty) + amount.ToString(CultureInfo.InvariantCulture);
            return Texts.Format("wallet.entry", Currency(currency), signed, Source(source));
        }
    }
}
