namespace Game.Core.Party
{
    public static class PartyTrialId
    {
        public const int MaxTrials = 255;
        public const int MaxTier = 65535;

        public static long Pack(int trialIndex, int tier, bool night) =>
            (long)(System.Math.Min(MaxTrials - 1, System.Math.Max(0, trialIndex)) + 1)
            | ((long)System.Math.Min(MaxTier, System.Math.Max(1, tier)) << 8)
            | (night ? 1L << 24 : 0);

        public static int Trial(long id) => (int)(id & MaxTrials) - 1;
        public static int Tier(long id) => System.Math.Max(1, (int)((id >> 8) & MaxTier));
        public static bool Night(long id) => ((id >> 24) & 1) != 0;
        public static bool Valid(long id) => id > 0 && Trial(id) >= 0;
    }
}
