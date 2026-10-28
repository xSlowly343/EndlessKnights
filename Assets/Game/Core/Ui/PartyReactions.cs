using Game.Core.Party;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PartyReactions
    {
        public static string Label(string id) => Texts.Get("party.reaction." + id);

        public static string Label(PartyReactionRules rules, int reaction) => rules.Knows(reaction) ? Label(rules.Ids[reaction]) : string.Empty;

        public static string Said(string playerName, string reaction) => Texts.Format("party.reaction.said", playerName, reaction);

        public static string Wait(PartyReactionRules rules) => Texts.Format("party.reaction.wait", rules.CooldownSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
    }
}
