using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Input;
using Game.Core.Math;
using Game.Core.Party;
using Game.Core.Sim.Rift;

namespace Game.Core.Sim
{
    public sealed class GameState
    {
        public const int QueueDepth = 3;

        public GameState(ulong seed = 1)
        {
            Rng = new Rng(seed);
            Drops = new DropState(seed);
        }

        public long Tick { get; set; }
        public long StartedUnixMs { get; set; }
        public Rng Rng { get; set; }
        public HeroClass Class { get; set; }
        public bool ClassChosen { get; set; } = true;
        public int ClassChanges { get; set; }
        public string ClassName => ClassChosen ? HeroClassNames.Of(Class) : HeroClassNames.Novice;

        public bool Is(HeroClass heroClass) => ClassChosen && Class == heroClass;

        private ClassTreeAllocation? _classTree;

        public ClassTreeAllocation ClassTree
        {
            get => _classTree ??= new ClassTreeAllocation(ClassName);
            set => _classTree = value;
        }

        public List<ClassTreeAllocation?> CommonTrees { get; set; } = new List<ClassTreeAllocation?>();

        public bool ClassTreeActive { get; set; }
        public int TreePointsEarned { get; set; }
        public int TreePointsLeft => TreePointsEarned - ClassTree.PointsSpent;
        public int TreePointsSpent => ClassTree.PointsSpent;

        public AscendancyState Ascendancy { get; set; } = new AscendancyState();

        public long Stage { get; set; } = 1;
        public long HighestStage { get; set; } = 1;
        public long RecordStage { get; set; } = 1;
        public int HeroLevel { get; set; } = 1;
        public int StrikeLevel { get; set; }
        public double HeroXp { get; set; }
        public int KillsOnStage { get; set; }
        public int BossTicksLeft { get; set; }
        public bool Farming { get; set; }
        public int FarmKills { get; set; }
        public int BossFailsInARow { get; set; }
        public double BossBestFraction { get; set; }

        public EnemyDef? Enemy { get; private set; }
        public EnemyType EnemyType { get; private set; }
        public BigNumber EnemyHp { get; set; }
        public BigNumber EnemyMaxHp { get; private set; }

