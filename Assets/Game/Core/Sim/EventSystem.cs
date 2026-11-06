using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim.Rift;

namespace Game.Core.Sim
{
    public enum EventFightResult
    {
        Started,
        NoEvent,
        NoFightsLeft,
        Busy
    }

    public sealed class EventSystem
    {
        private readonly GameState _state;
        private readonly StageSystem _stage;
        private readonly EventQueue _events;
        private EventRules _rules;
        private StageTable _home;
        private EnemyCatalog _enemies;
        private EventWindow? _window;
        private long _day = long.MinValue;

        public EventSystem(GameData data, GameState state, StageSystem stage, EventQueue events)
        {
            _rules = data.Events;
            _home = data.Stages;
            _enemies = data.Enemies;
            _state = state;
            _stage = stage;
            _events = events;
            if (State.FightInside)
                ApplyFightWorld();
        }

        public EventRules Rules => _rules;
        public EventState State => _state.Events;
        public EventWindow? Window => _window;
        public EventWindow? ShopWindow => _day == long.MinValue ? null : _rules.ShopWindowAt(_day);
        public bool Active => _window != null;
        public System.Action<string>? BossWon { get; set; }
        public bool FightInside => State.FightInside;
        public int FightsLeftToday => System.Math.Max(0, _rules.BossPerDay - (State.BossDay == _day ? State.BossFightsToday : 0));

        public void UseData(GameData data)
        {
            _rules = data.Events;
            _home = data.Stages;
            _enemies = data.Enemies;
            if (State.FightInside)
                ApplyFightWorld();
            if (_day != long.MinValue)
                SetDay(_day);
        }

        public void SetDay(long day)
        {
            _day = day;
            _window = _rules.WindowAt(day);
            var events = State;
            if (events.BossDay != day)
            {
                events.BossDay = day;
                events.BossFightsToday = 0;
            }
            if (_window is EventWindow window && window.Id != events.WindowId)
                StartWindow(window.Id);
        }

        public long Progress(EventGoalKind kind) => kind switch
        {
            EventGoalKind.Guests => State.Guests,
            EventGoalKind.BossFights => State.BossFights,
            EventGoalKind.BossWins => State.BossWins,
            EventGoalKind.Tokens => State.Tokens,
            EventGoalKind.DailyQuests => State.DailyQuests,
            EventGoalKind.PartyMinutes => State.PartyTicks / (60 * GameLoop.TicksPerSecond),
            _ => 0
        };

        public void NoteDailyQuest()
        {
            if (Active)
                State.DailyQuests++;
        }

        public void Tick(int firstEvent)
        {
            if (!Active)
                return;
            var events = State;
            if (_state.PartyAllies > 0)
                events.PartyTicks++;
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind == GameEventKind.EnemyKilled && events.Guest && gameEvent.Enemy?.Type == EnemyType.Normal)
                {
                    events.Guests++;
                    Earn(_rules.TokensPerGuest);
                }
                else if (gameEvent.Kind == GameEventKind.EnemySpawned)
                    NoteSpawn();
            }
            if (events.FightInside)
                FollowFight();
            CheckGoals();
        }

        public EventFightResult StartBossFight()
        {
            if (!Active)
                return EventFightResult.NoEvent;
            if (FightsLeftToday <= 0)
                return EventFightResult.NoFightsLeft;
            if (State.FightInside || _state.Dungeons.Inside || _state.Rift.Inside || _stage.Linked)
                return EventFightResult.Busy;
            var events = State;
            events.BossFightsToday++;
            events.BossFights++;
            events.FightHome = StageProgress.Capture(_state);
            events.FightInside = true;
            events.FightStage = System.Math.Max(1, _state.RecordStage);
            events.FightTicksLeft = 0;
            new StageProgress { Stage = events.FightStage, HighestStage = events.FightStage }.Restore(_state);
            ApplyFightWorld();
            Respawn();
            return EventFightResult.Started;
        }

        public bool AbandonFight()
        {
            if (!State.FightInside)
                return false;
            LeaveFight();
            return true;
        }

        public IReadOnlyList<string> GoalsToSend()
        {
            var ids = new List<string>();
            foreach (var id in State.GoalsDone)
            {
                if (!State.GoalsSent.Contains(id))
                    ids.Add(id);
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        public void Sent(string windowId, long tokens, IEnumerable<string> goals, long score)
        {
            var events = State;
            if (windowId != events.WindowId)
                return;
            events.UnsentTokens = System.Math.Max(0, events.UnsentTokens - System.Math.Max(0, tokens));
            foreach (var goal in goals)
                events.GoalsSent.Add(goal);
            if (score >= events.UnsentScore)
                events.UnsentScore = 0;
        }

        private void StartWindow(string id)
        {
            var events = State;
            events.WindowId = id;
            events.UnsentTokens = 0;
            events.Tokens = 0;
            events.Guests = 0;
            events.BossFights = 0;
            events.BossWins = 0;
            events.DailyQuests = 0;
            events.PartyTicks = 0;
            events.Spawns = 0;
            events.Guest = false;
            events.GoalsDone.Clear();
            events.GoalsSent.Clear();
            events.UnsentScore = 0;
            events.BestScore = 0;
        }

        private void NoteSpawn()
        {
            var events = State;
            if (_state.EnemyType != EnemyType.Normal)
            {
                events.Guest = false;
                return;
            }
            events.Spawns++;
            events.Guest = events.FightInside || events.Spawns % _rules.GuestEvery == 0;
        }

        private void Earn(long tokens)
        {
            State.UnsentTokens += tokens;
            State.Tokens += tokens;
        }

        private void FollowFight()
        {
            var events = State;
            if (_state.FightingBoss)
                events.FightTicksLeft = _state.BossTicksLeft;
            if (_state.Stage > events.FightStage)
            {
                events.BossWins++;
                Earn(_rules.BossTokens);
                var score = (long)events.FightTicksLeft * 1000 / GameLoop.TicksPerSecond;
                events.UnsentScore = System.Math.Max(events.UnsentScore, score);
                events.BestScore = System.Math.Max(events.BestScore, score);
                _events.Add(GameEvent.ForStage(GameEventKind.EventBossDefeated, _state.Tick, score));
                if (_window != null)
                    BossWon?.Invoke(_window.Value.Event.Id);
                LeaveFight();
            }
            else if (_state.BossFailsInARow > 0)
            {
                _events.Add(GameEvent.ForStage(GameEventKind.EventBossFailed, _state.Tick, events.FightStage));
                LeaveFight();
            }
        }

        private void CheckGoals()
        {
            var events = State;
            var goals = _rules.Goals;
            for (var i = 0; i < goals.Count; i++)
            {
                if (!events.GoalsDone.Contains(goals[i].Id) && Progress(goals[i].Kind) >= goals[i].Target)
                    events.GoalsDone.Add(goals[i].Id);
            }
        }

        private void LeaveFight()
        {
            var events = State;
            (events.FightHome ?? new StageProgress()).Restore(_state);
            events.FightHome = null;
            events.FightInside = false;
            events.Guest = false;
            _stage.UseData(_home, _enemies);
            _stage.RaisesRecord = true;
            Respawn();
        }

        private void ApplyFightWorld()
        {
            _stage.UseData(_home.ForDungeon(1, _rules.BossHpMultiplier, _rules.BossTimerSeconds), _enemies);
            _stage.RaisesRecord = false;
        }

        private void Respawn()
        {
            _state.SpawnEnemy(null, EnemyType.Normal, BigNumber.Zero);
            _stage.EnsureEnemy();
        }
    }
}
