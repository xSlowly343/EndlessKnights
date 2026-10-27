using System;

namespace Game.Core.Diagnostics
{
    public sealed class LogRing
    {
        private readonly object _gate = new object();
        private readonly string[] _lines;
        private int _next;
        private int _count;

        public LogRing(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, null);
            _lines = new string[capacity];
        }

        public int Capacity => _lines.Length;

        public int Count
        {
            get
            {
                lock (_gate)
                    return _count;
            }
        }

        public void Add(string line)
        {
            lock (_gate)
            {
                _lines[_next] = line;
                _next = (_next + 1) % _lines.Length;
                if (_count < _lines.Length)
                    _count++;
            }
        }

        public string[] Last(int max)
        {
            lock (_gate)
            {
                var take = System.Math.Min(System.Math.Max(0, max), _count);
                var result = new string[take];
                var start = (_next - take + _lines.Length) % _lines.Length;
                for (var i = 0; i < take; i++)
                    result[i] = _lines[(start + i) % _lines.Length];
                return result;
            }
        }
    }
}
