namespace Game.Core.Ui
{
    public sealed class SlashGate
    {
        public const int MaxTier = 3;
        public const int HitsForSecondTier = 2;
        public const int HitsForThirdTier = 4;

        private int _pending;

        public int Pending => _pending;

        public static int TierOf(int hits) =>
            hits <= 0 ? 0 : hits < HitsForSecondTier ? 1 : hits < HitsForThirdTier ? 2 : MaxTier;

        public void Hit(int hits = 1)
        {
            if (hits > 0)
                _pending = System.Math.Min(HitsForThirdTier, _pending + hits);
        }

        public int Take()
        {
            var tier = TierOf(_pending);
            _pending = 0;
            return tier;
        }
    }
}
