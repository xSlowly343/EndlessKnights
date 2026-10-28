using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class Unlocks
    {
        private readonly GameState _state;

        public Unlocks(UnlockRules rules, GameState state)
        {
            Rules = rules;
            _state = state;
        }

        public UnlockRules Rules { get; set; }

        public bool IsOpen(Feature feature)
        {
            var condition = Rules.For(feature);
            return _state.RecordStage >= condition.Stage && _state.Prestiges >= condition.Prestiges;
        }

        public bool ArtifactGroupOpen(int group) => _state.RecordStage >= Rules.ArtifactGroupStage(group);
    }
}
