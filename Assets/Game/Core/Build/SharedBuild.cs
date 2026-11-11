using System.Collections.Generic;

namespace Game.Core.Build
{
    public sealed class SharedBuild
    {
        public SharedBuild(string treeId, IReadOnlyList<int> ranks, IReadOnlyDictionary<int, int> majors, IReadOnlyList<IReadOnlyList<string>> skills)
        {
            TreeId = treeId;
            Ranks = ranks;
            Majors = majors;
            Skills = skills;
        }

        public string TreeId { get; }
        public IReadOnlyList<int> Ranks { get; }
        public IReadOnlyDictionary<int, int> Majors { get; }
        public IReadOnlyList<IReadOnlyList<string>> Skills { get; }

        public int Points
        {
            get
            {
                var points = 0;
                foreach (var rank in Ranks)
                    points += rank;
                return points;
            }
        }
    }
}
