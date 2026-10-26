using Game.Core.Content;
using Game.Core.Stats;

namespace Game.Core.Sim
{
    public sealed class KillResourceSystem : IKillTimeListener
    {
        private readonly GameState _state;
        private readonly StatSheet _hero;
        private readonly ResourceSystem _resource;
        private readonly CombatSystem _combat;

        public KillResourceSystem(GameState state, StatSheet hero, ResourceSystem resource, CombatSystem combat)
        {
            _state = state;
            _hero = hero;
            _resource = resource;
            _combat = combat;
        }

        public void OnKillTimed(EnemyType type, double lifeSeconds, bool shared)
        {
            GainPerKill();
            var saved = System.Math.Max(0, _combat.Conditions.FastKillSeconds - lifeSeconds);
            if (saved <= 0)
                return;
            GainSand(saved);
            if (type == EnemyType.Normal && !shared)
                Bank(saved);
        }

        private void GainPerKill()
        {
            var perKill = _resource.Rules.PerKill;
            if (perKill > 0)
                _resource.Gain(perKill * _hero.Value(CombatStats.ResourceGain, 1));
        }

        private void GainSand(double saved)
        {
            var perSecond = _resource.Rules.PerFastKillSecond;
            if (perSecond <= 0)
                return;
            _resource.Gain(saved * perSecond * System.Math.Max(0, _hero.Value(CombatStats.SandFromFastKills, 1)) * _hero.Value(CombatStats.ResourceGain, 1));
        }

        private void Bank(double saved)
        {
            var cap = _hero.Value(CombatStats.BossTimerBank, 0);
            if (cap > 0)
                _state.BankedSeconds = System.Math.Min(cap, _state.BankedSeconds + saved);
        }
    }
}
