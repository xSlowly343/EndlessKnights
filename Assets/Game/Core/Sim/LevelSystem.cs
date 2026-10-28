using Game.Core.Content;
using Game.Core.Content.Tree;

namespace Game.Core.Sim
{
    public sealed class LevelSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private HeroRules _rules;
        private StageTable _stages;
        private TreePointRules _points;

        public LevelSystem(HeroRules rules, StageTable stages, TreePointRules points, GameState state, EventQueue events)
        {
            _rules = rules;
            _stages = stages;
            _points = points;
            _state = state;
            _events = events;
            SyncTreePoints();
        }

        public void UseData(HeroRules rules, StageTable stages, TreePointRules points)
        {
            _rules = rules;
            _stages = stages;
            _points = points;
            SyncTreePoints();
        }

        public int GuardianPoints => (int)System.Math.Min(_rules.MaxGuardianPoints, (_state.RecordStage - 1) / _stages.BiomeLength);

        public int MilestonePoints
        {
            get
            {
                var points = 0;
                var ladders = _points.Ladders;
                for (var i = 0; i < ladders.Count; i++)
                    points += ladders[i].PointsAt(Counter(ladders[i].Counter));
                return points;
            }
        }

        public double XpToNextLevel => _state.HeroLevel >= _rules.MaxLevel ? 0 : _rules.XpToNext(_state.HeroLevel);

        public void OnKill(EnemyType type, double multiplier = 1)
        {
            if (_state.HeroLevel >= _rules.MaxLevel)
                return;
            _state.HeroXp += multiplier * _rules.XpPerKill * (type == EnemyType.Normal ? 1 : _rules.BossXpMultiplier);
            while (_state.HeroLevel < _rules.MaxLevel && _state.HeroXp >= _rules.XpToNext(_state.HeroLevel))
            {
                _state.HeroXp -= _rules.XpToNext(_state.HeroLevel);
                _state.HeroLevel++;
                _events.Add(GameEvent.ForStage(GameEventKind.LevelUp, _state.Tick, _state.HeroLevel));
            }
            if (_state.HeroLevel >= _rules.MaxLevel)
                _state.HeroXp = 0;
            SyncTreePoints();
        }

        public void OnStageReached(long stage)
        {
            if (stage <= _state.RecordStage)
                return;
            _state.RecordStage = stage;
            SyncTreePoints();
        }

        public void SyncTreePoints()
        {
            _state.Tree.PointsEarned = System.Math.Min(_state.HeroLevel, _rules.MaxLevel) + GuardianPoints + MilestonePoints;
            if (_state.RecordStage > _stages.BiomeLength)
                _state.Ascendancy.Complete(ActivityIds.FirstGuardian);
            if (_state.Prestiges > 0)
                _state.Ascendancy.Complete(ActivityIds.FirstPrestige);
        }

        private double Counter(PointCounter counter)
        {
            switch (counter)
            {
                case PointCounter.Keys:
                    return _state.TotalInput.Keys;
                case PointCounter.Clicks:
                    return _state.TotalInput.Clicks;
                case PointCounter.MouseKm:
                    return _state.TotalInput.MouseMeters / 1000;
                case PointCounter.Prestiges:
                    return _state.Prestiges;
                case PointCounter.Trials:
                    return _state.Chronicle.Completed.Count;
                default:
                    return _state.RecordStage;
            }
        }
    }
}
