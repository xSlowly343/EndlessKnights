using System;

namespace Game.Core.Math
{
    public sealed class Rng
    {
        private const double DoubleUnit = 1.0 / (1UL << 53);

        private ulong _state0;
        private ulong _state1;

        public Rng(ulong seed)
        {
            var mix = seed;
            _state0 = SplitMix64(ref mix);
            _state1 = SplitMix64(ref mix);
            if (_state0 == 0 && _state1 == 0)
                _state1 = 1;
        }

        public (ulong State0, ulong State1) State => (_state0, _state1);

        public static Rng FromState(ulong state0, ulong state1)
        {
            if (state0 == 0 && state1 == 0)
                throw new ArgumentException("State must not be all zeros.");
            var rng = new Rng(0);
            rng._state0 = state0;
            rng._state1 = state1;
            return rng;
        }

        public ulong NextULong()
        {
            var s1 = _state0;
            var s0 = _state1;
            var result = s0 + s1;
            _state0 = s0;
            s1 ^= s1 << 23;
            _state1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return result;
        }

        public double NextDouble() => (NextULong() >> 11) * DoubleUnit;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Range must not be empty.");
            var range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % range));
        }

        public bool Chance(double probability) => NextDouble() < probability;

        private static ulong SplitMix64(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
