using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyEncounter
    {
        public const int MaxEffects = 16;
        public const int NoSlot = -1;
        public const long Never = -(1L << 53);
        public const int MaxRepeatSeconds = 10;
        public const byte NoWorld = byte.MaxValue;
        private const int RecentTicks = MaxRepeatSeconds * GameLoop.TicksPerSecond + 1;

        private readonly PartyMember[] _members = new PartyMember[PartyRules.MaxSlots];
        private readonly EnemyEffect[] _effects = new EnemyEffect[MaxEffects];
        private readonly double[] _recent = new double[RecentTicks];
        private readonly ulong _seed;
        private PartyRules _rules;
        private StageTable _stages;
        private uint _previousEnemyId;
        private PartyEnemyKind _previousKind;
        private long _previousKilledTick = long.MinValue;
        private double _bossTimerCarry;

        public PartyEncounter(PartyRules rules, StageTable stages, ulong seed)
        {
            _rules = rules;
            _stages = stages;
            _seed = seed;
            for (var i = 0; i < _members.Length; i++)
                _members[i] = new PartyMember();
        }

        public long Tick { get; private set; }
        public uint EnemyId { get; private set; }
        public PartyEnemyKind EnemyKind { get; private set; }
        public int KillsOnStage { get; private set; }
        public bool Farming { get; private set; }
        public int FarmKills { get; private set; }
        public int BossTicksLeft { get; private set; }
        public double BarFull { get; private set; }
        public double BarLeft { get; private set; }
        public int Fighters { get; private set; }
        public long SpawnTick { get; private set; }
        public double LoopShare { get; private set; }
        public double SavedBossTicks { get; private set; }
        public PartyWorld World { get; private set; }
        public long RiftId { get; private set; }
        public byte PendingWorld { get; private set; } = NoWorld;
        public long PendingRiftId { get; private set; }
        public int SwitchTicksLeft { get; private set; }
        public int WeakenedSpawns { get; private set; }
        public double WeakenShare { get; private set; }
        public bool Switching => PendingWorld != NoWorld;
        public bool Started => EnemyId != 0;
        public PartyEventQueue Events { get; } = new PartyEventQueue();
        public int Capacity => _rules.MaxMembers;

        public bool FightingBoss => EnemyKind == PartyEnemyKind.Boss;
        public double BossSecondsLeft => BossTicksLeft * GameLoop.TickSeconds;
        public ulong EnemySeed => SeedFor(EnemyId);

        public PartyMember Member(int slot) => _members[slot];

        public void UseRules(PartyRules rules, StageTable stages)
        {
            _rules = rules;
            _stages = stages;
        }

        public int MemberCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _members.Length; i++)
                {
                    if (_members[i].Joined)
                        count++;
                }
                return count;
            }
        }

        public int SlotOf(ulong playerId)
        {
            for (var i = 0; i < _members.Length; i++)
            {
                if (_members[i].Joined && _members[i].PlayerId == playerId)
                    return i;
            }
            return NoSlot;
        }

        public int Join(ulong playerId)
        {
            var existing = SlotOf(playerId);
            if (existing != NoSlot)
            {
                SetConnected(existing, true);
                return existing;
            }
            if (MemberCount >= _rules.MaxMembers)
                return NoSlot;
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                if (member.Joined)
                    continue;
                member.Reset();
                member.PlayerId = playerId;
                member.Joined = true;
                member.Connected = true;
                member.LastInputTick = Tick;
                if (!Started)
                    SpawnNext();
                return i;
            }
            return NoSlot;
        }

        public int JoinGuest(ulong playerId, double seconds)
        {
            if (!Started || SlotOf(playerId) != NoSlot || !(seconds > 0))
                return NoSlot;
            var slot = Join(playerId);
            if (slot != NoSlot)
                _members[slot].GuestTicksLeft = System.Math.Max(1, Ticks(seconds));
            return slot;
        }

        public void Leave(int slot)
        {
            if (!IsMember(slot))
                return;
            var member = _members[slot];
            var hadWeight = member.Weight > 0;
            member.Reset();
            if (hadWeight)
                DropFighterMidFight();
        }

        public void ReportInput(int slot)
        {
            if (IsMember(slot))
                _members[slot].LastInputTick = Tick;
        }

        public void SetHeroClass(int slot, byte heroClass)
        {
            if (IsMember(slot))
                _members[slot].HeroClass = heroClass;
        }

        public void SetPeaceful(int slot, bool peaceful)
        {
            if (IsMember(slot))
                _members[slot].Peaceful = peaceful;
        }

        public void SetConnected(int slot, bool connected)
        {
            if (!IsMember(slot))
                return;
            var member = _members[slot];
            if (member.Connected == connected)
                return;
            member.Connected = connected;
            if (!connected)
                member.DisconnectedAtTick = Tick;
        }

        public PartyPresence Presence(int slot)
        {
            var member = _members[slot];
            if (!member.Connected)
                return PartyPresence.Offline;
            if (member.Peaceful)
                return PartyPresence.Peaceful;
            return Active(member) ? PartyPresence.Fighting : PartyPresence.Resting;
        }

        public int RelayTarget(int from)
        {
            if (!IsMember(from) || !_members[from].Connected || Tick - _members[from].LastInputTick > Ticks(_rules.Relay.SenderSeconds))
                return NoSlot;
            var recent = Ticks(_rules.Relay.RecipientSeconds);
            var target = NoSlot;
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                if (i == from || !member.Joined || !member.Connected || member.Peaceful || Tick - member.LastInputTick > recent)
                    continue;
                if (target == NoSlot || member.LastInputTick > _members[target].LastInputTick)
                    target = i;
            }
            return target;
        }

        public bool Typing(int slot) => IsMember(slot) && Tick - _members[slot].LastInputTick <= Ticks(_rules.Contribution.ActiveInputSeconds);

        public double Portion
        {
            get
            {
                var counting = 0;
                for (var i = 0; i < _members.Length; i++)
                {
                    if (_members[i].Joined && Counts(i))
                        counting++;
                }
                return BarFull / System.Math.Max(1, counting);
            }
        }

        public double DamageTakenMultiplier
        {
            get
            {
                var total = 0.0;
                for (var i = 0; i < _effects.Length; i++)
                {
                    if (_effects[i].Active)
                        total += _effects[i].DamageTaken;
                }
                return 1 + System.Math.Min(_rules.MaxEnemyDamageTaken, total);
            }
        }

        public double BossTimerRate
        {
            get
            {
                var rate = 1.0;
                for (var i = 0; i < _effects.Length; i++)
                {
                    if (_effects[i].Active)
                        rate *= 1 - _effects[i].TimerSlow;
                }
                return System.Math.Max(_rules.Timer.MinBossRate, rate);
            }
        }

        public EnemyEffect Effect(int index) => _effects[index];

        public bool ApplyEnemyEffect(int effectId, double damageTaken, double seconds, int owner = NoSlot, double timerSlow = 0)
        {
            if (!(damageTaken >= 0) || !(timerSlow >= 0) || !(damageTaken > 0 || timerSlow > 0) || !(seconds > 0) || !Started)
                return false;
            timerSlow = System.Math.Min(1, timerSlow);
            var ticks = Ticks(seconds);
            var free = NoSlot;
            for (var i = 0; i < _effects.Length; i++)
            {
                if (_effects[i].Active && _effects[i].Id == effectId)
                {
                    if (damageTaken < _effects[i].DamageTaken)
                        return false;
                    _effects[i].DamageTaken = damageTaken;
                    _effects[i].TimerSlow = System.Math.Max(_effects[i].TimerSlow, timerSlow);
                    _effects[i].TicksLeft = System.Math.Max(_effects[i].TicksLeft, ticks);
                    _effects[i].Owner = owner;
                    return true;
                }
                if (free == NoSlot && !_effects[i].Active)
                    free = i;
            }
            if (free == NoSlot)
                return false;
            _effects[free] = new EnemyEffect { Id = effectId, DamageTaken = damageTaken, TimerSlow = timerSlow, TicksLeft = ticks, Owner = owner };
            return true;
        }

        public double ApplyDamage(int slot, uint enemyId, double share, double limit = double.PositiveInfinity)
        {
            var applied = System.Math.Min(Applicable(slot, enemyId, share), limit);
            if (!(applied > 0))
                return 0;
            _recent[Tick % RecentTicks] += applied * KindScale;
            return Take(slot, applied);
        }

        public double RepeatRecent(int slot, double seconds)
        {
            if (!IsMember(slot) || !Started || !(seconds > 0))
                return 0;
            var ticks = System.Math.Min(RecentTicks - 1, Ticks(seconds));
            var sum = 0.0;
            for (var i = 0; i < ticks && Tick - i >= 0; i++)
                sum += _recent[(Tick - i) % RecentTicks];
            var applied = System.Math.Min(BarLeft, sum / KindScale);
            if (!(applied > 0))
                return 0;
            LoopShare += applied;
            return Take(slot, applied);
        }

        private double Take(int slot, double applied)
        {
            _members[slot].EnemyShare += applied;
            BarLeft -= applied;
            if (BarLeft <= 0)
                Kill();
            return applied;
        }

        private double KindScale => FightingBoss ? _stages.BossHpMultiplier : 1;

        public double Applicable(int slot, uint enemyId, double share)
        {
            if (!IsMember(slot) || !Started || !(share > 0) || double.IsInfinity(share))
                return 0;
            if (enemyId != EnemyId)
            {
                if (!IsLate(enemyId))
                    return 0;
                share *= KindRatio(_previousKind, EnemyKind);
            }
            var applied = System.Math.Min(BarLeft, share * DamageTakenMultiplier);
            return Typing(slot) ? applied : System.Math.Min(applied, IdleRoom(slot));
        }

        public bool WeakenNext(int enemies, double share)
        {
            if (enemies <= 0 || !(share > 0) || !Started)
                return false;
            WeakenedSpawns = System.Math.Max(WeakenedSpawns, System.Math.Min(_rules.MaxWeakenedSpawns, enemies));
            WeakenShare = System.Math.Max(WeakenShare, System.Math.Min(_rules.MaxWeakenShare, share));
            return true;
        }

        public bool RequestBossFight()
        {
            if (!Started || !Farming || FightingBoss)
                return false;
            Farming = false;
            Spawn(PartyEnemyKind.Boss);
            return true;
        }

        public bool ProposeWorld(PartyWorld world, long riftId)
        {
            if (!Started || Switching || (world == World && riftId == RiftId) || (world == PartyWorld.Home) != (riftId == 0))
                return false;
            PendingWorld = (byte)world;
            PendingRiftId = riftId;
            SwitchTicksLeft = System.Math.Max(1, Ticks(_rules.WorldSwitchSeconds));
            Events.Add(new PartyEvent(PartyEventKind.WorldPending, Tick, EnemyId, EnemyKind));
            return true;
        }

        public bool CancelWorld()
        {
            if (!Switching)
                return false;
            ClearPending();
            Events.Add(new PartyEvent(PartyEventKind.WorldCancelled, Tick, EnemyId, EnemyKind));
            return true;
        }

        public void SwitchWorld(PartyWorld world, long riftId)
        {
            ReleaseGuests(true);
            ClearPending();
            World = world;
            RiftId = riftId;
            KillsOnStage = 0;
            Farming = false;
            FarmKills = 0;
            _previousEnemyId = 0;
            WeakenedSpawns = 0;
            WeakenShare = 0;
            System.Array.Clear(_effects, 0, _effects.Length);
            System.Array.Clear(_recent, 0, _recent.Length);
            Events.Add(new PartyEvent(PartyEventKind.WorldChanged, Tick, EnemyId, EnemyKind));
            if (Started)
                Spawn(PartyEnemyKind.Normal);
        }

        private void ClearPending()
        {
            PendingWorld = NoWorld;
            PendingRiftId = 0;
            SwitchTicksLeft = 0;
        }

        public void Advance()
        {
            Tick++;
            _recent[Tick % RecentTicks] = 0;
            TickEffects();
            ReleaseGuests(false);
            DropLostFighters();
            if (Switching && --SwitchTicksLeft <= 0)
            {
                SwitchWorld((PartyWorld)PendingWorld, PendingRiftId);
                return;
            }
            if (!FightingBoss || !Started)
                return;
            var rate = BossTimerRate;
            SavedBossTicks += 1 - rate;
            _bossTimerCarry += rate;
            while (_bossTimerCarry >= 1 && BossTicksLeft > 0)
            {
                _bossTimerCarry -= 1;
                BossTicksLeft--;
            }
            if (BossTicksLeft > 0)
                return;
            Events.Add(new PartyEvent(PartyEventKind.BossFailed, Tick, EnemyId, EnemyKind));
            Farming = true;
            FarmKills = 0;
            Spawn(PartyEnemyKind.Normal);
        }

        public ulong SeedFor(uint enemyId) => SplitMix.At(_seed, enemyId);

        private bool IsMember(int slot) => slot >= 0 && slot < _members.Length && _members[slot].Joined;

        private bool IsLate(uint enemyId) =>
            enemyId != 0 && enemyId == _previousEnemyId && Tick - _previousKilledTick <= Ticks(_rules.LateDamageSeconds);

        private double KindRatio(PartyEnemyKind from, PartyEnemyKind to)
        {
            if (from == to)
                return 1;
            var boss = _stages.BossHpMultiplier;
            return to == PartyEnemyKind.Boss ? 1 / boss : boss;
        }

        private double IdleRoom(int slot)
        {
            var portion = Portion;
            var contribution = _rules.Contribution;
            var idleTaken = 0.0;
            var typing = 0;
            for (var i = 0; i < _members.Length; i++)
            {
                if (!_members[i].Joined)
                    continue;
                if (Typing(i))
                    typing++;
                else
                    idleTaken += _members[i].EnemyShare;
            }
            var own = contribution.IdlePortionShare * portion - _members[slot].EnemyShare;
            var shared = BarFull - contribution.TypingReserveShare * portion * typing - idleTaken;
            return System.Math.Max(0, System.Math.Min(own, shared));
        }

        private void Kill()
        {
            BarLeft = 0;
            var total = 0.0;
            for (var i = 0; i < _members.Length; i++)
                total += _members[i].EnemyShare;
            var rewarded = Reward();
            Events.Add(new PartyEvent(PartyEventKind.EnemyKilled, Tick, EnemyId, EnemyKind,
                ShareOf(0, total), ShareOf(1, total), ShareOf(2, total), ShareOf(3, total), rewarded,
                total > 0 ? LoopShare / total : 0, SavedBossTicks / GameLoop.TicksPerSecond));
            _previousEnemyId = EnemyId;
            _previousKind = EnemyKind;
            _previousKilledTick = Tick;
            if (FightingBoss)
            {
                Events.Add(new PartyEvent(PartyEventKind.StageCleared, Tick, EnemyId, EnemyKind));
                KillsOnStage = 0;
                Farming = false;
            }
            else if (Farming)
            {
                FarmKills++;
                if (FarmKills >= _stages.BossRetryAfterKills)
                    Farming = false;
            }
            else if (KillsOnStage < _stages.EnemiesPerStage)
            {
                KillsOnStage++;
            }
            SpawnNext();
        }

        private byte Reward()
        {
            var contribution = _rules.Contribution;
            var counted = contribution.MinShare * Portion;
            var lifetime = Tick - SpawnTick;
            var bankMax = Ticks(contribution.RestBankSeconds);
            var rewarded = 0;
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                if (!member.Joined)
                    continue;
                if (member.EnemyShare > 0 && member.EnemyShare >= counted)
                    member.LastContributionTick = Tick;
                if (!member.Connected || member.Peaceful || member.Guest)
                    continue;
                if (Typing(i))
                {
                    rewarded |= 1 << i;
                    member.RestBankTicks = System.Math.Min(bankMax, member.RestBankTicks + lifetime);
                }
                else if (member.RestBankTicks > 0)
                {
                    rewarded |= 1 << i;
                    member.RestBankTicks = System.Math.Max(0, member.RestBankTicks - lifetime);
                }
            }
            return (byte)rewarded;
        }

        private double ShareOf(int slot, double total) => total > 0 ? _members[slot].EnemyShare / total : 0;

        private void SpawnNext() =>
            Spawn(!Farming && KillsOnStage >= _stages.EnemiesPerStage ? PartyEnemyKind.Boss : PartyEnemyKind.Normal);

        private void Spawn(PartyEnemyKind kind)
        {
            EnemyId++;
            EnemyKind = kind;
            SpawnTick = Tick;
            Fighters = 0;
            LoopShare = 0;
            SavedBossTicks = 0;
            _bossTimerCarry = 0;
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                member.EnemyShare = 0;
                member.Weight = member.Joined && Counts(i) ? 1 : 0;
                Fighters += member.Weight;
            }
            BarFull = _rules.HpMultiplier(Fighters);
            BarLeft = BarFull * (1 - TakeWeaken());
            Events.Add(new PartyEvent(PartyEventKind.EnemySpawned, Tick, EnemyId, kind));
            if (kind != PartyEnemyKind.Boss)
                return;
            BossTicksLeft = Ticks(_stages.BossTimerSeconds);
            Events.Add(new PartyEvent(PartyEventKind.BossStarted, Tick, EnemyId, kind));
        }

        private double TakeWeaken()
        {
            if (WeakenedSpawns <= 0)
                return 0;
            var share = WeakenShare;
            if (--WeakenedSpawns == 0)
                WeakenShare = 0;
            return share;
        }

        private bool Counts(int slot)
        {
            var member = _members[slot];
            if (!member.Connected && Tick - member.DisconnectedAtTick > Ticks(_rules.DisconnectGraceSeconds))
                return false;
            if (member.Peaceful || member.Guest)
                return false;
            return Active(member);
        }

        private bool Active(PartyMember member)
        {
            var rest = Ticks(_rules.RestAfterSeconds);
            return Tick - member.LastInputTick <= rest || Tick - member.LastContributionTick <= rest;
        }

        private void DropLostFighters()
        {
            var grace = Ticks(_rules.DisconnectGraceSeconds);
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                if (member.Weight == 0 || member.Connected || Tick - member.DisconnectedAtTick <= grace)
                    continue;
                member.Weight = 0;
                DropFighterMidFight();
            }
        }

        private void DropFighterMidFight()
        {
            if (Fighters == 0)
                return;
            var before = _rules.HpMultiplier(Fighters);
            Fighters--;
            if (!FightingBoss)
                return;
            var after = _rules.HpMultiplier(Fighters);
            BarLeft *= after / before;
            BarFull = after;
        }

        private void ReleaseGuests(bool all)
        {
            var left = 0;
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                if (!member.Joined || !member.Guest)
                    continue;
                if (!all && --member.GuestTicksLeft > 0)
                    continue;
                member.Reset();
                left |= 1 << i;
            }
            if (left != 0)
                Events.Add(new PartyEvent(PartyEventKind.GuestLeft, Tick, EnemyId, EnemyKind, rewarded: (byte)left));
        }

        private void TickEffects()
        {
            for (var i = 0; i < _effects.Length; i++)
            {
                if (_effects[i].TicksLeft > 0)
                    _effects[i].TicksLeft--;
            }
        }

        private static int Ticks(double seconds) => (int)System.Math.Round(seconds * GameLoop.TicksPerSecond);
    }
}