        public BigNumber Gold { get; set; }
        public BigNumber RunGoldEarned { get; set; }
        public BigNumber RecentDps { get; set; }
        public BigNumber Echo { get; set; }
        public BigNumber EchoEarned { get; set; }
        public int Prestiges { get; set; }
        public Dictionary<string, int> Artifacts { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public RiftState Rift { get; } = new RiftState();
        public WeaponState Weapons { get; } = new WeaponState();
        public DropState Drops { get; }
        public ChronicleState Chronicle { get; } = new ChronicleState();
        public List<string> Atlas { get; } = new List<string>();
        public Dictionary<string, int> Resonance { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public Dictionary<string, int> Spirits { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public long Crits { get; set; }
        public double MouseBurstCarryPx { get; set; }
        public GearState Gear { get; } = new GearState();
        public HashSet<string> SetCollection { get; } = new HashSet<string>(System.StringComparer.Ordinal);

        public double Focus { get; set; } = 1;
        public double FocusMultiplier { get; set; } = 1;
        public double FocusDropped { get; set; }
        public double RelayFocus { get; set; }
        public int RelayFocusTicks { get; set; }
        public double Resource { get; set; }
        public long KeylessTicks { get; set; }
        public long MouseIdleTicks { get; set; } = long.MaxValue / 2;
        public long ClicklessTicks { get; set; } = long.MaxValue / 2;
        public bool EnemyWeakened { get; set; }
        public double WeakenGoldBonus { get; set; }
        public double WeakenVulnerabilityShare { get; set; }
        public int WeakenVulnerabilityTicks { get; set; }
        public double WishShare { get; set; }
        public BigNumber TotalDamage { get; set; }
        public DamageWindow DamageWindow { get; } = new DamageWindow();
        public double BossTimerRate { get; set; } = 1;
        public bool BossTimerBonusUsed { get; set; }
        public bool FocusRefill { get; set; }
        public BigNumber WishStored { get; set; }
        public double EnemyGoldBonus { get; set; }
        public int SureCritsLeft { get; set; }
        public bool SureCritsArmed { get; set; }
        public long WeakenedKills { get; set; }
        public double LastWeakenedKillSeconds { get; set; }
        public MouseWindow MouseWindow { get; } = new MouseWindow();
        public bool Reading { get; set; }
        public long IdleTicks { get; set; }
        public long IdleTicksBeforeInput { get; set; }
        public long MacroRhythmTicks { get; set; }

        public List<SkillSlot> Skills { get; } = new List<SkillSlot>();
        public int ActivePreset { get; set; }
        public InputStats TotalInput { get; } = new InputStats();

        private BuildPreset? _otherPreset;

        public BuildPreset OtherPreset
        {
            get => _otherPreset ??= new BuildPreset(new ClassTreeAllocation(ClassName));
            set => _otherPreset = value;
        }
        public List<GemDef> OwnedGems { get; } = new List<Content.Gems.GemDef>();
        public Dictionary<string, int> BoundGems { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public Dictionary<string, GemProgress> GemProgress { get; } = new Dictionary<string, GemProgress>(System.StringComparer.Ordinal);

        public GemProgress ProgressOf(GemDef gem)
        {
            if (!GemProgress.TryGetValue(gem.Id, out var progress))
            {
                progress = new GemProgress();
                GemProgress.Add(gem.Id, progress);
            }
            return progress;
        }

        public int LevelOf(GemDef gem) => GemProgress.TryGetValue(gem.Id, out var progress) ? progress.Level : 1;
        public BigNumber[] QueueDamage { get; } = new BigNumber[QueueDepth];

        public BigNumber ChainDamage
        {
            get => QueueDamage[0];
            set => QueueDamage[0] = value;
        }

        public double WoundedSpawnResource { get; set; }
        public double PendingResource { get; set; }
        public int AllyHitStatTicks { get; set; }
        public int PartyAllies { get; set; }
        public long PartyWaveStage { get; set; }
        public int PartyEnemiesOnStage { get; set; }
        public long WaveStage => PartyWaveStage > 0 ? PartyWaveStage : Stage;
        public bool SharedEnemy { get; set; }
        public double ReservedResource { get; set; }
        public int DecayHoldTicks { get; set; }
        public double BossTimerCarry { get; set; }
        public int FocusLockTicks { get; set; }
        public long Kills { get; set; }
        public long BossKills { get; set; }
        public int PartyMembers { get; set; } = 1;
        public double NextKillReward { get; set; } = 1;
        public double NextKillShardChance { get; set; }
        public double NextKillShards { get; set; }
        public long BossStarts { get; set; }
        public int HitsOnEnemy { get; set; }
        public int BossTimerStopTicks { get; set; }
        public int CannonShotsOnEnemy { get; set; }
        public bool WandererBoss { get; set; }
        public int GoldenTicks { get; set; }
        public BigNumber GoldenNormalHp { get; set; }
        public BigNumber GoldenDamage { get; set; }
        public int Barrels { get; set; }
        public int BarrelTicks { get; set; }
        public double BarrelMultiplier { get; set; }
        public int KillStreak { get; set; }
        public long TicksSinceKill { get; set; }
        public int AfterIdleTicks { get; set; }
        public int CurseFlicks { get; set; }
        public long EnemySpawnTick { get; private set; }
        public double BankedSeconds { get; set; }
        public MinionState[] Minions { get; } = new MinionState[MinionSystem.Capacity];
        public double BroadsidePx { get; set; }
        public double PoisonStacks { get; set; }
        public double BurnStacks { get; set; }
        public double DotCarry { get; set; }
        public double StirTurns { get; set; }
        public int OverheatTicks { get; set; }
        public int WeakenedSpawns { get; set; }
        public double BannerPx { get; set; }
        public double BannerBonus { get; set; }
        public int BannerDecayTicks { get; set; }
        public double WeakenShare { get; set; }
        public double AllyHeavyCastSeconds { get; set; }
        public double ResourceFill { get; set; }
        public double KeyHitRate { get; set; }
        public double MouseHitRate { get; set; }
        public long SkillCasts { get; set; }
        public double AutoCastKeys { get; set; }
        public long InputStreakTicks { get; set; }
        public HashSet<long> GatesCleared { get; } = new HashSet<long>();
        public int MentorCoopPoints { get; set; }
        public int ServerCoopHelpPoints { get; set; }
        public int CoopHelpPoints => System.Math.Max(MentorCoopPoints, ServerCoopHelpPoints);
        public bool EarlyBookSlot { get; set; }
        public ClanBonusState ClanBonus { get; } = new ClanBonusState();
        public BigNumber EnemyShieldFloor { get; set; }
        public DungeonState Dungeons { get; } = new DungeonState();
        public double IdleHitCarry { get; set; }
        public double CompanionHitCarry { get; set; }
        public int ActiveAuras { get; set; }
        public BigNumber Doom { get; set; }
        public int DoomTicks { get; set; }
        public BigNumber EchoDamage { get; set; }
        public int EchoTicks { get; set; }
        public double AuraKeyCarry { get; set; }
        public double HeadbangPx { get; set; }
        public double ClickHitCarry { get; set; }

        public void AddToQueue(BigNumber damage)
        {
            if (damage.Sign <= 0)
                return;
            for (var i = 0; i < QueueDamage.Length; i++)
                QueueDamage[i] += damage;
        }

        public void ClearQueue()
        {
            for (var i = 0; i < QueueDamage.Length; i++)
                QueueDamage[i] = BigNumber.Zero;
            WoundedSpawnResource = 0;
        }
        public HashSet<string> TutorialDone { get; } = new HashSet<string>(32, System.StringComparer.Ordinal);
        public bool TutorialSkipped { get; set; }

        public long KeyHits { get; set; }
        public long KeyStreak { get; set; }
        public double SupportKeyBonus { get; set; } = 1;
        public double SupportMouseBonus { get; set; } = 1;
        public double SupportStackCap { get; set; } = 1;
        public double VulnerabilityShare { get; set; }
        public int VulnerabilityTicks { get; set; }
        public int Combo { get; set; }
        public long LastComboStrikeTick { get; set; } = -1;
        public long ComboIdleTicks { get; set; }
        public int ComboTowardStrike { get; set; }
        public int KillsTowardRise { get; set; }
        public int KeysTowardShield { get; set; }
        public int StunTicks { get; set; }
        public double ClockworkSlow { get; set; }
        public double ClockworkFloor { get; set; } = 1;

        public double KeyCarry { get; set; }
        public double MouseCarryPx { get; set; }

        public double ElapsedSeconds => Tick * GameLoop.TickSeconds;
        public bool EnemyAlive => EnemyHp.Sign > 0;
        public bool FightingBoss => EnemyType != EnemyType.Normal;
        public double EnemyLifeSeconds => (Tick - EnemySpawnTick) * GameLoop.TickSeconds;
        public double BossSecondsLeft => BossTicksLeft * GameLoop.TickSeconds;

        public void SpawnEnemy(EnemyDef? enemy, EnemyType type, BigNumber hp)
        {
            Enemy = enemy;
            EnemyType = type;
            EnemyHp = hp;
            EnemyMaxHp = hp;
            HitsOnEnemy = 0;
            StunTicks = 0;
            BossTimerStopTicks = 0;
            CannonShotsOnEnemy = 0;
            WandererBoss = false;
            GoldenTicks = 0;
            GoldenDamage = BigNumber.Zero;
            BossTimerBonusUsed = false;
            EnemySpawnTick = Tick;
            PoisonStacks = 0;
            BurnStacks = 0;
            DotCarry = 0;
            Doom = BigNumber.Zero;
            DoomTicks = 0;
            VulnerabilityShare = 0;
            VulnerabilityTicks = 0;
            EnemyShieldFloor = BigNumber.Zero;
        }
    }
}
