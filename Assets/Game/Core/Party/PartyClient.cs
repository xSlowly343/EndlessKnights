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
            _loop.Stages.Unlink();
            Status = PartyLink.Solo;
            _lostAtTick = long.MinValue;
        }

        public void BeforeTick()
        {
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
                SyncEnemy();
        }

        public void AfterTick()
        {
            _tick++;
            if (InParty)
                CountDamage();
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
                    _inRoster = roster.SlotOf(_transport.LocalId) != PartyEncounter.NoSlot;
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
            if (_state.EnemyId > 0 && _state.EnemyId - 1 > _rewardedUpTo)
                _rewardedUpTo = _state.EnemyId - 1;
            _localEnemyId = 0;
            _lastHeardTick = _tick;
        }

        private void GoSolo()
        {
            _loop.Stages.Unlink();
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
                _sharesSinceState += share;
            }
            if (_loop.LastInput.HasAnyInput)
                _hitsInBatch++;
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

        private void SendToHost(int length) => _transport.Send(_hostId, _out, length);

        private bool Every(double seconds) => _tick % Ticks(seconds) == 0;

        private static int Ticks(double seconds) => System.Math.Max(1, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
    }
}
