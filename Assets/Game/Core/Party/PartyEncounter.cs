using Game.Core.Content;
using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public sealed class PartyEncounter
    {
        public const int MaxEffects = 16;
        public const int NoSlot = -1;

        private readonly PartyMember[] _members = new PartyMember[PartyRules.MaxSlots];
        private readonly EnemyEffect[] _effects = new EnemyEffect[MaxEffects];
        private readonly ulong _seed;
        private PartyRules _rules;
        private StageTable _stages;
        private uint _previousEnemyId;
        private PartyEnemyKind _previousKind;
        private long _previousKilledTick = long.MinValue;

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
            return Tick - member.LastInputTick > Ticks(_rules.RestAfterSeconds) ? PartyPresence.Resting : PartyPresence.Fighting;
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

        public EnemyEffect Effect(int index) => _effects[index];

        public bool ApplyEnemyEffect(int effectId, double damageTaken, double seconds, int owner = NoSlot)
        {
            if (!(damageTaken > 0) || !(seconds > 0) || !Started)
                return false;
            var ticks = Ticks(seconds);
            var free = NoSlot;
            for (var i = 0; i < _effects.Length; i++)
            {
                if (_effects[i].Active && _effects[i].Id == effectId)
                {
                    if (damageTaken < _effects[i].DamageTaken)
                        return false;
                    _effects[i].DamageTaken = damageTaken;
                    _effects[i].TicksLeft = System.Math.Max(_effects[i].TicksLeft, ticks);
                    _effects[i].Owner = owner;
                    return true;
                }
                if (free == NoSlot && !_effects[i].Active)
                    free = i;
            }
            if (free == NoSlot)
                return false;
            _effects[free] = new EnemyEffect { Id = effectId, DamageTaken = damageTaken, TicksLeft = ticks, Owner = owner };
            return true;
        }

        public double ApplyDamage(int slot, uint enemyId, double share)
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
            _members[slot].EnemyShare += applied;
            BarLeft -= applied;
            if (BarLeft <= 0)
                Kill();
            return applied;
        }

        public bool RequestBossFight()
        {
            if (!Started || !Farming || FightingBoss)
                return false;
            Farming = false;
            Spawn(PartyEnemyKind.Boss);
            return true;
        }

        public void Advance()
        {
            Tick++;
            TickEffects();
            DropLostFighters();
            if (!FightingBoss || !Started)
                return;
            BossTicksLeft--;
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

        private void Kill()
        {
            BarLeft = 0;
            var total = 0.0;
            for (var i = 0; i < _members.Length; i++)
                total += _members[i].EnemyShare;
            Events.Add(new PartyEvent(PartyEventKind.EnemyKilled, Tick, EnemyId, EnemyKind,
                ShareOf(0, total), ShareOf(1, total), ShareOf(2, total), ShareOf(3, total)));
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

        private double ShareOf(int slot, double total) => total > 0 ? _members[slot].EnemyShare / total : 0;

        private void SpawnNext() =>
            Spawn(!Farming && KillsOnStage >= _stages.EnemiesPerStage ? PartyEnemyKind.Boss : PartyEnemyKind.Normal);

        private void Spawn(PartyEnemyKind kind)
        {
            EnemyId++;
            EnemyKind = kind;
            Fighters = 0;
            for (var i = 0; i < _members.Length; i++)
            {
                var member = _members[i];
                member.EnemyShare = 0;
                member.Weight = member.Joined && Counts(i) ? 1 : 0;
                Fighters += member.Weight;
            }
            BarFull = _rules.HpMultiplier(Fighters);
            BarLeft = BarFull;
            Events.Add(new PartyEvent(PartyEventKind.EnemySpawned, Tick, EnemyId, kind));
            if (kind != PartyEnemyKind.Boss)
                return;
            BossTicksLeft = Ticks(_stages.BossTimerSeconds);
            Events.Add(new PartyEvent(PartyEventKind.BossStarted, Tick, EnemyId, kind));
        }

        private bool Counts(int slot)
        {
            var member = _members[slot];
            if (!member.Connected && Tick - member.DisconnectedAtTick > Ticks(_rules.DisconnectGraceSeconds))
                return false;
            if (member.Peaceful)
                return false;
            return Tick - member.LastInputTick <= Ticks(_rules.RestAfterSeconds);
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
