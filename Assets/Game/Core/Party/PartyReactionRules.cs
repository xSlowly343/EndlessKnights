using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Party
{
    public sealed class PartyReactionRules
    {
        public const int MaxReactions = 16;

        private PartyReactionRules(IReadOnlyList<string> ids, double cooldownSeconds, double showSeconds)
        {
            Ids = ids;
            CooldownSeconds = cooldownSeconds;
            ShowSeconds = showSeconds;
        }

        public IReadOnlyList<string> Ids { get; }
        public double CooldownSeconds { get; }
        public double ShowSeconds { get; }

        public bool Knows(int reaction) => reaction >= 0 && reaction < Ids.Count;

        public static PartyReactionRules Read(ContentNode node)
        {
            var idsNode = node.Field("ids");
            var ids = new List<string>();
            foreach (var item in idsNode.Items())
            {
                var id = item.AsString();
                if (id.Length == 0 || ids.Contains(id))
                    throw item.Error("must be a new, non-empty id");
                ids.Add(id);
            }
            if (ids.Count == 0 || ids.Count > MaxReactions)
                throw idsNode.Error($"must list 1 to {MaxReactions} reactions");
            return new PartyReactionRules(ids, node.Field("cooldownSeconds").AsNumber(0.5, 60), node.Field("showSeconds").AsNumber(0.5, 30));
        }
    }
}
