namespace Game.Core.Content.Slates
{
    public enum SlateRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public static class SlateRarityNames
    {
        private static readonly string[] Names = { "common", "rare", "epic", "legendary" };

        public static int Count => Names.Length;

        public static string Of(SlateRarity rarity) => Names[(int)rarity];

        public static bool TryParse(string name, out SlateRarity rarity)
        {
            for (var i = 0; i < Names.Length; i++)
            {
                if (Names[i] == name)
                {
                    rarity = (SlateRarity)i;
                    return true;
                }
            }
            rarity = SlateRarity.Common;
            return false;
        }
    }
}
