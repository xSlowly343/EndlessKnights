using Game.Core.Content.Gems;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public struct MinionState
    {
        public MinionKind Kind;
        public int TicksLeft;
        public double HitCarry;
        public double HitsPerSecond;
        public double Multiplier;
        public StatTags Tags;
        public double MouseBonus;
        public long Hits;
        public bool Risen;

        public bool Standing => TicksLeft > 0;
    }
}
