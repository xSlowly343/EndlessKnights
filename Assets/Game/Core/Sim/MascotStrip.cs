using System.Collections.Generic;
using Game.Core.Math;
using Game.Core.Party;

namespace Game.Core.Sim
{
    public enum MascotPose
    {
        Hidden,
        Walk,
        Stance,
        Strike,
        Charge,
        Cheer,
        Sit,
        Sleep
    }

    public enum MascotPlace
    {
        Behind,
        Front
    }

    public sealed class MascotStrip
    {
        private const ulong GestureSeed = 0x4D_A5C0_7UL;
        private readonly GameState _state;
        private readonly PartyGrowthLayer _growth;
        private readonly EventQueue _events;
        private readonly Rng _rng = new Rng(GestureSeed);
        private readonly List<string> _gestures = new List<string>();
        private PartyMascotRules _rules;
        private string _gesturesFor = string.Empty;
        private string _temper = string.Empty;
        private double _idle;
        private double _cheer;
        private double _untilGesture = -1;
        private double _gestureLeft;

        public MascotStrip(PartyMascotRules rules, GameState state, PartyGrowthLayer growth, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _growth = growth;
            _events = events;
        }

        public MascotPose Pose { get; private set; } = MascotPose.Hidden;
        public MascotPlace Place { get; private set; } = MascotPlace.Behind;
        public string Gesture { get; private set; } = string.Empty;
        public double GestureSecondsLeft => _gestureLeft;
        public string Beast => _growth.InParty ? _growth.MascotBeast : string.Empty;
        public string Stage => _growth.MascotStage?.Id ?? string.Empty;
        public string Temper => _temper;

        public void UseRules(PartyMascotRules rules)
        {
            _rules = rules;
            _gesturesFor = string.Empty;
        }

        public void SetTemper(string temper)
        {
            _temper = temper ?? string.Empty;
            _gesturesFor = string.Empty;
        }

        public void Tick(int firstEvent, bool hasInput)
        {
            _idle = hasInput ? 0 : _idle + GameLoop.TickSeconds;
            _cheer = System.Math.Max(0, _cheer - GameLoop.TickSeconds);
            for (var i = firstEvent; i < _events.Count; i++)
            {
                if (_events[i].Kind == GameEventKind.BossDefeated)
                    _cheer = _rules.CheerSeconds;
            }
            Pose = PoseNow(hasInput);
            Place = Pose == MascotPose.Charge ? MascotPlace.Front : MascotPlace.Behind;
            TickGesture();
        }

        private MascotPose PoseNow(bool hasInput)
        {
            if (Beast.Length == 0)
                return MascotPose.Hidden;
            if (_state.MascotCallTicks > 0)
                return MascotPose.Charge;
            if (_cheer > 0)
                return MascotPose.Cheer;
            if (_growth.MascotStrikes && hasInput && _state.EnemyAlive)
                return MascotPose.Strike;
            if (_state.EnemyAlive && _state.FightingBoss)
                return MascotPose.Stance;
            if (_idle >= _rules.SleepAfterSeconds)
                return MascotPose.Sleep;
            if (_idle >= _rules.SitAfterSeconds)
                return MascotPose.Sit;
            return MascotPose.Walk;
        }

        private void TickGesture()
        {
            var calm = Pose == MascotPose.Walk || Pose == MascotPose.Sit;
            var gestures = GesturesNow();
            if (!calm || gestures.Count == 0)
            {
                StopGesture();
                return;
            }
            if (_gestureLeft > 0)
            {
                _gestureLeft -= GameLoop.TickSeconds;
                if (_gestureLeft <= 0)
                    StopGesture();
                return;
            }
            if (_untilGesture < 0)
                _untilGesture = _rules.GestureMinSeconds + _rng.NextDouble() * (_rules.GestureMaxSeconds - _rules.GestureMinSeconds);
            _untilGesture -= GameLoop.TickSeconds;
            if (_untilGesture > 0)
                return;
            Gesture = gestures[_rng.NextInt(0, gestures.Count)];
            _gestureLeft = _rules.CheerSeconds;
            _untilGesture = -1;
        }

        private IReadOnlyList<string> GesturesNow()
        {
            var beast = Beast;
            if (!ReferenceEquals(beast, _gesturesFor))
            {
                _gesturesFor = beast;
                _gestures.Clear();
                var def = beast.Length == 0 ? null : _rules.Beast(beast);
                if (def != null)
                    _gestures.AddRange(def.Gestures);
                _gestures.AddRange(_rules.TemperGestures(_temper));
            }
            return _gestures;
        }

        private void StopGesture()
        {
            Gesture = string.Empty;
            _gestureLeft = 0;
        }
    }
}
