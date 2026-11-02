namespace Game.Core.Sim
{
    public sealed class AllyBoons
    {
        public int Fireflies { get; set; }
        public int FireflyTicks { get; set; }
        public double SparkPower { get; set; }
        public int SparkEvery { get; set; }
        public double BurstPower { get; set; }
        public int StrikesSinceSpark { get; set; }
        public long Sparks { get; set; }
        public int BeaconCharges { get; set; }
        public double BeaconPower { get; set; }
        public int Hushes { get; set; }
        public double HushPower { get; set; }
        public long HushHits { get; set; }
        public int AcidTicks { get; set; }
        public int RedTicks { get; set; }
        public int BlueTicks { get; set; }
        public int GoldTicks { get; set; }
        public double ElixirPower { get; set; }
        public double ElixirGold { get; set; }
        public int RedEvery { get; set; } = 5;
        public int RedStrikes { get; set; }
        public int ElixirStreak { get; set; }
        public long Elixirs { get; set; }
        public int LendTicks { get; set; }
        public double LendPower { get; set; }
        public long LentHits { get; set; }
        public double Pollen { get; set; }
        public double PollenRate { get; set; }
        public int PollenTicks { get; set; }
        public double PollenBurnSeconds { get; set; }
        public int PollenEvery { get; set; } = 1;
        public bool PollenQueue { get; set; }
        public double PollenLight { get; set; }
        public int PollenCrits { get; set; }
        public double PollenBurn { get; set; }
        public int PollenBurnTicks { get; set; }
        public double PollenBurnCarry { get; set; }
        public long PollenShakes { get; set; }
        public int PetalEvery { get; set; }
        public double PetalPower { get; set; }
        public int PetalTicks { get; set; }
        public int PetalMax { get; set; } = 3;
        public int PetalHits { get; set; }
        public int PetalSeeds { get; set; }
        public double PetalCarry { get; set; }
        public long PetalStings { get; set; }

        public double GoldMultiplier => GoldTicks > 0 ? 1 + ElixirGold : 1;

        public bool FreeSkills => BlueTicks > 0;

        public void Land(int count, int ticks, double power, int every, double burst, int max)
        {
            Fireflies = System.Math.Min(max, Fireflies + count);
            FireflyTicks = System.Math.Max(FireflyTicks, ticks);
            SparkPower = power;
            SparkEvery = every;
            BurstPower = burst;
        }

        public void Hush(int count, double power, int max)
        {
            Hushes = System.Math.Min(System.Math.Max(1, max), Hushes + count);
            HushPower = power;
        }

        public void Drink(int color, double power, int ticks, int redEvery, double gold, int philosopherEvery, int philosopherTicks)
        {
            Elixirs++;
            ElixirPower = power;
            RedEvery = System.Math.Max(1, redEvery);
            ElixirGold = gold;
            if (++ElixirStreak % System.Math.Max(1, philosopherEvery) == 0)
            {
                RedTicks = System.Math.Max(RedTicks, philosopherTicks);
                BlueTicks = System.Math.Max(BlueTicks, philosopherTicks);
                GoldTicks = System.Math.Max(GoldTicks, philosopherTicks);
                return;
            }
            if (color == 0)
                RedTicks = System.Math.Max(RedTicks, ticks);
            else if (color == 1)
                BlueTicks = System.Math.Max(BlueTicks, ticks);
            else
                GoldTicks = System.Math.Max(GoldTicks, ticks);
        }

        public void Flash(int charges, double power)
        {
            BeaconCharges = System.Math.Max(BeaconCharges, charges);
            BeaconPower = System.Math.Max(BeaconPower, power);
        }
    }
}
