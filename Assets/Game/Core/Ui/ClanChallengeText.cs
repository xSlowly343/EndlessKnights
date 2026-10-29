using System.Globalization;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class ClanChallengeText
    {
        private const double MetersInKm = 1000;

        public static string Title(string counter, double target) => Texts.Format("challenge.title." + counter, Amount(counter, target));

        public static string Progress(string counter, double progress, double target, bool done) =>
            done ? Texts.Get("challenge.done") : Texts.Format("challenge.progress", Amount(counter, progress), Amount(counter, target));

        public static string Yours(string counter, double yours, int seals) =>
            yours > 0 ? Texts.Format("challenge.yours", Amount(counter, yours), seals) : Texts.Format("challenge.notYet", seals);

        public static string Amount(string counter, double value) =>
            counter == "mouseMeters" ? Texts.Format("challenge.km", (value / MetersInKm).ToString("0.#", CultureInfo.InvariantCulture)) : Short(value);

        public static string Short(double value)
        {
            var whole = System.Math.Floor(value);
            if (whole < 1000)
                return whole.ToString("0", CultureInfo.InvariantCulture);
            if (whole < 1_000_000)
                return (System.Math.Floor(whole / 100) / 10).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            return (System.Math.Floor(whole / 100_000) / 10).ToString("0.#", CultureInfo.InvariantCulture) + "M";
        }
    }
}
