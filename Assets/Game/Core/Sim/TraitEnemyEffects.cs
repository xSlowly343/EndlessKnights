namespace Game.Core.Sim
{
    public sealed class TraitEnemyEffects
    {
        public const int Capacity = 8;

        private readonly TraitEnemyEffect[] _items = new TraitEnemyEffect[Capacity];

        public int Count { get; private set; }

        public TraitEnemyEffect this[int index] => _items[index];

        public void Add(int id, double strength, double seconds, double timerSlow = 0)
        {
            if (Count >= Capacity)
                return;
            _items[Count++] = new TraitEnemyEffect { Id = id, Strength = strength, Seconds = seconds, TimerSlow = timerSlow };
        }

        public void AddRepeat(int id, double share, double seconds)
        {
            if (Count >= Capacity)
                return;
            _items[Count++] = new TraitEnemyEffect { Id = id, Strength = share, Seconds = seconds, RepeatSeconds = seconds };
        }

        public void Clear() => Count = 0;
    }
}
