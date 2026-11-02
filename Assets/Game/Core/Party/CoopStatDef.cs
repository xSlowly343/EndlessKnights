using Game.Core.Stats;

namespace Game.Core.Party
{
    public readonly struct CoopStatDef
    {
        public readonly StatId Stat;
        public readonly ModifierKind Kind;
        public readonly double Cap;
        public readonly bool Negate;

        public CoopStatDef(StatId stat, ModifierKind kind, double cap, bool negate)
        {
            Stat = stat;
            Kind = kind;
            Cap = cap;
            Negate = negate;
        }
    }
}
