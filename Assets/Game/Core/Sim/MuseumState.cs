using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class MuseumState
    {
        public HashSet<string> Done { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public List<string> Shelf { get; } = new List<string>();
        public string Title { get; set; } = string.Empty;
    }
}
