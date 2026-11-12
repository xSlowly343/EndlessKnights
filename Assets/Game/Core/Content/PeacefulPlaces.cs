namespace Game.Core.Content
{
    public static class PeacefulPlaces
    {
        public static string Key(PeacefulPlace place) => place switch
        {
            PeacefulPlace.Hideout => "hideout",
            PeacefulPlace.Town => "town",
            PeacefulPlace.ClanCamp => "clanCamp",
            _ => "refuge"
        };

        public static PeacefulPlace Parse(string key) => key switch
        {
            "hideout" => PeacefulPlace.Hideout,
            "town" => PeacefulPlace.Town,
            "clanCamp" => PeacefulPlace.ClanCamp,
            _ => PeacefulPlace.Refuge
        };
    }
}
