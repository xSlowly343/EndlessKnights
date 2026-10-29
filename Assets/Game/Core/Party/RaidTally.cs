using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public readonly struct RaidPush
    {
        public RaidPush(double skill, double mouse, double keys, double idle, double seconds)
        {
            Skill = skill;
            Mouse = mouse;
            Keys = keys;
            Idle = idle;
            Seconds = seconds;
        }

        public double Skill { get; }
        public double Mouse { get; }
        public double Keys { get; }
        public double Idle { get; }
        public double Seconds { get; }
        public double Total => Skill + Mouse + Keys + Idle;

        public double Of(RaidInput input) => input switch
        {
            RaidInput.Skill => Skill,
            RaidInput.Mouse => Mouse,
            RaidInput.Keys => Keys,
            _ => Idle
        };
    }

    public sealed class RaidTally
    {
        private readonly GameState _state;
        private readonly StageSystem _stage;
        private readonly GhostSystem _ghost;
        private readonly EventQueue _events;
        private readonly double[] _shares = new double[RaidRules.InputKinds];
        private RaidRules _rules;
        private long _ticks;
        private BigNumber _ghostSeen;

        public RaidTally(RaidRules rules, GameState state, StageSystem stage, GhostSystem ghost, EventQueue events)
        {
            _rules = rules;
            _state = state;
            _stage = stage;
            _ghost = ghost;
            _events = events;
        }

        public double Seconds => (double)_ticks / GameLoop.TicksPerSecond;

        public double Share(RaidInput input) => _shares[(int)input];

        public void UseRules(RaidRules rules) => _rules = rules;

        public void Tick(int firstEvent)
        {
            _ticks++;
            var ghostDamage = _ghost.Damage - _ghostSeen;
            _ghostSeen = _ghost.Damage;
            var hp = _stage.Table.EnemyHp(_state.Stage);
            if (hp.Sign <= 0)
                return;
            var idle = _state.IdleTicks >= _rules.IdleSeconds * GameLoop.TicksPerSecond;
            var skill = idle ? RaidInput.Idle : RaidInput.Skill;
            var skillBefore = _shares[(int)skill];
            for (var i = firstEvent; i < _events.Count; i++)
            {
                var gameEvent = _events[i];
                if (gameEvent.Kind != GameEventKind.Damage || gameEvent.Source == DamageSource.None)
                    continue;
                var input = idle ? RaidInput.Idle : InputOf(gameEvent.Source);
                _shares[(int)input] += (gameEvent.Amount / hp).ToDouble();
            }
            if (ghostDamage.Sign > 0)
                _shares[(int)skill] = System.Math.Max(skillBefore, _shares[(int)skill] - (ghostDamage / hp).ToDouble());
        }

        public RaidPush Take()
        {
            var push = new RaidPush(_shares[(int)RaidInput.Skill], _shares[(int)RaidInput.Mouse], _shares[(int)RaidInput.Keys], _shares[(int)RaidInput.Idle], Seconds);
            for (var i = 0; i < _shares.Length; i++)
                _shares[i] = 0;
            _ticks = 0;
            return push;
        }

        private static RaidInput InputOf(DamageSource source) => source switch
        {
            DamageSource.Keyboard => RaidInput.Keys,
            DamageSource.Mouse => RaidInput.Mouse,
            _ => RaidInput.Skill
        };
    }
}
