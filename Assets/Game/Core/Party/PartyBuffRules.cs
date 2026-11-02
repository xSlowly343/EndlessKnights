using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyBuffRules
    {
        public const int MaxStats = 7;
        public const int MaxAuras = Sim.SkillLoadout.MaxSlots;

        private PartyBuffRules(double selfShare, double offerSeconds, IReadOnlyList<PartyStatLink> stats, double allyResourceMax, double allyResourceCooldownSeconds, CoopRules coop)
        {
            Coop = coop;
            SelfShare = selfShare;
            OfferSeconds = offerSeconds;
            Stats = stats;
            AllyResourceMax = allyResourceMax;
            AllyResourceCooldownSeconds = allyResourceCooldownSeconds;
        }

        public double SelfShare { get; }
        public double OfferSeconds { get; }
        public IReadOnlyList<PartyStatLink> Stats { get; }
        public double AllyResourceMax { get; }
        public double AllyResourceCooldownSeconds { get; }
        public CoopRules Coop { get; }

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
                    ModifierReader.ReadKind(item.Field("kind")),
                    item.HasField("negate") && item.Field("negate").AsBool()));
            }
            var allyResource = node.Field("allyResource");
            var rules = new PartyBuffRules(node.Field("selfShare").AsNumber(0, 1), node.Field("offerSeconds").AsNumber(0.1, 60), stats,
                allyResource.Field("max").AsNumber(0, 1000), allyResource.Field("cooldownSeconds").AsNumber(0, 600),
                node.HasField("coop") ? CoopRules.Read(node.Field("coop")) : CoopRules.Empty);
            return rules;
        }
    }
}
