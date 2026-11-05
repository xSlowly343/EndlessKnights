using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class AwakeningState
    {
        public List<string> Enchanted { get; } = new List<string>();
        public Dictionary<string, double> Progress { get; } = new Dictionary<string, double>(System.StringComparer.Ordinal);
        public double[] Last { get; } = new double[ArtifactAwakening.InputCount];
        public bool Primed { get; set; }
    }
}
