using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class CampSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private CampRules _rules;

        public CampSystem(CampRules rules, GameState state, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _events = events;
        }

        public CampRules Rules => _rules;
        public CampState State => _state.Camp;
        public CampTier Tier => _rules.Tiers[System.Math.Min(State.Tier, _rules.Tiers.Count - 1)];
        public CampTier? NextTier => State.Tier + 1 < _rules.Tiers.Count ? _rules.Tiers[State.Tier + 1] : null;

        public Unlocks? Unlocks { get; set; }

        public void UseRules(CampRules rules) => _rules = rules;

        public bool Reached(CampTier tier) => _state.RecordStage >= tier.FromRecord && (Unlocks == null || Unlocks.Met(tier.Requires));

        public void Tick()
        {
            var tier = State.Tier;
            while (tier + 1 < _rules.Tiers.Count && Reached(_rules.Tiers[tier + 1]))
                tier++;
            if (tier <= State.Tier)
                return;
            State.Tier = tier;
            _events.Add(GameEvent.ForStage(GameEventKind.CampTierReached, _state.Tick, tier));
        }

        public int Slots(string building) => Tier.SlotsOf(building);

        public bool WaitsForGuild(string building)
        {
            var def = _rules.Building(building);
            return def != null && def.Guild.Length > 0 && !State.Guilds.Contains(def.Guild);
        }

        public bool Open(string building) => Slots(building) > 0 && !WaitsForGuild(building);

        public void SetGuilds(IEnumerable<string> guilds)
        {
            State.Guilds.Clear();
            foreach (var guild in guilds)
                State.Guilds.Add(guild);
        }

        public long Count(string item) => State.Bag.TryGetValue(item, out var count) ? count : 0;

        public void Add(string item, long amount)
        {
            if (amount <= 0)
                return;
            State.Bag[item] = Count(item) + amount;
        }

        public bool Take(string item, long amount)
        {
            var have = Count(item);
            if (amount <= 0 || have < amount)
                return false;
            if (have == amount)
                State.Bag.Remove(item);
            else
                State.Bag[item] = have - amount;
            return true;
        }
    }
}
