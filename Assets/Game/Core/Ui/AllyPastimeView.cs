using Game.Core.Content;
using Game.Core.Party;
using Game.Core.Sim;
using Game.Core.Text;

namespace Game.Core.Ui
{
    public static class AllyPastimeView
    {
        public const string IconGroup = "pastime";

        private static readonly string[] ActivityIcons =
        {
            string.Empty, "pastime_garden", "pastime_alchemy", "pastime_fishing", "pastime_cooking",
            "pastime_forge", "pastime_dig", "pastime_distill", "pastime_meditation"
        };

        public static string Icon(PartyPastime pastime) => (int)pastime.Activity < ActivityIcons.Length ? ActivityIcons[(int)pastime.Activity] : string.Empty;

        public static string PlaceIcon(PartyPastime pastime) => !pastime.Known ? string.Empty : pastime.Place switch
        {
            StripPlace.Hideout => "place_hideout",
            StripPlace.Town => "place_town",
            StripPlace.ClanCamp => "place_clan_camp",
            _ => string.Empty
        };

        public static bool Shows(StripAlly ally) => ally.Presence == PartyPresence.Peaceful && ally.Pastime.Known;

        public static string Caption(PeacefulRules rules, PartyPastime pastime)
        {
            if (!pastime.Known)
                return string.Empty;
            var index = rules.IndexOf(PartyPastime.IdOf(pastime.Activity));
            var name = index >= 0 ? Texts.Pick(rules.Activities[index].NameRu, rules.Activities[index].NameEn) : string.Empty;
            return Texts.Format("strip.place.activity", StripSceneText.Place(pastime.Place), name);
        }
    }
}
