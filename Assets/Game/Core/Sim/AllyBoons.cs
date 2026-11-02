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
        public int Hushes { get; set; }
        public double HushPower { get; set; }
        public int HushOwner { get; set; } = -1;
        public int HushesOwed { get; set; }
        public long HushHits { get; set; }
        public int NameOwner { get; set; } = -1;
        public int NameEvery { get; set; }
        public int NameTicks { get; set; }
        public int NameHits { get; set; }
        public bool SyllableGiven { get; set; }
        public bool SyllableOwed { get; set; }
        public double SpringWind { get; set; }
        public double SpringSlow { get; set; }
        public int SpringTicks { get; set; }
        public double LoopShare { get; set; }
        public int LoopTicks { get; set; }
        public long LoopsSeen { get; set; }
        public int GearOwner { get; set; } = -1;
        public int GearTicks { get; set; }
        public long GearCuts { get; set; }
        public int ReagentOwner { get; set; } = -1;
        public int ReagentTicks { get; set; }
        public int ReagentCooldownTicks { get; set; }
        public bool ReagentOwed { get; set; }
        public int Flask { get; set; }
        public int AcidOwner { get; set; } = -1;
        public int AcidTicks { get; set; }
        public int AcidHitsOwed { get; set; }
        public int RedTicks { get; set; }
        public int BlueTicks { get; set; }
        public int GoldTicks { get; set; }
        public double ElixirPower { get; set; }
        public double ElixirGold { get; set; }
        public int RedEvery { get; set; } = 5;
        public int RedStrikes { get; set; }
        public int ElixirStreak { get; set; }
        public long Elixirs { get; set; }

        public double GoldMultiplier => GoldTicks > 0 ? 1 + ElixirGold : 1;

        public bool FreeSkills => BlueTicks > 0;

        public void Land(int count, int ticks, double power, int every, double burst, int owner, int max)
        {
            Fireflies = System.Math.Min(max, Fireflies + count);
            FireflyTicks = System.Math.Max(FireflyTicks, ticks);
            SparkPower = power;
            SparkEvery = every;
            BurstPower = burst;
            FireflyOwner = owner;
        }

        public void Hush(int count, double power, int owner, int max)
        {
            Hushes = System.Math.Min(System.Math.Max(1, max), Hushes + count);
            HushPower = power;
            HushOwner = owner;
        }

        public void CallName(int owner, int every, int ticks)
        {
            NameOwner = owner;
            NameEvery = System.Math.Max(1, every);
            NameTicks = System.Math.Max(NameTicks, ticks);
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
