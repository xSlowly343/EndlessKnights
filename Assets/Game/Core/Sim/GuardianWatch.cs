using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class GuardianWatch
    {
        private readonly GameState _state;
        private long _fightSpawnTick = long.MinValue;
        private double _mouseAtStart;
        private long _castsSeen;
        private long _lastCastTick = long.MinValue;

        public GuardianWatch(GameState state)
        {
            _state = state;
        }

        public double FightMouseMeters => _state.TotalInput.MouseMeters - _mouseAtStart;

        public ulong Conditions()
        {
            var mechanic = _state.EnemyAlive ? _state.Enemy?.Mechanic : null;
            if (mechanic == null)
            {
                _fightSpawnTick = long.MinValue;
                return 0;
            }
            Follow();
            return mechanic.Kind switch
            {
                GuardianMechanicKind.Shield => Shielded(mechanic.Value) ? Bit(ModifierCondition.GuardianShield) : 0,
                GuardianMechanicKind.Wet => (_state.Tick - _lastCastTick) * GameLoop.TickSeconds < mechanic.Value ? Bit(ModifierCondition.GuardianWet) : 0,
                GuardianMechanicKind.Rhythm => FightMouseMeters < mechanic.Value ? Bit(ModifierCondition.GuardianRhythm) : 0,
                GuardianMechanicKind.Silence => Bit(Silenced(mechanic.Value)),
                _ => 0
            };
        }

        private void Follow()
        {
            if (_fightSpawnTick != _state.EnemySpawnTick)
            {
                _fightSpawnTick = _state.EnemySpawnTick;
                _mouseAtStart = _state.TotalInput.MouseMeters;
                _castsSeen = _state.SkillCasts;
                _lastCastTick = long.MinValue / 2;
            }
            if (_state.SkillCasts > _castsSeen)
            {
                _castsSeen = _state.SkillCasts;
                _lastCastTick = _state.Tick;
            }
        }

        private bool Shielded(double share) =>
            _state.EnemyMaxHp.Sign > 0 && (_state.EnemyHp / _state.EnemyMaxHp).ToDouble() > 1 - share;

        private ModifierCondition Silenced(double seconds)
        {
            var phase = (long)System.Math.Floor(_state.EnemyLifeSeconds / seconds) % 3;
            return phase == 0 ? ModifierCondition.SilenceKeys : phase == 1 ? ModifierCondition.SilenceMouse : ModifierCondition.SilenceClicks;
        }

        private static ulong Bit(ModifierCondition condition) => ModifierConditions.Bit(condition);
    }
}
