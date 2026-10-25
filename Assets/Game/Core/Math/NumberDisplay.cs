namespace Game.Core.Math
{
    public static class NumberDisplay
    {
        public static NumberFormatSettings Settings { get; private set; } = NumberFormatSettings.Default;

        public static void Use(NumberFormatSettings settings) => Settings = settings;
    }
}
