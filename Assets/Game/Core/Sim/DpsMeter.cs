using Game.Core.Content;
using Game.Core.Math;

namespace Game.Core.Sim
{
    public sealed class DpsMeter
    {
        private readonly GameState _state;
        private OfflineRules _rules;
        private FocusRules _focus;

        public DpsMeter(OfflineRules rules, FocusRules focus, GameState state)
        {
            _rules = rules;
            _focus = focus;
            _state = state;
        }

        public void UseRules(OfflineRules rules, FocusRules focus)
        {
            _rules = rules;
            _focus = focus;
        }

        public void Tick(BigNumber tickDamage)
        {
            if (_state.IdleTicks * GameLoop.TickSeconds > _focus.ResetAfterIdleSeconds)
                return;
            var tickDps = tickDamage * GameLoop.TicksPerSecond;
            var weight = System.Math.Min(1, GameLoop.TickSeconds / _rules.DpsWindowSeconds);
            _state.RecentDps += (tickDps - _state.RecentDps) * weight;
            if (_state.RecentDps.Sign < 0)
                _state.RecentDps = BigNumber.Zero;
        }
    }
}
