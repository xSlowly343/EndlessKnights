namespace Game.Core.Content.Items
{
    public readonly struct GemDropChance
    {
        public GemDropChance(double normal, double boss)
        {
            Normal = normal;
            Boss = boss;
        }

        public double Normal { get; }
        public double Boss { get; }

        public double For(EnemyType type) => type == EnemyType.Normal ? Normal : Boss;

        public static GemDropChance Read(ContentNode node) =>
            new GemDropChance(OneIn(node.Field("normal")), OneIn(node.Field("boss")));

        private static double OneIn(ContentNode node)
        {
            var oneIn = node.AsNumber(0, 1e9);
            if (oneIn > 0 && oneIn < 1)
                throw node.Error("is '1 in N': use 0 for never or N of at least 1");
            return oneIn == 0 ? 0 : 1 / oneIn;
        }
    }
}
