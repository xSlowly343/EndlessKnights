namespace Game.Core.Sim
{
    public sealed class GhostcaptainState
    {
        public int Loaded { get; set; }
        public int LoadHits { get; set; }
        public bool Mixed { get; set; }
        public long Volleys { get; set; }
        public int BreachTicks { get; set; }
        public int BreachResendTicks { get; set; }
        public int Sailors { get; set; }
        public int BossSailors { get; set; }
        public int DawnTicks { get; set; }
        public double CrewCarry { get; set; }
        public long Dawns { get; set; }
        public int MateTicks { get; set; }
        public double MatePower { get; set; }
        public long MateShots { get; set; }
        public int ParrotCasts { get; set; }
        public long Repeats { get; set; }
        public double LastSkillPower { get; set; }

        public void OnSpawn()
        {
            if (Loaded > 0)
                Mixed = true;
        }
    }
}
