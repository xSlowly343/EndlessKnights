using Game.Core.Content.Gems;

namespace Game.Core.Ui
{
    public sealed class ShowQueue
    {
        private readonly ShowRules _rules;
        private readonly GemDef?[] _gems;
        private readonly int[] _sources;
        private readonly double[] _offeredAt;
        private readonly bool[] _urgent;
        private readonly bool[] _pushedOut;
        private int _first;
        private int _count;
        private double _freeAt = double.NegativeInfinity;
        private double _urgentUntil = double.NegativeInfinity;

        public ShowQueue(ShowRules rules)
        {
            _rules = rules;
            var size = rules.Capacity + 1;
            _gems = new GemDef?[size];
            _sources = new int[size];
            _offeredAt = new double[size];
            _urgent = new bool[size];
            _pushedOut = new bool[size];
        }

        public int Waiting => _count;

        public bool Busy(double now) => now < _freeAt;

        public bool UrgentBusy(double now) => now < _urgentUntil;

        public void Offer(GemDef gem, int source, bool urgent, double now)
        {
            if (_count == _gems.Length)
                Drop();
            if (_count == _rules.Capacity && !urgent)
                PushOutOldest();
            var at = (_first + _count) % _gems.Length;
            _gems[at] = gem;
            _sources[at] = source;
            _offeredAt[at] = now;
            _urgent[at] = urgent;
            _pushedOut[at] = false;
            _count++;
        }

        public bool TryTake(double now, out ShowCue cue)
        {
            for (var step = 0; step < _count; step++)
            {
                var at = (_first + step) % _gems.Length;
                if (_urgent[at])
                {
                    cue = Take(step, ShowSize.Full);
                    _freeAt = now + _rules.UrgentSeconds + _rules.GapSeconds;
                    _urgentUntil = now + _rules.UrgentSeconds;
                    return true;
                }
            }
            if (_count == 0)
            {
                cue = default;
                return false;
            }
            if (_pushedOut[_first] || (now < _freeAt && now - _offeredAt[_first] > _rules.WaitSeconds))
            {
                cue = Take(0, ShowSize.Small);
                return true;
            }
            if (now < _freeAt)
            {
                cue = default;
                return false;
            }
            cue = Take(0, ShowSize.Full);
            _freeAt = now + _rules.FullSeconds + _rules.GapSeconds;
            return true;
        }

        public void Clear()
        {
            while (_count > 0)
                Drop();
        }

        private void PushOutOldest()
        {
            for (var step = 0; step < _count; step++)
            {
                var at = (_first + step) % _gems.Length;
                if (_urgent[at] || _pushedOut[at])
                    continue;
                _pushedOut[at] = true;
                return;
            }
        }

        private ShowCue Take(int step, ShowSize size)
        {
            var at = (_first + step) % _gems.Length;
            var cue = new ShowCue(_gems[at]!, _sources[at], size);
            for (var move = step; move > 0; move--)
            {
                var to = (_first + move) % _gems.Length;
                var from = (_first + move - 1) % _gems.Length;
                _gems[to] = _gems[from];
                _sources[to] = _sources[from];
                _offeredAt[to] = _offeredAt[from];
                _urgent[to] = _urgent[from];
                _pushedOut[to] = _pushedOut[from];
            }
            Drop();
            return cue;
        }

        private void Drop()
        {
            _gems[_first] = null;
            _first = (_first + 1) % _gems.Length;
            _count--;
        }
    }
}
