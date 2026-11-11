using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Sim.Rift;

namespace Game.Core.Party
{
    public sealed class PartyClient : ITickHook
    {
        private const double HelloSeconds = 1;

        private readonly GameLoop _loop;
        private readonly IPartyTransport _transport;
        private readonly ulong _hostId;
        private readonly byte[] _in = new byte[PartyCodec.MaxPacketBytes];
        private readonly byte[] _out = new byte[PartyCodec.MaxPacketBytes];
        private PartyRules _rules;
        private EncounterStateMessage _state;
        private long _tick;
        private long _stateArrivalTick;
        private long _lastHeardTick;
        private long _lostAtTick;
        private long _joinStartTick;
        private bool _inRoster;
        private uint _localEnemyId;
        private uint _batchEnemyId;
        private uint _aheadOf;
        private uint _farmFirstId;
        private bool _guessed;
        private ulong _guessedSeed;
        private bool _killFlushed;
        private long _localStage;
        private BigNumber _unit;
        private double _total;
        private double _sentTotal;
        private double _sharesSinceState;
        private double _allyRatePerTick;
        private uint _sequence;
        private int _hitsInBatch;
        private uint _rewardedUpTo;
        private uint _countedUpTo;
        private uint _settledUpTo;
        private long _lastOwnInputTick = PartyEncounter.Never;
        private readonly double[] _contribution = new double[PartyRules.MaxSlots];
        private readonly int[] _reaction = { NoReaction, NoReaction, NoReaction, NoReaction };
        private readonly long[] _reactionTick = new long[PartyRules.MaxSlots];
        private readonly int[] _reactionSerial = new int[PartyRules.MaxSlots];
        private readonly double[] _allyShares = new double[PartyRules.MaxSlots];
        private readonly int[] _allyHits = new int[PartyRules.MaxSlots];
        private long _lastSentReactionTick = PartyEncounter.Never;
        private int _contributionKills;
        private const double KeysSeconds = 2;
        private const int DungeonResultTicks = 10;

        private readonly long[] _partyKeys = new long[PartyRules.MaxSlots * PartyKeysMessage.MaxKeys];
        private readonly byte[] _partyKeyCount = new byte[PartyRules.MaxSlots];
        private readonly int[] _keyPicks = new int[PartyKeysMessage.MaxKeys];
        private bool _keysNow;
        private long _dungeonId;
        private int _dungeonBossKills;
        private long _dungeonLeftTick;
        private int _ownSlot = PartyEncounter.NoSlot;
        private int _firstEvent;
        private bool _offerNow;
        private bool _loadoutNow;
        private PartyLoadoutMessage _sentLoadout;
        private readonly PartyLoadoutMessage[] _allyLoadouts = new PartyLoadoutMessage[PartyRules.MaxSlots];
        private long _refusedRift;
        private long _guestLinkTick;
        private int _auraBoundGem;
        private int _traitTimerTicks;
        private double _traitTimerSent;

        public const int NoReaction = -1;
        private const double MaxAllyShares = 1000;
        private const int MaxAllyHits = 1000;
        private const double AllyRateBlend = 0.5;
        private const int TraitTimerResendTicks = 10;
        private const double TraitTimerSeconds = 1.5;
        public static readonly int TraitTimerEffect = GemDef.NetIdOf("trait_boss_timer");

        public PartyClient(GameLoop loop, IPartyTransport transport, ulong hostId, PartyRules rules)
        {
            _loop = loop;
            _transport = transport;
            _hostId = hostId;
            _rules = rules;
            _lostAtTick = long.MinValue;
            loop.Hook = this;
        }

        public PartyLink Status { get; private set; }
        public bool Peaceful { get; set; }
        public long PartyRewards { get; private set; }
        public long UnpaidKills { get; private set; }
        public long MissedKills { get; private set; }
        public bool LastKillPaid { get; private set; } = true;
        public uint EnemyId => _state.EnemyId;
        public double BarFull => _state.BarFull;
        public double PredictedBarLeft => InParty ? Predicted() : 0;
        public bool RunningAhead => InParty && _guessed;
        public long EarlyKills { get; private set; }
        public RosterMessage Roster { get; private set; }
        public EnemyKilledMessage LastBossKill { get; private set; }
        public int BossKillsSeen { get; private set; }
        public double DamageTaken => System.Math.Max(1, _state.DamageTaken);
        public int OwnSlot => _ownSlot;
        public double SharesDealt { get; private set; }
        public FocusRelayMessage LastRelay { get; private set; }
        public bool IsGuest { get; private set; }
        public bool GuestFinished { get; private set; }
        public int RelaySerial { get; private set; }

        public int ReactionOf(int slot) =>
            slot >= 0 && slot < PartyRules.MaxSlots && _reaction[slot] != NoReaction && _tick - _reactionTick[slot] <= Ticks(_rules.Reactions.ShowSeconds) ? _reaction[slot] : NoReaction;

        public PartyReactionRules Reactions => _rules.Reactions;

        public int ReactionSerial(int slot) => slot >= 0 && slot < PartyRules.MaxSlots ? _reactionSerial[slot] : 0;

        public bool CanReact => InParty && _tick - _lastSentReactionTick >= Ticks(_rules.Reactions.CooldownSeconds);

        public bool SendReaction(int reaction)
        {
            if (!CanReact || !_rules.Reactions.Knows(reaction))
                return false;
            _lastSentReactionTick = _tick;
            SendToHost(PartyCodec.Encode(_out, new PartyReactionMessage { Reaction = (byte)reaction }));
            if (_ownSlot >= 0)
                ShowReaction(_ownSlot, reaction);
            return true;
        }

        public double Contribution(int slot) =>
            _contributionKills == 0 || slot < 0 || slot >= PartyRules.MaxSlots ? 0 : _contribution[slot] / _contributionKills;

        public int ContributionKills => _contributionKills;

        public BigNumber TakeAllyDamage(int slot)
        {
            if (slot < 0 || slot >= PartyRules.MaxSlots)
                return BigNumber.Zero;
            var share = _allyShares[slot];
            _allyShares[slot] = 0;
            return share > 0 && _unit.Sign > 0 ? _unit * share : BigNumber.Zero;
        }

