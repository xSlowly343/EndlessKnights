using System.Collections.Generic;
using Game.Core.Party;
using Game.Core.Party.Wire;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class PartyRoster
    {
        public static bool InParty(PartyLink link) => link == PartyLink.Linked || link == PartyLink.NoSignal;

        public static IReadOnlyList<PartyRow> Solo(GameState state)
        {
            var rows = new List<PartyRow> { Self(state, PartyPresence.Fighting) };
            for (var slot = 1; slot < PartyRules.MaxSlots; slot++)
                rows.Add(PartyRow.Free);
            return rows;
        }

        public static IReadOnlyList<PartyRow> From(GameState state, PartyClient client) =>
            From(state, client.Status, client.Roster, client.OwnSlot, client.ContributionKills > 0 ? client.Contribution : null, client.LastKillPaid,
                slot => PartyReactions.Label(client.Reactions, client.ReactionOf(slot)));

        public static IReadOnlyList<PartyRow> From(GameState state, PartyLink link, RosterMessage roster, int ownSlot,
            System.Func<int, double>? contribution = null, bool lastKillPaid = true, System.Func<int, string>? reaction = null)
        {
            if (!InParty(link) || ownSlot < 0)
                return Solo(state);
            var selfPresence = link == PartyLink.NoSignal ? PartyPresence.Offline : roster.Presence(ownSlot);
            var selfDetails = Texts.Format("party.classStage", HeroSummary.ClassName(state.Class), state.Stage)
                + Standing(roster, ownSlot, contribution) + (lastKillPaid ? string.Empty : Separator + Texts.Get("party.unpaid"));
            var rows = new List<PartyRow> { new PartyRow(true, ownSlot, Texts.Get("party.you"), selfDetails, selfPresence, reaction?.Invoke(ownSlot) ?? string.Empty) };
            var number = 1;
            for (var slot = 0; slot < PartyRules.MaxSlots; slot++)
            {
                if (slot == ownSlot)
                    continue;
                if (roster.Player(slot) == 0)
                    continue;
                number++;
                rows.Add(new PartyRow(false, slot, Texts.Format("party.player", number), ClassName(roster.HeroClass(slot)) + Standing(roster, slot, contribution), roster.Presence(slot),
                    reaction?.Invoke(slot) ?? string.Empty));
            }
            while (rows.Count < PartyRules.MaxSlots)
                rows.Add(PartyRow.Free);
            return rows;
        }

        public static int Members(IReadOnlyList<PartyRow> rows)
        {
            var count = 0;
            foreach (var row in rows)
            {
                if (!row.IsFree)
                    count++;
            }
            return count;
        }

        public static string Status(PartyPresence presence) => Texts.Get("party.status." + presence.ToString().ToLowerInvariant());

        public static string Headline(IReadOnlyList<PartyRow> rows) => Texts.Format("party.headline", Members(rows), rows.Count);

        public static string FreeSlot => Texts.Get("party.free");
        public static string Help => Texts.Get("party.help");
        public static string Links => Texts.Get("party.links");
        public static string Leave => Texts.Get("party.leave");
        public static string SoloHint => Texts.Get("party.soloHint");
        public static string Enemy => Texts.Get("party.enemy");
        public static string BarHint => Texts.Get("party.barHint");
        public static string NotYet => Texts.Get("party.notYet");

        private const string Separator = " · ";

        private static string Standing(RosterMessage roster, int slot, System.Func<int, double>? contribution)
        {
            var text = contribution == null
                ? string.Empty
                : Separator + Texts.Format("party.contribution", (int)System.Math.Round(contribution(slot) * 100, System.MidpointRounding.AwayFromZero));
            if (roster.Presence(slot) == PartyPresence.Resting)
                text += Separator + Texts.Format("party.restBank", roster.RestBankSeconds(slot) / 60);
            return text;
        }

        private static PartyRow Self(GameState state, PartyPresence presence) =>
            new PartyRow(true, PartyEncounter.NoSlot, Texts.Get("party.you"), Texts.Format("party.classStage", HeroSummary.ClassName(state.Class), state.Stage), presence);

        private static string ClassName(byte heroClass) =>
            heroClass < System.Enum.GetValues(typeof(HeroClass)).Length ? HeroSummary.ClassName((HeroClass)heroClass) : string.Empty;
    }
}
