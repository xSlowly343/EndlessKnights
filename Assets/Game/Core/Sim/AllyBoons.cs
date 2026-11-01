namespace Game.Core.Sim
{
    public sealed class AllyBoons
    {
        public int Fireflies { get; set; }
        public int FireflyTicks { get; set; }
        public double SparkPower { get; set; }
        public int SparkEvery { get; set; }
        public double BurstPower { get; set; }
        public int FireflyOwner { get; set; } = -1;
        public int StrikesSinceSpark { get; set; }
        public int SparksOwed { get; set; }
        public long Sparks { get; set; }
        public int BeaconCharges { get; set; }
        public double BeaconPower { get; set; }

        public void Land(int count, int ticks, double power, int every, double burst, int owner, int max)
        {
            Fireflies = System.Math.Min(max, Fireflies + count);
            FireflyTicks = System.Math.Max(FireflyTicks, ticks);
            SparkPower = power;
            SparkEvery = every;
            BurstPower = burst;
            FireflyOwner = owner;
        }

        public void Flash(int charges, double power)
        {
            BeaconCharges = System.Math.Max(BeaconCharges, charges);
            BeaconPower = System.Math.Max(BeaconPower, power);
        }
    }
}
