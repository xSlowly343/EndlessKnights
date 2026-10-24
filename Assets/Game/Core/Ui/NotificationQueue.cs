using System.Collections.Generic;

namespace Game.Core.Ui
{
    public sealed class NotificationQueue
    {
        public const int MaxVisible = 3;
        public const double LifetimeSeconds = 3;

        private readonly List<Notification> _items = new List<Notification>(MaxVisible);

        public IReadOnlyList<Notification> Items => _items;
        public bool IsEmpty => _items.Count == 0;

        public void Push(string text, double now)
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Text == text)
                    _items.RemoveAt(i);
            }
            if (_items.Count == MaxVisible)
                _items.RemoveAt(0);
            _items.Add(new Notification(text, now));
        }

        public bool Expire(double now)
        {
            var removed = false;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Age(now) < LifetimeSeconds)
                    continue;
                _items.RemoveAt(i);
                removed = true;
            }
            return removed;
        }

        public void Clear() => _items.Clear();
    }
}
