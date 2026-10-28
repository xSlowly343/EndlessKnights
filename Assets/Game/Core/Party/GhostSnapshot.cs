using Game.Core.Sim;

namespace Game.Core.Party
{
    public readonly struct GhostSnapshot
    {
        public GhostSnapshot(string userId, string name, HeroClass heroClass, double shareRate, int marks, double activity)
        {
            UserId = userId;
            Name = name;
            HeroClass = heroClass;
            ShareRate = shareRate;
            Marks = marks;
            Activity = activity;
        }

        public string UserId { get; }
        public string Name { get; }
        public HeroClass HeroClass { get; }
        public double ShareRate { get; }
        public int Marks { get; }
        public double Activity { get; }
    }
}