        public int TakeAllyHits(int slot)
        {
            if (slot < 0 || slot >= PartyRules.MaxSlots)
                return 0;
            var hits = _allyHits[slot];
            _allyHits[slot] = 0;
            return hits;
        }

        public ShownEffect EnemyEffect(int index)
        {
            var effect = _state.Effect(index);
            if (!InParty || !effect.Active || _state.EnemyId != _localEnemyId)
                return default;
            effect.TicksLeft = (int)System.Math.Max(0, effect.TicksLeft - (_tick - _stateArrivalTick));
            return effect;
        }

        public bool InParty => Status == PartyLink.Linked || Status == PartyLink.NoSignal;

        public PartyWorld World => _state.World;
        public bool IsLeader { get; set; }
        public long RiftId => _state.World == PartyWorld.Rift ? _state.RiftId : 0;
        public long RiftEndUnixMs => _state.World == PartyWorld.Rift ? _loop.Rift.PartyRiftEnd(_state.RiftId) : 0;
        public bool WorldSwitching => InParty && _state.Switching;
        public PartyWorld PendingWorld => _state.Switching ? (PartyWorld)_state.PendingWorld : _state.World;
        public double WorldSwitchSecondsLeft =>
            WorldSwitching ? System.Math.Max(0, _state.SwitchTicksLeft - (_tick - _stateArrivalTick)) * GameLoop.TickSeconds : 0;

        public bool ProposeWorld(PartyWorld world, long nowUnixMs)
        {
            if (!InParty || _state.Switching || world == _state.World)
                return false;
            long riftId = 0;
            if (world == PartyWorld.Rift)
            {
                var open = _loop.Rift.WindowAt(nowUnixMs);
                if (open == null)
                    return false;
                riftId = open.Value.Id;
            }
            SendToHost(PartyCodec.Encode(_out, new WorldRequestMessage { World = world, RiftId = riftId }));
            return true;
        }

        public bool RiftOpen(long nowUnixMs) => _loop.Rift.WindowAt(nowUnixMs) != null;

        public bool CanLaunchDungeon => InParty && !IsGuest && IsLeader && _state.World == PartyWorld.Home && !_state.Switching;

        public long DungeonId => _state.World == PartyWorld.Dungeon ? _state.RiftId : 0;

        public int PartyKeyCount(int slot) => slot >= 0 && slot < PartyRules.MaxSlots ? _partyKeyCount[slot] : 0;

        public long PartyKey(int slot, int index) =>
            index >= 0 && index < PartyKeyCount(slot) ? _partyKeys[slot * PartyKeysMessage.MaxKeys + index] : 0;

        public bool ProposeDungeon(long partyKey)
        {
            if (!CanLaunchDungeon || partyKey <= 0 || !HoldsPartyKey(partyKey))
                return false;
            SendToHost(PartyCodec.Encode(_out, new WorldRequestMessage { World = PartyWorld.Dungeon, RiftId = partyKey }));
            return true;
        }

        private bool HoldsPartyKey(long partyKey)
        {
            var slot = PartyDungeonId.Owner(partyKey);
            for (var i = 0; i < PartyKeyCount(slot); i++)
            {
                if (PartyKey(slot, i) == partyKey)
                    return true;
            }
            return false;
        }

        public bool CanCallBoss => InParty && !IsGuest && IsLeader && _state.Farming && _state.EnemyKind != PartyEnemyKind.Boss && !_state.Switching;

        public bool RequestBossFight()
        {
            if (!CanCallBoss)
                return false;
            SendToHost(PartyCodec.EncodeBossRequest(_out));
            return true;
        }

        public bool CancelWorldSwitch()
        {
            if (!WorldSwitching)
                return false;
            SendToHost(PartyCodec.Encode(_out, new WorldRequestMessage { World = _state.World, Cancel = true, RiftId = _state.RiftId }));
            return true;
        }

        public void UseRules(PartyRules rules) => _rules = rules;

        public void Join()
        {
            if (Status != PartyLink.Solo && Status != PartyLink.Away)
                return;
            _lostAtTick = _tick;
            _lastHeardTick = _tick;
            StartJoining();
        }

        public void JoinAsGuest()
        {
            if (Status != PartyLink.Solo && Status != PartyLink.Away)
                return;
            IsGuest = true;
            GuestFinished = false;
            Join();
        }

        public void Leave()
        {
            Unlink();
            Status = PartyLink.Solo;
            _lostAtTick = long.MinValue;
            IsGuest = false;
        }

        private void EndGuest()
        {
            Leave();
            GuestFinished = true;
        }

        public void Rejoin()
        {
            Unlink();
            _state = default;
            _stateArrivalTick = _tick;
            ForgetLocalEnemy();
            _rewardedUpTo = 0;
            _countedUpTo = 0;
            _contributionKills = 0;
            System.Array.Clear(_contribution, 0, _contribution.Length);
            for (var slot = 0; slot < _reaction.Length; slot++)
            {
                _reaction[slot] = NoReaction;
                _allyShares[slot] = 0;
                _allyHits[slot] = 0;
            }
            LastBossKill = default;
            _total = 0;
            _sentTotal = 0;
            _sharesSinceState = 0;
            _allyRatePerTick = 0;
            _hitsInBatch = 0;
            Roster = default;
            _ownSlot = PartyEncounter.NoSlot;
            _lostAtTick = _tick;
            _lastHeardTick = _tick;
            StartJoining();
        }

        public void BeforeTick()
        {
            _firstEvent = _loop.Events.Count;
            while (_transport.TryReceive(_in, out var length, out var from))
            {
                if (from == _hostId)
                    Receive(length);
            }
            if (Status == PartyLink.NoSignal && _lastHeardTick == _tick)
                Status = PartyLink.Linked;
            if (Status == PartyLink.Joining && _inRoster && _state.EnemyId != 0 && _stateArrivalTick >= _joinStartTick)
                Link();
            if (InParty)
            {
                FollowWorld();
                SyncEnemy();
                ShareAllies();
            }
        }

