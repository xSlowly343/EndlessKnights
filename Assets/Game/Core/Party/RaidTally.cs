using Game.Core.Math;
using Game.Core.Sim;

namespace Game.Core.Party
{
    public readonly struct RaidPush
    {
        public RaidPush(double skill, double mouse, double keys, double idle, double seconds, in ClanCounts counts, double coven = 0)
        {
            Coven = coven;
            Skill = skill;
            Mouse = mouse;
            Keys = keys;
            Idle = idle;
            Seconds = seconds;
            Counts = counts;
        }

        public double Skill { get; }
        public double Mouse { get; }
        public double Keys { get; }
        public double Idle { get; }
        public double Seconds { get; }
        public ClanCounts Counts { get; }
        public double Coven { get; }
        public double Total => Skill + Mouse + Keys + Idle;

        public double Of(RaidInput input) => input switch
        {
            RaidInput.Skill => Skill,
            RaidInput.Mouse => Mouse,
            RaidInput.Keys => Keys,
            _ => Idle
        };
    }

    public readonly struct ClanCounts
    {
        public ClanCounts(long keyPresses, long clicks, double mouseMeters, long bosses)
        {
            KeyPresses = keyPresses;
            Clicks = clicks;
            MouseMeters = mouseMeters;
            Bosses = bosses;
        }

        public long KeyPresses { get; }
        public long Clicks { get; }
        public double MouseMeters { get; }
        public long Bosses { get; }
    }

    public sealed class RaidTally
    {
        private readonly GameState _state;
        private readonly StageSystem _stage;
        private readonly GhostSystem _ghost;
        private readonly EventQueue _events;
        private readonly Stats.StatSheet _hero;
        private readonly double[] _shares = new double[RaidRules.InputKinds];
        private RaidRules _rules;
        private long _ticks;
        private BigNumber _ghostSeen;
        private ulong _keysSeen;
        private ulong _clicksSeen;
        private double _metersSeen;
        private long _bossesSeen;

        public RaidTally(RaidRules rules, GameState state, StageSystem stage, GhostSystem ghost, EventQueue events, Stats.StatSheet hero)
        {
            _hero = hero;
            _rules = rules;
            _state = state;
            _stage = stage;
            _ghost = ghost;
            _events = events;
            MarkCounts();
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
            var total = _state.TotalInput;
            var counts = new ClanCounts(Since(total.Keys, _keysSeen), Since(total.Clicks, _clicksSeen), System.Math.Max(0, total.MouseMeters - _metersSeen),
                System.Math.Max(0, _state.BossKills - _bossesSeen));
            var push = new RaidPush(_shares[(int)RaidInput.Skill], _shares[(int)RaidInput.Mouse], _shares[(int)RaidInput.Keys], _shares[(int)RaidInput.Idle], Seconds, counts,
                System.Math.Max(0, _hero.Value(CombatStats.CurseClanShare, 0)));
            for (var i = 0; i < _shares.Length; i++)
                _shares[i] = 0;
            _ticks = 0;
            MarkCounts();
            return push;
        }

        private static long Since(ulong now, ulong seen) => now > seen ? (long)(now - seen) : 0;

        private void MarkCounts()
        {
            _keysSeen = _state.TotalInput.Keys;
            _clicksSeen = _state.TotalInput.Clicks;
            _metersSeen = _state.TotalInput.MouseMeters;
            _bossesSeen = _state.BossKills;
        }

        private static RaidInput InputOf(DamageSource source) => source switch
        {
            DamageSource.Keyboard => RaidInput.Keys,
            DamageSource.Mouse => RaidInput.Mouse,
            DamageSource.Click => RaidInput.Mouse,
            _ => RaidInput.Skill
        };
    }
}
