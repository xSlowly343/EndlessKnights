using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class WhispererState
    {
        public const int MaxKnots = 7;

        public int Knots { get; set; }
        public double KnotHits { get; set; }
        public long KnotsTied { get; set; }
        public int BossHits { get; set; }
        public bool Named { get; set; }
        public int Syllables { get; set; }
        public EnemyDef? RememberedBoss { get; set; }
        public long Names { get; set; }
        public int CallTicks { get; set; }
        public int SilentHits { get; set; }
        public int QueueHits { get; set; }
        public double Grow { get; set; }
        public int BreakTicks { get; set; }
        public int BreakCooldownTicks { get; set; }
        public long HushesSent { get; set; }
        public long HushHitsBack { get; set; }
        public double Curse { get; set; }
        public int CurseTicks { get; set; }
        public bool SpawnPending { get; set; }
        public bool AllySpawnPending { get; set; }

        public void OnSpawn()
        {
            SpawnPending = true;
            AllySpawnPending = true;
        }
    }
}
