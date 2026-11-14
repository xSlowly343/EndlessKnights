using System.Collections.Generic;

namespace Game.Core.Party
{
    public sealed class PartyGrowthSave
    {
        public bool InParty { get; set; }
        public long JoinedUnixMs { get; set; }
        public int Members { get; set; }
        public Dictionary<int, int> TreeLevels { get; } = new Dictionary<int, int>();
        public List<string> TreeMajors { get; } = new List<string>();
        public List<int> TrialBest { get; } = new List<int>();
        public Dictionary<string, int> RelicLevels { get; } = new Dictionary<string, int>();
        public string MascotBeast { get; set; } = string.Empty;
        public int MascotLevel { get; set; }
        public Dictionary<string, int> RiteLevels { get; } = new Dictionary<string, int>();
        public List<string> Crests { get; } = new List<string>();
        public Dictionary<string, int> PartySetCounts { get; } = new Dictionary<string, int>();
        public long AnsweredUnixMs { get; set; }
        public long SeenUnixMs { get; set; }

        public bool FreshFor(double hours) => InParty && SeenUnixMs - AnsweredUnixMs <= (long)(hours * 3_600_000);

        public void Take(PartyGrowthLayer layer, long nowUnixMs)
        {
            InParty = layer.InParty;
            JoinedUnixMs = layer.JoinedUnixMs;
            Members = layer.Members;
            Copy(layer.TreeLevels, TreeLevels);
            Copy(layer.TreeMajors, TreeMajors);
            Copy(layer.TrialBest, TrialBest);
            Copy(layer.RelicLevels, RelicLevels);
            MascotBeast = layer.MascotBeast;
            MascotLevel = layer.MascotLevel;
            Copy(layer.RiteLevels, RiteLevels);
            Copy(layer.Crests, Crests);
            Copy(layer.PartySetCounts, PartySetCounts);
            if (nowUnixMs > 0)
            {
                AnsweredUnixMs = nowUnixMs;
                SeenUnixMs = System.Math.Max(SeenUnixMs, nowUnixMs);
            }
        }

        public void GiveTo(PartyGrowthLayer layer)
        {
            layer.SetTree(TreeLevels, TreeMajors);
            layer.SetTrials(TrialBest);
            layer.SetRelics(RelicLevels);
            layer.SetMascot(MascotBeast, MascotLevel, RiteLevels);
            layer.SetBanner(Crests);
            layer.SetPartySets(PartySetCounts);
            layer.SetStanding(InParty, JoinedUnixMs, Members, SeenUnixMs);
        }

        public void Clear()
        {
            InParty = false;
            JoinedUnixMs = 0;
            Members = 0;
            TreeLevels.Clear();
            TreeMajors.Clear();
            TrialBest.Clear();
            RelicLevels.Clear();
            MascotBeast = string.Empty;
            MascotLevel = 0;
            RiteLevels.Clear();
            Crests.Clear();
            PartySetCounts.Clear();
        }

        private static void Copy<TKey>(IReadOnlyDictionary<TKey, int> from, Dictionary<TKey, int> to) where TKey : notnull
        {
            if (ReferenceEquals(from, to))
                return;
            to.Clear();
            foreach (var pair in from)
                to[pair.Key] = pair.Value;
        }

        private static void Copy<T>(IReadOnlyList<T> from, List<T> to)
        {
            if (ReferenceEquals(from, to))
                return;
            to.Clear();
            for (var i = 0; i < from.Count; i++)
                to.Add(from[i]);
        }
    }
}
