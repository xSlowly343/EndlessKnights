using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class StripScene
    {
        private readonly GameState _state;
        private readonly PeacefulSystem _peaceful;
        private readonly EventQueue _events;
        private StripSceneRules _rules;
        private bool _started;
        private int _switchTicks;

        public StripScene(StripSceneRules rules, GameState state, PeacefulSystem peaceful, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _peaceful = peaceful;
            _events = events;
        }

        public StripPlace Place { get; private set; }
        public StripPlace PreviousPlace { get; private set; }
        public StripFight Fight { get; private set; } = StripFight.Enemy;
        public int SwitchTicksLeft { get; private set; }
        public StripTransition Transition { get; } = new StripTransition();
        public bool Switching => SwitchTicksLeft > 0;
        public double SwitchProgress => _switchTicks == 0 ? 1 : 1 - (double)SwitchTicksLeft / _switchTicks;
        public bool Peaceful => IsPeaceful(Place);
        public string Activity => Peaceful ? _peaceful.Activity : string.Empty;

        public void UseRules(StripSceneRules rules) => _rules = rules;

        public static bool IsPeaceful(StripPlace place) => place >= StripPlace.Hideout;

        public static StripPlace PlaceOf(GameState state)
        {
            if (state.Peaceful.Mode == PeacefulMode.Camp)
                return PlaceOf(state.Peaceful.Place);
            if (state.Events.FightInside)
                return StripPlace.EventFight;
            if (state.Dungeons.Inside)
                return StripPlace.Dungeon;
            return state.Rift.Inside ? StripPlace.Rift : StripPlace.Home;
        }

        public static StripPlace PlaceOf(PeacefulPlace place) => place switch
        {
            PeacefulPlace.Town => StripPlace.Town,
            PeacefulPlace.ClanCamp => StripPlace.ClanCamp,
            _ => StripPlace.Hideout
        };

        public static StripFight FightOf(GameState state, StripPlace place)
        {
            if (IsPeaceful(place))
                return StripFight.None;
            return state.EnemyType switch
            {
                EnemyType.Boss => StripFight.Boss,
                EnemyType.Guardian => StripFight.Guardian,
                _ => StripFight.Enemy
            };
        }

        public void Tick()
        {
            var place = PlaceOf(_state);
            var fight = FightOf(_state, place);
            if (!_started)
            {
                _started = true;
                Place = place;
                PreviousPlace = place;
                Fight = fight;
                return;
            }
            if (SwitchTicksLeft > 0)
                SwitchTicksLeft--;
            Transition.Tick();
            if (place != Place)
                MoveTo(place);
            if (fight == Fight)
                return;
            Fight = fight;
            _events.Add(GameEvent.ForStage(GameEventKind.StripFightChanged, _state.Tick, (long)fight));
        }

        private void MoveTo(StripPlace place)
        {
            PreviousPlace = Place;
            Place = place;
            _switchTicks = (int)System.Math.Round(_rules.SwitchSeconds * GameLoop.TicksPerSecond);
            SwitchTicksLeft = _switchTicks;
            Transition.Start(PreviousPlace, place, _rules.Portal(place));
            _events.Add(GameEvent.ForStage(GameEventKind.StripPlaceChanged, _state.Tick, (long)place));
        }
    }
}
