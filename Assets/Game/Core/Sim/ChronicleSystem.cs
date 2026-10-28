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

        public void UseCatalog(ChronicleCatalog catalog)
        {
            _catalog = catalog;
            Rebuild();
        }

        public Unlocks? Unlocks { get; set; }

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
            chronicle.ActivePrestige = _state.Prestiges;
            Rebuild();
            return TrialResult.Started;
        }

        public bool Abandon()
        {
            if (_state.Chronicle.Active == null)
                return false;
            _state.Chronicle.Active = null;
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
            if (_state.HighestStage >= trial.GoalStage)
                Complete(trial);
        }

        private void Complete(TrialDef trial)
        {
            var chronicle = _state.Chronicle;
            if (!chronicle.Completed.Contains(trial.Id))
                chronicle.Completed.Add(trial.Id);
            chronicle.Active = null;
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
