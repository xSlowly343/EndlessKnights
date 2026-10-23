namespace Game.Core.Party
{
    public struct EnemyEffect
    {
        public int Id;
        public double DamageTaken;
        public int TicksLeft;

        public bool Active => TicksLeft > 0;
    }
}
