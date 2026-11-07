namespace Game.Core.Sim
{
    public sealed class MountState
    {
        public int[] Levels { get; } = new int[Game.Core.Content.MountRules.Slots];
        public double Scan { get; set; }
        public const int Tiers = 5;

        public long Cutters { get; set; }
        public int FreeRebuilds { get; set; }
        public string[] Facets { get; } = NewFacets();

        private static string[] NewFacets()
        {
            var facets = new string[Game.Core.Content.MountRules.Slots * Tiers];
            for (var i = 0; i < facets.Length; i++)
                facets[i] = string.Empty;
            return facets;
        }
    }
}
