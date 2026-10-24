using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyBuffRules
    {
        public const int MaxStats = 6;
        public const int MaxAuras = 4;

        private PartyBuffRules(double selfShare, double offerSeconds, IReadOnlyList<PartyStatLink> stats)
        {
            SelfShare = selfShare;
            OfferSeconds = offerSeconds;
            Stats = stats;
        }

        public double SelfShare { get; }
        public double OfferSeconds { get; }
        public IReadOnlyList<PartyStatLink> Stats { get; }

        public static PartyBuffRules Read(ContentNode node)
        {
            var stats = new List<PartyStatLink>();
            foreach (var item in node.Field("stats").Items())
            {
                if (stats.Count == MaxStats)
                    throw item.Error($"no more than {MaxStats} party stats fit in one offer");
                stats.Add(new PartyStatLink(
                    new StatId(item.Field("from").AsString()),
                    new StatId(item.Field("to").AsString()),
                    ModifierReader.ReadKind(item.Field("kind"))));
            }
            return new PartyBuffRules(node.Field("selfShare").AsNumber(0, 1), node.Field("offerSeconds").AsNumber(0.1, 60), stats);
        }
    }
}
