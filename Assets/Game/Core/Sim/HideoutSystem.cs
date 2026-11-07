using System.Collections.Generic;
using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class HideoutSystem
    {
        private readonly GameState _state;
        private readonly CampSystem _camp;
        private HideoutRules _rules;
        private ClanCampRules _materials;

        public HideoutSystem(HideoutRules rules, ClanCampRules materials, GameState state, CampSystem camp)
        {
            _rules = rules;
            _materials = materials;
            _state = state;
            _camp = camp;
        }

        public HideoutRules Rules => _rules;
        public HideoutState State => _state.Hideout;
        public bool Inside => _state.Peaceful.Mode == PeacefulMode.Camp && _state.Peaceful.Place == PeacefulPlace.Hideout;
        public HideoutTier? NextTier => State.Tier < _rules.Tiers.Count ? _rules.Tiers[State.Tier] : null;

        public void UseRules(HideoutRules rules, ClanCampRules materials)
        {
            _rules = rules;
            _materials = materials;
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

        public void Leave()
        {
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
