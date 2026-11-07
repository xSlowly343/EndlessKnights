using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public enum CornerResult
    {
        Done,
        NoCorner,
        Unknown,
        NotOwned,
        Full,
        NotThere
    }

    public sealed class HideoutSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private HideoutRules _rules;
        private ClanCampRules _materials;
        private GardenRules _garden;

        public HideoutSystem(HideoutRules rules, ClanCampRules materials, GardenRules garden, GameState state, CampSystem camp)
        {
            _rules = rules;
            _materials = materials;
            _garden = garden;
            _state = state;
            _camp = camp;
        }

        public HideoutRules Rules => _rules;
        public HideoutState State => _state.Hideout;
        public bool Inside => _state.Peaceful.Mode == PeacefulMode.Camp && _state.Peaceful.Place == PeacefulPlace.Hideout;
        public DecorSystem? Decor { get; set; }
        public CompanionSystem? Companions { get; set; }
        public NestSystem? Nest { get; set; }
        public int CornerDecor => _rules.CornerDecorAt(State.Tier);
        public bool CornerOpen => State.Tier > 0 && State.HasCorner;
        public IReadOnlyList<string> Corner => _state.Decor.Corner;
        public HideoutTier? NextTier => State.Tier < _rules.Tiers.Count ? _rules.Tiers[State.Tier] : null;

        public void UseRules(HideoutRules rules, ClanCampRules materials, GardenRules garden)
        {
            _rules = rules;
            _materials = materials;
            _garden = garden;
        }

        public int PointsOf(string item) => _materials.PointsOf(item);

        public int LevelOf(string building) => State.Buildings.TryGetValue(building, out var level) ? level : 0;

        public void UseBuild(IReadOnlyDictionary<string, int> buildings, IReadOnlyList<string> canQueue, string project, int projectLevel, long projectPoints,
            long projectTarget, double projectHours, long hoursTogether, bool leader, IReadOnlyDictionary<string, int> wants, IReadOnlyList<string> myWants)
        {
            var hideout = State;
            hideout.Buildings.Clear();
            foreach (var pair in buildings)
                hideout.Buildings[pair.Key] = pair.Value;
            hideout.CanQueue.Clear();
            hideout.CanQueue.AddRange(canQueue);
            hideout.Project = project;
            hideout.ProjectLevel = projectLevel;
            hideout.ProjectPoints = projectPoints;
            hideout.ProjectTarget = projectTarget;
            hideout.ProjectHours = projectHours;
            hideout.HoursTogether = hoursTogether;
            hideout.Leader = leader;
            hideout.Wants.Clear();
            foreach (var pair in wants)
                hideout.Wants[pair.Key] = pair.Value;
            hideout.MyWants.Clear();
            hideout.MyWants.AddRange(myWants);
            hideout.PierRareBoost = LevelOf(_rules.Pier) > 0 ? _rules.PierRareBoost : 0;
            hideout.DummyBuilt = LevelOf(_rules.Dummy) > 0;
            hideout.Trophies.Built = LevelOf(_rules.TrophyHall) > 0;
            Fill(hideout.Forge, _rules.Crafts.ForgeBuilding, hideout.Forge.Partners);
            Fill(hideout.Cauldron, _rules.Crafts.CauldronBuilding, hideout.Cauldron.Partners);
        }

        public ClanCampResult PackMaterial(string item, int amount)
        {
            if (_materials.PointsOf(item) == 0)
                return ClanCampResult.NotAMaterial;
            if (amount <= 0 || amount > _materials.MaxGive)
                return ClanCampResult.BadAmount;
            return _camp.Take(item, amount) ? ClanCampResult.Done : ClanCampResult.NotEnough;
        }

        public void ReturnMaterial(string item, int amount)
        {
            if (_materials.PointsOf(item) > 0 && amount > 0)
                _camp.Add(item, amount);
        }

        public void Use(int tier, int members, long bosses, int orders, long sitSeconds, int fireLevel, IReadOnlyList<string> here)
        {
            var hideout = State;
            hideout.Tier = System.Math.Max(0, System.Math.Min(tier, _rules.Tiers.Count));
            hideout.Members = members;
            hideout.Bosses = bosses;
            hideout.Orders = orders;
            hideout.SitSeconds = sitSeconds;
            hideout.FireLevel = fireLevel;
            hideout.Here.Clear();
            hideout.Here.AddRange(here);
        }

        public CornerResult PutInCorner(string id)
        {
            if (!CornerOpen)
                return CornerResult.NoCorner;
            if (Decor == null || Decor.Rules.Item(id) == null)
                return CornerResult.Unknown;
            var corner = _state.Decor.Corner;
            if (corner.Contains(id))
                return CornerResult.Done;
            if (corner.Count >= CornerDecor)
                return CornerResult.Full;
            if (!Decor.PutAway(id))
                return CornerResult.NotOwned;
            corner.Add(id);
            return CornerResult.Done;
        }

        public CornerResult TakeFromCorner(string id)
        {
            if (!_state.Decor.Corner.Remove(id))
                return CornerResult.NotThere;
            Decor?.PlaceInFreeSpot(id);
            return CornerResult.Done;
        }

        public int ReturnCorner()
        {
            var corner = _state.Decor.Corner;
            var count = corner.Count;
            foreach (var id in corner)
                Decor?.PlaceInFreeSpot(id);
            corner.Clear();
            return count;
        }

        public void UseCorners(bool mine, IReadOnlyList<HideoutCorner> corners)
        {
            State.HasCorner = mine;
            State.Corners.Clear();
            State.Corners.AddRange(corners);
            if (!mine)
                ReturnCorner();
        }

        public const string ForgeStation = "forge";
        public const string CauldronStation = "cauldron";

        public string Station => !Inside ? string.Empty
            : _state.Peaceful.Activity == ForgeSystem.Activity ? ForgeStation
            : _state.Peaceful.Activity == AlchemySystem.Activity ? CauldronStation
            : string.Empty;

        public void UseCrafts(IReadOnlyList<string> forgePartners, IReadOnlyList<string> cauldronPartners)
        {
            Fill(State.Forge, _rules.Crafts.ForgeBuilding, forgePartners);
            Fill(State.Cauldron, _rules.Crafts.CauldronBuilding, cauldronPartners);
        }

        private void Fill(CraftStation station, string building, IReadOnlyList<string> partners)
        {
            station.Built = LevelOf(building) > 0;
            if (ReferenceEquals(partners, station.Partners))
                return;
            station.Partners.Clear();
            station.Partners.AddRange(partners);
        }

        public void UseTrophies(long bosses, long guardians, long riftDepth, long hours, IReadOnlyList<TrophyFeat> feats)
        {
            var trophies = State.Trophies;
            trophies.Bosses = bosses;
            trophies.Guardians = guardians;
            trophies.RiftDepth = riftDepth;
            trophies.Hours = hours;
            trophies.Feats.Clear();
            trophies.Feats.AddRange(feats);
        }

        public List<Potion> TakeSharedBrews()
        {
            var brews = new List<Potion>(_state.Alchemy.SharedBrews);
            _state.Alchemy.SharedBrews.Clear();
            return brews;
        }

        public const string PotionPrefix = "potion:";
        public const string FineSuffix = ":fine";
        public const string CompanionPetPrefix = "companion_pet:";
        public const string CompanionWarm = "companion_warm";
        public const string CompanionSoul = "companion_soul";

        public PeacefulWork TakeTending()
        {
            var work = _state.Peaceful.HideoutWork;
            var taken = new PeacefulWork { Keys = work.Keys, Clicks = work.Clicks, MouseMeters = work.MouseMeters, IdleSeconds = work.IdleSeconds };
            work.Keys = 0;
            work.Clicks = 0;
            work.MouseMeters = 0;
            work.IdleSeconds = 0;
            return taken;
        }

        public GardenResult PackSeed(string plantId)
        {
            var plant = _garden.Plant(plantId);
            if (plant == null)
                return GardenResult.UnknownPlant;
            return plant.Starter || _camp.Take(plant.Seed, 1) ? GardenResult.Done : GardenResult.NoSeed;
        }

        public void ReturnSeed(string plantId)
        {
            var plant = _garden.Plant(plantId);
            if (plant != null && !plant.Starter)
                _camp.Add(plant.Seed, 1);
        }

        public bool PackGoods(string item, int amount) => amount > 0 && _camp.Take(item, amount);

        private bool ReceiveCompanion(string item, long amount)
        {
            if (item == CompanionSoul)
            {
                Companions?.AddSouls(amount);
                return true;
            }
            if (item == CompanionWarm)
            {
                for (var i = 0; i < amount; i++)
                    Nest?.FriendWarm(0);
                return true;
            }
            if (!item.StartsWith(CompanionPetPrefix, System.StringComparison.Ordinal))
                return false;
            var companion = item.Substring(CompanionPetPrefix.Length);
            for (var i = 0; i < amount; i++)
                Companions?.FriendPet(companion);
            return true;
        }

        public void Receive(IReadOnlyDictionary<string, long> items)
        {
            foreach (var pair in items)
            {
                if (pair.Value <= 0 || ReceiveCompanion(pair.Key, pair.Value))
                    continue;
                if (!pair.Key.StartsWith(PotionPrefix, System.StringComparison.Ordinal))
                {
                    _camp.Add(pair.Key, pair.Value);
                    continue;
                }
                var fine = pair.Key.EndsWith(FineSuffix, System.StringComparison.Ordinal);
                var recipe = pair.Key.Substring(PotionPrefix.Length, pair.Key.Length - PotionPrefix.Length - (fine ? FineSuffix.Length : 0));
                for (var i = 0; i < pair.Value; i++)
                    _state.Alchemy.Potions.Add(new Potion { Recipe = recipe, Fine = fine });
            }
        }

        public void UseCommons(bool together, IReadOnlyList<SharedBed> beds, IReadOnlyDictionary<string, long> owed, bool storageBuilt,
            IReadOnlyDictionary<string, long> storage, IReadOnlyList<StorageEntry> journal, int takenToday, int takePerDay)
        {
            var hideout = State;
            hideout.Together = together;
            hideout.Beds.Clear();
            hideout.Beds.AddRange(beds);
            hideout.Owed.Clear();
            foreach (var pair in owed)
                hideout.Owed[pair.Key] = pair.Value;
            hideout.StorageBuilt = storageBuilt;
            hideout.Storage.Clear();
            foreach (var pair in storage)
                hideout.Storage[pair.Key] = pair.Value;
            hideout.Journal.Clear();
            hideout.Journal.AddRange(journal);
            hideout.TakenToday = takenToday;
            hideout.TakePerDay = takePerDay;
        }

        public void Leave()
        {
            UseTrophies(0, 0, 0, 0, System.Array.Empty<TrophyFeat>());
            UseCrafts(System.Array.Empty<string>(), System.Array.Empty<string>());
            UseCommons(false, System.Array.Empty<SharedBed>(), new Dictionary<string, long>(), false, new Dictionary<string, long>(), System.Array.Empty<StorageEntry>(), 0, 0);
            UseCorners(false, System.Array.Empty<HideoutCorner>());
            Use(0, 0, 0, 0, 0, 0, System.Array.Empty<string>());
            UseBuild(new Dictionary<string, int>(), System.Array.Empty<string>(), string.Empty, 0, 0, 0, 0, 0, false, new Dictionary<string, int>(), System.Array.Empty<string>());
            if (_state.Peaceful.Place == PeacefulPlace.Hideout)
                _state.Peaceful.Place = PeacefulPlace.Refuge;
        }

        public List<string> Open()
        {
            var open = new List<string>();
            for (var i = 0; i < State.Tier; i++)
                open.AddRange(_rules.Tiers[i].Opens);
            return open;
        }
    }
}
