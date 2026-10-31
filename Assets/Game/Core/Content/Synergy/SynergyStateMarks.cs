namespace Game.Core.Content.Synergy
{
    public readonly struct SynergyStateMarks
    {
        public const string PoisonField = "poisonOnEnemy";
        public const string BurnField = "burnOnEnemy";
        public const string MinionsField = "minionsOut";
        public const string StunField = "stunnedEnemy";
        public const string HunterMarkField = "markedEnemy";

        public SynergyStateMarks(int poison, int burn, int minions, int stun = 0, int hunterMark = 0)
        {
            HunterMark = hunterMark;
            Stun = stun;
            Poison = poison;
            Burn = burn;
            Minions = minions;
        }

        public int Poison { get; }
        public int Burn { get; }
        public int Minions { get; }
        public int Stun { get; }
        public int HunterMark { get; }
    }
}
