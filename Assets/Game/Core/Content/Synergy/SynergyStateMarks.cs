namespace Game.Core.Content.Synergy
{
    public readonly struct SynergyStateMarks
    {
        public const string PoisonField = "poisonOnEnemy";
        public const string BurnField = "burnOnEnemy";
        public const string MinionsField = "minionsOut";
        public const string StunField = "stunnedEnemy";
        public const string HunterMarkField = "markedEnemy";
        public const string BlindField = "blindedEnemy";
        public const string EvilEyeField = "evilEyeOnEnemy";
        public const string ColdField = "coldOnEnemy";
        public const string BloodField = "bloodOnEnemy";
        public const string BondField = "bondsUp";

        public SynergyStateMarks(int poison, int burn, int minions, int stun = 0, int hunterMark = 0, int blind = 0, int evilEye = 0, int cold = 0, int blood = 0, int bond = 0)
        {
            Cold = cold;
            Blood = blood;
            Bond = bond;
            EvilEye = evilEye;
            Blind = blind;
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
        public int Blind { get; }
        public int EvilEye { get; }
        public int Cold { get; }
        public int Blood { get; }
        public int Bond { get; }
    }
}
