namespace Game.Core.Content.Items
{
    public readonly struct RarityRule
    {
        public readonly int Affixes;
        public readonly int TopTiers;

        public RarityRule(int affixes, int topTiers)
        {
            Affixes = affixes;
            TopTiers = topTiers;
        }
    }
}
