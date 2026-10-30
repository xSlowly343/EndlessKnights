using System.Collections.Generic;

namespace Game.Core.Sim.Headless
{
    public sealed class PartySimulationResult
    {
        public PartySimulationResult(double seconds, long hostKills, IReadOnlyList<PartyMemberResult> members)
        {
            Seconds = seconds;
            HostKills = hostKills;
            Members = members;
        }

        public double Seconds { get; }
        public long HostKills { get; }
        public IReadOnlyList<PartyMemberResult> Members { get; }
        public IReadOnlyList<PartySample> Samples { get; set; } = System.Array.Empty<PartySample>();
        public double SecondsPerKill => HostKills > 0 ? Seconds / HostKills : Seconds;
    }
}
