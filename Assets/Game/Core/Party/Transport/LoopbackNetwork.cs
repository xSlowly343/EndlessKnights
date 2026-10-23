using System;
using Game.Core.Math;

namespace Game.Core.Party.Transport
{
    public sealed class LoopbackNetwork
    {
        private readonly Rng _rng;
        private readonly LoopbackPacket[] _packets;
        private readonly byte[][] _payloads;
        private readonly ulong[] _downLinks = new ulong[PartyRules.MaxSlots * 2];
        private int _downCount;

        public LoopbackNetwork(ulong seed, double minLatencyMs = 0, double maxLatencyMs = 0, double loss = 0, int capacity = 1024, int maxPacketBytes = 256)
        {
            if (maxLatencyMs < minLatencyMs)
                throw new ArgumentException("max latency must not be lower than min latency", nameof(maxLatencyMs));
            _rng = new Rng(seed);
            MinLatencyMs = minLatencyMs;
            MaxLatencyMs = maxLatencyMs;
            Loss = loss;
            _packets = new LoopbackPacket[capacity];
            _payloads = new byte[capacity][];
            for (var i = 0; i < capacity; i++)
                _payloads[i] = new byte[maxPacketBytes];
        }

        public double NowMs { get; private set; }
        public double MinLatencyMs { get; set; }
        public double MaxLatencyMs { get; set; }
        public double Loss { get; set; }
        public long Sent { get; private set; }
        public long Dropped { get; private set; }
        public long Delivered { get; private set; }

        public LoopbackTransport Connect(ulong id) => new LoopbackTransport(this, id);

        public void Advance(double ms) => NowMs += System.Math.Max(0, ms);

        public void SetLinkDown(ulong id, bool down)
        {
            var index = IndexOfDown(id);
            if (down && index < 0)
            {
                if (_downCount == _downLinks.Length)
                    throw new InvalidOperationException("too many links down");
                _downLinks[_downCount++] = id;
            }
            else if (!down && index >= 0)
            {
                _downLinks[index] = _downLinks[--_downCount];
            }
        }

        internal void Send(ulong from, ulong to, byte[] data, int length)
        {
            Sent++;
            if (IndexOfDown(from) >= 0 || IndexOfDown(to) >= 0 || _rng.Chance(Loss))
            {
                Dropped++;
                return;
            }
            var slot = FreeSlot();
            if (slot < 0 || length > _payloads[slot].Length)
            {
                Dropped++;
                return;
            }
            Buffer.BlockCopy(data, 0, _payloads[slot], 0, length);
            _packets[slot] = new LoopbackPacket
            {
                InFlight = true,
                DeliverAtMs = NowMs + MinLatencyMs + _rng.NextDouble() * (MaxLatencyMs - MinLatencyMs),
                From = from,
                To = to,
                Length = length
            };
        }

        internal bool TryReceive(ulong to, byte[] buffer, out int length, out ulong from)
        {
            var best = -1;
            for (var i = 0; i < _packets.Length; i++)
            {
                ref var packet = ref _packets[i];
                if (!packet.InFlight || packet.To != to || packet.DeliverAtMs > NowMs)
                    continue;
                if (best < 0 || packet.DeliverAtMs < _packets[best].DeliverAtMs)
                    best = i;
            }
            if (best < 0)
            {
                length = 0;
                from = 0;
                return false;
            }
            ref var found = ref _packets[best];
            found.InFlight = false;
            length = System.Math.Min(found.Length, buffer.Length);
            from = found.From;
            Buffer.BlockCopy(_payloads[best], 0, buffer, 0, length);
            Delivered++;
            return true;
        }

        private int FreeSlot()
        {
            for (var i = 0; i < _packets.Length; i++)
            {
                if (!_packets[i].InFlight)
                    return i;
            }
            return -1;
        }

        private int IndexOfDown(ulong id)
        {
            for (var i = 0; i < _downCount; i++)
            {
                if (_downLinks[i] == id)
                    return i;
            }
            return -1;
        }
    }
}
