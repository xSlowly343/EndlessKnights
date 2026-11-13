using Game.Core.Content;
using Game.Core.Content.Rift;

namespace Game.Core.Sim
{
    public sealed class Unlocks
    {
        private readonly GameState _state;
        private readonly EventQueue? _events;

        public Unlocks(UnlockRules rules, GameState state, EventQueue? events = null)
        {
            Rules = rules;
            _state = state;
            _events = events;
        }

        public UnlockRules Rules { get; set; }
        public CampSystem? Camp { get; set; }
        public RiftRules? Rift { get; set; }

        public bool InRift => Rift != null && _state.Rift.Inside;

        public bool IsOpen(Feature feature) => InRift ? _state.HighestStage >= Rift!.StageOf(feature) : _state.OpenedFeatures.Contains(feature) || Met(Rules.For(feature));

        public bool ArtifactGroupOpen(int group) => _state.RecordStage >= Rules.ArtifactGroupStage(group);

        public void Tick()
        {
            if (InRift)
            {
                TickRift();
                return;
            }
            for (var i = 0; i < UnlockRules.FeatureCount; i++)
            {
                var feature = (Feature)i;
                var condition = Rules.For(feature);
                if (condition.Always || _state.OpenedFeatures.Contains(feature) || !Met(condition))
                    continue;
                _state.OpenedFeatures.Add(feature);
                _events?.Add(GameEvent.ForStage(GameEventKind.FeatureOpened, _state.Tick, i));
            }
        }

        private void TickRift()
        {
            foreach (var (name, feature) in UnlockRules.FeatureNames)
            {
                if (!IsOpen(feature) || !_state.Rift.Opened.Add(name))
                    continue;
                _events?.Add(GameEvent.ForStage(GameEventKind.RiftFeatureOpened, _state.Tick, (long)feature));
            }
        }

        public bool Met(UnlockCondition condition)
        {
            if (condition.Always)
                return true;
            for (var r = 0; r < condition.Routes; r++)
            {
                if (Missing(condition, r) < 0)
                    return true;
            }
            return false;
        }

        public bool Met(UnlockNeed need) => Have(need) >= need.Value;

        public UnlockNeed? Missing(Feature feature)
        {
            if (IsOpen(feature))
                return null;
            if (InRift)
                return new UnlockNeed(UnlockNeedKind.Stage, string.Empty, Rift!.StageOf(feature));
            var condition = Rules.For(feature);
            var route = ClosestRoute(condition);
            return condition.Route(route)[Missing(condition, route)];
        }

        public long Have(UnlockNeed need)
        {
            switch (need.Kind)
            {
                case UnlockNeedKind.Stage:
                    return InRift ? _state.HighestStage : _state.RecordStage;
                case UnlockNeedKind.Prestiges:
                    return _state.Prestiges;
                case UnlockNeedKind.HeroLevel:
                    return _state.HeroLevel;
                case UnlockNeedKind.DungeonTier:
                    return _state.Dungeons.BestTier;
                case UnlockNeedKind.Trials:
                    return _state.Chronicle.Completed.Count;
                case UnlockNeedKind.Limits:
                    return _state.GatesCleared.Count;
                case UnlockNeedKind.RiftDepth:
                    return _state.Rift.Deepest;
                case UnlockNeedKind.CampTier:
                    return _state.Camp.Tier + 1;
                case UnlockNeedKind.CampBuilding:
                    return CampLevel(need.Id);
                case UnlockNeedKind.HideoutTier:
                    return _state.Hideout.Tier;
                case UnlockNeedKind.HideoutBuilding:
                    return _state.Hideout.Buildings.TryGetValue(need.Id, out var built) ? built : 0;
                default:
                    return 0;
            }
        }

        private long CampLevel(string building)
        {
            if (Camp != null && !Camp.Open(building))
                return 0;
            return _state.Camp.Levels.TryGetValue(building, out var level) ? System.Math.Max(1, level) : 1;
        }

        private int Missing(UnlockCondition condition, int route)
        {
            var needs = condition.Route(route);
            for (var n = 0; n < needs.Count; n++)
            {
                if (!Met(needs[n]))
                    return n;
            }
            return -1;
        }

        private int ClosestRoute(UnlockCondition condition)
        {
            var closest = 0;
            var fewest = int.MaxValue;
            for (var r = 0; r < condition.Routes; r++)
            {
                var unmet = 0;
                var needs = condition.Route(r);
                for (var n = 0; n < needs.Count; n++)
                {
                    if (!Met(needs[n]))
                        unmet++;
                }
                if (unmet < fewest)
                {
                    fewest = unmet;
                    closest = r;
                }
            }
            return closest;
        }
    }
}
