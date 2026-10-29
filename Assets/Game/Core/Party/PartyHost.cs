using Game.Core.Content.Rift;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyHost
    {
        private const double CutTolerance = 1e-9;

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
        private readonly double[] _allowance = new double[PartyRules.MaxSlots];
        private readonly double[] _hitAllowance = new double[PartyRules.MaxSlots];
        private readonly int[] _flags = new int[PartyRules.MaxSlots];
        private readonly long[] _lastReactionTick = new long[PartyRules.MaxSlots];
        private readonly long[] _lastRepeatTick = new long[PartyRules.MaxSlots];
        private readonly long[] _lastRelayTick = new long[PartyRules.MaxSlots];
        private readonly long[] _lastResourceTick = new long[PartyRules.MaxSlots];
        private readonly long[] _lastCastNoticeTick = new long[PartyRules.MaxSlots];
        private readonly uint[] _reactions = new uint[PartyRules.MaxSlots];
        private uint _enemyReactions;
        private readonly ulong[] _invitedGuests = new ulong[PartyRules.MaxSlots];
        private readonly double[] _invitedSeconds = new double[PartyRules.MaxSlots];
        private readonly double[] _recentShares = new double[PartyRules.MaxSlots];
        private readonly int[] _recentHits = new int[PartyRules.MaxSlots];
        private readonly RiftRules? _rift;
        private PartyRules _rules;
        private uint _stateSequence;
        private uint _rosterVersion;
        private bool _rosterChanged;
        private int _ticksToState;
        private int _ticksToRoster;

        public PartyHost(PartyEncounter encounter, IPartyTransport transport, PartyRules rules, RiftRules? rift = null)
        {
            _encounter = encounter;
            _transport = transport;
            _rules = rules;
            _rift = rift;
        }

        public long ClockUnixMs { get; set; }
        public ulong LeaderId { get; set; }

        public PartyEncounter Encounter => _encounter;
        public long Kills { get; private set; }
        public double AcceptedShares { get; private set; }
        public double CutShares { get; private set; }
        public double RepeatedShares { get; private set; }
        public long Relays { get; private set; }
        public int MostReactionsOnOneEnemy { get; private set; }

        public int Flags(int slot) => _flags[slot];

        public void UseRules(PartyRules rules) => _rules = rules;

        public bool InviteGuest(ulong playerId, double seconds)
        {
            if (playerId == 0 || !(seconds > 0) || !_encounter.FightingBoss || _encounter.World != PartyWorld.Home || _encounter.Switching
                || _encounter.SlotOf(playerId) != PartyEncounter.NoSlot || _encounter.MemberCount >= _encounter.Capacity)
                return false;
            var free = PartyEncounter.NoSlot;
            for (var i = 0; i < _invitedGuests.Length; i++)
            {
                if (_invitedGuests[i] == playerId)
                {
                    free = i;
                    break;
                }
                if (free == PartyEncounter.NoSlot && _invitedGuests[i] == 0)
                    free = i;
            }
            if (free == PartyEncounter.NoSlot)
                return false;
            _invitedGuests[free] = playerId;
            _invitedSeconds[free] = seconds;
            return true;
        }

        private double TakeInvite(ulong playerId)
        {
            for (var i = 0; i < _invitedGuests.Length; i++)
            {
                if (_invitedGuests[i] != playerId)
                    continue;
                _invitedGuests[i] = 0;
                return _invitedSeconds[i];
            }
            return 0;
        }

        public void Tick()
        {
            while (_transport.TryReceive(_in, out var length, out var from))
                Handle(from, length);
            _encounter.Advance();
            ReturnWhenTheRiftCloses();
            Refill();
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
                _encounter.SetStage(slot, hello.Stage);
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
                    _encounter.SetStage(slot, presence.Stage);
                    _encounter.SetCredit(slot, presence.Contribution);
                    break;
                case PartyMessageKind.CastEffect when PartyCodec.TryDecode(_in, length, out CastEffectMessage effect):
                    if (effect.OnEnemy)
                        Cast(slot, effect);
                    break;
                case PartyMessageKind.PartyOffer when PartyCodec.TryDecode(_in, length, out PartyOfferMessage offer):
                    offer.Slot = (byte)slot;
                    _encounter.NoteAuras(slot, offer.LongestAuraTicks);
                    BroadcastExcept(slot, PartyCodec.Encode(_out, offer));
                    break;
                case PartyMessageKind.PartyReaction when PartyCodec.TryDecode(_in, length, out PartyReactionMessage reaction):
                    React(slot, reaction);
                    break;
                case PartyMessageKind.Synergy when PartyCodec.TryDecode(_in, length, out SynergyMessage synergy):
                    _reactions[slot] = synergy.Reactions;
                    _enemyReactions |= synergy.Reactions;
                    break;
                case PartyMessageKind.AllyCast when PartyCodec.TryDecode(_in, length, out AllyCastMessage cast):
                    if (_encounter.Tick - _lastCastNoticeTick[slot] >= Ticks(_rules.Buffs.CastNoticeSeconds))
                    {
                        _lastCastNoticeTick[slot] = _encounter.Tick;
                        cast.Slot = (byte)slot;
                        BroadcastExcept(slot, PartyCodec.Encode(_out, cast));
                    }
                    break;
                case PartyMessageKind.PartyResource when PartyCodec.TryDecode(_in, length, out PartyResourceMessage resource):
                    ShareResource(slot, resource.Amount);
                    break;
                case PartyMessageKind.FocusRelay when PartyCodec.TryDecode(_in, length, out FocusRelayMessage relay):
                    Relay(slot, relay.Focus);
                    break;
                case PartyMessageKind.WorldRequest when PartyCodec.TryDecode(_in, length, out WorldRequestMessage request):
                    if (LeaderId == 0 || _encounter.Member(slot).PlayerId == LeaderId)
                        RequestWorld(request);
                    break;
                case PartyMessageKind.BossRequest:
                    if (LeaderId == 0 || _encounter.Member(slot).PlayerId == LeaderId)
                        _encounter.RequestBossFight();
                    break;
            }
        }

        private int Welcome(ulong from, int slot)
        {
            if (slot != PartyEncounter.NoSlot)
                return slot;
            var guestSeconds = TakeInvite(from);
            slot = guestSeconds > 0 ? _encounter.JoinGuest(from, guestSeconds) : _encounter.Join(from);
            if (slot == PartyEncounter.NoSlot)
                return slot;
            _lastSequence[slot] = 0;
            _lastEnemy[slot] = 0;
            _lastTotal[slot] = 0;
            _allowance[slot] = _rules.Caps.BurstBars;
            _hitAllowance[slot] = HitBurst;
            _flags[slot] = 0;
            _lastReactionTick[slot] = PartyEncounter.Never;
            _lastRepeatTick[slot] = PartyEncounter.Never;
            _lastRelayTick[slot] = PartyEncounter.Never;
            _lastResourceTick[slot] = PartyEncounter.Never;
            _lastCastNoticeTick[slot] = PartyEncounter.Never;
            _reactions[slot] = 0;
            _rosterChanged = true;
            _ticksToState = 0;
            return slot;
        }

        private void Cast(int slot, in CastEffectMessage effect)
        {
            if (effect.End)
            {
                _encounter.EndEnemyEffect(effect.EffectId, slot);
                return;
            }
            if (effect.WeakenEnemies > 0)
            {
                _encounter.WeakenNext(effect.WeakenEnemies, effect.WeakenShare, effect.WeakenGold, effect.Strength, effect.Seconds, effect.EffectId, slot);
                return;
            }
            if (!(effect.RepeatSeconds > 0))
            {
                _encounter.ApplyEnemyEffect(effect.EffectId, effect.Strength, effect.Seconds, slot, effect.TimerSlow);
                return;
            }
            var timer = _rules.Timer;
            if (_encounter.Tick - _lastRepeatTick[slot] < Ticks(timer.RepeatCooldownSeconds))
                return;
            _lastRepeatTick[slot] = _encounter.Tick;
            var applied = _encounter.RepeatRecent(slot, System.Math.Min(timer.RepeatMaxSeconds, effect.RepeatSeconds));
            AcceptedShares += applied;
            RepeatedShares += applied;
            _recentShares[slot] += applied;
        }

        private void ShareResource(int slot, double amount)
        {
            var buffs = _rules.Buffs;
            if (!(amount > 0) || double.IsInfinity(amount) || _encounter.Tick - _lastResourceTick[slot] < Ticks(buffs.AllyResourceCooldownSeconds))
                return;
            _lastResourceTick[slot] = _encounter.Tick;
            BroadcastExcept(slot, PartyCodec.Encode(_out, new PartyResourceMessage { Slot = (byte)slot, Amount = (float)System.Math.Min(buffs.AllyResourceMax, amount) }));
        }

        private void Relay(int slot, double dropped)
        {
            var bonus = _rules.Relay.BonusFor(dropped);
            if (!(bonus > 0) || _encounter.Tick - _lastRelayTick[slot] < Ticks(_rules.Relay.CooldownSeconds))
                return;
            var target = _encounter.RelayTarget(slot);
            if (target == PartyEncounter.NoSlot)
                return;
            _lastRelayTick[slot] = _encounter.Tick;
            Relays++;
            Broadcast(PartyCodec.Encode(_out, new FocusRelayMessage { From = (byte)slot, To = (byte)target, Focus = (float)bonus }));
        }

        private void RequestWorld(in WorldRequestMessage request)
        {
            if (request.Cancel)
            {
                _encounter.CancelWorld();
                return;
            }
            if (request.World == PartyWorld.Home)
            {
                _encounter.ProposeWorld(PartyWorld.Home, 0);
                return;
            }
            if (request.World == PartyWorld.Rift && _rift?.Schedule.WindowAt(ClockUnixMs) != null)
                _encounter.ProposeWorld(PartyWorld.Rift, ClockUnixMs / 1000);
        }

        private void ReturnWhenTheRiftCloses()
        {
            if (_encounter.World == PartyWorld.Rift && (_rift == null || ClockUnixMs >= _encounter.RiftId * 1000 + _rift.PartyDurationMs))
                _encounter.SwitchWorld(PartyWorld.Home, 0);
        }

        private void React(int slot, PartyReactionMessage reaction)
        {
            if (!_rules.Reactions.Knows(reaction.Reaction) || _encounter.Tick - _lastReactionTick[slot] < Ticks(_rules.Reactions.CooldownSeconds))
                return;
            _lastReactionTick[slot] = _encounter.Tick;
            reaction.Slot = (byte)slot;
            BroadcastExcept(slot, PartyCodec.Encode(_out, reaction));
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
            if (batch.Hits > _hitAllowance[slot])
                Flag(slot);
            _hitAllowance[slot] = System.Math.Max(0, _hitAllowance[slot] - System.Math.Max(0, batch.Hits));
            if (batch.Hits > 0)
            {
                _encounter.ReportInput(slot);
                _recentHits[slot] += batch.Hits;
            }
            if (!(delta > 0))
                return;
            var accepted = CappedDamage(slot, batch.EnemyId, delta);
            AcceptedShares += accepted;
            _recentShares[slot] += accepted;
        }

        private double CappedDamage(int slot, uint enemyId, double delta)
        {
            var barFull = _encounter.BarFull;
            var wanted = _encounter.Applicable(slot, enemyId, delta);
            var allowed = System.Math.Min(wanted, _allowance[slot] * barFull);
            if (wanted - allowed > CutTolerance * barFull)
            {
                CutShares += wanted - allowed;
                Flag(slot);
            }
            var applied = _encounter.ApplyDamage(slot, enemyId, delta, allowed);
            _allowance[slot] = System.Math.Max(0, _allowance[slot] - applied / barFull);
            return applied;
        }

        private void Flag(int slot) => _flags[slot]++;

        private void Refill()
        {
            var step = _rules.Caps.BarsPerSecond / GameLoop.TicksPerSecond;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                _allowance[slot] = System.Math.Min(_rules.Caps.BurstBars, _allowance[slot] + step);
                _hitAllowance[slot] = System.Math.Min(HitBurst, _hitAllowance[slot] + 1);
            }
        }

        private double HitBurst => _rules.Caps.HitBurstSeconds * GameLoop.TicksPerSecond;

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
                        MostReactionsOnOneEnemy = System.Math.Max(MostReactionsOnOneEnemy, BitCount(_enemyReactions));
                        Broadcast(PartyCodec.Encode(_out, EnemyKilledMessage.Of(partyEvent)));
                        break;
                    case PartyEventKind.GuestLeft:
                        _rosterChanged = true;
                        break;
                    case PartyEventKind.EnemySpawned:
                        _enemyReactions = ActiveReactions();
                        _ticksToState = 0;
                        break;
                    case PartyEventKind.WorldPending:
                    case PartyEventKind.WorldCancelled:
                    case PartyEventKind.WorldChanged:
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
                var state = EncounterStateMessage.Of(_encounter, ++_stateSequence);
                for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                {
                    state.SetRecent(slot, (float)_recentShares[slot]);
                    state.SetHits(slot, _recentHits[slot]);
                    _recentShares[slot] = 0;
                    _recentHits[slot] = 0;
                }
                Broadcast(PartyCodec.Encode(_out, state));
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

        private uint ActiveReactions()
        {
            var active = 0u;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (_encounter.Member(slot).Joined)
                    active |= _reactions[slot];
            }
            return active;
        }

        private static int BitCount(uint mask)
        {
            var count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
