using System;

namespace Game.Core.Party
{
    public sealed class PartyEventQueue
    {
        private PartyEvent[] _items;

        public PartyEventQueue(int capacity = 64)
        {
            _items = new PartyEvent[System.Math.Max(1, capacity)];
        }

        public int Count { get; private set; }

        public PartyEvent this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "No event at this index.");
                return _items[index];
            }
        }

        public void Add(in PartyEvent partyEvent)
        {
            if (Count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            _items[Count++] = partyEvent;
        }

        public int CountOf(PartyEventKind kind)
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
