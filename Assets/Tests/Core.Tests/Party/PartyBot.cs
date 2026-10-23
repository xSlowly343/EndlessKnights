using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;

namespace Game.Core.Tests.Party
{
    public sealed class PartyBot
    {
        private readonly IPartyTransport _transport;
        private readonly ulong _host;
        private readonly byte[] _buffer = new byte[PartyCodec.MaxPacketBytes];
        private readonly double _sharePerTick;
        private uint _sequence;
        private int _ticks;
        private double _total;

        public PartyBot(IPartyTransport transport, ulong host, double sharePerTick)
        {
            _transport = transport;
            _host = host;
            _sharePerTick = sharePerTick;
        }

        public bool Joined { get; private set; }
        public EncounterStateMessage State { get; private set; }
        public HashSet<uint> Kills { get; } = new HashSet<uint>();
        public int DuplicateKills { get; private set; }
        public double SentShares { get; private set; }

        public void Tick(bool fighting)
        {
            while (_transport.TryReceive(_buffer, out var length, out _))
                Receive(length);
            _ticks++;
            if (!Joined && _ticks % 10 == 1)
                Send(PartyCodec.Encode(_buffer, new HelloMessage { Stage = 1 }));
            if (_ticks % 50 == 0)
                Send(PartyCodec.Encode(_buffer, new PresenceMessage { Presence = PartyPresence.Fighting, Stage = 1 }));
            if (!Joined || State.EnemyId == 0)
                return;
            if (fighting)
            {
                _total += _sharePerTick;
                SentShares += _sharePerTick;
            }
            if (fighting && _ticks % 5 == 0)
                Send(PartyCodec.Encode(_buffer, new DamageBatchMessage { Sequence = ++_sequence, EnemyId = State.EnemyId, Keys = _total, Hits = 5 }));
        }

        private void Receive(int length)
        {
            switch (PartyCodec.KindOf(_buffer, length))
            {
                case PartyMessageKind.Roster when PartyCodec.TryDecode(_buffer, length, out RosterMessage roster):
                    Joined = roster.SlotOf(_transport.LocalId) != PartyEncounter.NoSlot;
                    break;
                case PartyMessageKind.EncounterState when PartyCodec.TryDecode(_buffer, length, out EncounterStateMessage state):
                    if (state.Sequence <= State.Sequence)
                        break;
                    if (state.EnemyId != State.EnemyId)
                        _total = 0;
                    State = state;
                    break;
                case PartyMessageKind.EnemyKilled when PartyCodec.TryDecode(_buffer, length, out EnemyKilledMessage killed):
                    if (!Kills.Add(killed.EnemyId))
                        DuplicateKills++;
                    break;
            }
        }

        private void Send(int length) => _transport.Send(_host, _buffer, length);
    }
}
