namespace Game.Core.Ui
{
    public readonly struct WorldSpan
    {
        public WorldSpan(long index, long startMs, long endMs, int pick, int previousPick)
        {
            Index = index;
            StartMs = startMs;
            EndMs = endMs;
            Pick = pick;
            PreviousPick = previousPick;
        }

        public long Index { get; }
        public long StartMs { get; }
        public long EndMs { get; }
        public int Pick { get; }
        public int PreviousPick { get; }

        public bool Holds(long utcMs) => utcMs >= StartMs && utcMs < EndMs;
    }

    public static class WorldWeather
    {
        private const int SpansPerBlock = 12;
        private const long IndexStride = 64;

        public static WorldSpan At(BiomeLook look, BiomeLifeRules rules, long utcMs) =>
            Find(look.Seed, rules.WeatherMinMs, rules.WeatherMaxMs, look, System.Math.Max(0, utcMs));

        public static WorldSpan RareAt(BiomeLook look, BiomeLifeRules rules, long utcMs) =>
            Find(look.Seed ^ 0x5CE9E5UL, rules.RareMinMs, rules.RareMaxMs, null, System.Math.Max(0, utcMs));

        public static ulong SeedOf(string id)
        {
            var hash = 14695981039346656037UL;
            for (var i = 0; i < id.Length; i++)
                hash = (hash ^ id[i]) * 1099511628211UL;
            return hash;
        }

        private static WorldSpan Find(ulong seed, long minMs, long maxMs, BiomeLook? look, long utcMs)
        {
            var blockMs = maxMs * SpansPerBlock;
            var block = utcMs / blockMs;
            var previous = block > 0 ? Walk(seed, minMs, maxMs, look, block - 1, long.MaxValue, -1).Pick : -1;
            return Walk(seed, minMs, maxMs, look, block, utcMs, previous);
        }

        private static WorldSpan Walk(ulong seed, long minMs, long maxMs, BiomeLook? look, long block, long utcMs, int previous)
        {
            var blockMs = maxMs * SpansPerBlock;
            var start = block * blockMs;
            var end = start + blockMs;
            var at = start;
            var pick = previous;
            for (var i = 0; ; i++)
            {
                var roll = Mix(seed ^ (ulong)(block * IndexStride + i));
                var left = end - at;
                var length = left <= maxMs ? left : minMs + (long)(Unit(roll) * (maxMs - minMs));
                if (left > maxMs && left - length < minMs)
                    length = left - minMs;
                var before = pick;
                pick = look == null ? 0 : look.PickWeather(Unit(Mix(roll)));
                if (utcMs < at + length || at + length >= end)
                    return new WorldSpan(block * IndexStride + i, at, at + length, pick, before);
                at += length;
            }
        }

        private static double Unit(ulong roll) => (roll >> 11) / (double)(1UL << 53);

        private static ulong Mix(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        public static double Scatter(ulong seed, long index, int slot) => Unit(Mix(seed ^ (ulong)(index * 131 + slot + 7)));
    }
}
