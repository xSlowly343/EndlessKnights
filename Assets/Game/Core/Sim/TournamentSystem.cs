using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum TournamentJoin
    {
        Joined,
        AlreadyJoined,
        UnknownFormat,
        PrestigeNotReady
    }

    public sealed class TournamentSystem
    {
        private readonly GameState _state;
        private readonly PrestigeSystem _prestige;
        private TournamentRules _rules;
        private TournamentFormat? _format;

        public TournamentSystem(TournamentRules rules, GameState state, PrestigeSystem prestige)
        {
            _rules = rules;
            _state = state;
            _prestige = prestige;
            _format = rules.Format(state.Tournament.Format);
        }

        public TournamentRules Rules => _rules;
        public TournamentState State => _state.Tournament;
        public TournamentFormat? Format => _format;

        public void UseRules(TournamentRules rules)
        {
            _rules = rules;
            _format = rules.Format(_state.Tournament.Format);
        }

        public bool CanJoin(string formatId)
        {
            var format = _rules.Format(formatId);
            return format != null && (!format.Prestige || _prestige.Available);
        }

        public TournamentJoin Join(long index, string formatId)
        {
            var tournament = _state.Tournament;
            if (tournament.Index == index)
                return TournamentJoin.AlreadyJoined;
            var format = _rules.Format(formatId);
            if (format == null)
                return TournamentJoin.UnknownFormat;
            if (format.Prestige && !_prestige.TryPrestige())
                return TournamentJoin.PrestigeNotReady;
            tournament.Index = index;
            tournament.Format = format.Id;
            tournament.BaseBosses = _state.BossKills;
            tournament.BaseKills = _state.Kills;
            tournament.Best = 0;
            tournament.Unsent = false;
            _format = format;
            Tick();
            return TournamentJoin.Joined;
        }

        public void Tick()
        {
            if (_format == null || !_state.Tournament.Joined)
                return;
            var score = System.Math.Min(_format.MaxScore, Score(_format.Kind));
            var tournament = _state.Tournament;
            if (score <= tournament.Best)
                return;
            tournament.Best = score;
            tournament.Unsent = true;
        }

        public void Sent(long index, long score)
        {
            var tournament = _state.Tournament;
            if (tournament.Index == index && tournament.Best <= score)
                tournament.Unsent = false;
        }

        public void Follow(long currentIndex)
        {
            var tournament = _state.Tournament;
            if (!tournament.Joined || tournament.Index == currentIndex)
                return;
            tournament.Index = -1;
            tournament.Format = string.Empty;
            tournament.Best = 0;
            tournament.Unsent = false;
            _format = null;
        }

        private long Score(TournamentScoreKind kind) => kind switch
        {
            TournamentScoreKind.Climb => _state.HighestStage,
            TournamentScoreKind.Bosses => System.Math.Max(0, _state.BossKills - _state.Tournament.BaseBosses),
            _ => System.Math.Max(0, _state.Kills - _state.Tournament.BaseKills)
        };
    }
}
