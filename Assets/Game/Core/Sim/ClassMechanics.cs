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
            if (_state.Is(HeroClass.Cartomancer))
                return ColdHit();
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

        public bool EvilEyeOn =>
            _state.Is(HeroClass.Whisperer) && _state.EnemyAlive && _rules.EvilEye.DamageTaken > 0
            && (_hero.Value(CombatStats.EvilEyeBossOnly, 0) < 1
                || _state.EnemyType == EnemyType.Boss
                || (_state.EnemyType == EnemyType.Guardian && _hero.Value(CombatStats.EvilEyeOnGuardians, 0) >= 1));

        public double EvilEyeShare => EvilEyeOn
            ? (_rules.EvilEye.DamageTaken + System.Math.Max(0, _hero.Value(CombatStats.EvilEyeEffect, 0))) * (1 + System.Math.Max(0, _hero.Value(CombatStats.EvilEyePower, 0)))
            : 0;

        private double ColdHit()
        {
            if (_state.EnemyAlive)
                _state.Cold = System.Math.Min(_rules.Cold.Max, _state.Cold + _rules.Cold.PerKeyHit);
            return 1;
        }

        public double ColdShare => _state.Is(HeroClass.Cartomancer) && _state.EnemyAlive && _state.Cold > 0
            ? _rules.Cold.DamageTaken * System.Math.Min(1, _state.Cold / _rules.Cold.Max)
                * (1 + System.Math.Max(0, _hero.Value(CombatStats.ColdPower, 0))) * System.Math.Max(0, _hero.Value(CombatStats.EnemyDebuffEffect, 1))
            : 0;

        private void FadeCold()
        {
            if (_state.Cold <= 0)
                return;
            _state.Cold *= 1 - _rules.Cold.FadePerSecond * GameLoop.TickSeconds;
            if (_state.Cold < 1e-6 || !_state.EnemyAlive)
                _state.Cold = 0;
        }

        public void EndTick(int keyHits, double mousePx)
        {
            if (_state.StunTicks > 0)
                _state.StunTicks = _state.EnemyAlive ? _state.StunTicks - 1 : 0;
            TickHunterMark();
            if (_state.BlindTicks > 0)
                _state.BlindTicks = _state.EnemyAlive ? _state.BlindTicks - 1 : 0;
            Clockwork();
            FadeCold();
        }

        private void Clockwork()
        {
            var running = _state.Is(HeroClass.Clockwright) && _rules.ClockworkSlow > 0 && _hero.Value(ClockwrightStats.SpringPower, 0) <= 0
                && _state.Resource > _rules.ClockworkAbove + _hero.Value(CombatStats.ClockworkAbove, 0);
            _state.ClockworkSlow = running ? System.Math.Min(1, _rules.ClockworkSlow + System.Math.Max(0, _hero.Value(CombatStats.ClockworkSlow, 0))) : 0;
            _state.ClockworkFloor = _rules.ClockworkFloor;
        }
    }
}
