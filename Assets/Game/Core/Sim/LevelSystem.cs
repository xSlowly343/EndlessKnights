using Game.Core.Content;

namespace Game.Core.Sim
{
    public sealed class LevelSystem
    {
        private readonly GameState _state;
        private readonly EventQueue _events;
        private HeroRules _rules;
        private StageTable _stages;

        public LevelSystem(HeroRules rules, StageTable stages, GameState state, EventQueue events)
        {
            _rules = rules;
            _stages = stages;
            _state = state;
            _events = events;
            SyncTreePoints();
        }

        public void UseData(HeroRules rules, StageTable stages)
        {
            _rules = rules;
            _stages = stages;
            SyncTreePoints();
        }

        public int GuardianPoints => (int)System.Math.Min(_rules.MaxGuardianPoints, (_state.RecordStage - 1) / _stages.BiomeLength);

        public double XpToNextLevel => _state.HeroLevel >= _rules.MaxLevel ? 0 : _rules.XpToNext(_state.HeroLevel);

        public void OnKill(EnemyType type)
        {
            if (_state.HeroLevel >= _rules.MaxLevel)
                return;
            _state.HeroXp += _rules.XpPerKill * (type == EnemyType.Normal ? 1 : _rules.BossXpMultiplier);
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
            _state.Tree.PointsEarned = System.Math.Min(_state.HeroLevel, _rules.MaxLevel) + GuardianPoints;
            if (_state.RecordStage > _stages.BiomeLength)
                _state.Ascendancy.Complete(ActivityIds.FirstGuardian);
            if (_state.Prestiges > 0)
                _state.Ascendancy.Complete(ActivityIds.FirstPrestige);
        }
    }
}
