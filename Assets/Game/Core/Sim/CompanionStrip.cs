using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum StripPose
    {
        Hidden,
        Stand,
        Sit,
        Sleep,
        Hop,
        Run,
        Hide,
        Cheer,
        Sign,
        Bring,
        Flash
    }

    public enum StripMode
    {
        Always,
        HomeOnly,
        Off
    }

    public sealed class CompanionStrip
    {
        private readonly GameState _state;
        private readonly CompanionSystem _companions;
        private readonly ExpeditionSystem _expeditions;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private double _cheer;
        private double _flash;
        private bool _away;
        private double _idle;
        private double _series;

        public CompanionStrip(GameState state, CompanionSystem companions, ExpeditionSystem expeditions, PeacefulSystem peaceful, EventQueue events)
        {
            _state = state;
            _companions = companions;
            _expeditions = expeditions;
            _peaceful = peaceful;
            _events = events;
        }

        private CompanionStripRules Rules => _companions.Rules.Strip;

        public StripMode Mode { get; set; }
        public StripPose Pose { get; private set; } = StripPose.Stand;
        public double ReturnSeconds { get; private set; }
        public string BringItem { get; private set; } = string.Empty;

        public string Companion
        {
            get
            {
                var shown = _companions.State.Shown;
                return shown.Length > 0 && _companions.Known(shown) ? shown : _companions.Rules.Companions.Count > 0 ? _companions.Rules.Companions[0].Id : string.Empty;
            }
        }

        public CompanionResult Show(string companion)
        {
            if (!_companions.Known(companion))
                return CompanionResult.UnknownCompanion;
            _companions.State.Shown = companion;
            BringItem = string.Empty;
            _away = _expeditions.Busy(companion);
            return CompanionResult.Done;
        }

        public bool Click()
        {
            if (BringItem.Length == 0)
                return false;
            BringItem = string.Empty;
            return true;
        }

        public double IdleSeconds => _idle;
        public double SeriesSeconds => _series;

        public void Tick(int firstEvent, bool hasInput)
        {
            _idle = hasInput ? 0 : _idle + GameLoop.TickSeconds;
            if (hasInput)
                _series += GameLoop.TickSeconds;
            else if (_idle >= Rules.SeriesPauseSeconds)
                _series = 0;
            var companion = Companion;
            var rules = Rules;
            _cheer = System.Math.Max(0, _cheer - GameLoop.TickSeconds);
            _flash = System.Math.Max(0, _flash - GameLoop.TickSeconds);
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var kind = _events[i].Kind;
                if (kind == GameEventKind.BossDefeated && _companions.Hearts(companion) >= rules.CheerHearts)
                    _cheer = rules.CheerSeconds;
                else if (kind == GameEventKind.CompanionEvolved)
                    _flash = rules.FlashSeconds;
            }
            var away = _expeditions.Busy(companion);
            if (_away && !away)
                BringItem = BestFind(companion);
            _away = away;
            ReturnSeconds = away ? SecondsLeft(companion) : 0;
            Pose = PoseNow(away);
        }

        private StripPose PoseNow(bool away)
        {
            var rules = Rules;
            if (Mode == StripMode.Off || (Mode == StripMode.HomeOnly && !_peaceful.InCamp))
                return StripPose.Hidden;
            if (away)
                return StripPose.Sign;
            if (_flash > 0)
                return StripPose.Flash;
            if (BringItem.Length > 0)
                return StripPose.Bring;
            if (_cheer > 0)
                return StripPose.Cheer;
            if (_state.Enemy != null && _state.EnemyType != EnemyType.Normal && !_peaceful.InCamp)
                return StripPose.Hide;
            var idle = _idle;
            if (idle >= rules.SleepAfterSeconds)
                return StripPose.Sleep;
            if (idle >= rules.SitAfterSeconds)
                return StripPose.Sit;
            if (_series >= rules.RunAfterSeconds)
                return StripPose.Run;
            if (_series >= rules.HopAfterSeconds)
                return StripPose.Hop;
            return StripPose.Stand;
        }

        private double SecondsLeft(string companion)
        {
            var active = _state.Expeditions.Active;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i].Companion == companion || active[i].Partner == companion)
                    return active[i].Seconds;
            }
            return 0;
        }

        private string BestFind(string companion)
        {
            var reports = _state.Expeditions.Reports;
            for (var i = reports.Count - 1; i >= 0; i--)
            {
                var report = reports[i];
                if (report.Companion != companion)
                    continue;
                if (report.Rare.Length > 0)
                    return report.Rare;
                var place = _expeditions.Rules.Biome(report.Biome);
                if (place == null)
                    return string.Empty;
                var best = -1;
                for (var item = 0; item < report.Items.Length && item < place.Items.Count; item++)
                {
                    if (report.Items[item] > 0 && (best < 0 || report.Items[item] > report.Items[best]))
                        best = item;
                }
                return best < 0 ? string.Empty : place.Items[best].Item;
            }
            return string.Empty;
        }
    }
}
