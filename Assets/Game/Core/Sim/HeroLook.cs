namespace Game.Core.Sim
{
    public readonly struct HeroLook : System.IEquatable<HeroLook>
    {
        public HeroLook(byte cloak, byte eyes, byte strike)
        {
            Cloak = cloak;
            Eyes = eyes;
            Strike = strike;
        }

        public byte Cloak { get; }
        public byte Eyes { get; }
        public byte Strike { get; }

        public bool Equals(HeroLook other) => Cloak == other.Cloak && Eyes == other.Eyes && Strike == other.Strike;

        public override bool Equals(object? obj) => obj is HeroLook other && Equals(other);

        public override int GetHashCode() => Cloak | (Eyes << 8) | (Strike << 16);

        public static bool operator ==(HeroLook left, HeroLook right) => left.Equals(right);

        public static bool operator !=(HeroLook left, HeroLook right) => !left.Equals(right);
    }
}
