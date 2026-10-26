namespace Game.Core.Sim
{
    public struct CannonState
    {
        public int TicksLeft;
        public double ShotCarry;
        public double ShotsPerSecond;
        public double Multiplier;
        public Stats.StatTags Tags;

        public bool Standing => TicksLeft > 0;
    }
}
