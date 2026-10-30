using Game.Core.Content;
using Game.Core.Input;

namespace Game.Core.Sim
{
    public sealed class TutorialSystem
    {
        private readonly GameState _state;
        private readonly StrikeUpgradeSystem _upgrades;
        private TutorialRules _rules;
        private bool[] _triggered;
        private long _kills;
        private int _current = -1;
        private double _progress;
        private long _shownTick;
        private int _treePointsAtShow;
        private int _strikeLevelAtShow;

        public TutorialSystem(TutorialRules rules, GameState state, StrikeUpgradeSystem upgrades)
        {
            _rules = rules;
            _state = state;
            _upgrades = upgrades;
            _triggered = new bool[rules.Steps.Count];
        }

        public TutorialStep? Current => _current < 0 ? null : _rules.Steps[_current];

        public void UseRules(TutorialRules rules)
        {
            _rules = rules;
            _triggered = new bool[rules.Steps.Count];
            _current = -1;
        }

        public void Dismiss()
        {
            if (_current >= 0)
                Complete();
        }

        public void Tick(in InputSnapshot input, EventQueue events, int firstEvent)
        {
            if (_state.TutorialSkipped)
                return;
            for (var i = firstEvent; i < events.Count; i++)
                Observe(events[i].Kind);
            Watch(TutorialShow.Start);
            if (_current < 0 && WaitingFor(TutorialShow.UpgradeAffordable) && _upgrades.Affordable() > 0)
                Watch(TutorialShow.UpgradeAffordable);
            if (_current < 0)
                ShowNext();
            if (_current >= 0)
                Progress(input);
        }

        private void Observe(GameEventKind kind)
        {
            switch (kind)
            {
                case GameEventKind.EnemyKilled:
                    _kills++;
                    Watch(TutorialShow.Kills);
                    break;
                case GameEventKind.BossStarted:
                    Watch(TutorialShow.BossStarted);
                    break;
                case GameEventKind.BossFailed:
                    Watch(TutorialShow.BossFailed);
                    break;
                case GameEventKind.LevelUp:
                    Watch(TutorialShow.LevelUp);
                    break;
            }
            if (_current < 0)
                return;
            var done = _rules.Steps[_current].Done;
            if ((done == TutorialDone.SkillCast && kind == GameEventKind.SkillCast) ||
                (done == TutorialDone.BossEnded && (kind == GameEventKind.BossDefeated || kind == GameEventKind.BossFailed)))
                _progress++;
        }

        private bool WaitingFor(TutorialShow show)
        {
            for (var i = 0; i < _rules.Steps.Count; i++)
            {
                var step = _rules.Steps[i];
                if (step.Show == show && !_triggered[i] && !_state.TutorialDone.Contains(step.Id))
                    return true;
            }
            return false;
        }

        private void Watch(TutorialShow show)
        {
            for (var i = 0; i < _rules.Steps.Count; i++)
            {
                var step = _rules.Steps[i];
                if (step.Show != show || _triggered[i] || _state.TutorialDone.Contains(step.Id))
                    continue;
                if (show == TutorialShow.Kills && _kills < step.ShowCount)
                    continue;
                _triggered[i] = true;
            }
        }

        private void ShowNext()
        {
            for (var i = 0; i < _rules.Steps.Count; i++)
            {
                if (!_triggered[i] || _state.TutorialDone.Contains(_rules.Steps[i].Id))
                    continue;
                _current = i;
                _progress = 0;
                _shownTick = _state.Tick;
                _treePointsAtShow = _state.TreePointsSpent;
                _strikeLevelAtShow = _state.StrikeLevel;
                return;
            }
        }

        private void Progress(in InputSnapshot input)
        {
            var step = _rules.Steps[_current];
            switch (step.Done)
            {
                case TutorialDone.Keys:
                    _progress += input.KeyDowns;
                    break;
                case TutorialDone.MousePx:
                    _progress += input.MouseDistancePx;
                    break;
                case TutorialDone.Seconds:
                    _progress = (_state.Tick - _shownTick) * GameLoop.TickSeconds;
                    break;
                case TutorialDone.TreeNode:
                    _progress = _state.TreePointsSpent - _treePointsAtShow;
                    break;
                case TutorialDone.StrikeUpgrade:
                    _progress = _state.StrikeLevel - _strikeLevelAtShow;
                    break;
            }
            var expired = (_state.Tick - _shownTick) * GameLoop.TickSeconds >= step.MaxSeconds;
            if (_progress >= step.DoneCount || expired)
                Complete();
        }

        private void Complete()
        {
            _state.TutorialDone.Add(_rules.Steps[_current].Id);
            _current = -1;
        }
    }
}
