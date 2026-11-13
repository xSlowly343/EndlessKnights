using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public readonly struct ObservatoryForecast
    {
        public ObservatoryForecast(string fish, string plant)
        {
            Fish = fish;
            Plant = plant;
        }

        public string Fish { get; }
        public string Plant { get; }
    }

    public sealed class GuildSystem
    {
        private readonly GameState _state;
        private readonly StageTable _stages;
        private readonly FishingRules _fishing;
        private readonly GardenRules _garden;
        private readonly EventQueue _events;
        private GuildRules _rules;
        private int _openSites = -1;
        private long _seenKills;

        public GuildSystem(GuildRules rules, GameState state, StageTable stages, FishingRules fishing, GardenRules garden, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _stages = stages;
            _fishing = fishing;
            _garden = garden;
            _events = events;
            _seenKills = state.Kills;
        }

        public GuildRules Rules => _rules;
        public long UnsentActions => _state.Peaceful.UnsentActions;

        public void UseRules(GuildRules rules)
        {
            _rules = rules;
            _openSites = -1;
        }

        public bool SiteOpen(TownSiteDef site) => _state.RecordStage >= site.FromStage;

        public TownSiteDef? NextSite
        {
            get
            {
                foreach (var site in _rules.Sites)
                {
                    if (!SiteOpen(site))
                        return site;
                }
                return null;
            }
        }

        public void Tick(int firstEvent)
        {
            WatchSites();
            GatherAtSites();
            for (var i = firstEvent; i < _events.Count; i++)
                _state.Peaceful.Gain(ResourceOf(_events[i].Kind), 1);
        }

        public static string ResourceOf(GameEventKind kind) => kind switch
        {
            GameEventKind.PotionBrewed => TownResource.Potions,
            GameEventKind.FishCaught => TownResource.Fish,
            GameEventKind.ItemSmelted => TownResource.Metalwork,
            GameEventKind.ItemTempered => TownResource.Metalwork,
            GameEventKind.DishCooked => TownResource.Dishes,
            GameEventKind.FindDug => TownResource.Relics,
            GameEventKind.TinctureDistilled => TownResource.Tinctures,
            _ => string.Empty
        };

        private void WatchSites()
        {
            var open = 0;
            while (open < _rules.Sites.Count && SiteOpen(_rules.Sites[open]))
                open++;
            if (_openSites >= 0)
            {
                for (var i = _openSites; i < open; i++)
                    _events.Add(GameEvent.ForStage(GameEventKind.TownSiteOpened, _state.Tick, i));
            }
            _openSites = open;
        }

        private void GatherAtSites()
        {
            var fresh = _state.Kills - _seenKills;
            _seenKills = _state.Kills;
            if (fresh <= 0)
                return;
            var seen = _state.Peaceful.SiteKills;
            var kills = seen + fresh;
            _state.Peaceful.SiteKills = kills;
            for (var i = 0; i < _openSites; i++)
            {
                var site = _rules.Sites[i];
                _state.Peaceful.Gain(site.Resource, (long)(System.Math.Floor(kills * site.PerKill) - System.Math.Floor(seen * site.PerKill)));
            }
        }

        public bool DistrictOpen(DistrictDef district) => district.Biome.Length == 0 || BiomeCleared(district.Biome);

        public int Level(string guild) => _rules.LevelAt(_state.Town.Reputation.TryGetValue(guild, out var reputation) ? reputation : 0);

        public bool Built(string guild) => _state.Camp.Guilds.Contains(guild);

        public int HallLevel(string building)
        {
            var def = _rules.Building(building);
            return def == null ? 0 : _rules.LevelOf(def, _state.Camp.Guilds);
        }

        public bool ObservatoryOpen
        {
            get
            {
                if (_state.Perk(MuseumPerk.ForecastSight) > 0)
                    return true;
                foreach (var district in _rules.Districts)
                {
                    if (district.Observatory && DistrictOpen(district))
                        return true;
                }
                return false;
            }
        }

        public long Unsent(string resource) =>
            resource == TownResource.Labour ? _state.Peaceful.UnsentActions : _state.Peaceful.UnsentGains.TryGetValue(resource, out var unsent) ? unsent : 0;

        public long Take(string resource, long cap)
        {
            var taken = System.Math.Min(System.Math.Max(0, cap), Unsent(resource));
            Change(resource, -taken);
            return taken;
        }

        public void Return(string resource, long amount) => Change(resource, System.Math.Max(0, amount));

        public Dictionary<string, long> TakeFor(TownBoard board)
        {
            var taken = new Dictionary<string, long>(System.StringComparer.Ordinal);
            if (board.Phase == TownBoardPhase.Done)
                return taken;
            foreach (var stock in board.Stocks)
            {
                var amount = Take(stock.Resource, stock.LeftToday);
                if (amount > 0)
                    taken[stock.Resource] = amount;
            }
            return taken;
        }

        public void Return(Dictionary<string, long> gains)
        {
            foreach (var gain in gains)
                Return(gain.Key, gain.Value);
        }

        public void Settle(Dictionary<string, long> sent, IReadOnlyDictionary<string, long> accepted)
        {
            foreach (var gain in sent)
                Return(gain.Key, gain.Value - (accepted.TryGetValue(gain.Key, out var took) ? took : 0));
        }

        private void Change(string resource, long by)
        {
            if (by == 0)
                return;
            if (resource == TownResource.Labour)
            {
                _state.Peaceful.UnsentActions += by;
                return;
            }
            var left = Unsent(resource) + by;
            if (left > 0)
                _state.Peaceful.UnsentGains[resource] = left;
            else
                _state.Peaceful.UnsentGains.Remove(resource);
        }

        public ObservatoryForecast Forecast(long day)
        {
            var fish = string.Empty;
            var fishCount = 0;
            foreach (var spot in _fishing.Spots)
            {
                if (BiomeCleared(spot.Biome))
                    fishCount += spot.Fish.Count;
            }
            if (fishCount > 0)
            {
                var pick = (int)(Mix(day, 1) % (ulong)fishCount);
                foreach (var spot in _fishing.Spots)
                {
                    if (!BiomeCleared(spot.Biome))
                        continue;
                    if (pick < spot.Fish.Count)
                    {
                        fish = spot.Fish[pick].Id;
                        break;
                    }
                    pick -= spot.Fish.Count;
                }
            }
            var plants = _garden.Plants;
            var plant = plants.Count > 0 ? plants[(int)(Mix(day, 2) % (ulong)plants.Count)].Id : string.Empty;
            return new ObservatoryForecast(fish, plant);
        }

        private bool BiomeCleared(string biome)
        {
            if (biome == _stages.Heart)
                return _state.RecordStage >= _stages.HeartFromStage;
            var rotation = _stages.Rotation;
            for (var i = 0; i < rotation.Count; i++)
            {
                if (rotation[i] == biome)
                    return _state.RecordStage >= _stages.OpensAt(i) + _stages.BiomeLength;
            }
            return false;
        }

        private static ulong Mix(long day, ulong salt)
        {
            var x = (ulong)day * 0x9E3779B97F4A7C15UL + salt * 0xBF58476D1CE4E5B9UL;
            x ^= x >> 31;
            x *= 0x94D049BB133111EBUL;
            return x ^ (x >> 29);
        }
    }
}
