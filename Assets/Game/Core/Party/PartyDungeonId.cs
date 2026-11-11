namespace Game.Core.Party
{
    public static class PartyDungeonId
    {
        public const long MaxStage = (1L << 24) - 1;
        public const int MaxTier = 255;
        public const int MaxModifiers = 12;
        public const int MaxStages = 7;
        public const int MaxAttempts = 7;

        public static long Pack(long stage, int tier, int modifierMask, int owner, int stages, int attempts) =>
            System.Math.Min(MaxStage, System.Math.Max(1, stage))
            | ((long)System.Math.Min(MaxTier, System.Math.Max(1, tier)) << 24)
            | ((long)(modifierMask & ((1 << MaxModifiers) - 1)) << 32)
            | ((long)(owner & 3) << 44)
            | ((long)System.Math.Min(MaxStages, System.Math.Max(1, stages)) << 46)
            | ((long)System.Math.Min(MaxAttempts, System.Math.Max(0, attempts)) << 49);

        public static long Stage(long id) => id & MaxStage;
        public static int Tier(long id) => (int)((id >> 24) & MaxTier);
        public static int ModifierMask(long id) => (int)((id >> 32) & ((1 << MaxModifiers) - 1));
        public static int Owner(long id) => (int)((id >> 44) & 3);
        public static int Stages(long id) => System.Math.Max(1, (int)((id >> 46) & MaxStages));
        public static int Attempts(long id) => (int)((id >> 49) & MaxAttempts);
    }
}
