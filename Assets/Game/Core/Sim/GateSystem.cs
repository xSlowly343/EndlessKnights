using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GateSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private readonly ModifierLayer _layer;
        private StageTable _stages;

        public GateSystem(StageTable stages, GameState state, StatSheet hero, EventQueue events)
        {
            _stages = stages;
            _state = state;
            _events = events;
            _layer = new ModifierLayer(hero);
            Rebuild();
        }

        public IReadOnlyList<Modifier> Modifiers => _layer.Applied;

        public void UseStages(StageTable stages)
        {
            _stages = stages;
            Rebuild();
        }

        public bool Cleared(long stage) => _state.GatesCleared.Contains(stage);

        public StageGate? TryClear(long stage)
        {
            var gate = _stages.GateAt(stage);
            if (gate == null || !_state.GatesCleared.Add(stage))
                return null;
            Rebuild();
            _events.Add(GameEvent.ForStage(GameEventKind.GateCleared, _state.Tick, stage));
            return gate;
        }

        public void Rebuild()
        {
            var modifiers = new List<Modifier>();
            foreach (var gate in _stages.Gates)
            {
                if (_state.GatesCleared.Contains(gate.Stage))
                    modifiers.AddRange(gate.FirstKill);
            }
            _layer.Set(modifiers);
        }
    }
}