        public void AfterTick()
        {
            _tick++;
            if (InParty)
            {
                CountDamage();
                WatchCasts();
                EndAuraBoundDebuff();
                ShareTraitTimer();
                ShareTraitResource();
                ShareTraitEffects();
            }
            PassDroppedFocus();
            WatchSignal();
            Send();
        }

        private void Receive(int length)
        {
            if (Status == PartyLink.Away || (Status == PartyLink.Solo && _lostAtTick == long.MinValue))
                return;
            _lastHeardTick = _tick;
            if (Status == PartyLink.Solo)
                StartJoining();
            switch (PartyCodec.KindOf(_in, length))
            {
                case PartyMessageKind.Roster when PartyCodec.TryDecode(_in, length, out RosterMessage roster):
                    OnRoster(roster);
                    break;
                case PartyMessageKind.PartyOffer when PartyCodec.TryDecode(_in, length, out PartyOfferMessage offer):
                    if (InParty)
                        OnOffer(offer);
                    break;
                case PartyMessageKind.PartyKeys when PartyCodec.TryDecode(_in, length, out PartyKeysMessage keys):
                    if (InParty && keys.Slot != _ownSlot)
                        StoreKeys(keys);
                    break;
                case PartyMessageKind.PartyReaction when PartyCodec.TryDecode(_in, length, out PartyReactionMessage reaction):
                    if (InParty && reaction.Slot < PartyRules.MaxSlots && reaction.Slot != _ownSlot && _rules.Reactions.Knows(reaction.Reaction))
                        ShowReaction(reaction.Slot, reaction.Reaction);
                    break;
                case PartyMessageKind.PartyLoadout when PartyCodec.TryDecode(_in, length, out PartyLoadoutMessage loadout):
                    if (InParty && loadout.Slot < PartyRules.MaxSlots && loadout.Slot != _ownSlot)
                        _allyLoadouts[loadout.Slot] = loadout;
                    break;
                case PartyMessageKind.PartyResource when PartyCodec.TryDecode(_in, length, out PartyResourceMessage resource):
                    if (InParty && resource.Slot != _ownSlot)
                        _loop.Resource.GiftFromAlly(System.Math.Min(resource.Amount, _rules.Buffs.AllyResourceMax));
                    break;
                case PartyMessageKind.FocusRelay when PartyCodec.TryDecode(_in, length, out FocusRelayMessage relay):
                    if (InParty)
                        OnRelay(relay);
                    break;
                case PartyMessageKind.EncounterState when PartyCodec.TryDecode(_in, length, out EncounterStateMessage state):
                    if (state.Sequence > _state.Sequence)
                        OnState(state);
                    break;
                case PartyMessageKind.EnemyKilled when PartyCodec.TryDecode(_in, length, out EnemyKilledMessage killed):
                    if (InParty)
                        OnKilled(killed);
                    break;
                case PartyMessageKind.EncounterMoment when PartyCodec.TryDecode(_in, length, out EncounterMomentMessage moment):
                    if (InParty && moment.Kind == PartyEventKind.BossFailed && moment.EnemyId == _localEnemyId)
                        EndLocalEnemy(true);
                    break;
            }
        }

        private void ShowReaction(int slot, int reaction)
        {
            _reaction[slot] = reaction;
            _reactionTick[slot] = _tick;
            _reactionSerial[slot]++;
        }

        private void OnRelay(in FocusRelayMessage relay)
        {
            if (relay.From >= PartyRules.MaxSlots || relay.To >= PartyRules.MaxSlots || relay.From == relay.To)
                return;
            var rules = _rules.Relay;
            if (relay.To == _ownSlot)
                _loop.Focus.Receive(System.Math.Min(relay.Focus, rules.MaxBonus), rules.MaxBonus, rules.Seconds);
            LastRelay = relay;
            RelaySerial++;
        }

