using System.Collections.Generic;
using Game.Core.Build;
using Game.Core.Content;
using Game.Core.Content.Gems;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class GameState
    {
        public GameState(ulong seed = 1)
        {
            Rng = new Rng(seed);
        }

        public long Tick { get; set; }
        public Rng Rng { get; set; }
        public HeroClass Class { get; set; }

        private TreeAllocation? _tree;

        public TreeAllocation Tree
        {
            get => _tree ??= new TreeAllocation(HeroClassNames.Of(Class));
            set => _tree = value;
        }

        public long Stage { get; set; } = 1;
        public long HighestStage { get; set; } = 1;
        public long RecordStage { get; set; } = 1;
        public int HeroLevel { get; set; } = 1;
        public double HeroXp { get; set; }
        public int KillsOnStage { get; set; }
        public int BossTicksLeft { get; set; }
        public bool Farming { get; set; }
        public int FarmKills { get; set; }
        public int BossFailsInARow { get; set; }

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

        public double Focus { get; set; } = 1;
        public double FocusMultiplier { get; set; } = 1;
        public double Energy { get; set; }
        public long IdleTicks { get; set; }
        public long MacroRhythmTicks { get; set; }

        public List<SkillSlot> Skills { get; } = new List<SkillSlot>();
        public int ActivePreset { get; set; }

        private BuildPreset? _otherPreset;

        public BuildPreset OtherPreset
        {
            get => _otherPreset ??= new BuildPreset(new TreeAllocation(HeroClassNames.Of(Class)));
            set => _otherPreset = value;
        }
        public List<GemDef> OwnedGems { get; } = new List<Content.Gems.GemDef>();
        public BigNumber ChainDamage { get; set; }

        public long KeyHits { get; set; }
        public int Combo { get; set; }
        public long ComboIdleTicks { get; set; }
        public int Notes { get; set; }

        public double KeyCarry { get; set; }
        public double ClickCarry { get; set; }
        public double MouseCarryPx { get; set; }

        public double ElapsedSeconds => Tick * GameLoop.TickSeconds;
        public bool EnemyAlive => EnemyHp.Sign > 0;
        public bool FightingBoss => EnemyType != EnemyType.Normal;
        public double BossSecondsLeft => BossTicksLeft * GameLoop.TickSeconds;

        public void SpawnEnemy(EnemyDef? enemy, EnemyType type, BigNumber hp)
        {
            Enemy = enemy;
            EnemyType = type;
            EnemyHp = hp;
            EnemyMaxHp = hp;
        }
    }
}
