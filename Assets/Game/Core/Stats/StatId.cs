using System;

namespace Game.Core.Stats
{
    public readonly struct StatId : IEquatable<StatId>
    {
        public readonly string Name;

        public StatId(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Stat name must not be empty.", nameof(name));
            Name = name;
        }

        public bool Equals(StatId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StatId other && Equals(other);
        public override int GetHashCode() => Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name);
        public override string ToString() => Name;

        public static bool operator ==(StatId a, StatId b) => a.Equals(b);
        public static bool operator !=(StatId a, StatId b) => !a.Equals(b);
    }
}
