namespace Game.Core.Sim
{
    public sealed class RunedancerState
    {
        public const int HitWindowTicks = 10;
        public const int CritWindowTicks = 100;

        private readonly double[] _hits = new double[HitWindowTicks];
        private readonly int[] _crits = new int[CritWindowTicks];
        private int _hitIndex;
        private int _critIndex;

        public double Runes { get; set; }
        public int RuneIdleTicks { get; set; }
        public int DoubleRunesLeft { get; set; }
        public bool WordOnEnemy { get; set; }
        public long Words { get; set; }
        public int SealTicks { get; set; }
        public double SealShare { get; set; }
        public double BladeProgress { get; set; }
        public int Blades { get; set; }
        public int BladeFallTicks { get; set; }
        public int WhirlBlades { get; set; }
        public int WhirlLingerTicks { get; set; }
        public int WhirlSpinTicks { get; set; }
        public double WhirlCarry { get; set; }
        public double ShadowCarry { get; set; }
        public bool NewEnemy { get; set; }
        public double RunesBefore { get; set; }
        public int BladesBefore { get; set; }
        public double HitsThisTick { get; set; }
        public int CritsThisTick { get; set; }
        public double HitsLastSecond { get; private set; }

        public void OnSpawn()
        {
            RunesBefore = Runes;
            BladesBefore = Blades;
            Runes = 0;
            Blades = 0;
            BladeProgress = 0;
            WordOnEnemy = false;
            SealTicks = 0;
            NewEnemy = true;
        }

        public void PushHits()
        {
            HitsLastSecond += HitsThisTick - _hits[_hitIndex];
            _hits[_hitIndex] = HitsThisTick;
            _hitIndex = (_hitIndex + 1) % HitWindowTicks;
            HitsThisTick = 0;
            PushCrits();
            if (HitsLastSecond < 1e-9)
                HitsLastSecond = 0;
        }

        public int Crits(int ticks)
        {
            var sum = 0;
            for (var i = 1; i <= ticks && i <= CritWindowTicks; i++)
                sum += _crits[(_critIndex - i + CritWindowTicks) % CritWindowTicks];
            return sum;
        }

        private void PushCrits()
        {
            _crits[_critIndex] = CritsThisTick;
            _critIndex = (_critIndex + 1) % CritWindowTicks;
            CritsThisTick = 0;
        }
    }
}
