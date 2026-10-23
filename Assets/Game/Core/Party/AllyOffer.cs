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

        public void Clear()
        {
            Present = false;
            System.Array.Clear(Stats, 0, Stats.Length);
            for (var i = 0; i < Auras.Length; i++)
                Auras[i].Clear();
        }
    }
}
