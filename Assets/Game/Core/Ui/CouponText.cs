using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class CouponText
    {
        public const string Done = "done";

        public static string Empty => Texts.Get("coupon.empty");

        public static string Offline => Texts.Get("coupon.offline");

        public static string Failed => Texts.Get("coupon.failed");

        public static string Result(string status)
        {
            var key = "coupon." + status;
            return Texts.Has(key) && status != "empty" && status != "offline" ? Texts.Get(key) : Failed;
        }
    }
}
