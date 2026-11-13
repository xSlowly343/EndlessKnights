using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class CompanionProgress
    {
        public int Level { get; set; } = 1;
        public int Stage { get; set; }
        public double Experience { get; set; }
        public double FedToday { get; set; }
        public bool FavouriteKnown { get; set; }
        public long Fed { get; set; }
        public List<string> Traits { get; } = new List<string>();
        public Dictionary<string, double> TraitTrips { get; } = new Dictionary<string, double>(System.StringComparer.Ordinal);
        public double HomeSeconds { get; set; }
        public double Bond { get; set; }
        public int PetsToday { get; set; }
        public long LastPetTick { get; set; } = long.MinValue;
        public int FavouriteBondsToday { get; set; }
        public int FriendPetsToday { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Accessory { get; set; } = string.Empty;
        public string Coat { get; set; } = string.Empty;
        public int Stars { get; set; }
        public string Form { get; set; } = string.Empty;
        public List<string> Forms { get; } = new List<string>();
        public int OwnSparks { get; set; }
        public int Trips { get; set; }
        public long InstantDay { get; set; } = -1;
        public string Treat { get; set; } = string.Empty;
        public long DelicacyDay { get; set; } = -1000;
        public long FirstTripDay { get; set; } = -1;
        public int Story { get; set; }
        public HashSet<string> Talents { get; } = new HashSet<string>(System.StringComparer.Ordinal);
        public long FreeResetDay { get; set; } = -1;
        public double ForageSeconds { get; set; }
        public long Forages { get; set; }
        public string Temper { get; set; } = string.Empty;
        public string Student { get; set; } = string.Empty;
        public int Circle { get; set; }
        public string Path { get; set; } = string.Empty;
        public List<string> Forks { get; } = new List<string>();
        public double CircleCount { get; set; }
        public long CircleDay { get; set; } = -1;
        public long AbilityDay { get; set; } = -1;
        public List<string> ColossusAnswers { get; } = new List<string>();
        public string ColossusForm { get; set; } = string.Empty;
        public long TemperFreeDay { get; set; } = -1;
    }

    public sealed class CompanionEgg
    {
        public string Biome { get; set; } = string.Empty;
        public double SecondsLeft { get; set; }
    }

    public sealed class CompanionState
    {
        public string Visiting { get; set; } = string.Empty;
        public string VisitHost { get; set; } = string.Empty;
        public long VisitEndsUnixMs { get; set; }
        public List<CompanionEgg> Eggs { get; } = new List<CompanionEgg>();
        public double EggCooldown { get; set; }
        public long Sparks { get; set; }
        public double CoalToday { get; set; }
        public long Hatched { get; set; }
        public string Shown { get; set; } = string.Empty;
        public long Souls { get; set; }
        public List<CompanionGearItem> Gear { get; } = new List<CompanionGearItem>();
        public int NextGearId { get; set; } = 1;
        public long BoardDay { get; set; } = -1;
        public List<string> Board { get; } = new List<string>();
        public List<string> BoardDone { get; } = new List<string>();
        public int Rerolls { get; set; }
        public string Helper { get; set; } = string.Empty;
        public string HelpGuild { get; set; } = string.Empty;
        public double HelpSeconds { get; set; }
        public long BurrowWeek { get; set; } = -1;
        public int BurrowRuns { get; set; } = 1;
        public Dictionary<int, int> TreeRanks { get; } = new Dictionary<int, int>();
        public Dictionary<int, string> TreeMajors { get; } = new Dictionary<int, string>();
        public double MenagerieExperience { get; set; }
        public int MenagerieLevel { get; set; } = 1;
        public long TreeResetDay { get; set; } = -1;
        public long RewindDay { get; set; } = -1;
        public double SoulHours { get; set; }
        public long SoulHostDay { get; set; } = -1;
        public long SoulTrialWeek { get; set; } = -1;
        public long SoulTreeWeek { get; set; } = -1;
        public long Essence { get; set; }
        public double QuickBiteSeconds { get; set; }
        public double QuickBiteFactor { get; set; } = 1;
        public bool SureForge { get; set; }
        public double XpBoostSeconds { get; set; }
        public double XpBoost { get; set; }
        public Dictionary<string, double> HostEssenceDays { get; } = new Dictionary<string, double>(System.StringComparer.Ordinal);
        public long Day { get; set; } = -1;
        public List<string> Home { get; } = new List<string>();
        public HashSet<string> Owned { get; } = new HashSet<string>(OwnedCapacity, System.StringComparer.Ordinal);
        public const int OwnedCapacity = 64;
        public Dictionary<string, CompanionProgress> Progress { get; } = new Dictionary<string, CompanionProgress>(System.StringComparer.Ordinal);
    }

    public sealed class CompanionGearAffix
    {
        public Content.CompanionGearStat Stat { get; set; }
        public double Value { get; set; }
    }

    public sealed class CompanionGearItem
    {
        public int Id { get; set; }
        public Content.CompanionGearSlot Slot { get; set; }
        public int Rarity { get; set; }
        public string Set { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public List<CompanionGearAffix> Affixes { get; } = new List<CompanionGearAffix>();
    }
}
