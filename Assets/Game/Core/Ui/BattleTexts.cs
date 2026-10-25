using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class BattleTexts
    {
        public static string Retreat => Texts.Get("battle.retreat");
        public static string Crit => Texts.Get("battle.crit");

        public static string Stage(long stage, bool bossFight) => Texts.Format(bossFight ? "battle.stageBoss" : "battle.stage", stage);

        public static string Seconds(int tenths) => Texts.Format("battle.seconds", (tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture));

        public static string Percent(int percent) => Texts.Format("battle.percent", percent);

        public static int Tenths(double seconds) => (int)System.Math.Ceiling(System.Math.Max(0, seconds) * 10 - 1e-9);

        public static int PercentOf(double fraction) => (int)System.Math.Ceiling(System.Math.Clamp(fraction, 0, 1) * 100 - 1e-9);
    }
}
