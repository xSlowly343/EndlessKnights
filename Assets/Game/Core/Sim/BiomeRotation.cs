using Game.Core.Content;

namespace Game.Core.Sim
{
    public static class BiomeRotation
    {
        public static string Current(StageTable stages, GameState state, long stage)
        {
            if (stages.InHeart(stage) || !stages.Rotates)
                return stages.Biome(stage);
            return IsRotating(stages, state.Biome) ? state.Biome : stages.Biome(stage);
        }

        public static void Start(StageTable stages, GameState state)
        {
            if (!stages.Rotates || IsRotating(stages, state.Biome))
                return;
            state.Biome = stages.Biome(state.Stage);
            state.PreviousBiome = string.Empty;
            state.BiomeSinceTick = state.Tick;
        }

        public static long TicksPerTurn(StageTable stages) => (long)System.Math.Round(stages.RotationMinutes * 60 * GameLoop.TicksPerSecond);

        public static bool TryTurn(StageTable stages, GameState state)
        {
            if (!stages.Rotates || stages.InHeart(state.Stage))
                return false;
            Start(stages, state);
            if (state.Tick - state.BiomeSinceTick < TicksPerTurn(stages))
                return false;
            var rotation = stages.Rotation;
            var choices = 0;
            for (var i = 0; i < rotation.Count; i++)
            {
                if (Allowed(rotation[i], state))
                    choices++;
            }
            var pick = state.Rng.NextInt(0, choices);
            for (var i = 0; i < rotation.Count; i++)
            {
                if (!Allowed(rotation[i], state))
                    continue;
                if (pick-- > 0)
                    continue;
                NoteLeft(rotation, state);
                state.BiomeFresh = LongestAway(rotation, state, i);
                state.PreviousBiome = state.Biome;
                state.Biome = rotation[i];
                state.BiomeSinceTick = state.Tick;
                return true;
            }
            return false;
        }

        public static int Index(StageTable stages, GameState state, long stage)
        {
            if (!stages.Rotates || stages.InHeart(stage))
                return -1;
            var biome = Current(stages, state, stage);
            var rotation = stages.Rotation;
            for (var i = 0; i < rotation.Count; i++)
            {
                if (rotation[i] == biome)
                    return i;
            }
            return -1;
        }

        private static void NoteLeft(System.Collections.Generic.IReadOnlyList<string> rotation, GameState state)
        {
            for (var i = 0; i < rotation.Count && i < state.BiomeLeftTicks.Length; i++)
            {
                if (rotation[i] == state.Biome)
                    state.BiomeLeftTicks[i] = state.Tick;
            }
        }

        private static bool LongestAway(System.Collections.Generic.IReadOnlyList<string> rotation, GameState state, int next)
        {
            if (next >= state.BiomeLeftTicks.Length)
                return false;
            var left = state.BiomeLeftTicks;
            for (var i = 0; i < rotation.Count && i < left.Length; i++)
            {
                if (i != next && rotation[i] != state.Biome && left[i] < left[next])
                    return false;
            }
            return true;
        }

        private static bool Allowed(string biome, GameState state) => biome != state.Biome && biome != state.PreviousBiome;

        private static bool IsRotating(StageTable stages, string biome)
        {
            if (biome.Length == 0)
                return false;
            var rotation = stages.Rotation;
            for (var i = 0; i < rotation.Count; i++)
            {
                if (rotation[i] == biome)
                    return true;
            }
            return false;
        }
    }
}
