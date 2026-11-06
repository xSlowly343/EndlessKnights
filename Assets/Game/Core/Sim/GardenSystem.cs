using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum GardenResult
    {
        Done,
        NoBed,
        BedTaken,
        UnknownPlant,
        NoSeed,
        NotReady,
        Closed
    }

    public sealed class GardenSystem
    {
        public const string Activity = "garden";

        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private GardenRules _rules;

        public GardenSystem(GardenRules rules, GameState state, CampSystem camp, PeacefulSystem peaceful, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _peaceful = peaceful;
            _events = events;
        }

        public GardenRules Rules => _rules;
        public GardenState State => _state.Garden;
        public bool Open => _camp.Open(BedBuilding);

        public void UseRules(GardenRules rules) => _rules = rules;

        public GardenResult Plant(int bed, string plantId)
        {
            if (!Open)
                return GardenResult.Closed;
            EnsureBeds();
            if (bed < 0 || bed >= OpenBeds)
                return GardenResult.NoBed;
            if (!State.Beds[bed].Empty)
                return GardenResult.BedTaken;
            var plant = _rules.Plant(plantId);
            if (plant == null)
                return GardenResult.UnknownPlant;
            if (!plant.Starter && !_camp.Take(plant.Seed, 1))
                return GardenResult.NoSeed;
            var target = State.Beds[bed];
            target.Plant = plantId;
            target.Grown = 0;
            target.Ready = false;
            return GardenResult.Done;
        }

        public GardenResult Harvest(int bed)
        {
            if (bed < 0 || bed >= OpenBeds)
                return GardenResult.NoBed;
            var target = State.Beds[bed];
            if (target.Empty || !target.Ready)
                return GardenResult.NotReady;
            var plant = _rules.Plant(target.Plant);
            if (plant != null)
            {
                _camp.Add(plant.Herb, plant.Yield);
                State.Album.Add(plant.Id);
                if (!plant.Starter && _state.Rng.NextDouble() < _rules.SeedBackChance)
                    _camp.Add(plant.Seed, 1);
            }
            State.Harvests++;
            target.Plant = string.Empty;
            target.Grown = 0;
            target.Ready = false;
            return GardenResult.Done;
        }

        public void Tick()
        {
            if (!Open)
                return;
            EnsureBeds();
            Grow(GameLoop.TickSeconds);
            if (_peaceful.InCamp && _peaceful.Activity == Activity)
                Tend(_peaceful.Work(Activity));
        }

        public void Advance(double seconds)
        {
            if (seconds > 0 && Open)
            {
                EnsureBeds();
                Grow(seconds);
            }
        }

        public int OpenBeds => System.Math.Min(State.Beds.Count, _camp.Slots(BedBuilding));

        private const string BedBuilding = "garden_bed";

        private void EnsureBeds()
        {
            var most = 0;
            var tiers = _camp.Rules.Tiers;
            for (var i = 0; i < tiers.Count; i++)
                most = System.Math.Max(most, tiers[i].SlotsOf(BedBuilding));
            while (State.Beds.Count < most)
                State.Beds.Add(new GardenBed());
        }

        private void Tend(PeacefulWork work)
        {
            Boost(GardenInput.Keys, work.Keys);
            Boost(GardenInput.Mouse, work.MouseMeters);
            Boost(GardenInput.Clicks, work.Clicks);
            Boost(GardenInput.Idle, work.IdleSeconds);
            work.Keys = 0;
            work.MouseMeters = 0;
            work.Clicks = 0;
            work.IdleSeconds = 0;
        }

        private void Boost(GardenInput input, double units)
        {
            if (units <= 0)
                return;
            var beds = State.Beds;
            var open = OpenBeds;
            for (var i = 0; i < open; i++)
            {
                var bed = beds[i];
                if (bed.Empty || bed.Ready)
                    continue;
                var plant = _rules.Plant(bed.Plant);
                if (plant != null && plant.Input == input)
                    GrowBed(i, units * plant.SecondsPerUnit);
            }
        }

        private void Grow(double seconds)
        {
            var beds = State.Beds;
            var open = OpenBeds;
            for (var i = 0; i < open; i++)
            {
                if (!beds[i].Empty && !beds[i].Ready)
                    GrowBed(i, seconds);
            }
        }

        private void GrowBed(int index, double seconds)
        {
            var bed = State.Beds[index];
            var plant = _rules.Plant(bed.Plant);
            if (plant == null)
                return;
            bed.Grown += seconds;
            if (bed.Grown < plant.GrowSeconds)
                return;
            bed.Ready = true;
            _events.Add(GameEvent.ForStage(GameEventKind.PlantReady, _state.Tick, index));
            TryMutate(index);
        }

        private void TryMutate(int index)
        {
            var plant = State.Beds[index].Plant;
            for (var side = -1; side <= 1; side += 2)
            {
                var neighbor = index + side;
                if (neighbor < 0 || neighbor >= OpenBeds || State.Beds[neighbor].Empty)
                    continue;
                foreach (var mutation in _rules.Mutations)
                {
                    if (!mutation.Matches(plant, State.Beds[neighbor].Plant) || _state.Rng.NextDouble() >= mutation.Chance + _state.Museum.Perk(MuseumPerk.MutationChance))
                        continue;
                    var child = _rules.Plant(mutation.Child)!;
                    _camp.Add(child.Seed, 1);
                    _events.Add(GameEvent.ForStage(GameEventKind.MutationFound, _state.Tick, index));
                }
            }
        }
    }
}
