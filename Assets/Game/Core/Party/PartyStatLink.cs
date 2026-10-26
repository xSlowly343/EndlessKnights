using Game.Core.Stats;

namespace Game.Core.Party
{
    public readonly struct PartyStatLink
    {
        public readonly StatId From;
        public readonly StatId To;
        public readonly ModifierKind Kind;

        public PartyStatLink(StatId from, StatId to, ModifierKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        public double ValueOf(StatSheet sheet) =>
            Kind == ModifierKind.Flat ? sheet.Value(From, 0) : sheet.Value(From, 1) - 1;
    }
}
