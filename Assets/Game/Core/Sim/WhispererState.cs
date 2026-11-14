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
        public EnemyDef? RememberedBoss { get; set; }
        public long Names { get; set; }
        public int SilentHits { get; set; }
        public int QueueHits { get; set; }
        public double Grow { get; set; }
        public int BreakTicks { get; set; }
        public int BreakCooldownTicks { get; set; }
        public long HushesSent { get; set; }
        public long HushHitsBack { get; set; }
        public const int MaxCurses = 4;

        public double[] CurseSlots { get; } = new double[MaxCurses];
        public int[] CurseSlotTicks { get; } = new int[MaxCurses];
        public int[] CurseSlotGems { get; } = new int[MaxCurses];
        public bool SpawnPending { get; set; }

        public double Curse
        {
            get
            {
                var total = 0.0;
                for (var i = 0; i < MaxCurses; i++)
                {
                    if (CurseSlotTicks[i] > 0)
                        total += CurseSlots[i];
                }
                return total;
            }
        }

        public int CurseTicks
        {
            get
            {
                var longest = 0;
                for (var i = 0; i < MaxCurses; i++)
                    longest = System.Math.Max(longest, CurseSlotTicks[i]);
                return longest;
            }
        }

        public int CursesLaid
        {
            get
            {
                var count = 0;
                for (var i = 0; i < MaxCurses; i++)
                {
                    if (CurseSlotTicks[i] > 0)
                        count++;
                }
                return count;
            }
        }

        public void LayCurse(int gem, double strength, int ticks, int limit)
        {
            var slot = CurseSlotFor(gem, System.Math.Max(1, System.Math.Min(MaxCurses, limit)));
            var held = CurseSlotTicks[slot] > 0 ? CurseSlots[slot] : 0;
            CurseSlots[slot] = System.Math.Max(held, strength);
            CurseSlotTicks[slot] = System.Math.Max(CurseSlotTicks[slot], ticks);
            CurseSlotGems[slot] = gem;
        }

        public void ExtendCurses(int ticks)
        {
            for (var i = 0; i < MaxCurses; i++)
            {
                if (CurseSlotTicks[i] > 0)
                    CurseSlotTicks[i] += ticks;
            }
        }

        public void FadeCurses()
        {
            for (var i = 0; i < MaxCurses; i++)
            {
                if (CurseSlotTicks[i] > 0 && --CurseSlotTicks[i] == 0)
                    ClearCurse(i);
            }
        }

        public void ClearCurses()
        {
            for (var i = 0; i < MaxCurses; i++)
            {
                CurseSlotTicks[i] = 0;
                ClearCurse(i);
            }
        }

        private void ClearCurse(int slot)
        {
            CurseSlots[slot] = 0;
            CurseSlotGems[slot] = 0;
        }

        private int CurseSlotFor(int gem, int limit)
        {
            for (var i = 0; i < limit; i++)
            {
                if (CurseSlotTicks[i] > 0 && CurseSlotGems[i] == gem)
                    return i;
            }
            for (var i = 0; i < limit; i++)
            {
                if (CurseSlotTicks[i] <= 0)
                    return i;
            }
            var weakest = 0;
            for (var i = 1; i < limit; i++)
            {
                if (CurseSlots[i] < CurseSlots[weakest])
                    weakest = i;
            }
            return weakest;
        }

        public void OnSpawn()
        {
            SpawnPending = true;
        }
    }
}
