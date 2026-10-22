using System;

namespace Game.Core.Stats
{
    public readonly struct Modifier : IEquatable<Modifier>
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

        public bool Equals(Modifier other) => Stat == other.Stat && Kind == other.Kind && Value.Equals(other.Value) && Tags == other.Tags;
        public override bool Equals(object? obj) => obj is Modifier other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Stat, Kind, Value, Tags);
        public static bool operator ==(Modifier a, Modifier b) => a.Equals(b);
        public static bool operator !=(Modifier a, Modifier b) => !a.Equals(b);
    }
}
