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
        private long _lastOwnInputTick = PartyEncounter.Never;
        private readonly double[] _contribution = new double[PartyRules.MaxSlots];
        private readonly int[] _reaction = { NoReaction, NoReaction, NoReaction, NoReaction };
        private readonly long[] _reactionTick = new long[PartyRules.MaxSlots];
        private readonly int[] _glowGem = new int[PartyBuffRules.MaxAuras];
        private readonly float[] _glowStrength = new float[PartyBuffRules.MaxAuras];
        private readonly long[] _glowUntil = new long[PartyBuffRules.MaxAuras];
        private readonly int[] _reactionSerial = new int[PartyRules.MaxSlots];
        private readonly double[] _allyShares = new double[PartyRules.MaxSlots];
        private long _lastSentReactionTick = PartyEncounter.Never;
        private int _contributionKills;
        private int _ownSlot = PartyEncounter.NoSlot;
        private int _firstEvent;
        private bool _offerNow;
        private long _refusedRift;
        private long _guestLinkTick;
        private uint _sentReactions;
        private int _auraBoundGem;

        public const int NoReaction = -1;
        private const double MaxAllyShares = 1000;

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
        public bool LastKillPaid { get; private set; } = true;
        public uint EnemyId => _state.EnemyId;
        public double BarFull => _state.BarFull;
        public double PredictedBarLeft => InParty ? Predicted() : 0;
        public RosterMessage Roster { get; private set; }
        public EnemyKilledMessage LastBossKill { get; private set; }
        public int BossKillsSeen { get; private set; }
        public double DamageTaken => System.Math.Max(1, _state.DamageTaken);
        public int OwnSlot => _ownSlot;
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
            _localEnemyId = 0;
            _rewardedUpTo = 0;
            _countedUpTo = 0;
            _contributionKills = 0;
            System.Array.Clear(_contribution, 0, _contribution.Length);
            for (var slot = 0; slot < _reaction.Length; slot++)
            {
                _reaction[slot] = NoReaction;
                _allyShares[slot] = 0;
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
                ShareMarks();
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
                ShareReactions();
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
                case PartyMessageKind.PartyReaction when PartyCodec.TryDecode(_in, length, out PartyReactionMessage reaction):
                    if (InParty && reaction.Slot < PartyRules.MaxSlots && reaction.Slot != _ownSlot && _rules.Reactions.Knows(reaction.Reaction))
                        ShowReaction(reaction.Slot, reaction.Reaction);
                    break;
                case PartyMessageKind.AllyCast when PartyCodec.TryDecode(_in, length, out AllyCastMessage cast):
                    if (InParty && cast.Slot != _ownSlot && _loop.Data.Gems.TryGetByNetId(cast.GemNetId, out var castGem) && castGem.Skill != null)
                        NoteAllyCast(castGem.Skill.CooldownSeconds);
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
            Roster = roster;
            _ownSlot = roster.SlotOf(_transport.LocalId);
            _inRoster = _ownSlot != PartyEncounter.NoSlot;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot == _ownSlot || roster.Player(slot) == 0 || roster.Presence(slot) == PartyPresence.Offline)
                    _loop.PartyBuffs.ClearAlly(slot);
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
            var gems = _loop.Data.Gems;
            for (var i = 0; i < ally.Auras.Length; i++)
            {
                var offered = offer.Aura(i);
                var aura = ally.Auras[i];
                var previousGem = aura.Gem;
                var previousTicks = aura.TicksLeft;
                aura.Clear();
                if (!offered.Active || !gems.TryGetByNetId(offered.GemNetId, out var gem) || !PartyBuffs.Shared(gem))
                    continue;
                aura.Gem = gem;
                aura.Strength = System.Math.Max(0, offered.Strength);
                aura.TicksLeft = offered.TicksLeft;
                if (gem.Skill!.Effect is AuraEffect { Reserves: false } && (!ReferenceEquals(previousGem, gem) || offered.TicksLeft > previousTicks + 1))
                    NoteAllyCast(gem.Skill!.CooldownSeconds);
            }
            _loop.PartyBuffs.MarkChanged();
        }

        private void OnState(in EncounterStateMessage state)
        {
            if (InParty && _state.EnemyId != 0 && (state.World != _state.World || state.RiftId != _state.RiftId))
                AbandonLocalEnemy();
            var elapsed = System.Math.Max(1, _tick - _stateArrivalTick);
            _allyRatePerTick = state.EnemyId == _state.EnemyId
                ? System.Math.Max(0, (_state.BarLeft - state.BarLeft - _sharesSinceState) / elapsed)
                : 0;
            if (InParty && state.EnemyId > _localEnemyId && _localEnemyId != 0)
                EndLocalEnemy(_loop.State.FightingBoss && state.Farming);
            _state = state;
            _stateArrivalTick = _tick;
            _sharesSinceState = 0;
            if (InParty)
                _loop.Resource.NoteAllyHits(AllyHits(state));
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot != _ownSlot && InParty)
                    _allyShares[slot] = System.Math.Min(_allyShares[slot] + state.Recent(slot), MaxAllyShares);
            }
        }

        private int AllyHits(in EncounterStateMessage state)
        {
            var hits = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot != _ownSlot)
                    hits += state.Hits(slot);
            }
            return hits;
        }

        private void AbandonLocalEnemy()
        {
            if (_localEnemyId > _rewardedUpTo)
                _rewardedUpTo = _localEnemyId;
            _localEnemyId = 0;
            _total = 0;
            _sentTotal = 0;
            _hitsInBatch = 0;
        }

        private void FollowWorld()
        {
            var rift = _loop.Rift;
            if (_state.EnemyId == 0 || IsGuest)
                return;
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

        private void OnKilled(in EnemyKilledMessage killed)
        {
            CountContribution(killed);
            if (killed.EnemyKind == PartyEnemyKind.Boss && killed.EnemyId > LastBossKill.EnemyId)
            {
                LastBossKill = killed;
                BossKillsSeen++;
            }
            if (killed.EnemyId <= _rewardedUpTo)
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
            _rewardedUpTo = _localEnemyId;
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
            _sentReactions = 0;
            _auraBoundGem = 0;
            _guestLinkTick = _tick;
            _loop.Stages.Link(IsGuest);
            _loop.Skills.ShareEnemy(true);
            _offerNow = true;
            if (_state.EnemyId > 0 && _state.EnemyId - 1 > _rewardedUpTo)
                _rewardedUpTo = _state.EnemyId - 1;
            _localEnemyId = 0;
            _lastHeardTick = _tick;
            _countedUpTo = _state.EnemyId > 0 ? _state.EnemyId - 1 : 0;
            _contributionKills = 0;
            System.Array.Clear(_contribution, 0, _contribution.Length);
            LastKillPaid = true;
        }

        private void Unlink()
        {
            _loop.State.PartyAllies = 0;
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
            _localEnemyId = 0;
            _total = 0;
            _sentTotal = 0;
        }

        private void SyncEnemy()
        {
            var gameState = _loop.State;
            var fresh = _state.EnemyId > _rewardedUpTo;
            if (fresh && (_localEnemyId != _state.EnemyId || gameState.Stage != _localStage || !gameState.EnemyAlive))
            {
                var newEnemy = _localEnemyId != _state.EnemyId;
                SpawnLocal(_state.EnemyId, _state.EnemyKind == PartyEnemyKind.Boss, _state.EnemySeed, _state.BarFull, _state.KillsOnStage, _state.Farming, _state.BossTicksLeft);
                if (newEnemy)
                    HitWithChain();
            }
            if (_localEnemyId == 0 || _localEnemyId <= _rewardedUpTo)
                return;
            gameState.EnemyHp = _unit * System.Math.Max(Predicted(), StageSystem.StaggerHpShare);
            if (!gameState.FightingBoss)
                return;
            gameState.BossTimerRate = _state.BossTimerRate;
            gameState.BossTicksLeft = (int)System.Math.Max(0, _state.BossTicksLeft - System.Math.Floor((_tick - _stateArrivalTick) * (double)_state.BossTimerRate));
        }

        private void SpawnLocal(uint enemyId, bool boss, ulong seed, double barFull, int killsOnStage, bool farming, int bossTicksLeft)
        {
            if (_localEnemyId != enemyId)
            {
                FlushBatch();
                _total = 0;
                _sentTotal = 0;
            }
            _localEnemyId = enemyId;
            _localStage = _loop.State.Stage;
            var weakened = _state.EnemyId == enemyId && _state.Weakened;
            _unit = _loop.Stages.SpawnLinked(boss, seed, barFull, killsOnStage, farming, bossTicksLeft, weakened, weakened ? _state.WeakenedGold : 0);
        }

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
        }

        private double Predicted()
        {
            var predicted = _state.BarLeft - _sharesSinceState - _allyRatePerTick * (_tick - _stateArrivalTick);
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
            }
            if (_loop.LastInput.HasAnyInput && !_loop.Focus.MacroDetected)
            {
                _hitsInBatch++;
                _lastOwnInputTick = _tick;
            }
        }

        private void ShareMarks()
        {
            var synergy = _loop.Synergy;
            var rules = synergy.Rules;
            var allies = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                var present = slot != _ownSlot && Roster.Player(slot) != 0 && Roster.Presence(slot) != PartyPresence.Offline;
                if (!present)
                {
                    synergy.SetAlly(slot, false, default, 0);
                    continue;
                }
                var marks = 0;
                var ally = _loop.PartyBuffs.Ally(slot);
                for (var i = 0; i < ally.Auras.Length; i++)
                {
                    if (ally.Auras[i].Active)
                        marks |= rules.MaskOf(ally.Auras[i].Gem!);
                }
                for (var i = 0; i < ShownEffect.MaxShown; i++)
                {
                    var effect = EnemyEffect(i);
                    if (effect.Active && effect.Owner == slot && _loop.Data.Gems.TryGetByNetId(effect.Id, out var gem))
                        marks |= rules.MaskOf(gem);
                }
                synergy.SetAlly(slot, true, (HeroClass)Roster.HeroClass(slot), marks);
                allies++;
            }
            _loop.State.PartyAllies = allies;
        }

        private void WatchCasts()
        {
            var events = _loop.Events;
            for (var i = _firstEvent; i < events.Count; i++)
            {
                var gameEvent = events[i];
                var effect = gameEvent.Gem?.Skill?.Effect;
                if (gameEvent.Kind == GameEventKind.SkillCast && effect is EnemyDebuffEffect debuff)
                {
                    SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                    {
                        EffectId = gameEvent.Gem!.NetId,
                        OnEnemy = true,
                        Strength = _loop.Skills.DebuffStrength(gameEvent.Gem!),
                        Seconds = gameEvent.Gem.Skill!.DurationSeconds,
                        TimerSlow = debuff.BossTimerSlow
                    }));
                    if (debuff.WhileAura)
                        _auraBoundGem = gameEvent.Gem.NetId;
                }
                else if (gameEvent.Kind == GameEventKind.SkillCast && effect is WeakenNextEffect weaken)
                    SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                    {
                        EffectId = gameEvent.Gem!.NetId,
                        OnEnemy = true,
                        WeakenEnemies = (byte)System.Math.Min(byte.MaxValue, weaken.Enemies),
                        WeakenShare = WeakenShare(gameEvent.Gem, weaken),
                        WeakenGold = WeakenGold(gameEvent.Gem),
                        Strength = weaken.VulnerabilityShare,
                        Seconds = weaken.VulnerabilitySeconds
                    }));
                else if (gameEvent.Kind == GameEventKind.SkillCast && effect is PulseEffect { BossTimerSlow: > 0 } pulse)
                    SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                    {
                        EffectId = gameEvent.Gem!.NetId,
                        OnEnemy = true,
                        Seconds = gameEvent.Gem.Skill!.DurationSeconds,
                        TimerSlow = pulse.BossTimerSlow
                    }));
                else if (gameEvent.Kind == GameEventKind.SkillCast && effect is RepeatRecentEffect)
                    SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                    {
                        EffectId = gameEvent.Gem!.NetId,
                        OnEnemy = true,
                        RepeatSeconds = _loop.Skills.LastRepeatSeconds
                    }));
                else if (gameEvent.Kind == GameEventKind.AuraChanged && effect is AuraEffect aura && aura.Party)
                    _offerNow = true;
                if (gameEvent.Kind == GameEventKind.SkillCast && effect is AuraEffect { AllyResource: > 0 } gift)
                    SendToHost(PartyCodec.Encode(_out, new PartyResourceMessage { Amount = (float)gift.AllyResource }));
                if (gameEvent.Kind != GameEventKind.SkillCast || gameEvent.Gem?.Skill == null)
                    continue;
                if (gameEvent.Gem.Skill.Role == SkillRole.Heavy)
                    SendToHost(PartyCodec.Encode(_out, new AllyCastMessage { GemNetId = gameEvent.Gem.NetId }));
                ShareCurse(gameEvent.Gem);
            }
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

        private void ShareReactions()
        {
            var reactions = _loop.Synergy.ReactionMask;
            if (reactions == _sentReactions || Status != PartyLink.Linked)
                return;
            _sentReactions = reactions;
            SendToHost(PartyCodec.Encode(_out, new SynergyMessage { Reactions = reactions }));
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

        private double WeakenShare(GemDef gem, WeakenNextEffect weaken)
        {
            var share = weaken.HpShare;
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                if (ReferenceEquals(skills[i].Skill, gem) && _loop.Skills.IsNative(i))
                    share = System.Math.Max(share, gem.Skill!.Native.WeakenShare);
            }
            return share;
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
                SendToHost(PartyCodec.Encode(_out, new HelloMessage { HeroClass = (byte)_loop.State.Class, Stage = _loop.State.Stage }));
            if (InParty && Every(_rules.DamageBatchSeconds))
                FlushBatch();
            if (InParty && (_offerNow || Every(_rules.Buffs.OfferSeconds)))
                SendOffer();
            if ((InParty || Status == PartyLink.Joining) && Every(_rules.PresenceSeconds))
                SendToHost(PartyCodec.Encode(_out, new PresenceMessage
                {
                    Presence = Peaceful ? PartyPresence.Peaceful : PartyPresence.Fighting,
                    Stage = _loop.State.Stage,
                    Resource = (float)_loop.State.Resource
                }));
        }

        private void FlushBatch()
        {
            if (_localEnemyId == 0 || (_total <= _sentTotal && _hitsInBatch == 0))
                return;
            SendToHost(PartyCodec.Encode(_out, new DamageBatchMessage
            {
                Sequence = ++_sequence,
                EnemyId = _localEnemyId,
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
                offer.SetStat(i, buffs.OwnValue(i));
            var count = 0;
            var skills = _loop.State.Skills;
            for (var i = 0; i < skills.Count && count < PartyBuffRules.MaxAuras; i++)
            {
                var slot = skills[i];
                if (!slot.AuraApplied || !(slot.Def.Effect is AuraEffect aura) || !aura.Party)
                    continue;
                offer.SetAura(count++, Resonate(slot, slot.AuraStrength, slot.ActiveTicks));
            }
            for (var i = 0; i < skills.Count && count < PartyBuffRules.MaxAuras; i++)
            {
                if (!_loop.Skills.SharesStacks(i, out var stacks))
                    continue;
                var slot = skills[i];
                offer.SetAura(count++, Resonate(slot, System.Math.Min(stacks.MaxTotal, stacks.PerStack.Value * slot.Stacks), slot.StackTicks > 0 ? slot.StackTicks : slot.ActiveTicks));
            }
            for (var i = 0; i < _glowGem.Length && count < PartyBuffRules.MaxAuras; i++)
            {
                if (_glowGem[i] == 0 || _glowUntil[i] <= _tick || Offered(offer, count, _glowGem[i]))
                    continue;
                offer.SetAura(count++, new OfferedAura { GemNetId = _glowGem[i], Strength = _glowStrength[i], TicksLeft = (int)(_glowUntil[i] - _tick) });
            }
            SendToHost(PartyCodec.Encode(_out, offer));
        }

        private OfferedAura Resonate(SkillSlot slot, double strength, int ticks)
        {
            var offered = new OfferedAura { GemNetId = slot.Skill.NetId, Strength = (float)strength, TicksLeft = ticks };
            var resonance = ResonanceOf(slot);
            if (resonance == null)
                return offered;
            offered.Strength = (float)(strength * resonance.AllyPower);
            offered.TicksLeft = ticks + Ticks(resonance.AfterSeconds);
            Glow(offered);
            return offered;
        }

        private static SupportDef? ResonanceOf(SkillSlot slot)
        {
            for (var i = 0; i < slot.Supports.Length; i++)
            {
                var support = slot.Supports[i]?.Support;
                if (support != null && support.Kind == SupportKind.Resonance)
                    return support;
            }
            return null;
        }

        private void Glow(in OfferedAura offered)
        {
            var free = 0;
            for (var i = 0; i < _glowGem.Length; i++)
            {
                if (_glowGem[i] == offered.GemNetId)
                {
                    free = i;
                    break;
                }
                if (_glowUntil[i] < _glowUntil[free])
                    free = i;
            }
            _glowGem[free] = offered.GemNetId;
            _glowStrength[free] = offered.Strength;
            _glowUntil[free] = _tick + offered.TicksLeft;
        }

        private static bool Offered(in PartyOfferMessage offer, int count, int gemNetId)
        {
            for (var i = 0; i < count; i++)
            {
                if (offer.Aura(i).GemNetId == gemNetId)
                    return true;
            }
            return false;
        }

        private void NoteAllyCast(double cooldownSeconds) =>
            _loop.State.AllyHeavyCastSeconds = System.Math.Max(_loop.State.AllyHeavyCastSeconds, cooldownSeconds);

        private void SendToHost(int length) => _transport.Send(_hostId, _out, length);

        private bool Every(double seconds) => _tick % Ticks(seconds) == 0;

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
