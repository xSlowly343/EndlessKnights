using Game.Core.Content;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class MarketText
    {
        public static string Title => Texts.Get("market.title");

        public static string Empty => Texts.Get("market.empty");

        public static string Item(CompanionTreatRules treats, string item) =>
            treats.Treat(item) is { } treat ? Texts.Pick(treat.NameRu, treat.NameEn) : item;

        public static string Lot(CompanionTreatRules treats, string item, int amount, int price, string seller) =>
            Texts.Format("market.lot", Item(treats, item), amount, price, (long)amount * price, seller);

        public static string Own(CompanionTreatRules treats, string item, int amount, int price, bool expired) =>
            Texts.Format(expired ? "market.own.expired" : "market.own", Item(treats, item), amount, price);

        public static string Proceeds(TownMarketRules market, int amount, int price) =>
            Texts.Format("market.proceeds", market.Proceeds(amount, price), market.Fees(market.Total(amount, price)));

        public static string Balance(long balance) => Texts.Format("market.balance", balance);

        public static string Bought(CompanionTreatRules treats, string item, int amount, long paid) =>
            Texts.Format("market.bought", Item(treats, item), amount, paid);

        public static string Returned(CompanionTreatRules treats, string item, int amount) =>
            Texts.Format("market.returned", Item(treats, item), amount);

        public static string Listed(CompanionTreatRules treats, string item, int amount, int price) =>
            Texts.Format("market.listed", Item(treats, item), amount, price);

        public static string Result(TownResult result) => Texts.Get(result switch
        {
            TownResult.Done => "market.result.done",
            TownResult.NotForSale => "market.result.notForSale",
            TownResult.BadAmount => "market.result.badAmount",
            TownResult.BadPrice => "market.result.badPrice",
            TownResult.TooManyLots => "market.result.tooManyLots",
            TownResult.NotEnough => "market.result.notEnough",
            _ => "market.result.failed"
        });

        public static string Refusal(string serverError)
        {
            if (serverError.Contains("not enough"))
                return Texts.Get("market.refusal.noMoney");
            if (serverError.Contains("lot is gone"))
                return Texts.Get("market.refusal.gone");
            if (serverError.Contains("too many lots"))
                return Texts.Get("market.result.tooManyLots");
            if (serverError.Contains("inbox is full"))
                return Texts.Get("market.refusal.inbox");
            return Texts.Get("market.result.failed");
        }

        public static bool Rejected(string serverError) =>
            serverError.Contains("not for sale") || serverError.Contains("bad amount") || serverError.Contains("bad price") || serverError.Contains("too many lots")
            || serverError.Contains("bad request");
    }
}
