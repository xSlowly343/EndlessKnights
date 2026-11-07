using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class CaravanView
    {
        public static string Road(CaravanSystem caravan)
        {
            var next = caravan.NextStop;
            var name = Texts.Pick(next.NameRu, next.NameEn);
            var total = Texts.Format("caravan.total", Km(caravan.TotalMeters));
            return caravan.Blocked && caravan.LeftToNext <= 0
                ? total + " · " + Texts.Format("caravan.blocked", name)
                : total + " · " + Texts.Format("caravan.next", name, Km(caravan.LeftToNext));
        }

        private static string Km(double meters) => (meters / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
