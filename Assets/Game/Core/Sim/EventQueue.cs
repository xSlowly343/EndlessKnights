using System;

namespace Game.Core.Sim
{
    public sealed class EventQueue
    {
        private GameEvent[] _items;

        public EventQueue(int capacity = 256)
        {
            _items = new GameEvent[System.Math.Max(1, capacity)];
        }

        public int Count { get; private set; }

        public GameEvent this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "No event at this index.");
                return _items[index];
            }
        }

        public void Add(in GameEvent gameEvent)
        {
            if (Count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            _items[Count++] = gameEvent;
        }

        public int CountOf(GameEventKind kind)
        {
            var count = 0;
            for (var i = 0; i < Count; i++)
            {
                if (_items[i].Kind == kind)
                    count++;
            }
            return count;
        }

        public void Clear() => Count = 0;
    }
}
