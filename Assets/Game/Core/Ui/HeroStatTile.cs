namespace Game.Core.Ui
{
    public readonly struct HeroStatTile
    {
        public readonly string Label;
        public readonly string Value;
        public readonly HeroStatAccent Accent;

        public HeroStatTile(string label, string value, HeroStatAccent accent = HeroStatAccent.Plain)
        {
            Label = label;
            Value = value;
            Accent = accent;
        }
    }
}
