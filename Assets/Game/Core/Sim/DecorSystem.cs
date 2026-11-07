using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum DecorResult
    {
        Done,
        Unknown,
        NotOwned,
        AlreadyOwned,
        NoItems,
        NoSpot,
        BiomeClosed
    }

    public enum GuestAct
    {
        Gift,
        Water,
        Hammer
    }

    public sealed class DecorSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly GardenSystem _garden;
        private readonly ForgeSystem _forge;
        private readonly ExpeditionSystem _expeditions;
        private DecorRules _rules;

        public DecorSystem(DecorRules rules, GameState state, CampSystem camp, GardenSystem garden, ForgeSystem forge, ExpeditionSystem expeditions)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _garden = garden;
            _forge = forge;
            _expeditions = expeditions;
        }

        public DecorRules Rules => _rules;
        public DecorState State => _state.Decor;
        public int Spots => _rules.SpotsOf(_camp.Tier.Id);

        public void UseRules(DecorRules rules) => _rules = rules;

        public bool Owns(string id) => State.Owned.Contains(id) || _rules.Item(id)?.Start == true;

        public bool CanCraft(DecorDef item)
        {
            foreach (var cost in item.Cost)
            {
                if (_camp.Count(cost.Key) < cost.Value)
                    return false;
            }
            return true;
        }

        public DecorResult Craft(string id)
        {
            var item = _rules.Item(id);
            if (item == null)
                return DecorResult.Unknown;
            if (Owns(id))
                return DecorResult.AlreadyOwned;
            if (!CanCraft(item))
                return DecorResult.NoItems;
            foreach (var cost in item.Cost)
                _camp.Take(cost.Key, cost.Value);
            State.Owned.Add(id);
            return DecorResult.Done;
        }

        public DecorResult Place(int spot, string id)
        {
            if (spot < 0 || spot >= Spots)
                return DecorResult.NoSpot;
            if (_rules.Item(id) == null)
                return DecorResult.Unknown;
            if (!Owns(id))
                return DecorResult.NotOwned;
            EnsureSpots();
            var moved = State.Placed.IndexOf(id);
            if (moved >= 0)
                State.Placed[moved] = string.Empty;
            State.Corner.Remove(id);
            State.Placed[spot] = id;
            return DecorResult.Done;
        }

        public DecorResult Clear(int spot)
        {
            if (spot < 0 || spot >= State.Placed.Count)
                return DecorResult.NoSpot;
            State.Placed[spot] = string.Empty;
            return DecorResult.Done;
        }

        public DecorResult SetTheme(string biome)
        {
            if (biome.Length > 0 && !_expeditions.BiomeOpen(biome))
                return DecorResult.BiomeClosed;
            State.Theme = biome;
            return DecorResult.Done;
        }

        public int MonumentLevel(MonumentDef monument)
        {
            var value = ValueOf(monument.Input);
            var level = 0;
            while (level < monument.Steps.Count && value >= monument.Steps[level])
                level++;
            return level;
        }

        public double ValueOf(MonumentInput input)
        {
            var total = _state.TotalInput;
            return input switch
            {
                MonumentInput.MouseKm => total.MouseMeters / 1000,
                MonumentInput.Keys => total.Keys,
                _ => total.Clicks
            };
        }

        public bool Guest(GuestAct act, int bed)
        {
            switch (act)
            {
                case GuestAct.Water:
                    if (!_garden.Water(bed, _rules.WaterSeconds))
                        return false;
                    State.GuestsWatered++;
                    return true;
                case GuestAct.Hammer:
                    _forge.GuestStrike(_rules.GuestHammers);
                    State.GuestHammers++;
                    return true;
                default:
                    State.Gifts++;
                    return true;
            }
        }

        public bool PutAway(string id)
        {
            if (!Owns(id))
                return false;
            var placed = State.Placed.IndexOf(id);
            if (placed >= 0)
                State.Placed[placed] = string.Empty;
            return true;
        }

        public bool PlaceInFreeSpot(string id)
        {
            EnsureSpots();
            var spots = System.Math.Min(Spots, State.Placed.Count);
            for (var i = 0; i < spots; i++)
            {
                if (State.Placed[i].Length > 0)
                    continue;
                State.Placed[i] = id;
                return true;
            }
            return false;
        }

        private void EnsureSpots()
        {
            while (State.Placed.Count < _rules.MostSpots)
                State.Placed.Add(string.Empty);
        }
    }
}
