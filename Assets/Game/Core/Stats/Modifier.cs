using System;

namespace Game.Core.Stats
{
    public readonly struct Modifier : IEquatable<Modifier>
    {
        public readonly StatId Stat;
        public readonly ModifierKind Kind;
        public readonly double Value;
        public readonly StatTags Tags;
        public readonly ModifierPool Pool;
        public readonly ModifierCondition When;

        public Modifier(StatId stat, ModifierKind kind, double value, StatTags tags = StatTags.None, ModifierPool pool = ModifierPool.General, ModifierCondition when = ModifierCondition.Always)
        {
            When = when;
            Stat = stat;
            Kind = kind;
            Value = value;
            Tags = tags;
            Pool = pool;
        }

        public Modifier InPool(ModifierPool pool) => new Modifier(Stat, Kind, Value, Tags, pool, When);

        public Modifier WithValue(double value) => new Modifier(Stat, Kind, value, Tags, Pool, When);

        public bool ActiveUnder(ulong conditions) => When == ModifierCondition.Always || (conditions & ModifierConditions.Bit(When)) != 0;

        public bool AppliesTo(StatId stat, StatTags tags) => Stat == stat && (Tags & tags) == Tags;

        public bool Equals(Modifier other) => Stat == other.Stat && Kind == other.Kind && Value.Equals(other.Value) && Tags == other.Tags && Pool == other.Pool && When == other.When;
        public override bool Equals(object? obj) => obj is Modifier other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Stat, Kind, Value, Tags, Pool, When);
        public static bool operator ==(Modifier a, Modifier b) => a.Equals(b);
        public static bool operator !=(Modifier a, Modifier b) => !a.Equals(b);
    }
}
