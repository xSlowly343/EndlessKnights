namespace Game.Core.Content
{
    public static class PeacefulPlaces
    {
        public static string Key(PeacefulPlace place) => place switch
        {
            PeacefulPlace.Town => "town",
            PeacefulPlace.ClanCamp => "clanCamp",
            _ => "hideout"
        };

        public static PeacefulPlace Parse(string key) => key switch
        {
            "town" => PeacefulPlace.Town,
            "clanCamp" => PeacefulPlace.ClanCamp,
            _ => PeacefulPlace.Hideout
        };
    }
}
