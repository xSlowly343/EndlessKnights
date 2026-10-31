using System;

namespace Game.Core.Stats
{
    public readonly struct StatId : IEquatable<StatId>
    {
        public const char SkillSeparator = ':';

        public readonly string Name;

        public StatId(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Stat name must not be empty.", nameof(name));
            Name = name;
        }

        public static StatId ForSkill(StatId stat, string skillId) => new StatId(stat.Name + SkillSeparator + skillId);

        public bool IsForSkill => Name.IndexOf(SkillSeparator) >= 0;
        public string BaseName => IsForSkill ? Name.Substring(0, Name.IndexOf(SkillSeparator)) : Name;
        public string SkillId => IsForSkill ? Name.Substring(Name.IndexOf(SkillSeparator) + 1) : string.Empty;

        public bool Equals(StatId other) => string.Equals(Name, other.Name, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is StatId other && Equals(other);
        public override int GetHashCode() => Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name);
        public override string ToString() => Name;

        public static bool operator ==(StatId a, StatId b) => a.Equals(b);
        public static bool operator !=(StatId a, StatId b) => !a.Equals(b);
    }
}
