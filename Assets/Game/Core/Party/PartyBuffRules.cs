using System.Collections.Generic;
using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Party
{
    public sealed class PartyBuffRules
    {
        public const int MaxStats = 6;
        public const int MaxAuras = Sim.SkillLoadout.MaxSlots;

        private PartyBuffRules(double selfShare, double offerSeconds, IReadOnlyList<PartyStatLink> stats, double allyResourceMax, double allyResourceCooldownSeconds)
        {
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
        public double ClearDayAbove { get; private set; }
        public double ClearDayCritChance { get; private set; }
        public double TypingSeconds { get; private set; }
        public double TypingPartyDamage { get; private set; }

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
            var allyResource = node.Field("allyResource");
            var rules = new PartyBuffRules(node.Field("selfShare").AsNumber(0, 1), node.Field("offerSeconds").AsNumber(0.1, 60), stats,
                allyResource.Field("max").AsNumber(0, 1000), allyResource.Field("cooldownSeconds").AsNumber(0, 600));
            if (node.HasField("heardByAll"))
            {
                var heard = node.Field("heardByAll");
                rules.TypingSeconds = heard.Field("typingSeconds").AsNumber(0, 60);
                rules.TypingPartyDamage = heard.Field("partyDamage").AsNumber(0, 10);
            }
            if (!node.HasField("clearDay"))
                return rules;
            var clearDay = node.Field("clearDay");
            rules.ClearDayAbove = clearDay.Field("resourceAbove").AsNumber(0, 1e6);
            rules.ClearDayCritChance = clearDay.Field("partyCritChance").AsNumber(0, 10);
            return rules;
        }
    }
}
