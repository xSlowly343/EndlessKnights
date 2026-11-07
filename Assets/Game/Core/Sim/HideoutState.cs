using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class HideoutCorner
    {
        public HideoutCorner(string name, string hero, string path, IReadOnlyList<string> decor)
        {
            Name = name;
            Hero = hero;
            Path = path;
            Decor = decor;
        }

        public string Name { get; }
        public string Hero { get; }
        public string Path { get; }
        public IReadOnlyList<string> Decor { get; }
    }

    public sealed class HideoutState
    {
        public int Tier { get; set; }
        public int Members { get; set; }
        public long Bosses { get; set; }
        public int Orders { get; set; }
        public long SitSeconds { get; set; }
        public int FireLevel { get; set; }
        public List<string> Here { get; } = new List<string>();
        public Dictionary<string, int> Buildings { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public List<string> CanQueue { get; } = new List<string>();
        public Dictionary<string, int> Wants { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public List<string> MyWants { get; } = new List<string>();
        public string Project { get; set; } = string.Empty;
        public int ProjectLevel { get; set; }
        public long ProjectPoints { get; set; }
        public long ProjectTarget { get; set; }
        public double ProjectHours { get; set; }
        public long HoursTogether { get; set; }
        public bool Leader { get; set; }
        public bool HasCorner { get; set; }
        public List<HideoutCorner> Corners { get; } = new List<HideoutCorner>();
    }
}
