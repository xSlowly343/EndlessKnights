using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum ColossusResult
    {
        Done,
        NoSeed,
        AlreadyPlanted,
        GardenClosed
    }

    public sealed class ColossusSeedSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private readonly GardenSystem _garden;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private ColossusSeedRules _rules;

        public ColossusSeedSystem(ColossusSeedRules rules, GameState state, CampSystem camp, GardenSystem garden, PeacefulSystem peaceful, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _camp = camp;
            _garden = garden;
            _peaceful = peaceful;
            _events = events;
        }

        public ColossusSeedRules Rules => _rules;
        public ColossusState State => _state.Colossus;
        public bool HasSeed => _camp.Count(_rules.Seed) > 0;

        public void UseRules(ColossusSeedRules rules) => _rules = rules;

        public double Share(double done, double need) => need <= 0 ? 1 : System.Math.Min(1, done / need);

        public bool Ripe => State.Seconds >= _rules.GrowSeconds && State.Keys >= _rules.NeedKeys && State.Clicks >= _rules.NeedClicks
            && State.MouseMeters >= _rules.NeedMouseMeters && State.IdleSeconds >= _rules.NeedIdleSeconds;

        public ColossusResult Plant()
        {
            if (State.Planted || State.Grown)
                return ColossusResult.AlreadyPlanted;
            if (!_garden.Open)
                return ColossusResult.GardenClosed;
            if (!_camp.Take(_rules.Seed, 1))
                return ColossusResult.NoSeed;
            State.Planted = true;
            State.Seconds = 0;
            State.Keys = 0;
            State.Clicks = 0;
            State.MouseMeters = 0;
            State.IdleSeconds = 0;
            return ColossusResult.Done;
        }

        public void NoteGuardians(int firstEvent)
        {
            for (var i = firstEvent; i < _events.Count; i++)
            {
                if (_events[i].Kind != GameEventKind.BossDefeated || _events[i].Enemy?.Type != EnemyType.Guardian || State.Cooldown > 0)
                    continue;
                State.Cooldown = _rules.CooldownSeconds;
                State.SeedsWaiting++;
                _events.Add(GameEvent.ForStage(GameEventKind.ColossusSeedFound, _state.Tick, 0));
            }
        }

        public void Tick()
        {
            Pass(GameLoop.TickSeconds);
            if (State.Planted && _peaceful.InCamp && _peaceful.Activity == GardenSystem.Activity)
            {
                var work = _peaceful.Work(GardenSystem.Activity);
                State.Keys += work.Keys;
                State.Clicks += work.Clicks;
                State.MouseMeters += work.MouseMeters;
                State.IdleSeconds += work.IdleSeconds;
            }
            CheckRipe();
        }

        public void Advance(double seconds)
        {
            Pass(System.Math.Max(0, seconds));
            CheckRipe();
        }

        public void Collect()
        {
            if (State.SeedsWaiting > 0)
            {
                _camp.Add(_rules.Seed, State.SeedsWaiting);
                State.SeedsWaiting = 0;
            }
            if (!State.Grown)
                return;
            var blooms = (int)System.Math.Min(_rules.MaxBlooms, State.SinceBloom / _rules.BloomEverySeconds);
            if (blooms <= 0)
                return;
            State.SinceBloom -= blooms * _rules.BloomEverySeconds;
            _camp.Add(_rules.Bloom, blooms);
        }

        private void Pass(double seconds)
        {
            State.Cooldown = System.Math.Max(0, State.Cooldown - seconds);
            if (State.Planted)
                State.Seconds += seconds;
            if (State.Grown)
                State.SinceBloom = System.Math.Min(_rules.MaxBlooms * _rules.BloomEverySeconds, State.SinceBloom + seconds);
        }

        private void CheckRipe()
        {
            if (!State.Planted || !Ripe)
                return;
            State.Planted = false;
            State.Grown = true;
            State.SinceBloom = 0;
            _camp.Add(_rules.Bloom, 1);
            _events.Add(GameEvent.ForStage(GameEventKind.ColossusGrown, _state.Tick, 0));
        }
    }
}
