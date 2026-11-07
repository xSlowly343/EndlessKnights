namespace Game.Core.Sim
{
    public sealed class PeacefulBonus
    {
        public double Speed { get; internal set; }
        public double Yield { get; internal set; }
        public double Luck { get; internal set; }
        public double Mastery { get; internal set; }
        public double Party { get; internal set; }

        public double TimeFactor => 1 / (1 + System.Math.Max(0, Speed));
        public double YieldFactor => 1 + System.Math.Max(0, Yield);
        public double LuckFactor => 1 + System.Math.Max(0, Luck);
        public double MasteryChance => System.Math.Min(1, System.Math.Max(0, Mastery));

        public long More(long amount) => (long)System.Math.Floor(amount * YieldFactor + 1e-9);

        public int More(int amount) => (int)System.Math.Floor(amount * YieldFactor + 1e-9);

        public bool RollMastery(Game.Core.Math.Rng rng) => MasteryChance > 0 && rng.NextDouble() < MasteryChance;
    }
}
