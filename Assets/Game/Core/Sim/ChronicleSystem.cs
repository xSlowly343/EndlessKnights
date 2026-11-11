using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ChronicleSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly LevelSystem _levels;
        private readonly ModifierLayer _layer;
        private readonly List<Modifier> _modifiers = new List<Modifier>();
        private readonly List<TrialDef> _weekly = new List<TrialDef>();
        private ChronicleCatalog _catalog;

        public ChronicleSystem(ChronicleCatalog catalog, GameState state, StatSheet hero, EventQueue events, LevelSystem levels)
        {
            _catalog = catalog;
            _state = state;
            _events = events;
            _levels = levels;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public ChronicleCatalog Catalog => _catalog;
        public TrialDef? Active => _state.Chronicle.Active != null && _catalog.TryGet(_state.Chronicle.Active, out var trial) ? trial : null;
        public int CompletedCount => _state.Chronicle.Completed.Count;

        public bool IsCompleted(string id) => _state.Chronicle.Completed.Contains(id);
        public IReadOnlyList<TrialDef> WeeklyTrials => _weekly;
        public bool WeeklyRunning => _state.Chronicle.Active != null && _state.Chronicle.Weekly;

        public long WeeklyBest(string id) => _state.Chronicle.WeeklyBest.TryGetValue(id, out var best) ? best : 0;

        public void SetDay(long day)
        {
            var chronicle = _state.Chronicle;
            var week = ChronicleCatalog.WeekOf(day);
            if (week != chronicle.Week)
            {
                if (chronicle.Weekly && chronicle.Active != null)
                    Abandon();
                chronicle.Week = week;
                chronicle.WeeklyBest.Clear();
                chronicle.WeeklyUnsent.Clear();
            }
            _catalog.WeeklyTrials(week, _weekly);
        }

        public TrialResult StartWeekly(string id)
        {
            if (!IsWeekly(id))
                return TrialResult.NotThisWeek;
            var result = Start(id);
            if (result == TrialResult.Started)
                _state.Chronicle.Weekly = true;
            return result;
        }

        public void WeeklySent(string id, long stage)
        {
            if (WeeklyBest(id) <= stage)
                _state.Chronicle.WeeklyUnsent.Remove(id);
        }

        private bool IsWeekly(string id)
        {
            foreach (var trial in _weekly)
            {
                if (trial.Id == id)
                    return true;
            }
            return false;
        }

        public void UseCatalog(ChronicleCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public Unlocks? Unlocks { get; set; }
        public CompanionSystem? Companions { get; set; }

        public TrialResult Start(string id)
        {
            if (Unlocks?.IsOpen(Feature.Chronicle) == false)
                return TrialResult.Locked;
            var chronicle = _state.Chronicle;
            if (!_catalog.TryGet(id, out _))
                return TrialResult.UnknownTrial;
            if (chronicle.Active != null)
                return TrialResult.AlreadyRunning;
            if (_state.Prestiges < _catalog.MinPrestiges)
                return TrialResult.NeedsPrestige;
            if (_state.Rift.Inside)
                return TrialResult.InsideRift;
            if (_state.HighestStage > 1)
                return TrialResult.RunNotFresh;
            chronicle.Active = id;
            chronicle.Weekly = false;
            chronicle.ActivePrestige = _state.Prestiges;
            Rebuild();
            return TrialResult.Started;
        }

        public bool Abandon()
        {
            if (_state.Chronicle.Active == null)
                return false;
            _state.Chronicle.Active = null;
            _state.Chronicle.Weekly = false;
            Rebuild();
            return true;
        }

        public void Tick()
        {
            var trial = Active;
            if (trial == null)
            {
                if (_state.Chronicle.Active != null)
                    Abandon();
                return;
            }
            if (_state.Prestiges != _state.Chronicle.ActivePrestige || _state.Rift.Inside)
            {
                Abandon();
                return;
            }
            if (_state.Chronicle.Weekly)
                TrackWeekly(trial);
            else if (_state.HighestStage >= trial.GoalStage)
                Complete(trial);
        }

        private void TrackWeekly(TrialDef trial)
        {
            var chronicle = _state.Chronicle;
            if (_state.HighestStage > WeeklyBest(trial.Id))
            {
                chronicle.WeeklyBest[trial.Id] = _state.HighestStage;
                if (!chronicle.WeeklyUnsent.Contains(trial.Id))
                    chronicle.WeeklyUnsent.Add(trial.Id);
            }
            if (_state.HighestStage < trial.GoalStage)
                return;
            Companions?.NoteWeeklyTrial(chronicle.Week);
            if (chronicle.Completed.Contains(trial.Id))
                return;
            chronicle.Completed.Add(trial.Id);
            Rebuild();
            _levels.SyncTreePoints();
            _events.Add(GameEvent.ForStage(GameEventKind.TrialCompleted, _state.Tick, trial.GoalStage));
        }

        private void Complete(TrialDef trial)
        {
            var chronicle = _state.Chronicle;
            if (!chronicle.Completed.Contains(trial.Id))
                chronicle.Completed.Add(trial.Id);
            chronicle.Active = null;
            chronicle.Weekly = false;
            Rebuild();
            _levels.SyncTreePoints();
            _events.Add(GameEvent.ForStage(GameEventKind.TrialCompleted, _state.Tick, trial.GoalStage));
        }

        private void Rebuild()
        {
            _modifiers.Clear();
            foreach (var id in _state.Chronicle.Completed)
            {
                if (_catalog.TryGet(id, out var done))
                    _modifiers.AddRange(done.Reward);
            }
            var active = Active;
            if (active != null)
                _modifiers.AddRange(active.Rules);
            _state.Chronicle.SkillsLocked = active != null && active.NoSkills;
            _layer.Set(_modifiers);
        }
    }
}
