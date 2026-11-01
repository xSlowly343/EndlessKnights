namespace Game.Core.Sim
{
    public sealed class SandwalkerState
    {
        public const int MirageCapacity = 16;

        private readonly int[] _mirageTicks = new int[MirageCapacity];

        public bool Bottom { get; set; }
        public int Grains { get; set; }
        public double StepGrainPx { get; set; }
        public long Flips { get; set; }
        public double RoadPx { get; set; }
        public long Camps { get; set; }
        public int CaravanPartyTicks { get; set; }
        public int CaravanGoldTicks { get; set; }
        public double HazePx { get; set; }
        public int MirageHits { get; set; }
        public int Mirages { get; private set; }
        public int MirageStrikeTicks { get; set; }
        public long MiragesRaised { get; set; }
        public long MiragesFaded { get; set; }
        public int TicksSinceStep { get; set; } = int.MaxValue / 2;
        public double StepSinceCastPx { get; set; }
        public int PartyTimeTicks { get; set; }
        public int PartyStopTicks { get; set; }
        public bool SpawnPending { get; set; }

        public int MirageTicks(int index) => _mirageTicks[index];

        public bool AddMirage(int ticks, int max)
        {
            if (Mirages >= max || Mirages >= MirageCapacity)
                return false;
            _mirageTicks[Mirages++] = ticks;
            return true;
        }

        public int FadeMirages()
        {
            var faded = 0;
            for (var i = Mirages - 1; i >= 0; i--)
            {
                if (--_mirageTicks[i] > 0)
                    continue;
                _mirageTicks[i] = _mirageTicks[--Mirages];
                faded++;
            }
            return faded;
        }

        public void TrimMirages(int max)
        {
            if (Mirages > max)
                Mirages = System.Math.Max(0, max);
        }

        public void ClearMirages() => Mirages = 0;

        public void OnSpawn() => SpawnPending = true;
    }
}
