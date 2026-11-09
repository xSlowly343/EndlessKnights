using Game.Core.Content;

namespace Game.Core.Sim
{
    public static class BiomeRotation
    {
        public static string Current(StageTable stages, GameState state, long stage)
        {
            if (!stages.RotatesAt(stage))
                return stages.Biome(stage);
            return IsOpen(stages, state.Biome, stage) ? state.Biome : stages.Biome(stage);
        }

        public static void Start(StageTable stages, GameState state)
        {
            if (!stages.RotatesAt(state.Stage) || IsOpen(stages, state.Biome, state.Stage))
                return;
            state.Biome = stages.Biome(state.Stage);
            state.PreviousBiome = string.Empty;
            state.BiomeSinceTick = state.Tick;
        }

        public static long TicksPerTurn(StageTable stages) => (long)System.Math.Round(stages.RotationMinutes * 60 * GameLoop.TicksPerSecond);

        public static bool TryTurn(StageTable stages, GameState state)
        {
            if (!stages.RotatesAt(state.Stage))
                return false;
            Start(stages, state);
            if (state.Tick - state.BiomeSinceTick < TicksPerTurn(stages))
                return false;
            var pool = stages.Pool;
            var choices = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                if (Allowed(stages, i, state))
                    choices++;
            }
            if (choices == 0)
                return false;
            var pick = state.Rng.NextInt(0, choices);
            for (var i = 0; i < pool.Count; i++)
            {
                if (!Allowed(stages, i, state))
                    continue;
                if (pick-- > 0)
                    continue;
                NoteLeft(pool, state);
                state.BiomeFresh = LongestAway(stages, state, i);
                state.PreviousBiome = state.Biome;
                state.Biome = pool[i];
                state.BiomeSinceTick = state.Tick;
                return true;
            }
            return false;
        }

        public static int Index(StageTable stages, GameState state, long stage)
        {
            if (!stages.Rotates)
                return -1;
            var biome = Current(stages, state, stage);
            var pool = stages.Pool;
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] == biome)
                    return i;
            }
            return -1;
        }

        private static void NoteLeft(System.Collections.Generic.IReadOnlyList<string> pool, GameState state)
        {
            for (var i = 0; i < pool.Count && i < state.BiomeLeftTicks.Length; i++)
            {
                if (pool[i] == state.Biome)
                    state.BiomeLeftTicks[i] = state.Tick;
            }
        }

        private static bool LongestAway(StageTable stages, GameState state, int next)
        {
            if (next >= state.BiomeLeftTicks.Length)
                return false;
            var pool = stages.Pool;
            var left = state.BiomeLeftTicks;
            for (var i = 0; i < pool.Count && i < left.Length; i++)
            {
                if (i != next && stages.IsOpen(i, state.Stage) && pool[i] != state.Biome && left[i] < left[next])
                    return false;
            }
            return true;
        }

        private static bool Allowed(StageTable stages, int index, GameState state)
        {
            var biome = stages.Pool[index];
            return stages.IsOpen(index, state.Stage) && biome != state.Biome && biome != state.PreviousBiome;
        }

        private static bool IsOpen(StageTable stages, string biome, long stage)
        {
            if (biome.Length == 0)
                return false;
            var pool = stages.Pool;
            for (var i = 0; i < pool.Count; i++)
            {
                if (pool[i] == biome)
                    return stages.IsOpen(i, stage);
            }
            return false;
        }
    }
}
