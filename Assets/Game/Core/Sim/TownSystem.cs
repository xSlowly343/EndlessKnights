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
        BadAmount
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
            var fresh = (int)System.Math.Min(_rules.OrdersKept, days * _rules.OrdersPerDay);
            for (var i = 0; i < fresh && State.Orders.Count < _rules.OrdersKept; i++)
                Issue();
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
            State.Reputation[guild] = ReputationOf(guild) + _rules.Reputation;
            _state.Drops.Shards += _rules.Shards;
            _state.Drops.Dust += _rules.Dust;
            if (_state.Rng.NextDouble() < _rules.RecipeChance)
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
            State.Orders.Add(new TownOrder { Request = pick.Id, Amount = _state.Rng.NextInt(pick.Min, pick.Max + 1) });
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
