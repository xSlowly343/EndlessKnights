using Game.Core.Content.Gems;
using Game.Core.Math;
using Game.Core.Party.Transport;
using Game.Core.Party.Wire;
using Game.Core.Sim;

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
        private int _ownSlot = PartyEncounter.NoSlot;
        private int _firstEvent;
        private bool _offerNow;

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
        public uint EnemyId => _state.EnemyId;
        public double BarFull => _state.BarFull;
        public double PredictedBarLeft => InParty ? Predicted() : 0;
        public RosterMessage Roster { get; private set; }
        public EnemyKilledMessage LastBossKill { get; private set; }
        public int BossKillsSeen { get; private set; }
        public double DamageTaken => System.Math.Max(1, _state.DamageTaken);
        public int OwnSlot => _ownSlot;

        public ShownEffect EnemyEffect(int index)
        {
            var effect = _state.Effect(index);
            if (!InParty || !effect.Active || _state.EnemyId != _localEnemyId)
                return default;
            effect.TicksLeft = (int)System.Math.Max(0, effect.TicksLeft - (_tick - _stateArrivalTick));
            return effect;
        }

        public bool InParty => Status == PartyLink.Linked || Status == PartyLink.NoSignal;

        public void UseRules(PartyRules rules) => _rules = rules;

        public void Join()
        {
            if (Status != PartyLink.Solo && Status != PartyLink.Away)
                return;
            _lostAtTick = _tick;
            _lastHeardTick = _tick;
            StartJoining();
        }

        public void Leave()
        {
            Unlink();
            Status = PartyLink.Solo;
            _lostAtTick = long.MinValue;
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
            }
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
                if (!offered.Active || !gems.TryGetByNetId(offered.GemNetId, out var gem) || !(gem.Skill?.Effect is AuraEffect effect) || !effect.Party)
                    continue;
                aura.Gem = gem;
                aura.Strength = System.Math.Max(0, offered.Strength);
                aura.TicksLeft = offered.TicksLeft;
                if (!effect.Reserves && (!ReferenceEquals(previousGem, gem) || offered.TicksLeft > previousTicks + 1))
                    NoteAllyCast(gem.Skill!.CooldownSeconds);
            }
            _loop.PartyBuffs.MarkChanged();
        }

        private void OnState(in EncounterStateMessage state)
        {
            var elapsed = System.Math.Max(1, _tick - _stateArrivalTick);
            _allyRatePerTick = state.EnemyId == _state.EnemyId
                ? System.Math.Max(0, (_state.BarLeft - state.BarLeft - _sharesSinceState) / elapsed)
                : 0;
            if (InParty && state.EnemyId > _localEnemyId && _localEnemyId != 0)
                EndLocalEnemy(_loop.State.FightingBoss && state.Farming);
            _state = state;
            _stateArrivalTick = _tick;
            _sharesSinceState = 0;
        }

        private void OnKilled(in EnemyKilledMessage killed)
        {
            if (killed.EnemyKind == PartyEnemyKind.Boss && killed.EnemyId > LastBossKill.EnemyId)
            {
                LastBossKill = killed;
                BossKillsSeen++;
            }
            if (killed.EnemyId <= _rewardedUpTo)
                return;
            if (killed.EnemyId != _localEnemyId)
                SpawnLocal(killed.EnemyId, killed.EnemyKind == PartyEnemyKind.Boss, killed.EnemyId, _state.BarFull, _state.KillsOnStage, _state.Farming, 0);
            EndLocalEnemy(false);
        }

        private void EndLocalEnemy(bool bossFailed)
        {
            if (_localEnemyId <= _rewardedUpTo)
                return;
            _rewardedUpTo = _localEnemyId;
            if (bossFailed)
            {
                _loop.Stages.ConfirmLinkedBossFailed();
                return;
            }
            _loop.Stages.ConfirmLinkedKill();
            PartyRewards++;
        }

        private void StartJoining()
        {
            Status = PartyLink.Joining;
            _joinStartTick = _tick;
            _inRoster = false;
        }

        private void Link()
        {
            Status = PartyLink.Linked;
            _loop.Stages.Link();
            _loop.Skills.ShareEnemy(true);
            _offerNow = true;
            if (_state.EnemyId > 0 && _state.EnemyId - 1 > _rewardedUpTo)
                _rewardedUpTo = _state.EnemyId - 1;
            _localEnemyId = 0;
            _lastHeardTick = _tick;
        }

        private void Unlink()
        {
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
                SpawnLocal(_state.EnemyId, _state.EnemyKind == PartyEnemyKind.Boss, _state.EnemySeed, _state.BarFull, _state.KillsOnStage, _state.Farming, _state.BossTicksLeft);
            if (_localEnemyId == 0 || _localEnemyId <= _rewardedUpTo)
                return;
            gameState.EnemyHp = _unit * System.Math.Max(Predicted(), StageSystem.StaggerHpShare);
            if (gameState.FightingBoss)
                gameState.BossTicksLeft = (int)System.Math.Max(0, _state.BossTicksLeft - (_tick - _stateArrivalTick));
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
            _unit = _loop.Stages.SpawnLinked(boss, seed, barFull, killsOnStage, farming, bossTicksLeft);
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
            if (_loop.LastInput.HasAnyInput)
                _hitsInBatch++;
        }

        private void ShareMarks()
        {
            var synergy = _loop.Synergy;
            var rules = synergy.Rules;
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
            }
        }

        private void WatchCasts()
        {
            var events = _loop.Events;
            for (var i = _firstEvent; i < events.Count; i++)
            {
                var gameEvent = events[i];
                var effect = gameEvent.Gem?.Skill?.Effect;
                if (gameEvent.Kind == GameEventKind.SkillCast && effect is EnemyDebuffEffect debuff)
                    SendToHost(PartyCodec.Encode(_out, new CastEffectMessage
                    {
                        EffectId = gameEvent.Gem!.NetId,
                        OnEnemy = true,
                        Strength = _loop.Skills.DebuffStrength(gameEvent.Gem!),
                        Seconds = gameEvent.Gem.Skill!.DurationSeconds
                    }));
                else if (gameEvent.Kind == GameEventKind.AuraChanged && effect is AuraEffect aura && aura.Party)
                    _offerNow = true;
            }
        }

        private void WatchSignal()
        {
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
                offer.SetAura(count++, new OfferedAura { GemNetId = slot.Skill.NetId, Strength = (float)slot.AuraStrength, TicksLeft = slot.ActiveTicks });
            }
            SendToHost(PartyCodec.Encode(_out, offer));
        }

        private void NoteAllyCast(double cooldownSeconds) =>
            _loop.State.AllyHeavyCastSeconds = System.Math.Max(_loop.State.AllyHeavyCastSeconds, cooldownSeconds);

        private void SendToHost(int length) => _transport.Send(_hostId, _out, length);

        private bool Every(double seconds) => _tick % Ticks(seconds) == 0;

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
