namespace Game.Core.Math
{
    public static class SplitMix
    {
        public static ulong At(ulong seed, ulong index)
        {
            var z = seed + 0x9E3779B97F4A7C15UL * index;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public static ulong After(ulong value)
        {
            var z = (value ^ (value >> 31) ^ (value >> 62)) * 0x319642B2D24D8EC3UL;
            z = (z ^ (z >> 27) ^ (z >> 54)) * 0x96DE1B173F119089UL;
            z = z ^ (z >> 30) ^ (z >> 60);
            return At(z, 1);
        }
    }
}
