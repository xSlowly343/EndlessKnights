using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class ClassMechanics
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private ClassRules _rules;

        public ClassMechanics(ClassRules rules, GameState state, StatSheet hero)
        {
            _hero = hero;
            _rules = rules;
            _state = state;
        }

        public void UseRules(ClassRules rules) => _rules = rules;

        public double OnKeyHit(out HitFlags flags)
        {
            flags = HitFlags.None;
            return 1;
        }

        public bool MarkFits =>
            _hero.Value(CombatStats.MarkBossOnly, 0) < 1
            || _state.EnemyType == EnemyType.Boss
            || (_state.EnemyType == EnemyType.Guardian && _hero.Value(CombatStats.MarkOnGuardians, 0) >= 1);

        public void PlaceHunterMark()
        {
            if (!_state.EnemyAlive || !MarkFits)
                return;
            if (_hero.Value(CombatStats.MarkForever, 0) >= 1)
            {
                _state.HunterMarkTicks = int.MaxValue;
                return;
            }
            var seconds = _rules.HunterMark.Seconds + _hero.Value(CombatStats.MarkSeconds, 0);
            _state.HunterMarkTicks = System.Math.Max(_state.HunterMarkTicks, (int)System.Math.Round(seconds * GameLoop.TicksPerSecond));
        }

        private void TickHunterMark()
        {
            if (_state.MarkedEnemyDied)
            {
                _state.MarkedEnemyDied = false;
                if (_hero.Value(CombatStats.MarkJump, 0) >= 1)
                    PlaceHunterMark();
            }
            if (_state.HunterMarkTicks > 0 && _state.HunterMarkTicks != int.MaxValue)
                _state.HunterMarkTicks = _state.EnemyAlive ? _state.HunterMarkTicks - 1 : 0;
        }

        public void EndTick(int keyHits, double mousePx)
        {
            if (_state.StunTicks > 0)
                _state.StunTicks = _state.EnemyAlive ? _state.StunTicks - 1 : 0;
            TickHunterMark();
            if (_state.BlindTicks > 0)
                _state.BlindTicks = _state.EnemyAlive ? _state.BlindTicks - 1 : 0;
        }
    }
}
