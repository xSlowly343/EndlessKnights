using Game.Core.Content;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class GameState
    {
        public long Tick { get; set; }

        public EnemyDef? Enemy { get; private set; }
        public EnemyType EnemyType { get; private set; }
        public BigNumber EnemyHp { get; set; }
        public BigNumber EnemyMaxHp { get; private set; }

        public double KeyCarry { get; set; }
        public double ClickCarry { get; set; }
        public double MouseCarryPx { get; set; }

        public double ElapsedSeconds => Tick * GameLoop.TickSeconds;
        public bool EnemyAlive => EnemyHp.Sign > 0;

        public void SpawnEnemy(EnemyDef? enemy, EnemyType type, BigNumber hp)
        {
            Enemy = enemy;
            EnemyType = type;
            EnemyHp = hp;
            EnemyMaxHp = hp;
        }
    }
}
