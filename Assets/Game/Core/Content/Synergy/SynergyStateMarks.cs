namespace Game.Core.Content.Synergy
{
    public readonly struct SynergyStateMarks
    {
        public const string PoisonField = "poisonOnEnemy";
        public const string MinionsField = "minionsOut";
        public const string BloodField = "brewOnEnemy";
        public const string BondField = "pollenOnHero";

        public SynergyStateMarks(int poison, int minions, int blood = 0, int bond = 0)
        {
            Poison = poison;
            Minions = minions;
            Blood = blood;
            Bond = bond;
        }

        public int Poison { get; }
        public int Minions { get; }
        public int Blood { get; }
        public int Bond { get; }
    }
}
