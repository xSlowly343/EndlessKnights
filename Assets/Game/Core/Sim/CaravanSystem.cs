using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class CaravanSystem
    {
        public const int MaxWaiting = 64;

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly ExpeditionSystem _expeditions;
        private CaravanRules _rules;

        public CaravanSystem(CaravanRules rules, GameState state, CampSystem camp, ExpeditionSystem expeditions)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _expeditions = expeditions;
        }

        public CaravanRules Rules => _rules;
        public CaravanState State => _state.Caravan;
        public CaravanStop NextStop => _rules.Stops[State.Next % _rules.Stops.Count];
        public bool Blocked => !_expeditions.BiomeOpen(NextStop.Biome);
        public double TotalMeters => State.Laps * _rules.LapMeters + State.Traveled;
        public double LeftToNext => System.Math.Max(0, NextStop.Meters - State.Traveled);

        public void UseRules(CaravanRules rules)
        {
            _rules = rules;
            State.Next %= _rules.Stops.Count;
        }

        public void Tick()
        {
            var meters = _state.TotalInput.MouseMeters;
            if (State.SeenMouseMeters < 0 || meters < State.SeenMouseMeters)
                State.SeenMouseMeters = meters;
            var moved = meters - State.SeenMouseMeters;
            State.SeenMouseMeters = meters;
            if (moved > 0)
                Travel(moved * (1 + _state.Perk(MuseumPerk.LongerRoad)));
        }

        public void Collect()
        {
            while (State.Waiting > 0)
            {
                State.Waiting--;
                var stop = _rules.Stops[(State.Next - State.Waiting - 1 + _rules.Stops.Count * MaxWaiting) % _rules.Stops.Count];
                foreach (var good in stop.Goods)
                    _camp.Add(good.Item, good.Count);
            }
        }

        private void Travel(double meters)
        {
            while (meters > 0)
            {
                if (Blocked)
                {
                    State.Traveled = System.Math.Min(State.Traveled + meters, NextStop.Meters);
                    return;
                }
                var left = NextStop.Meters - State.Traveled;
                if (meters < left)
                {
                    State.Traveled += meters;
                    return;
                }
                meters -= left;
                State.Traveled = NextStop.Meters;
                Arrive();
            }
        }

        private void Arrive()
        {
            State.Arrivals++;
            State.LastStop = NextStop.Id;
            if (State.Waiting < MaxWaiting)
                State.Waiting++;
            State.Next++;
            if (State.Next >= _rules.Stops.Count)
            {
                State.Next = 0;
                State.Laps++;
                State.Traveled = 0;
            }
        }
    }
}
