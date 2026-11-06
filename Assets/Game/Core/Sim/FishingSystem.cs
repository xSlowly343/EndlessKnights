using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum FishingResult
    {
        Done,
        Closed,
        UnknownSpot,
        SpotClosed
    }

    public sealed class FishingSystem
    {
        public const string Activity = "fishing";

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly ExpeditionSystem _expeditions;
        private readonly EventQueue _events;
        private FishingRules _rules;

        public FishingSystem(FishingRules rules, GameState state, CampSystem camp, PeacefulSystem peaceful, ExpeditionSystem expeditions, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _peaceful = peaceful;
            _expeditions = expeditions;
            _events = events;
        }

        public FishingRules Rules => _rules;
        public FishingState State => _state.Fishing;
        public bool Open => _camp.Open(_rules.Building);
        public bool RiftWindow { get; set; }

        public void UseRules(FishingRules rules) => _rules = rules;

        public string CurrentSpot => State.Spot.Length > 0 ? State.Spot : _rules.Spots.Count > 0 ? _rules.Spots[0].Biome : string.Empty;

        public bool SpotOpen(string biome) => _rules.Spot(biome) != null && _expeditions.BiomeOpen(biome);

        public FishingResult SetSpot(string biome)
        {
            if (_rules.Spot(biome) == null)
                return FishingResult.UnknownSpot;
            if (!_expeditions.BiomeOpen(biome))
                return FishingResult.SpotClosed;
            State.Spot = biome;
            Reset();
            return FishingResult.Done;
        }

        public FishRecord? Record(string id) => State.Album.TryGetValue(id, out var record) ? record : null;

        public double LegendLeft(LegendFish legend) =>
            System.Math.Max(0, legend.Stamina - (State.LegendTired.TryGetValue(legend.Id, out var tired) ? tired : 0));

        public double ReelNeeded => State.Phase != FishingPhase.Fight ? 0 : Need(State.HookedKind, State.Hooked);

        public void Tick()
        {
            if (!_peaceful.InCamp || _peaceful.Activity != Activity || !Open || !SpotOpen(CurrentSpot))
            {
                if (State.Phase != FishingPhase.Idle)
                    Reset();
                return;
            }
            var work = _peaceful.Work(Activity);
            var clicks = work.Clicks;
            var meters = work.MouseMeters;
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            switch (State.Phase)
            {
                case FishingPhase.Idle:
                    Cast();
                    break;
                case FishingPhase.Waiting:
                    if ((State.Timer -= GameLoop.TickSeconds) <= 0)
                        Bite();
                    break;
                case FishingPhase.Bite:
                    if (clicks > 0)
                        Strike();
                    else if ((State.Timer -= GameLoop.TickSeconds) <= 0)
                        Miss();
                    break;
                case FishingPhase.Fight:
                    Reel(meters);
                    break;
            }
        }

        private void Cast()
        {
            State.Phase = FishingPhase.Waiting;
            State.Timer = (_rules.BiteMinSeconds + _state.Rng.NextDouble() * (_rules.BiteMaxSeconds - _rules.BiteMinSeconds)) * (1 - _state.Museum.Perk(MuseumPerk.BiteFaster));
            State.Hooked = string.Empty;
            State.Reeled = 0;
        }

        private void Bite()
        {
            var spot = _rules.Spot(CurrentSpot)!;
            if (_state.Rng.NextDouble() < _rules.BottleChance + _state.Museum.Perk(MuseumPerk.BottleChance))
                Hook(FishCatchKind.Bottle, string.Empty);
            else if (!State.Album.ContainsKey(spot.Legend.Id) && _state.Rng.NextDouble() < _rules.LegendChance + _state.Museum.Perk(MuseumPerk.LegendChance))
                Hook(FishCatchKind.Legend, spot.Legend.Id);
            else if (RiftWindow && _rules.RiftFish.Count > 0 && _state.Rng.NextDouble() < _rules.RiftChance)
                Hook(FishCatchKind.Fish, Pick(_rules.RiftFish).Id);
            else
                Hook(FishCatchKind.Fish, Pick(spot.Fish).Id);
            State.Phase = FishingPhase.Bite;
            State.Timer = _rules.BiteWindowSeconds;
            _events.Add(GameEvent.ForStage(GameEventKind.FishBite, _state.Tick, (long)State.HookedKind));
        }

        private void Hook(FishCatchKind kind, string id)
        {
            State.HookedKind = kind;
            State.Hooked = id;
        }

        private void Strike()
        {
            State.Phase = FishingPhase.Fight;
            State.Timer = _rules.FightSeconds;
            State.Reeled = 0;
        }

        private void Miss()
        {
            State.Missed++;
            _events.Add(GameEvent.ForStage(GameEventKind.FishMissed, _state.Tick, 0));
            Reset();
        }

        private void Reel(double meters)
        {
            State.Reeled += System.Math.Max(0, meters);
            if (State.Reeled >= Need(State.HookedKind, State.Hooked) * (1 - _state.Museum.Perk(MuseumPerk.ReelEasier)))
            {
                Land();
                return;
            }
            if ((State.Timer -= GameLoop.TickSeconds) > 0)
                return;
            if (State.HookedKind == FishCatchKind.Legend)
                Tire();
            State.Escaped++;
            _events.Add(GameEvent.ForStage(GameEventKind.FishEscaped, _state.Tick, (long)State.HookedKind));
            Reset();
        }

        private double Need(FishCatchKind kind, string id)
        {
            switch (kind)
            {
                case FishCatchKind.Bottle:
                    return _rules.BottleReelMeters;
                case FishCatchKind.Legend:
                    var legend = _rules.Legend(id);
                    return legend == null ? 0 : LegendLeft(legend);
                default:
                    return _rules.Fish(id)?.ReelMeters ?? 0;
            }
        }

        private void Tire()
        {
            State.LegendTired.TryGetValue(State.Hooked, out var tired);
            State.LegendTired[State.Hooked] = tired + State.Reeled;
        }

        private void Land()
        {
            switch (State.HookedKind)
            {
                case FishCatchKind.Bottle:
                    State.Bottles++;
                    _state.Drops.Shards += _rules.BottleShards;
                    _state.Drops.Dust += _rules.BottleDust;
                    State.LastCatch = string.Empty;
                    State.LastCm = 0;
                    break;
                case FishCatchKind.Legend:
                    var legend = _rules.Legend(State.Hooked)!;
                    State.LegendTired.Remove(legend.Id);
                    Keep(legend.Id, legend.Cm);
                    break;
                default:
                    var fish = _rules.Fish(State.Hooked)!;
                    Keep(fish.Id, fish.MinCm + _state.Rng.NextDouble() * (fish.MaxCm - fish.MinCm));
                    break;
            }
            _events.Add(GameEvent.ForStage(GameEventKind.FishCaught, _state.Tick, (long)State.HookedKind));
            Reset();
        }

        private void Keep(string id, double cm)
        {
            cm = System.Math.Round(cm, 1);
            if (!State.Album.TryGetValue(id, out var record))
            {
                record = new FishRecord();
                State.Album[id] = record;
            }
            record.Count++;
            record.BestCm = System.Math.Max(record.BestCm, cm);
            State.Caught++;
            State.LastCatch = id;
            State.LastCm = cm;
            _camp.Add(id, 1);
        }

        private FishDef Pick(IReadOnlyList<FishDef> fish)
        {
            var total = 0.0;
            for (var i = 0; i < fish.Count; i++)
                total += fish[i].Weight;
            var roll = _state.Rng.NextDouble() * total;
            for (var i = 0; i < fish.Count; i++)
            {
                roll -= fish[i].Weight;
                if (roll < 0)
                    return fish[i];
            }
            return fish[fish.Count - 1];
        }

        private void Reset()
        {
            State.Phase = FishingPhase.Idle;
            State.Timer = 0;
            State.Reeled = 0;
            State.Hooked = string.Empty;
        }
    }
}
