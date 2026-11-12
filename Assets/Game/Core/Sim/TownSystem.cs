using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum TownResult
    {
        Done,
        NoOrder,
        NotEnough,
        NotGiftable,
        BadAmount,
        NoRedraw,
        Already,
        NotForSale,
        BadPrice,
        TooManyLots,
        NoOffer
    }

    public sealed class TownSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly AlchemySystem _alchemy;
        private readonly ExpeditionSystem _expeditions;
        private readonly EventQueue _events;
        private TownRules _rules;

        public TownSystem(TownRules rules, GameState state, CampSystem camp, AlchemySystem alchemy, ExpeditionSystem expeditions, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _alchemy = alchemy;
            _expeditions = expeditions;
            _events = events;
        }

        public TownRules Rules => _rules;
        public TownState State => _state.Town;

        public void UseRules(TownRules rules) => _rules = rules;

        public long ReputationOf(string guild) => State.Reputation.TryGetValue(guild, out var value) ? value : 0;

        public void SetDay(long day)
        {
            if (State.Day >= 0 && day <= State.Day)
                return;
            var days = State.Day < 0 ? 1 : System.Math.Min(day - State.Day, _rules.OrdersKept);
            State.Day = day;
            State.Orders.RemoveAll(order => day - order.Day >= _rules.OrderDays);
            var fresh = (int)System.Math.Min(_rules.OrdersKept, days * _rules.OrdersPerDay);
            for (var i = 0; i < fresh && State.Orders.Count < _rules.OrdersKept; i++)
                Issue();
        }

        public bool CanRedraw => _state.Perk(MuseumPerk.FreeRedraw) > 0 && State.RedrawDay != State.Day;

        public TownResult Redraw(int index)
        {
            if (index < 0 || index >= State.Orders.Count)
                return TownResult.NoOrder;
            if (!CanRedraw)
                return TownResult.NoRedraw;
            var before = State.Orders.Count;
            Issue();
            if (State.Orders.Count == before)
                return TownResult.NoOrder;
            State.Orders.RemoveAt(index);
            State.RedrawDay = State.Day;
            return TownResult.Done;
        }

        public bool Available(TownRequest request)
        {
            if (request.WantsPotion && !_alchemy.Knows(request.Recipe))
                return false;
            if (request.Plant.Length > 0 && !_state.Garden.Album.Contains(request.Plant))
                return false;
            return request.Biome.Length == 0 || _expeditions.BiomeOpen(request.Biome);
        }

        public bool CanFulfil(int index)
        {
            if (index < 0 || index >= State.Orders.Count)
                return false;
            var order = State.Orders[index];
            var request = _rules.Request(order.Request);
            if (request == null)
                return false;
            return request.WantsPotion ? _alchemy.PotionsOf(request.Recipe) >= order.Amount : _camp.Count(request.Item) >= order.Amount;
        }

        public TownResult Fulfil(int index)
        {
            if (index < 0 || index >= State.Orders.Count)
                return TownResult.NoOrder;
            var order = State.Orders[index];
            var request = _rules.Request(order.Request);
            if (request == null)
            {
                State.Orders.RemoveAt(index);
                return TownResult.NoOrder;
            }
            var paid = request.WantsPotion ? _alchemy.TakePotions(request.Recipe, order.Amount) : _camp.Take(request.Item, order.Amount);
            if (!paid)
                return TownResult.NotEnough;
            State.Orders.RemoveAt(index);
            State.Done++;
            var guild = request.Resident.Guild;
            State.Reputation[guild] = ReputationOf(guild) + (long)System.Math.Round(_rules.Reputation * (1 + _state.Perk(MuseumPerk.Reputation)));
            _state.Drops.Shards += _rules.Shards;
            _state.Drops.Dust += _rules.Dust * (1 + _state.Perk(MuseumPerk.OrderDust));
            if (_state.Rng.NextDouble() < _rules.RecipeChance + _state.Perk(MuseumPerk.OrderRecipe))
                _alchemy.Teach();
            _events.Add(GameEvent.ForStage(GameEventKind.OrderDone, _state.Tick, index));
            return TownResult.Done;
        }

        public TownResult PackGift(string item, int amount)
        {
            if (!_rules.Giftable(item))
                return TownResult.NotGiftable;
            if (amount <= 0 || amount > _rules.GiftMaxAmount)
                return TownResult.BadAmount;
            return _camp.Take(item, amount) ? TownResult.Done : TownResult.NotEnough;
        }

        public TownResult ReturnGift(string item, int amount) => Receive(item, amount);

        public TownResult ReceiveGift(string id, string item, int amount)
        {
            if (State.Gifts.Contains(id))
                return TownResult.Already;
            var result = Receive(item, amount);
            if (result != TownResult.Done)
                return result;
            State.Gifts.Add(id);
            if (State.Gifts.Count > _rules.GiftInbox * 2)
                State.Gifts.RemoveRange(0, State.Gifts.Count - _rules.GiftInbox * 2);
            return TownResult.Done;
        }

        public TownResult PackLot(string item, int amount, int price, int lotsOnTheCounter)
        {
            var market = _rules.Market;
            if (!market.Sells(item))
                return TownResult.NotForSale;
            if (amount <= 0 || amount > market.MaxAmount)
                return TownResult.BadAmount;
            if (price < market.MinPrice || price > market.MaxPrice)
                return TownResult.BadPrice;
            if (lotsOnTheCounter + State.Offers.Count >= market.MaxLots)
                return TownResult.TooManyLots;
            if (!_camp.Take(item, amount))
                return TownResult.NotEnough;
            State.OfferSeq++;
            State.Offers.Add(new TownOffer { Nonce = "d" + System.Math.Max(0, State.Day).ToString(System.Globalization.CultureInfo.InvariantCulture) + "n" + State.OfferSeq.ToString(System.Globalization.CultureInfo.InvariantCulture), Item = item, Amount = amount, Price = price });
            return TownResult.Done;
        }

        public TownOffer? Offer(string nonce)
        {
            for (var i = 0; i < State.Offers.Count; i++)
            {
                if (State.Offers[i].Nonce == nonce)
                    return State.Offers[i];
            }
            return null;
        }

        public TownResult SettleLot(string nonce) => Offer(nonce) is { } offer && State.Offers.Remove(offer) ? TownResult.Done : TownResult.NoOffer;

        public TownResult ReturnLot(string nonce)
        {
            if (Offer(nonce) is not { } offer)
                return TownResult.NoOffer;
            State.Offers.Remove(offer);
            _camp.Add(offer.Item, offer.Amount);
            return TownResult.Done;
        }

        public TownResult ReceiveLot(string id, string item, int amount)
        {
            if (State.Deliveries.Contains(id))
                return TownResult.Already;
            if (!_rules.Market.Sells(item))
                return TownResult.NotForSale;
            if (amount <= 0 || amount > _rules.Market.MaxAmount)
                return TownResult.BadAmount;
            _camp.Add(item, amount);
            State.Deliveries.Add(id);
            if (State.Deliveries.Count > _rules.Market.Inbox * 2)
                State.Deliveries.RemoveRange(0, State.Deliveries.Count - _rules.Market.Inbox * 2);
            return TownResult.Done;
        }

        public TownResult Receive(string item, int amount)
        {
            if (!_rules.Giftable(item))
                return TownResult.NotGiftable;
            if (amount <= 0 || amount > _rules.GiftMaxAmount)
                return TownResult.BadAmount;
            _camp.Add(item, amount);
            return TownResult.Done;
        }

        private void Issue()
        {
            var pool = new List<TownRequest>();
            foreach (var request in _rules.Requests)
            {
                if (Available(request) && !Ordered(request.Id))
                    pool.Add(request);
            }
            if (pool.Count == 0)
                return;
            var pick = pool[_state.Rng.NextInt(0, pool.Count)];
            State.Orders.Add(new TownOrder { Request = pick.Id, Amount = _state.Rng.NextInt(pick.Min, pick.Max + 1), Day = State.Day });
        }

        private bool Ordered(string request)
        {
            foreach (var order in State.Orders)
            {
                if (order.Request == request)
                    return true;
            }
            return false;
        }
    }
}
