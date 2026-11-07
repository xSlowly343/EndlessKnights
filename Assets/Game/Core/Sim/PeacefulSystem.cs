using Game.Core.Content;
using Game.Core.Input;

namespace Game.Core.Sim
{
    public enum PeacefulResult
    {
        Done,
        Same,
        Busy
    }

    public sealed class PeacefulSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private PeacefulRules _rules;

        public PeacefulSystem(PeacefulRules rules, GameState state, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _events = events;
        }

        public PeacefulRules Rules => _rules;
        public PeacefulState State => _state.Peaceful;
        public bool InCamp => State.Mode == PeacefulMode.Camp;
        public bool InHideout => InCamp && State.Place == PeacefulPlace.Hideout;

        public void SetPlace(PeacefulPlace place) => State.Place = place;

        public string Activity
        {
            get
            {
                var activity = State.Activity;
                return _rules.IndexOf(activity) >= 0 ? activity : _rules.Activities[0].Id;
            }
        }

        public void UseRules(PeacefulRules rules) => _rules = rules;

        public PeacefulWork Work(string activity)
        {
            if (!State.Work.TryGetValue(activity, out var work))
            {
                work = new PeacefulWork();
                State.Work[activity] = work;
            }
            return work;
        }

        public PeacefulResult SetMode(PeacefulMode mode)
        {
            if (State.Mode == mode)
                return PeacefulResult.Same;
            if (mode == PeacefulMode.Camp && Busy)
                return PeacefulResult.Busy;
            State.Mode = mode;
            State.BattleSeconds = 0;
            State.QuietSeconds = 0;
            State.TeaOffered = false;
            _events.Add(GameEvent.ForStage(GameEventKind.PeacefulModeChanged, _state.Tick, (long)mode));
            return PeacefulResult.Done;
        }

        public bool SetActivity(string activity)
        {
            if (_rules.IndexOf(activity) < 0 || activity == Activity)
                return false;
            State.Activity = activity;
            _events.Add(GameEvent.ForStage(GameEventKind.PeacefulActivityChanged, _state.Tick, _rules.IndexOf(activity)));
            return true;
        }

        public bool SetPreset(string id, bool on)
        {
            if (_rules.PresetById(id) == null)
                return false;
            return on ? State.Presets.Add(id) : State.Presets.Remove(id);
        }

        public void ApplySchedule(int dayOfWeek, int minuteOfDay)
        {
            PeacefulPreset? covering = null;
            foreach (var preset in _rules.Presets)
            {
                if (State.Presets.Contains(preset.Id) && preset.Covers(dayOfWeek, minuteOfDay))
                {
                    covering = preset;
                    break;
                }
            }
            var id = covering?.Id ?? string.Empty;
            if (id == State.ScheduledBy)
                return;
            State.ScheduledBy = id;
            if (covering != null)
                SetMode(covering.Mode);
        }

        public void Tick(in InputSnapshot input, double metersPerPixel)
        {
            if (InCamp)
                WorkOn(input, metersPerPixel);
            else
                WatchForTea(input);
        }

        private bool Busy => _state.Rift.Inside || _state.Dungeons.Inside || _state.Events.FightInside;

        private void WorkOn(in InputSnapshot input, double metersPerPixel)
        {
            if (input.WheelTicks != 0)
                Turn(input.WheelTicks);
            if (State.Place == PeacefulPlace.Hideout)
            {
                CountActions(input.KeyDowns + input.LeftClicks + input.RightClicks + input.MiddleClicks);
                Add(State.HideoutWork, input, metersPerPixel);
                if (State.Activity == ForgeSystem.Activity || State.Activity == AlchemySystem.Activity)
                    Add(Work(State.Activity), input, metersPerPixel);
                return;
            }
            CountActions(input.KeyDowns + input.LeftClicks + input.RightClicks + input.MiddleClicks);
            Add(Work(Activity), input, metersPerPixel);
        }

        private static void Add(PeacefulWork work, in InputSnapshot input, double metersPerPixel)
        {
            work.Keys += input.KeyDowns;
            work.Clicks += input.LeftClicks + input.RightClicks + input.MiddleClicks;
            work.MouseMeters += input.MouseDistancePx * metersPerPixel;
            if (!input.HasAnyInput)
                work.IdleSeconds += GameLoop.TickSeconds;
        }

        private void CountActions(int actions)
        {
            if (actions <= 0)
                return;
            var peaceful = _state.Peaceful;
            peaceful.ActionCarry += actions * (1 + _state.Perk(MuseumPerk.BuildBoost));
            var whole = (long)System.Math.Floor(peaceful.ActionCarry);
            peaceful.UnsentActions += whole;
            peaceful.ActionCarry -= whole;
        }

        private void Turn(int wheelTicks)
        {
            var count = _rules.Activities.Count;
            var index = _rules.IndexOf(Activity);
            var next = ((index + wheelTicks) % count + count) % count;
            SetActivity(_rules.Activities[next].Id);
        }

        private void WatchForTea(in InputSnapshot input)
        {
            var state = State;
            var idleLimit = _rules.TeaIdleMinutes * 60;
            if (input.HasAnyInput)
            {
                if (state.QuietSeconds >= idleLimit)
                    state.BattleSeconds = 0;
                state.QuietSeconds = 0;
                state.TeaOffered = false;
            }
            else
                state.QuietSeconds += GameLoop.TickSeconds;
            if (state.QuietSeconds < idleLimit)
            {
                state.BattleSeconds += GameLoop.TickSeconds;
                return;
            }
            if (!state.TeaPause || state.TeaOffered || state.BattleSeconds < _rules.TeaBattleMinutes * 60)
                return;
            state.TeaOffered = true;
            _events.Add(GameEvent.ForStage(GameEventKind.TeaPauseSuggested, _state.Tick, (long)(state.BattleSeconds / 60)));
        }
    }
}
