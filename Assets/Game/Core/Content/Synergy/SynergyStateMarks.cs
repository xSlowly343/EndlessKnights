namespace Game.Core.Content.Synergy
{
    public readonly struct SynergyStateMarks
    {
        public const string PoisonField = "poisonOnEnemy";
        public const string BurnField = "burnOnEnemy";
        public const string MinionsField = "minionsOut";

        public SynergyStateMarks(int poison, int burn, int minions)
        {
            Poison = poison;
            Burn = burn;
            Minions = minions;
        }

        public int Poison { get; }
        public int Burn { get; }
        public int Minions { get; }
    }
}
