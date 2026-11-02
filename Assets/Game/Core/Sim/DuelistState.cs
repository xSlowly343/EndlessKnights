namespace Game.Core.Sim
{
    public sealed class DuelistState
    {
        public const int HitWindowTicks = 10;

        private readonly double[] _hits = new double[HitWindowTicks];
        private int _hitIndex;

        public int BreachWaitTicks { get; set; }
        public int BreachOpenTicks { get; set; }
        public int BreachPressTicks { get; set; }
        public int LungesInBreach { get; set; }
        public long Lunges { get; set; }
        public int TouchProgress { get; set; }
        public int Marks { get; set; }
        public int MarksBefore { get; set; }
        public int CarryMarks { get; set; }
        public long Touches { get; set; }
        public int PhantomSwitchTicks { get; set; }
        public int PhantomLingerTicks { get; set; }
        public double PhantomPauseCarry { get; set; }
        public bool PhantomFighting { get; set; }
        public bool NewEnemy { get; set; }
        public long Spawns { get; private set; }
        public double HitsThisTick { get; set; }
        public double HitsLastSecond { get; private set; }

        public bool BreachOpen => BreachOpenTicks > 0;

        public void OnSpawn()
        {
            MarksBefore = Marks;
            Marks = 0;
            TouchProgress = 0;
            NewEnemy = true;
            Spawns++;
        }

        public void PushHits()
        {
            HitsLastSecond += HitsThisTick - _hits[_hitIndex];
            _hits[_hitIndex] = HitsThisTick;
            _hitIndex = (_hitIndex + 1) % HitWindowTicks;
            HitsThisTick = 0;
            if (HitsLastSecond < 1e-9)
                HitsLastSecond = 0;
        }
    }
}
