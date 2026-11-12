namespace Game.Core.Ui
{
    public enum AuraTheme
    {
        Might,
        Edge,
        Hunt,
        Flow,
        Fortune,
        Hex,
        Kin,
        Calm
    }

    public static class AuraThemes
    {
        public const int Count = 8;

        private static readonly string[] Keys = { "might", "edge", "hunt", "flow", "fortune", "hex", "kin", "calm" };

        public static string Key(AuraTheme theme) => Keys[(int)theme];

        public static bool TryParse(string key, out AuraTheme theme)
        {
            for (var i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] != key)
                    continue;
                theme = (AuraTheme)i;
                return true;
            }
            theme = AuraTheme.Might;
            return false;
        }
    }
}