        private void OnRoster(in RosterMessage roster)
        {
            if (roster.Version != Roster.Version)
            {
                _loadoutNow = true;
                _keysNow = true;
            }
            Roster = roster;
            _ownSlot = roster.SlotOf(_transport.LocalId);
            _inRoster = _ownSlot != PartyEncounter.NoSlot;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot == _ownSlot || roster.Player(slot) == 0 || roster.Presence(slot) == PartyPresence.Offline)
                {
                    _loop.PartyBuffs.ClearAlly(slot);
                    _allyLoadouts[slot] = default;
                }
            }
        }

        private void OnOffer(in PartyOfferMessage offer)
        {
            if (offer.Slot >= PartyRules.MaxSlots || offer.Slot == _ownSlot)
                return;
            var ally = _loop.PartyBuffs.Ally(offer.Slot);
            ally.Present = true;
            for (var i = 0; i < ally.Stats.Length; i++)
                ally.Stats[i] = System.Math.Max(0, offer.Stat(i));
            ally.HeroClass = Roster.HeroClass(offer.Slot);
            System.Array.Clear(ally.Coop, 0, ally.Coop.Length);
            for (var i = 0; i < offer.CoopCount; i++)
            {
                var coop = offer.Coop(i);
                if (coop.Stat < ally.Coop.Length && coop.Value > 0)
                    ally.Coop[coop.Stat] = coop.Value;
            }
            var gems = _loop.Data.Gems;
            for (var i = 0; i < ally.Auras.Length; i++)
            {
                var offered = offer.Aura(i);
                var aura = ally.Auras[i];
                aura.Clear();
                if (!offered.Active || !gems.TryGetByNetId(offered.GemNetId, out var gem) || !PartyBuffs.Shared(gem))
                    continue;
                aura.Gem = gem;
                aura.Strength = System.Math.Max(0, offered.Strength);
                aura.TicksLeft = offered.TicksLeft;
            }
            _loop.PartyBuffs.MarkChanged();
        }

        private void OnState(in EncounterStateMessage state)
        {
            if (InParty && _state.EnemyId != 0 && (state.World != _state.World || state.RiftId != _state.RiftId))
                AbandonLocalEnemy();
            if (state.EnemyKind == PartyEnemyKind.Boss)
                _loop.Stages.LinkedGateBoss = state.GateBoss;
            NoteFarming(state);
            if (InParty)
                SettleHistory(state);
            var elapsed = System.Math.Max(1, _tick - _stateArrivalTick);
            _allyRatePerTick += (AllyRecent(state) / elapsed - _allyRatePerTick) * AllyRateBlend;
            if (InParty && state.EnemyId > _localEnemyId && _localEnemyId != 0)
                EndLocalEnemy(_loop.State.FightingBoss && state.Farming);
            _state = state;
            _stateArrivalTick = _tick;
            _sharesSinceState = 0;
            if (_guessed && state.EnemyId >= _localEnemyId)
                MeetGuess();
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot == _ownSlot || !InParty)
                    continue;
                _allyShares[slot] = System.Math.Min(_allyShares[slot] + state.Recent(slot), MaxAllyShares);
                _allyHits[slot] = System.Math.Min(_allyHits[slot] + state.Hits(slot), MaxAllyHits);
            }
        }

        private void NoteFarming(in EncounterStateMessage state)
        {
            if (!state.Farming)
                _farmFirstId = 0;
            else if (!_state.Farming)
                _farmFirstId = InParty && _state.EnemyKind == PartyEnemyKind.Boss ? _state.EnemyId + 1 : 0;
        }

        private void MeetGuess()
        {
            _guessed = false;
            if (_state.EnemyId != _localEnemyId || _localEnemyId <= _rewardedUpTo)
                return;
            if (_state.EnemyKind != PartyEnemyKind.Normal || _state.EnemySeed != _guessedSeed)
            {
                SpawnLocal(_state.EnemyId, _state.EnemyKind == PartyEnemyKind.Boss, _state.EnemySeed, _state.BarFull, _state.KillsOnStage, _state.Farming, _state.BossTicksLeft);
                return;
            }
            Relabel(_localEnemyId);
            _loop.Stages.MarkLinkedWeakened(_state.Weakened, _state.WeakenedGold);
        }

        private double AllyRecent(in EncounterStateMessage state)
        {
            var sum = 0.0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot != _ownSlot)
                    sum += System.Math.Max(0, state.Recent(slot));
            }
            return sum;
        }

        private void SettleHistory(in EncounterStateMessage state)
        {
            var first = state.EnemyId > EnemyHistory.Depth ? state.EnemyId - EnemyHistory.Depth : 1;
            for (var id = System.Math.Max(_rewardedUpTo + 1, first); id < state.EnemyId; id++)
                SettleFromHistory(id, state.History, (int)(state.EnemyId - 1 - id));
        }

        private void SettleFromHistory(uint enemyId, in EnemyHistory history, int age)
        {
            var killed = history.KilledAt(age);
            var boss = history.BossAt(age);
            if (enemyId == _aheadOf)
            {
                SettleAhead(killed && history.RewardedFor(age, _ownSlot), killed);
                return;
            }
            if (enemyId != _localEnemyId)
            {
                if (!killed)
                {
                    _rewardedUpTo = enemyId;
                    Settle(enemyId);
                    return;
                }
                SpawnLocal(enemyId, boss, enemyId, _state.BarFull, _state.KillsOnStage, _state.Farming, 0);
            }
            if (killed)
                EndLocalEnemy(false, history.RewardedFor(age, _ownSlot));
            else if (boss)
                EndLocalEnemy(true);
            else
                DropLocalEnemy();
        }

        private void DropLocalEnemy()
        {
            _rewardedUpTo = _localEnemyId;
            Settle(_localEnemyId);
            _loop.Stages.SkipLinkedKill();
        }

        private void AbandonLocalEnemy()
        {
            if (_localEnemyId > _rewardedUpTo)
                _rewardedUpTo = _localEnemyId;
            if (_localEnemyId > _settledUpTo)
                _settledUpTo = _localEnemyId;
            _loop.Stages.SettleOwedLinkedKill(false);
            ForgetLocalEnemy();
            _hitsInBatch = 0;
        }

        private void ForgetLocalEnemy()
        {
            _localEnemyId = 0;
            _batchEnemyId = 0;
            _aheadOf = 0;
            _guessed = false;
            _total = 0;
            _sentTotal = 0;
        }

        private void FollowWorld()
        {
            var rift = _loop.Rift;
            if (_state.EnemyId == 0 || IsGuest)
                return;
            FollowDungeon();
            if (_state.World == PartyWorld.Dungeon)
            {
                if (rift.Inside)
                    rift.Leave();
                return;
            }
            if (_state.World == PartyWorld.Rift)
            {
                if ((!rift.Inside || _loop.State.Rift.WindowId != _state.RiftId) && _refusedRift != _state.RiftId
                    && rift.EnterPartyRift(_state.RiftId) != RiftEntry.Entered)
                    _refusedRift = _state.RiftId;
            }
            else if (rift.Inside)
            {
                rift.Leave();
            }
        }

        private void FollowDungeon()
        {
            var dungeons = _loop.Dungeons;
            if (_state.World == PartyWorld.Dungeon)
            {
                if (_dungeonId == _state.RiftId)
                    return;
                _dungeonId = _state.RiftId;
                _dungeonBossKills = BossKillsSeen;
                _dungeonLeftTick = 0;
                if (_loop.Rift.Inside)
                    _loop.Rift.Leave();
                dungeons.EnterParty(_dungeonId, PartyDungeonId.Owner(_dungeonId) == _ownSlot);
                return;
            }
            if (_dungeonId == 0)
                return;
            var completed = BossKillsSeen - _dungeonBossKills >= PartyDungeonId.Stages(_dungeonId);
            if (!completed && _dungeonLeftTick == 0)
            {
                _dungeonLeftTick = _tick;
                return;
            }
            if (!completed && _tick - _dungeonLeftTick < DungeonResultTicks)
                return;
            dungeons.LeaveParty(completed);
            _dungeonId = 0;
            _dungeonLeftTick = 0;
        }

        private void SendKeys()
        {
            var dungeons = _loop.Dungeons;
            var keys = dungeons.Keys;
            var message = new PartyKeysMessage { Slot = (byte)System.Math.Max(0, _ownSlot) };
            for (var i = 0; i < PartyKeysMessage.MaxKeys; i++)
                _keyPicks[i] = -1;
            for (var pick = 0; pick < PartyKeysMessage.MaxKeys; pick++)
            {
                var best = -1;
                for (var i = 0; i < keys.Count; i++)
                {
                    if (!dungeons.PartyReady(keys[i]) || Picked(i, pick))
                        continue;
                    if (best < 0 || keys[i].Tier > keys[best].Tier || (keys[i].Tier == keys[best].Tier && dungeons.StageOf(keys[i]) > dungeons.StageOf(keys[best])))
                        best = i;
                }
                if (best < 0)
                    break;
                _keyPicks[pick] = best;
                message.SetKey(pick, dungeons.PartyId(keys[best], _ownSlot));
                message.Count++;
            }
            var changed = KeysChanged(message);
            if (!changed && (!_keysNow || message.Count == 0))
                return;
            _keysNow = false;
            StoreKeys(message);
            SendToHost(PartyCodec.Encode(_out, message));
        }

        private bool KeysChanged(in PartyKeysMessage message)
        {
            if (_partyKeyCount[message.Slot] != message.Count)
                return true;
            for (var i = 0; i < message.Count; i++)
            {
                if (_partyKeys[message.Slot * PartyKeysMessage.MaxKeys + i] != message.Key(i))
                    return true;
            }
            return false;
        }

        private bool Picked(int index, int picked)
        {
            for (var i = 0; i < picked; i++)
            {
                if (_keyPicks[i] == index)
                    return true;
            }
            return false;
        }

        private void StoreKeys(in PartyKeysMessage message)
        {
            if (message.Slot >= PartyRules.MaxSlots)
                return;
            _partyKeyCount[message.Slot] = message.Count;
            for (var i = 0; i < PartyKeysMessage.MaxKeys; i++)
                _partyKeys[message.Slot * PartyKeysMessage.MaxKeys + i] = i < message.Count ? message.Key(i) : 0;
        }

        private void OnKilled(in EnemyKilledMessage killed)
        {
            CountContribution(killed);
            if (killed.EnemyKind == PartyEnemyKind.Boss && killed.EnemyId > LastBossKill.EnemyId)
            {
                LastBossKill = killed;
                BossKillsSeen++;
            }
            if (killed.EnemyId == _aheadOf)
            {
                SettleAhead(killed.RewardedFor(_ownSlot), true);
                return;
            }
            if (killed.EnemyId != _rewardedUpTo + 1)
                return;
            if (killed.EnemyId != _localEnemyId)
                SpawnLocal(killed.EnemyId, killed.EnemyKind == PartyEnemyKind.Boss, killed.EnemyId, _state.BarFull, _state.KillsOnStage, _state.Farming, 0);
            EndLocalEnemy(false, killed.RewardedFor(_ownSlot));
        }

        private void CountContribution(in EnemyKilledMessage killed)
        {
            if (killed.EnemyId <= _countedUpTo)
                return;
            _countedUpTo = killed.EnemyId;
            _contributionKills++;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                _contribution[slot] += killed.Contribution(slot);
        }

        private void EndLocalEnemy(bool bossFailed) => EndLocalEnemy(bossFailed, LocallyPaid());

        private void EndLocalEnemy(bool bossFailed, bool paid)
        {
            if (_localEnemyId <= _rewardedUpTo)
                return;
            if (_aheadOf != 0)
                SettleAhead(true, true);
            _guessed = false;
            _rewardedUpTo = _localEnemyId;
            Settle(_localEnemyId);
            if (IsGuest)
            {
                _loop.Stages.SkipLinkedKill();
                return;
            }
            if (bossFailed)
            {
                _loop.Stages.ConfirmLinkedBossFailed();
                return;
            }
            LastKillPaid = paid;
            if (!paid)
            {
                _loop.Stages.SkipLinkedKill();
                UnpaidKills++;
                return;
            }
            _loop.Stages.ConfirmLinkedKill();
            PartyRewards++;
        }

        private void SettleAhead(bool paid, bool killed)
        {
            var enemyId = _aheadOf;
            _aheadOf = 0;
            if (enemyId > _rewardedUpTo)
                _rewardedUpTo = enemyId;
            Settle(enemyId);
            _loop.Stages.SettleOwedLinkedKill(paid);
            if (!killed)
                return;
            LastKillPaid = paid;
            if (paid)
                PartyRewards++;
            else
                UnpaidKills++;
        }

        private void Settle(uint enemyId)
        {
            if (enemyId > _settledUpTo + 1)
                MissedKills += enemyId - _settledUpTo - 1;
            if (enemyId > _settledUpTo)
                _settledUpTo = enemyId;
        }

        private bool LocallyPaid() =>
            _tick - _lastOwnInputTick <= Ticks(_rules.Contribution.ActiveInputSeconds) || (_ownSlot >= 0 && Roster.RestBankSeconds(_ownSlot) > 0);

        private void StartJoining()
        {
            Status = PartyLink.Joining;
            _joinStartTick = _tick;
            _inRoster = false;
        }

        private void Link()
        {
            Status = PartyLink.Linked;
            _auraBoundGem = 0;
            _guestLinkTick = _tick;
            _loop.Stages.Link(IsGuest);
            _loop.Skills.ShareEnemy(true);
            _offerNow = true;
            _loadoutNow = true;
            _keysNow = true;
            if (_state.EnemyId > 0 && _state.EnemyId - 1 > _rewardedUpTo)
                _rewardedUpTo = _state.EnemyId - 1;
            _settledUpTo = _rewardedUpTo;
            ForgetLocalEnemy();
            _farmFirstId = 0;
            _lastHeardTick = _tick;
            _countedUpTo = _state.EnemyId > 0 ? _state.EnemyId - 1 : 0;
            _contributionKills = 0;
            System.Array.Clear(_contribution, 0, _contribution.Length);
            LastKillPaid = true;
        }

        public PartyLoadoutMessage AllyLoadout(int slot) =>
            slot >= 0 && slot < _allyLoadouts.Length && slot != _ownSlot ? _allyLoadouts[slot] : default;

        private void Unlink()
        {
            System.Array.Clear(_allyLoadouts, 0, _allyLoadouts.Length);
            System.Array.Clear(_partyKeys, 0, _partyKeys.Length);
            System.Array.Clear(_partyKeyCount, 0, _partyKeyCount.Length);
            _dungeonId = 0;
            _dungeonLeftTick = 0;
            _loop.State.PartyAllies = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
                _loop.Gear.SetAllyEcho(slot, 0);
            _loop.Stages.SettleOwedLinkedKill(true);
            _loop.Stages.Unlink();
            _loop.Skills.ShareEnemy(false);
            _loop.PartyBuffs.ClearAllies();
            _loop.Synergy.ClearAllies();
        }

        private void GoSolo()
        {
            Unlink();
            Status = PartyLink.Solo;
            _lostAtTick = _tick;
            ForgetLocalEnemy();
        }

        private void SyncEnemy()
        {
            var gameState = _loop.State;
            var fresh = _state.EnemyId > _rewardedUpTo;
            if (fresh && !_guessed && (_localEnemyId != _state.EnemyId || gameState.Stage != _localStage || !gameState.EnemyAlive))
            {
                var newEnemy = _localEnemyId != _state.EnemyId;
                SpawnLocal(_state.EnemyId, _state.EnemyKind == PartyEnemyKind.Boss, _state.EnemySeed, _state.BarFull, _state.KillsOnStage, _state.Farming, _state.BossTicksLeft);
                if (newEnemy)
                    HitWithChain();
            }
            if (_localEnemyId == 0 || _localEnemyId <= _rewardedUpTo)
                return;
            gameState.EnemyHp = _unit * System.Math.Max(Predicted(), StageSystem.StaggerHpShare);
            gameState.EnemyShieldFloor = _state.EnemyId == _localEnemyId && _state.ShieldFloor > 0 ? _unit * _state.ShieldFloor : BigNumber.Zero;
            gameState.PartyEnemiesOnStage = (int)System.Math.Min(int.MaxValue, _state.EnemiesOnStage);
            if (!gameState.FightingBoss)
                return;
            gameState.BossTimerRate = _state.BossTimerRate;
            gameState.BossTicksLeft = (int)System.Math.Max(0, _state.BossTicksLeft - System.Math.Floor((_tick - _stateArrivalTick) * (double)_state.BossTimerRate));
        }

        private void SpawnLocal(uint enemyId, bool boss, ulong seed, double barFull, int killsOnStage, bool farming, int bossTicksLeft)
        {
            if (_localEnemyId != enemyId)
                _killFlushed = false;
            Relabel(enemyId);
            _guessed = false;
            _localEnemyId = enemyId;
            _localStage = _loop.State.Stage;
            var weakened = _state.EnemyId == enemyId && _state.Weakened;
            _unit = _loop.Stages.SpawnLinked(boss, seed, barFull, killsOnStage, farming, bossTicksLeft, weakened, weakened ? _state.WeakenedGold : 0, _state.WaveStage);
        }

        private void Relabel(uint enemyId)
        {
            if (_batchEnemyId == enemyId)
                return;
            FlushBatch();
            _batchEnemyId = enemyId;
            _total = 0;
            _sentTotal = 0;
        }

        private bool CanRunAhead =>
            !IsGuest && !_guessed && _aheadOf == 0 && _localEnemyId != 0 && _localEnemyId == _state.EnemyId && _localEnemyId > _rewardedUpTo
            && _state.EnemyKind == PartyEnemyKind.Normal && !_state.Switching && !_loop.State.FightingBoss && _loop.State.Stage == _localStage
            && NextIsNormal && LocallyPaid();

        private bool NextIsNormal => _state.Farming
            ? _farmFirstId != 0 && _state.EnemyId >= _farmFirstId && _state.EnemyId - _farmFirstId + 1 < _loop.Data.Stages.BossRetryAfterKills
            : _state.KillsOnStage < byte.MaxValue && _state.KillsOnStage + 1 < _state.EnemiesOnStage;

        private void RunAhead()
        {
            FlushBatch();
            _aheadOf = _localEnemyId;
            _guessed = true;
            _guessedSeed = SplitMix.After(_state.EnemySeed);
            _killFlushed = false;
            EarlyKills++;
            _loop.Stages.EndLinkedEnemyOwed();
            _localEnemyId++;
            var killsOnStage = _state.Farming ? _state.KillsOnStage : _state.KillsOnStage + 1;
            _unit = _loop.Stages.SpawnLinked(false, _guessedSeed, _state.BarFull, killsOnStage, _state.Farming, 0, _state.Weakened, _state.Weakened ? _state.WeakenedGold : 0, _state.WaveStage);
            HitWithChain();
        }

        private double BarLeftAtHost() => _state.BarLeft - _sharesSinceState - _allyRatePerTick * (_tick - _stateArrivalTick);

        private void HitWithChain()
        {
            var damage = _loop.Stages.TakeLinkedChainDamage();
            if (damage.Sign <= 0 || _unit.Sign <= 0)
                return;
            var share = (damage / _unit).ToDouble();
            if (!(share > 0))
                return;
            _total += share;
            _sharesSinceState += share * System.Math.Max(1, _state.DamageTaken);
            SharesDealt += share * System.Math.Max(1, _state.DamageTaken);
        }

        private double Predicted()
        {
            var predicted = BarLeftAtHost();
            if (_guessed && _localEnemyId > _state.EnemyId)
                predicted += _state.BarFull;
            return System.Math.Min(_state.BarFull, System.Math.Max(0, predicted));
        }

        private void CountDamage()
        {
            if (_localEnemyId == 0 || _unit.Sign <= 0)
                return;
            var share = (_loop.Combat.LastTickDamage / _unit).ToDouble();
            if (share > 0)
            {
                _total += share;
                _sharesSinceState += share * System.Math.Max(1, _state.DamageTaken);
                SharesDealt += share * System.Math.Max(1, _state.DamageTaken);
            }
            if (_loop.LastInput.HasAnyInput && !_loop.Focus.MacroDetected)
            {
                _hitsInBatch++;
                _lastOwnInputTick = _tick;
            }
            if (CanRunAhead && BarLeftAtHost() <= 0)
            {
                RunAhead();
            }
            else if (!_killFlushed && InParty && share > 0 && Predicted() <= 0)
            {
                _killFlushed = true;
                FlushBatch();
            }
        }

        private void ShareAllies()
        {
            var synergy = _loop.Synergy;
            var allies = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var present = slot != _ownSlot && Roster.Player(slot) != 0 && Roster.Presence(slot) != PartyPresence.Offline;
                _loop.Gear.SetAllyEcho(slot, present ? Roster.EchoLines(slot) : 0);
                if (!present)
                {
                    synergy.SetAlly(slot, false, default);
                    continue;
                }
                synergy.SetAlly(slot, true, (HeroClass)Roster.HeroClass(slot), Roster.Presence(slot) == PartyPresence.Fighting);
                allies++;
            }
            _loop.State.PartyAllies = allies;
            synergy.OwnFighting = _ownSlot < 0 || Roster.Presence(_ownSlot) == PartyPresence.Fighting;
        }

        private void WatchCasts()
        {
            var events = _loop.Events;
            for (var i = _firstEvent; i < events.Count; i++)
            {
                var gameEvent = events[i];
                var effect = gameEvent.Gem?.Skill?.Effect;
                if (gameEvent.Kind == GameEventKind.AuraChanged && effect is AuraEffect aura && aura.Party)
                    _offerNow = true;
                if (gameEvent.Kind == GameEventKind.SkillCast && effect is AuraEffect { AllyResource: > 0 } gift)
                    SendToHost(PartyCodec.Encode(_out, new PartyResourceMessage { Amount = (float)gift.AllyResource }));
                if (gameEvent.Kind != GameEventKind.SkillCast || gameEvent.Gem?.Skill == null)
                    continue;
                ShareCurse(gameEvent.Gem);
            }
        }

        private void ShareTraitTimer()
        {
            var slow = System.Math.Max(-PartyEncounter.MaxTimerPenalty, System.Math.Min(1, _loop.State.TraitPartyTimerSlow));
            if (_traitTimerSent != 0 && (System.Math.Sign(slow) != System.Math.Sign(_traitTimerSent) || System.Math.Abs(slow) < System.Math.Abs(_traitTimerSent)))
                EndTraitTimer();
            if (slow == 0 || _traitTimerTicks-- > 0)
                return;
            _traitTimerTicks = TraitTimerResendTicks - 1;
            _traitTimerSent = slow;
            SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
            {
                EffectId = TraitTimerEffect,
                OnEnemy = true,
                Seconds = TraitTimerSeconds,
                TimerSlow = slow
            }));
        }

        private void EndTraitTimer()
        {
            SendToHost(PartyCodec.Encode(_out, new CastEffectMessage { EffectId = TraitTimerEffect, OnEnemy = true, End = true }));
            _traitTimerSent = 0;
            _traitTimerTicks = 0;
            _traitTimerTicks = 0;
        }

        private void ShareTraitEffects()
        {
            var effects = _loop.State.TraitEnemyEffects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                {
                    EffectId = effect.Id,
                    OnEnemy = true,
                    Strength = effect.Strength,
                    Seconds = effect.Seconds,
                    TimerSlow = effect.TimerSlow,
                    RepeatSeconds = effect.RepeatSeconds
                }));
            }
            effects.Clear();
        }

        private void ShareTraitResource()
        {
            var gift = _loop.State.TraitPartyResourceGift;
            if (!(gift > 0))
                return;
            _loop.State.TraitPartyResourceGift = 0;
            SendToHost(PartyCodec.Encode(_out, new PartyResourceMessage { Amount = (float)gift }));
        }

        private void EndAuraBoundDebuff()
        {
            if (_auraBoundGem == 0)
                return;
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                if (skills[i].Active && skills[i].Skill.NetId == _auraBoundGem)
                    return;
            }
            SendToHost(PartyCodec.Encode(_out, new CastEffectMessage { EffectId = _auraBoundGem, OnEnemy = true, End = true }));
            _auraBoundGem = 0;
        }

        private void PassDroppedFocus()
        {
            var dropped = _loop.State.FocusDropped;
            if (!(dropped > 0))
                return;
            _loop.State.FocusDropped = 0;
            if (Status == PartyLink.Linked && !IsGuest)
                SendToHost(PartyCodec.Encode(_out, FocusRelayMessage.Dropped(dropped)));
        }

        private void ShareCurse(GemDef gem)
        {
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                if (!ReferenceEquals(skills[i].Skill, gem))
                    continue;
                var supports = skills[i].Supports;
                for (var s = 0; s < supports.Length; s++)
                {
                    var support = supports[s];
                    if (support?.Support == null || !(support.Support.VulnerabilityShare > 0))
                        continue;
                    SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                    {
                        EffectId = support.NetId,
                        OnEnemy = true,
                        Strength = support.Support.VulnerabilityShare,
                        Seconds = support.Support.VulnerabilitySeconds
                    }));
                }
                return;
            }
        }

        private double WeakenGold(GemDef gem)
        {
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                if (ReferenceEquals(skills[i].Skill, gem) && _loop.Skills.IsNative(i))
                    return gem.Skill!.Native.WeakenedGold;
            }
            return 0;
        }

        private void WatchSignal()
        {
            if (IsGuest && (Status == PartyLink.Linked || Status == PartyLink.NoSignal) && _tick - _guestLinkTick >= Ticks(_rules.Help.Seconds))
            {
                EndGuest();
                return;
            }
            if (IsGuest && Status == PartyLink.Solo && _lostAtTick != long.MinValue)
            {
                EndGuest();
                return;
            }
            var silent = _tick - _lastHeardTick;
            if (Status == PartyLink.Linked && silent > Ticks(_rules.SilenceSeconds))
                Status = PartyLink.NoSignal;
            else if (Status == PartyLink.NoSignal && silent > Ticks(_rules.DisconnectGraceSeconds))
                GoSolo();
            else if (Status == PartyLink.Joining && _lostAtTick != long.MinValue && silent > Ticks(_rules.DisconnectGraceSeconds))
                Status = PartyLink.Solo;
            if (Status == PartyLink.Solo && _lostAtTick != long.MinValue && _tick - _lostAtTick > Ticks(_rules.RejoinWindowSeconds))
                Status = PartyLink.Away;
        }

        private void Send()
        {
            if (Status == PartyLink.Joining && Every(HelloSeconds))
                SendToHost(PartyCodec.Encode(_out, new HelloMessage { HeroClass = (byte)_loop.State.Class, Stage = _loop.State.Stage, EchoLines = _loop.Gear.EchoMask }));
            if (InParty && Every(_rules.DamageBatchSeconds))
                FlushBatch();
            if (InParty && (_offerNow || Every(_rules.Buffs.OfferSeconds)))
                SendOffer();
            if (InParty)
                SendLoadout();
            if (InParty && !IsGuest && _ownSlot >= 0 && Every(KeysSeconds))
                SendKeys();
            if ((InParty || Status == PartyLink.Joining) && Every(_rules.PresenceSeconds))
                SendToHost(PartyCodec.Encode(_out, new PresenceMessage
                {
                    Presence = Peaceful || _loop.Peaceful.InCamp ? PartyPresence.Peaceful : PartyPresence.Fighting,
                    Stage = _loop.State.Stage,
                    Resource = (float)_loop.State.Resource,
                    Contribution = (float)System.Math.Max(0, _loop.Hero.Value(CombatStats.PartyContribution, 0)),
                    PierceShare = (float)Positive(CombatStats.PierceShare),
                    EnemySkip = (byte)System.Math.Min(byte.MaxValue, System.Math.Floor(Positive(CombatStats.EnemySkip) + 1e-9)),
                    SplashShare = (float)Positive(CombatStats.SplashShare),
                    SplashTargets = (byte)System.Math.Min(byte.MaxValue, System.Math.Floor(Positive(CombatStats.SplashTargets) + 1e-9)),
                    BossPierce = (float)Positive(CombatStats.BossPierce),
                    StageCarry = (float)Positive(CombatStats.StageCarry),
                    AtGate = AtUnclearedGate
                }));
        }

        private bool AtUnclearedGate => _loop.Data.Stages.GateAt(_loop.State.Stage) != null && !_loop.State.GatesCleared.Contains(_loop.State.Stage);

        private double Positive(Game.Core.Stats.StatId stat) => System.Math.Max(0, _loop.Hero.Value(stat, 0));

        private void FlushBatch()
        {
            if (_batchEnemyId == 0 || (_total <= _sentTotal && _hitsInBatch == 0))
                return;
            SendToHost(PartyCodec.Encode(_out, new DamageBatchMessage
            {
                Sequence = ++_sequence,
                EnemyId = _batchEnemyId,
                Keys = _total,
                Hits = _hitsInBatch
            }));
            _sentTotal = _total;
            _hitsInBatch = 0;
        }

        private void SendOffer()
        {
            _offerNow = false;
            var buffs = _loop.PartyBuffs;
            var offer = new PartyOfferMessage();
            for (var i = 0; i < buffs.Rules.Stats.Count; i++)
                offer.SetStat(i, buffs.OfferedValue(i));
            for (var i = 0; i < buffs.Rules.Coop.Stats.Count; i++)
            {
                if (buffs.CoopOffered(i) > 0 && !offer.AddCoop(i, buffs.CoopOffered(i)))
                    break;
            }
            var count = 0;
            var giving = PartyRole.Giving(_loop.Hero);
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count && count < PartyBuffRules.MaxAuras; i++)
            {
                var slot = skills[i];
                if (!slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura) || !aura.Party)
                    continue;
                offer.SetAura(count++, Offered(slot, slot.AuraStrength * giving, slot.ActiveTicks));
            }
            for (var i = 0; i < skills.Count && count < PartyBuffRules.MaxAuras; i++)
            {
                if (!_loop.Skills.SharesStacks(i, out var stacks))
                    continue;
                var slot = skills[i];
                offer.SetAura(count++, Offered(slot, System.Math.Min(stacks.MaxTotal, stacks.PerStack.Value * slot.Stacks) * giving, slot.StackTicks > 0 ? slot.StackTicks : slot.ActiveTicks));
            }
            SendToHost(PartyCodec.Encode(_out, offer));
        }

        private void SendLoadout()
        {
            if (!_loadoutNow && !Every(_rules.Buffs.OfferSeconds))
                return;
            var loadout = OwnLoadout();
            if (!_loadoutNow && loadout.SameAs(_sentLoadout) && !Every(_rules.PresenceSeconds))
                return;
            _loadoutNow = false;
            _sentLoadout = loadout;
            SendToHost(PartyCodec.Encode(_out, loadout));
        }

        private PartyLoadoutMessage OwnLoadout()
        {
            var loadout = new PartyLoadoutMessage();
            var autos = 0;
            var auras = 0;
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                var slot = skills[i];
                var position = slot.Role switch
                {
                    SkillSlotRole.Auto => PartyLoadoutMessage.PositionOf(SkillSlotRole.Auto, autos++),
                    SkillSlotRole.Aura => PartyLoadoutMessage.PositionOf(SkillSlotRole.Aura, auras++),
                    _ => PartyLoadoutMessage.PositionOf(slot.Role, 0)
                };
                if (position < 0)
                    continue;
                loadout.Set(position, new LoadoutSkill
                {
                    GemNetId = slot.Skill.NetId,
                    Level = (byte)System.Math.Min(byte.MaxValue, _loop.State.LevelOf(slot.Skill)),
                    Combo = (byte)_loop.Skills.ComboOf(slot.Skill)
                });
            }
            return loadout;
        }

        private static OfferedAura Offered(SkillSlot slot, double strength, int ticks) =>
            new OfferedAura { GemNetId = slot.Skill.NetId, Strength = (float)strength, TicksLeft = ticks };

        private void SendToHost(int length) => _transport.Send(_hostId, _out, length);

        private bool Every(double seconds) => _tick % Ticks(seconds) == 0;

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
