using Game.Core.Party;
using Game.Core.Party.Wire;

namespace Game.Core.Ui
{
    public static class StripAllies
    {
        public const int MaxShown = PartyRules.MaxSlots - 1;

        public static int Fill(PartyClient client, StripAlly[] into)
        {
            if (!client.InParty)
                return 0;
            var count = 0;
            var roster = client.Roster;
            for (var slot = 0; slot < PartyRules.MaxSlots && Room(count, into) && client.OwnSlot >= 0; slot++)
            {
                if (slot == client.OwnSlot || roster.Player(slot) == 0)
                    continue;
                into[count] = new StripAlly(slot, count + 1, roster.Presence(slot), PartyReactions.Label(client.Reactions, client.ReactionOf(slot)));
                count++;
            }
            return count;
        }

        public static int Fill(RosterMessage roster, int ownSlot, System.Func<int, string> reaction, StripAlly[] into)
        {
            var count = 0;
            for (var slot = 0; slot < PartyRules.MaxSlots && Room(count, into) && ownSlot >= 0; slot++)
            {
                if (slot == ownSlot || roster.Player(slot) == 0)
                    continue;
                into[count] = new StripAlly(slot, count + 1, roster.Presence(slot), reaction(slot));
                count++;
            }
            return count;
        }

        private static bool Room(int count, StripAlly[] into) => count < into.Length && count < MaxShown;

        public static string StatusMark(PartyPresence presence) => presence switch
        {
            PartyPresence.Resting => "z",
            PartyPresence.Peaceful => "~",
            PartyPresence.Offline => "!",
            _ => string.Empty
        };
    }
}
