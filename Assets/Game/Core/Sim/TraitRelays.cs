namespace Game.Core.Sim
{
    public sealed class TraitRelays
    {
        public const int Capacity = 16;

        private readonly TraitRelay[] _items = new TraitRelay[Capacity];

        public int Count { get; private set; }

        public TraitRelay this[int index] => _items[index];

        public void Add(in TraitRelay relay)
        {
            if (Count < Capacity)
                _items[Count++] = relay;
        }

        public void Clear() => Count = 0;
    }
}
