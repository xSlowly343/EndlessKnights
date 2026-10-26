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
    }
}
