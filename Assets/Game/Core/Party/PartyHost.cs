using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyHost
    {
        private readonly PartyEncounter _encounter;
        private readonly IPartyTransport _transport;
        private readonly byte[] _in = new byte[PartyCodec.MaxPacketBytes];
        private readonly byte[] _out = new byte[PartyCodec.MaxPacketBytes];
        private readonly long[] _lastHeardTick = new long[PartyRules.MaxSlots];
        private readonly uint[] _lastSequence = new uint[PartyRules.MaxSlots];
        private readonly uint[] _lastEnemy = new uint[PartyRules.MaxSlots];
        private readonly double[] _lastTotal = new double[PartyRules.MaxSlots];
        private readonly int[] _messagesThisSecond = new int[PartyRules.MaxSlots];
        private readonly PartyPresence[] _sentPresence = new PartyPresence[PartyRules.MaxSlots];
        private PartyRules _rules;
        private uint _stateSequence;
        private uint _rosterVersion;
        private bool _rosterChanged;
        private int _ticksToState;
        private int _ticksToRoster;

        public PartyHost(PartyEncounter encounter, IPartyTransport transport, PartyRules rules)
        {
            _encounter = encounter;
            _transport = transport;
            _rules = rules;
        }

        public PartyEncounter Encounter => _encounter;
        public long Kills { get; private set; }
        public double AcceptedShares { get; private set; }

        public void UseRules(PartyRules rules) => _rules = rules;

        public void Tick()
        {
            while (_transport.TryReceive(_in, out var length, out var from))
                Handle(from, length);
            _encounter.Advance();
            if (_encounter.Tick % GameLoop.TicksPerSecond == 0)
                System.Array.Clear(_messagesThisSecond, 0, _messagesThisSecond.Length);
            WatchSilence();
            WatchPresence();
            Flush();
        }

        private void Handle(ulong from, int length)
        {
            var kind = PartyCodec.KindOf(_in, length);
            var slot = _encounter.SlotOf(from);
            if (kind == PartyMessageKind.Hello && PartyCodec.TryDecode(_in, length, out HelloMessage hello))
            {
                slot = Welcome(from, slot);
                if (slot != PartyEncounter.NoSlot && _encounter.Member(slot).HeroClass != hello.HeroClass)
                {
                    _encounter.SetHeroClass(slot, hello.HeroClass);
                    _rosterChanged = true;
                }
            }
            if (slot == PartyEncounter.NoSlot || ++_messagesThisSecond[slot] > _rules.MaxMessagesPerSecond)
                return;
            Heard(slot);
            switch (kind)
            {
                case PartyMessageKind.DamageBatch when PartyCodec.TryDecode(_in, length, out DamageBatchMessage batch):
                    Damage(slot, batch);
                    break;
                case PartyMessageKind.Presence when PartyCodec.TryDecode(_in, length, out PresenceMessage presence):
                    _encounter.SetPeaceful(slot, presence.Presence == PartyPresence.Peaceful);
                    break;
                case PartyMessageKind.CastEffect when PartyCodec.TryDecode(_in, length, out CastEffectMessage effect):
                    if (effect.OnEnemy)
                        _encounter.ApplyEnemyEffect(effect.EffectId, effect.Strength, effect.Seconds, slot);
                    break;
                case PartyMessageKind.PartyOffer when PartyCodec.TryDecode(_in, length, out PartyOfferMessage offer):
                    offer.Slot = (byte)slot;
                    BroadcastExcept(slot, PartyCodec.Encode(_out, offer));
                    break;
            }
        }

        private int Welcome(ulong from, int slot)
        {
            if (slot != PartyEncounter.NoSlot)
                return slot;
            slot = _encounter.Join(from);
            if (slot == PartyEncounter.NoSlot)
                return slot;
            _lastSequence[slot] = 0;
            _lastEnemy[slot] = 0;
            _lastTotal[slot] = 0;
            _rosterChanged = true;
            _ticksToState = 0;
            return slot;
        }

        private void Heard(int slot)
        {
            _lastHeardTick[slot] = _encounter.Tick;
            if (_encounter.Member(slot).Connected)
                return;
            _encounter.SetConnected(slot, true);
            _rosterChanged = true;
        }

        private void Damage(int slot, in DamageBatchMessage batch)
        {
            if (batch.Sequence <= _lastSequence[slot])
                return;
            _lastSequence[slot] = batch.Sequence;
            var total = batch.Total;
            var delta = batch.EnemyId == _lastEnemy[slot] ? total - _lastTotal[slot] : total;
            if (batch.EnemyId != _lastEnemy[slot] || total > _lastTotal[slot])
            {
                _lastEnemy[slot] = batch.EnemyId;
                _lastTotal[slot] = total;
            }
            if (batch.Hits > 0)
                _encounter.ReportInput(slot);
            if (delta > 0)
                AcceptedShares += _encounter.ApplyDamage(slot, batch.EnemyId, delta);
        }

        private void WatchSilence()
        {
            var silence = (long)System.Math.Round(_rules.SilenceSeconds * GameLoop.TicksPerSecond);
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var member = _encounter.Member(slot);
                if (!member.Joined || !member.Connected || _encounter.Tick - _lastHeardTick[slot] <= silence)
                    continue;
                _encounter.SetConnected(slot, false);
                _rosterChanged = true;
            }
        }

        private void WatchPresence()
        {
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var presence = _encounter.Member(slot).Joined ? _encounter.Presence(slot) : PartyPresence.Offline;
                if (presence == _sentPresence[slot])
                    continue;
                _sentPresence[slot] = presence;
                _rosterChanged = true;
            }
        }

        private void Flush()
        {
            var events = _encounter.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var partyEvent = events[i];
                switch (partyEvent.Kind)
                {
                    case PartyEventKind.EnemyKilled:
                        Kills++;
                        Broadcast(PartyCodec.Encode(_out, EnemyKilledMessage.Of(partyEvent)));
                        break;
                    case PartyEventKind.EnemySpawned:
                        _ticksToState = 0;
                        break;
                    default:
                        Broadcast(PartyCodec.Encode(_out, new EncounterMomentMessage
                        {
                            Kind = partyEvent.Kind,
                            Tick = partyEvent.Tick,
                            EnemyId = partyEvent.EnemyId,
                            BossTicksLeft = _encounter.BossTicksLeft
                        }));
                        break;
                }
            }
            events.Clear();

            if (--_ticksToRoster <= 0 || _rosterChanged)
            {
                _ticksToRoster = Ticks(_rules.PresenceSeconds);
                _rosterChanged = false;
                Broadcast(PartyCodec.Encode(_out, RosterMessage.Of(_encounter, ++_rosterVersion)));
            }
            if (--_ticksToState <= 0 && _encounter.Started)
            {
                _ticksToState = Ticks(_rules.StateBroadcastSeconds);
                Broadcast(PartyCodec.Encode(_out, EncounterStateMessage.Of(_encounter, ++_stateSequence)));
            }
        }

        private void Broadcast(int length) => BroadcastExcept(PartyEncounter.NoSlot, length);

        private void BroadcastExcept(int skipped, int length)
        {
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var member = _encounter.Member(slot);
                if (member.Joined && slot != skipped)
                    _transport.Send(member.PlayerId, _out, length);
            }
        }

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
