using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class BestiarySystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private BestiaryCatalog _catalog;

        public BestiarySystem(BestiaryCatalog catalog, GameState state, EventQueue events)
        {
            _catalog = catalog;
            _state = state;
            _events = events;
        }

        public BestiaryCatalog Catalog => _catalog;
        public BestiaryState State => _state.Bestiary;

        public void UseCatalog(BestiaryCatalog catalog) => _catalog = catalog;

        public long KillsOf(string enemy) => State.Kills.TryGetValue(enemy, out var kills) ? kills : 0;

        public bool IsOpen(string enemy) => State.Opened.Contains(enemy);

        public void Tick(int firstEvent)
        {
            var count = _events.Count;
            for (var i = firstEvent; i < count; i++)
            {
                var gameEvent = _events[i];
                var counted = gameEvent.Kind == GameEventKind.EnemyKilled ? 1
                    : gameEvent.Kind == GameEventKind.EnemiesSkipped ? (long)gameEvent.Amount.ToDouble()
                    : 0;
                if (counted <= 0 || gameEvent.Enemy == null)
                    continue;
                var enemy = gameEvent.Enemy;
                var kills = KillsOf(enemy.Id) + counted;
                State.Kills[enemy.Id] = kills;
                if (kills < _catalog.KillsToOpen(enemy.Type) || State.Opened.Contains(enemy.Id) || !_catalog.TryGet(enemy.Id, out _))
                    continue;
                State.Opened.Add(enemy.Id);
                _events.Add(GameEvent.ForEnemy(GameEventKind.BestiaryEntryOpened, _state.Tick, gameEvent.Stage, enemy));
            }
        }
    }
}
