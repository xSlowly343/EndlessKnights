using System.Collections.Generic;

namespace Game.Core.Party
{
    public readonly struct ChatLine
    {
        public ChatLine(long seq, string from, string name, string role, string text, long atUnixMs)
        {
            Seq = seq;
            From = from;
            Name = name;
            Role = role;
            Text = text;
            AtUnixMs = atUnixMs;
        }

        public long Seq { get; }
        public string From { get; }
        public string Name { get; }
        public string Role { get; }
        public string Text { get; }
        public long AtUnixMs { get; }
    }

    public sealed class ChatLog
    {
        private readonly List<ChatLine> _lines = new List<ChatLine>();
        private readonly int _history;
        private long _readSeq;

        public ChatLog(string channel, int history)
        {
            Channel = channel;
            _history = history;
        }

        public string Channel { get; }
        public string Room { get; private set; } = string.Empty;
        public long LastSeq { get; private set; }
        public long MutedUntilUnixMs { get; private set; }
        public bool CanModerate { get; private set; }
        public string Role { get; private set; } = string.Empty;
        public IReadOnlyList<ChatLine> Lines => _lines;

        public int Unread
        {
            get
            {
                var unread = 0;
                foreach (var line in _lines)
                {
                    if (line.Seq > _readSeq)
                        unread++;
                }
                return unread;
            }
        }

        public void Receive(string room, long seq, IReadOnlyList<ChatLine> lines, IReadOnlyList<long> removed, bool canModerate, string role, long mutedUntilUnixMs)
        {
            if (room != Room)
            {
                _lines.Clear();
                _readSeq = 0;
                LastSeq = 0;
                Room = room;
            }
            foreach (var line in lines)
            {
                if (line.Seq > LastSeq)
                {
                    _lines.Add(line);
                    LastSeq = line.Seq;
                }
            }
            if (seq > LastSeq)
                LastSeq = seq;
            foreach (var gone in removed)
                Remove(gone);
            if (_lines.Count > _history)
                _lines.RemoveRange(0, _lines.Count - _history);
            CanModerate = canModerate;
            Role = role;
            MutedUntilUnixMs = mutedUntilUnixMs;
        }

        public void MarkRead() => _readSeq = LastSeq;

        public bool CanDelete(ChatLine line, string userId) => CanModerate || line.From == userId;

        public bool IsMuted(long nowUnixMs) => MutedUntilUnixMs > nowUnixMs;

        public void Hide(string userId) => _lines.RemoveAll(line => line.From == userId);

        private void Remove(long seq)
        {
            for (var i = 0; i < _lines.Count; i++)
            {
                if (_lines[i].Seq == seq)
                {
                    _lines.RemoveAt(i);
                    return;
                }
            }
        }
    }
}
