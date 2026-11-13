using System.Collections.Generic;

namespace Game.Core.Sim
{
    public sealed class HideoutCorner
    {
        public HideoutCorner(string name, string hero, string path, IReadOnlyList<string> decor)
        {
            Name = name;
            Hero = hero;
            Path = path;
            Decor = decor;
        }

        public string Name { get; }
        public string Hero { get; }
        public string Path { get; }
        public IReadOnlyList<string> Decor { get; }
    }

    public sealed class SharedBed
    {
        public SharedBed(string plant, double share, bool ready, bool tended)
        {
            Plant = plant;
            Share = share;
            Ready = ready;
            Tended = tended;
        }

        public string Plant { get; }
        public double Share { get; }
        public bool Ready { get; }
        public bool Tended { get; }
        public bool Empty => Plant.Length == 0;
    }

    public readonly struct StorageEntry
    {
        public StorageEntry(string name, string item, long amount, string build = "")
        {
            Name = name;
            Item = item;
            Amount = amount;
            Build = build;
        }

        public string Name { get; }
        public string Item { get; }
        public long Amount { get; }
        public string Build { get; }
    }

    public readonly struct PartyStationLine
    {
        public PartyStationLine(string building, int level, string branch, string cost, bool canUpgrade, bool canBranch)
        {
            Building = building;
            Level = level;
            Branch = branch;
            Cost = cost;
            CanUpgrade = canUpgrade;
            CanBranch = canBranch;
        }

        public string Building { get; }
        public int Level { get; }
        public string Branch { get; }
        public string Cost { get; }
        public bool CanUpgrade { get; }
        public bool CanBranch { get; }
    }

    public readonly struct StorageForBuildLine
    {
        public StorageForBuildLine(string item, long amount, int pointsEach, bool canGive)
        {
            Item = item;
            Amount = amount;
            PointsEach = pointsEach;
            CanGive = canGive;
        }

        public string Item { get; }
        public long Amount { get; }
        public int PointsEach { get; }
        public long Points => Amount * PointsEach;
        public bool CanGive { get; }
    }

    public sealed class CraftStation
    {
        public bool Built { get; set; }
        public List<string> Partners { get; } = new List<string>();
        public bool Together => Built && Partners.Count > 0;
    }

    public readonly struct TrophyFeat
    {
        public TrophyFeat(string id, int level)
        {
            Id = id;
            Level = level;
        }

        public string Id { get; }
        public int Level { get; }
    }

    public sealed class TrophyHall
    {
        public bool Built { get; set; }
        public long Bosses { get; set; }
        public long Guardians { get; set; }
        public long RiftDepth { get; set; }
        public long Hours { get; set; }
        public List<TrophyFeat> Feats { get; } = new List<TrophyFeat>();
    }

    public readonly struct PartyOrderNeed
    {
        public PartyOrderNeed(string counter, long have, long target)
        {
            Counter = counter;
            Have = have;
            Target = target;
        }

        public string Counter { get; }
        public long Have { get; }
        public long Target { get; }
    }

    public sealed class PartyOrder
    {
        public string Id { get; set; } = string.Empty;
        public bool Done { get; set; }
        public int DaysLeft { get; set; }
        public bool CanRedraw { get; set; }
        public List<PartyOrderNeed> Needs { get; } = new List<PartyOrderNeed>();
    }

    public sealed class HideoutState
    {
        public PartyOrder Order { get; } = new PartyOrder();
        public long ClaimSeq { get; set; }
        public int Tier { get; set; }
        public int Members { get; set; }
        public long Bosses { get; set; }
        public int Orders { get; set; }
        public long SitSeconds { get; set; }
        public int FireLevel { get; set; }
        public List<string> Here { get; } = new List<string>();
        public Dictionary<string, int> Buildings { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public List<string> CanQueue { get; } = new List<string>();
        public Dictionary<string, int> Wants { get; } = new Dictionary<string, int>(System.StringComparer.Ordinal);
        public List<string> MyWants { get; } = new List<string>();
        public string Project { get; set; } = string.Empty;
        public int ProjectLevel { get; set; }
        public long ProjectPoints { get; set; }
        public long ProjectTarget { get; set; }
        public double ProjectHours { get; set; }
        public long HoursTogether { get; set; }
        public bool Leader { get; set; }
        public bool HasCorner { get; set; }
        public List<HideoutCorner> Corners { get; } = new List<HideoutCorner>();
        public bool Together { get; set; }
        public CraftStation Forge { get; } = new CraftStation();
        public CraftStation Cauldron { get; } = new CraftStation();
        public TrophyHall Trophies { get; } = new TrophyHall();
        public double PierRareBoost { get; set; }
        public bool DummyBuilt { get; set; }
        public List<SharedBed> Beds { get; } = new List<SharedBed>();
        public Dictionary<string, long> Owed { get; } = new Dictionary<string, long>(System.StringComparer.Ordinal);
        public bool StorageBuilt { get; set; }
        public Dictionary<string, long> Storage { get; } = new Dictionary<string, long>(System.StringComparer.Ordinal);
        public List<StorageEntry> Journal { get; } = new List<StorageEntry>();
        public int TakenToday { get; set; }
        public int TakePerDay { get; set; }
        public bool ShareMaterials { get; set; }
        public int ShareKeep { get; set; } = -1;
        public long ForBuildPoints { get; set; }
        public long ProjectFromStorage { get; set; }
    }
}
