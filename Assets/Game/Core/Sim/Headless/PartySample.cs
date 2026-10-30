namespace Game.Core.Sim.Headless
{
    public sealed class PartySample
    {
        public PartySample(double seconds, long hostKills, long[] rewards, double[] hostShares, long[] restBankSeconds)
        {
            Seconds = seconds;
            HostKills = hostKills;
            Rewards = rewards;
            HostShares = hostShares;
            RestBankSeconds = restBankSeconds;
        }

        public double Seconds { get; }
        public long HostKills { get; }
        public long[] Rewards { get; }
        public double[] HostShares { get; }
        public long[] RestBankSeconds { get; }
    }
}
