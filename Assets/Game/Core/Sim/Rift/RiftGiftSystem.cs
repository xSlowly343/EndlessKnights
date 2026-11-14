using System.Collections.Generic;
using Game.Core.Content.Rift;
using Game.Core.Math;
using Game.Core.Stats;

namespace Game.Core.Sim.Rift
{
    public enum RiftGiftResult
    {
        Taken,
        NotOffered,
        Outside
    }

    public sealed class RiftGiftSystem
    {
        private const ulong OfferSalt = 0x6A1F_7E57UL;

        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private RiftGiftRules _rules;

        public RiftGiftSystem(RiftGiftRules rules, GameState state, StatSheet hero, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _events = events;
            _layer = new ModifierLayer(hero, ModifierPool.World);
            Rebuild();
        }

        public RiftGiftRules Rules => _rules;
        public IReadOnlyList<string> Offer => _state.Rift.GiftOffer;
        public IReadOnlyList<string> Taken => _state.Rift.Gifts;
        public bool Choosing => _state.Rift.Inside && _state.Rift.GiftOffer.Count > 0;
        public long NextAt => _rules.Every * _state.Rift.GiftOffers;

        public void UseRules(RiftGiftRules rules)
        {
            _rules = rules;
            Rebuild();
        }

        public void Tick()
        {
            _layer.Muted = !_state.Rift.Inside;
            var rift = _state.Rift;
            if (!rift.Inside || rift.GiftOffer.Count > 0 || rift.GiftOffers >= _rules.EarnedBy(_state.HighestStage) || _rules.Pool.Count == 0)
                return;
            MakeOffer();
        }

        public RiftGiftResult Choose(string id)
        {
            var rift = _state.Rift;
            if (!rift.Inside)
                return RiftGiftResult.Outside;
            if (!rift.GiftOffer.Contains(id) || !_rules.TryGet(id, out _))
                return RiftGiftResult.NotOffered;
            rift.Gifts.Add(id);
            rift.GiftOffer.Clear();
            Rebuild();
            _events.Add(GameEvent.ForStage(GameEventKind.RiftGiftChosen, _state.Tick, rift.Gifts.Count));
            return RiftGiftResult.Taken;
        }

        public void Rebuild()
        {
            _modifiers.Clear();
            foreach (var id in _state.Rift.Gifts)
            {
                if (_rules.TryGet(id, out var gift))
                    _modifiers.AddRange(gift.Modifiers);
            }
            _layer.Set(_modifiers);
            _layer.Muted = !_state.Rift.Inside;
        }

        private void MakeOffer()
        {
            var rift = _state.Rift;
            var rng = new Rng((ulong)rift.WindowId ^ OfferSalt ^ ((ulong)rift.GiftOffers << 20));
            var fresh = new List<RiftGift>();
            foreach (var gift in _rules.Pool)
            {
                if (!rift.Gifts.Contains(gift.Id))
                    fresh.Add(gift);
            }
            var source = fresh.Count >= _rules.Offer ? fresh : new List<RiftGift>(_rules.Pool);
            while (rift.GiftOffer.Count < _rules.Offer && source.Count > 0)
            {
                var index = rng.NextInt(0, source.Count);
                rift.GiftOffer.Add(source[index].Id);
                source.RemoveAt(index);
            }
            rift.GiftOffers++;
            _events.Add(GameEvent.ForStage(GameEventKind.RiftGiftOffered, _state.Tick, rift.GiftOffers));
        }
    }
}
