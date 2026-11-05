using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class SeasonPassSystem
    {
        private readonly GameState _state;
        private SeasonPassRules _rules;

        public SeasonPassSystem(SeasonPassRules rules, GameState state)
        {
            _rules = rules;
            _state = state;
        }

        public SeasonPassRules Rules => _rules;
        public SeasonPassState State => _state.SeasonPass;
        public SeasonPassSeason? Season => _rules.Season(State.Season);
        public int Level => _rules.LevelAt(State.Points);
        public long PointsInLevel => Level >= _rules.Levels ? _rules.PointsPerLevel : State.Points - (long)Level * _rules.PointsPerLevel;
        public bool Complete => Level >= _rules.Levels;

        public void UseRules(SeasonPassRules rules) => _rules = rules;

        public void Tick()
        {
            var pass = State;
            var minutes = (long)(_state.TotalInput.PlaySeconds / 60);
            if (pass.CountedMinutes < 0 || minutes < pass.CountedMinutes)
            {
                pass.CountedMinutes = minutes;
                return;
            }
            var fresh = minutes - pass.CountedMinutes;
            if (fresh <= 0)
                return;
            pass.CountedMinutes = minutes;
            pass.UnsentPoints += fresh * _rules.PointsPerActiveMinute;
        }

        public void Sent(long points) => State.UnsentPoints = System.Math.Max(0, State.UnsentPoints - System.Math.Max(0, points));

        public void Apply(int season, long points, bool premium)
        {
            var pass = State;
            pass.Season = season;
            pass.Points = System.Math.Max(0, System.Math.Min(points, _rules.MaxPoints));
            pass.Premium = premium;
        }

        public bool Reached(int level) => level <= Level;
    }
}
