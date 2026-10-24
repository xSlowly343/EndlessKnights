namespace Game.Core.Ui
{
    public readonly struct HeroHand
    {
        public readonly string Label;
        public readonly string Name;
        public readonly string Rarity;
        public readonly string? Color;

        public HeroHand(string label, string name, string rarity, string? color)
        {
            Label = label;
            Name = name;
            Rarity = rarity;
            Color = color;
        }

        public bool IsEmpty => Color == null;
    }
}
