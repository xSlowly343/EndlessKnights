namespace Game.Core.Stats
{
    public readonly struct Modifier
    {
        public readonly StatId Stat;
        public readonly ModifierKind Kind;
        public readonly double Value;
        public readonly StatTags Tags;

        public Modifier(StatId stat, ModifierKind kind, double value, StatTags tags = StatTags.None)
        {
            Stat = stat;
            Kind = kind;
            Value = value;
            Tags = tags;
        }

        public bool AppliesTo(StatId stat, StatTags tags) => Stat == stat && (Tags & tags) == Tags;
    }
}
