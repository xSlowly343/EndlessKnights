using System.Collections.Generic;
using System.Globalization;
using Game.Core.Sim;

namespace Game.Core.Ui
{
    public sealed class TownOrderLine
    {
        public int Index { get; internal set; }
        public string Request { get; internal set; } = string.Empty;
        public string Resident { get; internal set; } = string.Empty;
        public string Guild { get; internal set; } = string.Empty;
        public string Text { get; internal set; } = string.Empty;
        public int Amount { get; internal set; }
        public long Have { get; internal set; }
        public bool Ready { get; internal set; }
    }

    public static class TownView
    {
        public static List<TownOrderLine> Orders(TownSystem town, CampSystem camp, AlchemySystem alchemy, bool russian = true)
        {
            var lines = new List<TownOrderLine>();
            var orders = town.State.Orders;
            for (var i = 0; i < orders.Count; i++)
            {
                var request = town.Rules.Request(orders[i].Request);
                if (request == null)
                    continue;
                var amount = orders[i].Amount;
                lines.Add(new TownOrderLine
                {
                    Index = i,
                    Request = request.Id,
                    Resident = russian ? request.Resident.NameRu : request.Resident.NameEn,
                    Guild = request.Resident.Guild,
                    Text = string.Format(CultureInfo.InvariantCulture, russian ? request.TextRu : request.TextEn, amount),
                    Amount = amount,
                    Have = request.WantsPotion ? alchemy.PotionsOf(request.Recipe) : camp.Count(request.Item),
                    Ready = town.CanFulfil(i)
                });
            }
            return lines;
        }

        public static List<(string Guild, long Reputation)> Reputation(TownSystem town)
        {
            var lines = new List<(string, long)>();
            foreach (var resident in town.Rules.Residents)
            {
                if (!lines.Exists(line => line.Item1 == resident.Guild))
                    lines.Add((resident.Guild, town.ReputationOf(resident.Guild)));
            }
            return lines;
        }
    }
}
