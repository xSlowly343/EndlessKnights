using System;
using System.Globalization;

namespace Game.Core.Math
{
    public readonly struct BigNumber : IEquatable<BigNumber>, IComparable<BigNumber>
    {
        private const int NegligibleExponentGap = 17;

        public static readonly BigNumber Zero = new BigNumber(0d, 0L);
        public static readonly BigNumber One = new BigNumber(1d, 0L);

        public readonly double Mantissa;
        public readonly long Exponent;

        private BigNumber(double mantissa, long exponent)
        {
            Mantissa = mantissa;
            Exponent = exponent;
        }

        public bool IsZero => Mantissa == 0d;
        public bool IsNegative => Mantissa < 0d;
        public int Sign => System.Math.Sign(Mantissa);

        public static BigNumber Create(double mantissa, long exponent)
        {
            if (double.IsNaN(mantissa) || double.IsInfinity(mantissa))
                throw new ArgumentOutOfRangeException(nameof(mantissa), mantissa, "Mantissa must be a finite number.");
            if (mantissa == 0d)
                return Zero;

            var shift = (long)System.Math.Floor(System.Math.Log10(System.Math.Abs(mantissa)));
            var scaled = mantissa / System.Math.Pow(10d, shift);
            var normalizedExponent = exponent + shift;

            if (System.Math.Abs(scaled) >= 10d)
            {
                scaled /= 10d;
                normalizedExponent++;
            }
            else if (System.Math.Abs(scaled) < 1d)
            {
                scaled *= 10d;
                normalizedExponent--;
            }

            return new BigNumber(scaled, normalizedExponent);
        }

        public static BigNumber FromDouble(double value) => Create(value, 0L);

        public double ToDouble() => Mantissa * System.Math.Pow(10d, Exponent);

        public double Log10()
        {
            if (IsZero)
                return double.NegativeInfinity;
            if (IsNegative)
                return double.NaN;
            return System.Math.Log10(Mantissa) + Exponent;
        }

        public BigNumber Pow(double power)
        {
            if (double.IsNaN(power) || double.IsInfinity(power))
                throw new ArgumentOutOfRangeException(nameof(power), power, "Power must be a finite number.");
            if (power == 0d)
                return One;
            if (IsZero)
            {
                if (power < 0d)
                    throw new DivideByZeroException("Zero cannot be raised to a negative power.");
                return Zero;
            }
            if (IsNegative)
                throw new ArgumentOutOfRangeException(nameof(power), power, "Negative numbers cannot be raised to a power.");

            var exponentPart = Exponent * power;
            var wholeExponent = System.Math.Floor(exponentPart);
            var fractionalLog = exponentPart - wholeExponent + System.Math.Log10(Mantissa) * power;
            var fractionalWhole = System.Math.Floor(fractionalLog);
            return Create(System.Math.Pow(10d, fractionalLog - fractionalWhole), (long)(wholeExponent + fractionalWhole));
        }

        public static BigNumber Max(BigNumber a, BigNumber b) => a >= b ? a : b;
        public static BigNumber Min(BigNumber a, BigNumber b) => a <= b ? a : b;

        public static implicit operator BigNumber(double value) => FromDouble(value);

        public static BigNumber operator -(BigNumber value) => new BigNumber(-value.Mantissa, value.Exponent);

        public static BigNumber operator +(BigNumber a, BigNumber b)
        {
            if (a.IsZero)
                return b;
            if (b.IsZero)
                return a;

            var larger = a.Exponent >= b.Exponent ? a : b;
            var smaller = a.Exponent >= b.Exponent ? b : a;
            var gap = larger.Exponent - smaller.Exponent;
            if (gap > NegligibleExponentGap)
                return larger;

            return Create(larger.Mantissa + smaller.Mantissa / System.Math.Pow(10d, gap), larger.Exponent);
        }

        public static BigNumber operator -(BigNumber a, BigNumber b) => a + -b;

        public static BigNumber operator *(BigNumber a, BigNumber b)
        {
            if (a.IsZero || b.IsZero)
                return Zero;
            return Create(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);
        }

        public static BigNumber operator /(BigNumber a, BigNumber b)
        {
            if (b.IsZero)
                throw new DivideByZeroException();
            if (a.IsZero)
                return Zero;
            return Create(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);
        }

        public int CompareTo(BigNumber other)
        {
            if (Sign != other.Sign)
                return Sign.CompareTo(other.Sign);
            if (IsZero)
                return 0;
            if (Exponent != other.Exponent)
                return IsNegative ? other.Exponent.CompareTo(Exponent) : Exponent.CompareTo(other.Exponent);
            return Mantissa.CompareTo(other.Mantissa);
        }

        public bool Equals(BigNumber other) => Mantissa.Equals(other.Mantissa) && Exponent == other.Exponent;
        public override bool Equals(object? obj) => obj is BigNumber other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Mantissa, Exponent);

        public static bool operator ==(BigNumber a, BigNumber b) => a.Equals(b);
        public static bool operator !=(BigNumber a, BigNumber b) => !a.Equals(b);
        public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;

        public override string ToString() =>
            Mantissa.ToString("R", CultureInfo.InvariantCulture) + "e" + Exponent.ToString(CultureInfo.InvariantCulture);
    }
}
