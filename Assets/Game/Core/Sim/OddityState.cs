using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class OddityState
    {
        public const ulong SeedSalt = 0x0DD17E5EED5A17UL;

        public OddityState(ulong seed)
        {
            Rng = new Rng(seed ^ SeedSalt);
        }

        public Rng Rng { get; set; }
        public OddityKind Kind { get; set; }
        public int DummyTicks { get; set; }
        public BigNumber DummyNormalHp { get; set; }
        public BigNumber DummyDamage { get; set; }
        public double DummyKills { get; set; }
        public int PartsLeft { get; set; }
        public int SplitTicks { get; set; }
        public BigNumber PartHp { get; set; }
        public int TrailStages { get; set; }
        public int CrowdStages { get; set; }
        public int SproutStacks { get; set; }
        public double PendingResourceShare { get; set; }
        public double PendingGemKills { get; set; }
        public int WispCooldownTicks { get; set; } = -1;
        public int WispTicks { get; set; }
        public double WispActions { get; set; }
        public int GoldSpreeTicks { get; set; }
        public double GoldSpreeMultiplier { get; set; } = 1;
        public int DamageSpreeTicks { get; set; }
        public double DamageSpreeMultiplier { get; set; } = 1;
        public double ShardCarry { get; set; }

        public bool WispFlying => WispTicks > 0;
        public double GoldSpree => GoldSpreeTicks > 0 ? GoldSpreeMultiplier : 1;

        public void OnSpawn()
        {
            Kind = OddityKind.None;
            DummyTicks = 0;
            DummyDamage = BigNumber.Zero;
        }

        public void ResetRun()
        {
            OnSpawn();
            DummyKills = 0;
            PartsLeft = 0;
            SplitTicks = 0;
            TrailStages = 0;
            CrowdStages = 0;
            SproutStacks = 0;
        }
    }
}
