using Game.Core.Sim;

namespace Game.Core.Party
{
    public readonly struct GhostSnapshot
    {
        public GhostSnapshot(string userId, string name, HeroClass heroClass, double shareRate, double activity)
        {
            UserId = userId;
            Name = name;
            HeroClass = heroClass;
            ShareRate = shareRate;
            Activity = activity;
        }

        public string UserId { get; }
        public string Name { get; }
        public HeroClass HeroClass { get; }
        public double ShareRate { get; }
        public double Activity { get; }
    }
}
