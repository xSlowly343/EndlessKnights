using Game.Core.Sim;

namespace Game.Core.Party
{
    public enum AllyActivity : byte
    {
        None,
        Garden,
        Alchemy,
        Fishing,
        Cooking,
        Forge,
        Dig,
        Distill,
        Meditation
    }

    public readonly struct PartyPastime
    {
        private const int PlaceShift = 4;
        private const int ActivityMask = 0x0F;

        public PartyPastime(AllyActivity activity, StripPlace place)
        {
            Activity = activity;
            Place = place;
        }

        public AllyActivity Activity { get; }
        public StripPlace Place { get; }
        public bool Known => Activity != AllyActivity.None;
        public byte Packed => (byte)(((int)Place << PlaceShift) | ((int)Activity & ActivityMask));

        public static PartyPastime Unpack(byte packed)
        {
            var activity = packed & ActivityMask;
            var place = packed >> PlaceShift;
            return new PartyPastime(activity <= (int)AllyActivity.Meditation ? (AllyActivity)activity : AllyActivity.None,
                place <= (int)StripPlace.Town ? (StripPlace)place : StripPlace.Refuge);
        }

        public static AllyActivity ActivityOf(string id) => id switch
        {
            "garden" => AllyActivity.Garden,
            "alchemy" => AllyActivity.Alchemy,
            "fishing" => AllyActivity.Fishing,
            "cooking" => AllyActivity.Cooking,
            "forge" => AllyActivity.Forge,
            "dig" => AllyActivity.Dig,
            "distill" => AllyActivity.Distill,
            "meditation" => AllyActivity.Meditation,
            _ => AllyActivity.None
        };

        public static string IdOf(AllyActivity activity) => activity switch
        {
            AllyActivity.Garden => "garden",
            AllyActivity.Alchemy => "alchemy",
            AllyActivity.Fishing => "fishing",
            AllyActivity.Cooking => "cooking",
            AllyActivity.Forge => "forge",
            AllyActivity.Dig => "dig",
            AllyActivity.Distill => "distill",
            AllyActivity.Meditation => "meditation",
            _ => string.Empty
        };
    }
}
