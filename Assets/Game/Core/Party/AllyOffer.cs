namespace Game.Core.Party
{
    public sealed class AllyOffer
    {
        public AllyOffer()
        {
            for (var i = 0; i < Auras.Length; i++)
                Auras[i] = new AllyAura();
        }

        public bool Present { get; set; }
        public double[] Stats { get; } = new double[PartyBuffRules.MaxStats];
        public AllyAura[] Auras { get; } = new AllyAura[PartyBuffRules.MaxAuras];
        public double[] Coop { get; } = new double[CoopRules.MaxStats];
        public int HeroClass { get; set; } = -1;

        public void Clear()
        {
            Present = false;
            System.Array.Clear(Stats, 0, Stats.Length);
            System.Array.Clear(Coop, 0, Coop.Length);
            HeroClass = -1;
            for (var i = 0; i < Auras.Length; i++)
                Auras[i].Clear();
        }
    }
}
